using System.Text.Json.Nodes;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;
public static class ProfileBackupService {
 static readonly string Database=System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","backups","profiles.sqlite");
 static readonly SemaphoreSlim Gate=new(1,1);
 public static async ValueTask<string> Handle(CharacterDataRequest request,MongoId session,SptProfile profile,SaveServer saves,JsonUtil json,HttpResponseUtil http,TemplateTable templates,CancellationToken ct){
  await Gate.WaitAsync(ct);try{
   var store=new SqliteSnapshots(Database);string current=json.Serialize(profile)!;string revision=ProfileRestoreRules.Revision(current);
   object Status()=>new{ok=true,revision,backups=store.List(),categories=ProfileRestoreRules.Categories,database="LocalGearMail/backups/profiles.sqlite"};
   if(request.Action=="backupList")return http.GetBody(Status());
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
    saves.GetProfiles()[session]=restored;await saves.SaveProfileAsync(session,CancellationToken.None);
   }catch{
    saves.GetProfiles()[session]=profile;
    try{await saves.SaveProfileAsync(session,CancellationToken.None);}catch{throw new IOException("还原写入失败，内存已回退。请保留数据库和还原前备份 "+rollback.Id+"，关闭客户端后检查存档文件。");}
    throw new IOException("还原保存失败，已恢复原存档。");
   }
   return http.GetBody(new{ok=true,restored=true,rollback=rollback.Id,requiresClientRestart=true,message="已还原。请退出客户端并重新登录后再操作物品或进入战局。"});
  }finally{Gate.Release();}
 }
}
