using System.Text.Json.Nodes;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;
public static class ProfileBackupService {
 static readonly string Database=System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","backups","profiles.sqlite");
 static readonly string SettingsPath=System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","backups","backup-settings.json");
 static readonly SemaphoreSlim Gate=new(1,1);
 sealed record SettingsState(bool AutoPreRaid,int AutoKeep);
 static SettingsState LoadSettings(){
  try{
   if(!File.Exists(SettingsPath))return new(false,5);
   var doc=System.Text.Json.JsonSerializer.Deserialize<SettingsState>(File.ReadAllText(SettingsPath));
   return doc==null?new(false,5):new(doc.AutoPreRaid,Math.Clamp(doc.AutoKeep,1,20));
  }catch{return new(false,5);}
 }
 static void SaveSettings(SettingsState state){
  var clean=new SettingsState(state.AutoPreRaid,Math.Clamp(state.AutoKeep,1,20));
  Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
  string tmp=SettingsPath+".tmp";
  File.WriteAllText(tmp,System.Text.Json.JsonSerializer.Serialize(clean));
  File.Move(tmp,SettingsPath,true);
 }
 public static void TryAutoRaidBackup(MongoId session,SaveServer saves,JsonUtil json,string map){
  if(!Gate.Wait(0))return;
  try{
   var settings=LoadSettings();if(!settings.AutoPreRaid)return;
   var profile=saves.GetProfile(session);if(profile==null)return;
   string raw=json.Serialize(profile)??"";if(raw.Length==0)return;
   var store=new SqliteSnapshots(Database);
   string tag=string.IsNullOrWhiteSpace(map)?"战前自动":"战前自动 · "+map.Trim();
   if(tag.Length>120)tag=tag[..120];
   store.Add(session.ToString(),ProfileRestoreRules.Nickname(raw),tag,raw);
   store.TrimPrefix(session.ToString(),"战前自动",settings.AutoKeep);
  }catch{}finally{Gate.Release();}
 }
 public static async ValueTask<string> Handle(CharacterDataRequest request,MongoId session,SptProfile profile,SaveServer saves,JsonUtil json,HttpResponseUtil http,TemplateTable templates,CancellationToken ct){
  await Gate.WaitAsync(ct);try{
   var store=new SqliteSnapshots(Database);string current=json.Serialize(profile)!;string revision=ProfileRestoreRules.Revision(current);
   object Status(){var s=LoadSettings();return new{ok=true,revision,backups=store.List(),categories=ProfileRestoreRules.Categories,database="LocalGearMail/backups/profiles.sqlite",autoPreRaid=s.AutoPreRaid,autoKeep=s.AutoKeep};}
   if(request.Action=="backupList")return http.GetBody(Status());
   if(request.Action=="backupSettings"){
    var cur=LoadSettings();
    bool autoPre=request.Settings.TryGetValue("autoPreRaid",out var a)&&a.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False?a.GetBoolean():cur.AutoPreRaid;
    int keep=request.Settings.TryGetValue("autoKeep",out var k)&&k.ValueKind==System.Text.Json.JsonValueKind.Number&&k.TryGetInt32(out int kv)?Math.Clamp(kv,1,20):cur.AutoKeep;
    SaveSettings(new(autoPre,keep));
    return http.GetBody(Status());
   }
   if(request.Action=="backupDelete"){
    if(!store.Delete(request.Id))throw new ArgumentException("未找到该历史备份。");
    return http.GetBody(Status());
   }
   if(request.Action=="backupExport"){
    string raw=store.Read(request.Id);
    var meta=store.List().FirstOrDefault(r=>r.Id==request.Id.ToLowerInvariant());
    return http.GetBody(new{ok=true,export=new{version=1,id=meta?.Id??request.Id,nickname=meta?.Nickname??ProfileRestoreRules.Nickname(raw),created=meta?.Created??DateTime.UtcNow.ToString("O"),label=meta?.Label??"导出备份",profile=JsonNode.Parse(raw)}});
   }
   if(request.Action=="backupImport"){
    if(request.StashItems is not {ValueKind:System.Text.Json.JsonValueKind.Object} incoming)throw new ArgumentException("请选择有效的存档备份 JSON 文件。");
    string label=request.Id.Trim();if(label.Length>120)label=label[..120];
    System.Text.Json.JsonElement payload=incoming;
    if(incoming.TryGetProperty("profile",out var inner)&&inner.ValueKind==System.Text.Json.JsonValueKind.Object){
     payload=inner;
     if(label.Length==0&&incoming.TryGetProperty("label",out var lbl)&&lbl.ValueKind==System.Text.Json.JsonValueKind.String)label=lbl.GetString()?.Trim()??"";
    }
    string raw=payload.GetRawText();
    var node=JsonNode.Parse(raw) as JsonObject??throw new ArgumentException("导入文件不是有效的存档 JSON。");
    ProfileRestoreRules.Validate(node,id=>templates.Items.ContainsKey(id));
    _=json.Deserialize<SptProfile>(raw)??throw new ArgumentException("导入文件无法转换为当前服务端存档。");
    string nick=ProfileRestoreRules.Nickname(raw);
    store.Add(session.ToString(),nick,label.Length>0?label:"导入备份",raw);
    return http.GetBody(Status());
   }
   if(request.Action=="backupCreate"){
    string label=request.Id.Trim();if(label.Length>120)throw new ArgumentException("备份名称最多 120 字。");
    store.Add(session.ToString(),ProfileRestoreRules.Nickname(current),label.Length>0?label:"手动备份",current);return http.GetBody(Status());
   }
   if(request.Action is not ("backupPreview" or "backupRestore"))throw new ArgumentException("未知备份操作。");
   string source=store.Read(request.Id);
   string result=ProfileRestoreRules.Build(source,current,request.Categories,id=>templates.Items.ContainsKey(new MongoId(id)));
   // Use the server's own serializer as a second compatibility check before
   // writing either the live profile dictionary or a profile file.
   var restored=json.Deserialize<SptProfile>(result)??throw new ArgumentException("备份无法转换为当前服务端存档。");
   if(restored.CharacterData?.PmcData?.Id!=profile.CharacterData?.PmcData?.Id)throw new ArgumentException("目标角色身份校验失败。");
   string preview=ProfileRestoreRules.Revision(request.Id+"\n"+string.Join(",",request.Categories)+"\n"+revision+"\n"+ProfileRestoreRules.Revision(source));
   if(request.Action=="backupPreview")return http.GetBody(new{ok=true,preview,revision,source=ProfileRestoreRules.Nickname(source),target=ProfileRestoreRules.Nickname(current),categories=request.Categories,itemCount=restored.CharacterData?.PmcData?.Inventory?.Items?.Count??0,requiresClientRestart=true});
   if(request.Revision!=revision||request.Preview!=preview)throw new ArgumentException("目标存档或选项已改变，请重新预览后还原。");
   ct.ThrowIfCancellationRequested();
   var rollback=store.Add(session.ToString(),ProfileRestoreRules.Nickname(current),"还原前自动备份",current);
   if(ProfileRestoreRules.Revision(json.Serialize(saves.GetProfile(session))!)!=revision)throw new ArgumentException("保存期间存档已改变，请重新预览。");
   try{
    // GetProfiles returns a copy. Update the existing live instance so other
    // server services holding this profile see the restored data as well.
    await LiveProfileState.RestoreAndSave(profile,restored,session,saves,json);
   }catch{
    LiveProfileState.Replace(profile,json.Deserialize<SptProfile>(current)??throw new IOException("原存档无法回退。"));
    try{await saves.SaveProfileAsync(session,CancellationToken.None);}catch{throw new IOException("还原写入失败，内存已回退。请保留数据库和还原前备份 "+rollback.Id+"，关闭客户端后检查存档文件。");}
    throw new IOException("还原保存失败，已恢复原存档。");
   }
   return http.GetBody(new{ok=true,restored=true,rollback=rollback.Id,requiresClientRestart=true,message="已还原。请退出客户端并重新登录后再操作物品或进入战局。"});
  }finally{Gate.Release();}
 }
}
