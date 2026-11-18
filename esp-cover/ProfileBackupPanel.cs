using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class ProfileBackupPanel {
 record Row(string Id,string Session,string Name,string Created,string Label,int Bytes);
 static readonly (string Key,string Title)[] choices={ ("stash","仓库物品（含容器、配件与仓库容量）"),("skills","技能"),("progress","等级、经验、战绩、成就与商城余额"),("health","生命值、能量与水分"),("quests","任务、任务计数与任务物品"),("traders","商人好感与解锁进度"),("all","全部游戏数据（含装备、藏身处、邮件与 MOD 数据）")};
 static readonly HashSet<string> selected=new(){"stash"};static Row[] rows=Array.Empty<Row>();static Task<string>? request;static Task<string?>? dialogTask;
 static string action="",label="",picked="",revision="",preview="",previewText="",notice="",confirmDelete="",dialogMode="",exportTarget="";static bool loaded,restored,autoPreRaid;static int autoKeep=5;static Vector2 scroll;
 static void Invalidate(){preview=previewText="";confirmDelete="";}
 static void Begin(string operation,Dictionary<string,object?>? extra=null){
  if(request!=null)return;if(ItemSpawner.InRaid()){notice="请退出战局后备份或还原。";return;}
  action=operation;confirmDelete="";
  var payload=new Dictionary<string,object?>{{"action",operation},{"id",operation=="backupCreate"?label:picked},{"categories",selected.OrderBy(s=>s).ToArray()},{"revision",revision},{"preview",preview}};
  if(extra!=null)foreach(var pair in extra)payload[pair.Key]=pair.Value;
  request=CharacterDataPanel.Call("/local/gear/data","characterData",payload);notice="处理中…";
 }
 static void PollDialog(){
  if(dialogTask is not {IsCompleted:true} done)return;
  string mode=dialogMode;dialogTask=null;dialogMode="";
  try{
   string? path=done.GetAwaiter().GetResult();if(string.IsNullOrWhiteSpace(path))return;
   if(mode=="export"){exportTarget=path;Begin("backupExport");}
   else if(mode=="import"){
    using var doc=JsonDocument.Parse(System.IO.File.ReadAllText(path));
    Begin("backupImport",new(){{"id",System.IO.Path.GetFileNameWithoutExtension(path)},{"stashItems",doc.RootElement.Clone()}});
   }
  }catch(Exception e){notice="备份文件选择失败："+e.GetBaseException().Message;}
 }
 internal static void Tick(){
  PollDialog();
  if(request?.IsCompleted!=true)return;var task=request;request=null;
  try{
   using var doc=JsonDocument.Parse(task.GetAwaiter().GetResult());var root=doc.RootElement;
   if(root.TryGetProperty("err",out var code)&&code.GetInt32()!=0)throw new ArgumentException(root.TryGetProperty("errmsg",out var message)?message.GetString():"备份操作失败。");
   var data=CharacterDataPanel.Data(root);
   if(data.TryGetProperty("autoPreRaid",out var apr))autoPreRaid=apr.GetBoolean();
   if(data.TryGetProperty("autoKeep",out var ak))autoKeep=ak.GetInt32();
   if(data.TryGetProperty("backups",out var list)){
    rows=list.EnumerateArray().Select(e=>new Row(e.GetProperty("Id").GetString()!,e.GetProperty("Session").GetString()!,e.GetProperty("Nickname").GetString()!,e.GetProperty("Created").GetString()!,e.GetProperty("Label").GetString()!,e.GetProperty("Bytes").GetInt32())).ToArray();
    revision=data.GetProperty("revision").GetString()!;if(picked.Length>0&&rows.All(r=>r.Id!=picked))picked="";Invalidate();
    notice=action switch{"backupCreate"=>"备份已写入 SQLite。","backupDelete"=>"已删除选中备份。","backupImport"=>"已从文件导入备份。","backupSettings"=>autoPreRaid?$"已开启进图前自动备份（保留最近 {autoKeep} 份）。":"已关闭进图前自动备份。",_=>"已读取历史备份。"};
   }
   if(data.TryGetProperty("export",out var exported)){
    string dir=exportTarget;exportTarget="";
    if(!string.IsNullOrWhiteSpace(dir)){
     string nick=exported.GetProperty("nickname").GetString()??"profile";
     string lbl=exported.GetProperty("label").GetString()??"backup";
     string bid=exported.GetProperty("id").GetString()??"snap";
     string safe=string.Concat((nick+"-"+lbl).Select(c=>System.IO.Path.GetInvalidFileNameChars().Contains(c)?'_':c));
     if(safe.Length>48)safe=safe[..48];
     string file=System.IO.Path.Combine(dir,$"{safe}-{(bid.Length>=8?bid[..8]:bid)}.profile-backup.json");
     System.IO.File.WriteAllText(file,JsonSerializer.Serialize(exported,new JsonSerializerOptions{WriteIndented=true}));
     notice="已导出备份到："+file;
    }
   }
   if(data.TryGetProperty("preview",out var p)){preview=p.GetString()!;revision=data.GetProperty("revision").GetString()!;previewText="来源："+data.GetProperty("source").GetString()+" → 当前角色："+data.GetProperty("target").GetString()+"\n还原后 PMC 物品总数："+data.GetProperty("itemCount").GetInt32()+"。还原将替换所选类别，执行前自动备份当前存档。";notice="预览完成，请核对来源和还原范围。";}
   if(data.TryGetProperty("restored",out var result)&&result.GetBoolean()){restored=true;Invalidate();notice="已还原。请立即退出客户端并重新登录，再操作物品或进入战局。";}
  }catch(Exception e){exportTarget="";notice=e.Message;Invalidate();}
 }
 internal static void Reset(){request=null;dialogTask=null;dialogMode=exportTarget="";rows=Array.Empty<Row>();loaded=restored=false;picked=revision=preview=previewText=notice=confirmDelete="";}
 internal static void Draw(Rect rect){
  Tick();if(!loaded){loaded=true;Begin("backupList");}float w=rect.width-26;var area=new Rect(rect.x,rect.y,rect.width,Math.Max(50,rect.height-68));float height=850+rows.Length*74;scroll=GUI.BeginScrollView(area,scroll,new Rect(0,0,rect.width-20,height));bool enabled=GUI.enabled;GUI.enabled=enabled&&request==null&&dialogTask==null&&!ItemSpawner.InRaid()&&!restored;
  try{float y=0;InterfaceMenu.Label(new(0,y,w,30),"SQLite 存档备份与还原",InterfaceMenu.Accent,19);y+=44;
   InterfaceMenu.Paragraph(new(0,y,w,66),"每份备份保存完整游戏存档，可在当前或新建角色中按类别还原。数据库位于服务端 LocalGearMail/backups/profiles.sqlite，复制该文件即可搬迁历史备份。",null,12);y+=76;
   label=InterfaceMenu.Field(new(0,y,w-270,32),label);if(InterfaceMenu.Button(new(w-258,y,148,32),"备份当前角色"))Begin("backupCreate");if(InterfaceMenu.Button(new(w-100,y,100,32),"刷新列表"))Begin("backupList");y+=44;
   float half=(w-12)/2;
   if(InterfaceMenu.Button(new(0,y,half,32),autoPreRaid?$"进图前自动备份 · 开启（保留 {autoKeep} 份）":"进图前自动备份 · 关闭",autoPreRaid))Begin("backupSettings",new(){{"settings",new Dictionary<string,object?>{{"autoPreRaid",!autoPreRaid},{"autoKeep",autoKeep}}}});
   if(InterfaceMenu.Button(new(half+12,y,half,32),dialogTask!=null?"选择中…":"从文件导入备份")){dialogMode="import";dialogTask=WindowsDialogs.PickOpenFile("导入存档备份","存档备份 (*.json)\0*.json\0所有文件 (*.*)\0*.*\0");}
   y+=44;
   InterfaceMenu.Label(new(0,y,w,25),"还原范围",InterfaceMenu.Accent,15);y+=32;
   foreach(var choice in choices){bool on=selected.Contains(choice.Key);if(InterfaceMenu.Button(new(0,y,w,30),(on?"✓ ":"○ ")+I18n.Text(choice.Title),on)){if(choice.Key=="all"){selected.Clear();selected.Add("all");}else{selected.Remove("all");if(!selected.Remove(choice.Key))selected.Add(choice.Key);}Invalidate();}y+=36;}
   bool old=GUI.enabled;GUI.enabled=old&&picked.Length>0&&selected.Count>0;if(InterfaceMenu.Button(new(0,y,half,34),"预览还原结果"))Begin("backupPreview");GUI.enabled=old&&preview.Length>0;if(InterfaceMenu.Button(new(half+12,y,half,34),"确认替换所选数据"))Begin("backupRestore");y+=44;
   GUI.enabled=old&&picked.Length>0;
   if(InterfaceMenu.Button(new(0,y,half,32),"导出选中到文件夹")){dialogMode="export";dialogTask=WindowsDialogs.PickFolder("选择存档备份导出目录");}
   if(InterfaceMenu.Button(new(half+12,y,half,32),confirmDelete==picked&&picked.Length>0?"再次点击确认删除":"删除选中备份",confirmDelete==picked&&picked.Length>0)){if(confirmDelete==picked)Begin("backupDelete");else{confirmDelete=picked;notice="请再次点击以永久删除该历史备份。";}}
   GUI.enabled=old;y+=44;
   InterfaceMenu.Paragraph(new(0,y,w,82),previewText.Length>0?previewText:"先选择下方的历史备份，再预览还原。全部还原保留新账号登录信息及角色身份；还原后必须重新登录客户端。",null,12);y+=94;
   foreach(var row in rows){string time=DateTime.TryParse(row.Created,out var stamp)?stamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"):row.Created;if(InterfaceMenu.Button(new(0,y,w,64),row.Name+" · "+row.Label+"\n"+time+" · "+(row.Bytes/1024)+" KB · "+row.Id[..8],picked==row.Id)){picked=row.Id;Invalidate();}y+=74;}
   if(rows.Length==0)InterfaceMenu.Paragraph(new(0,y,w,45),"还没有备份。点击“备份当前角色”创建第一份。",null,13);
  }finally{GUI.enabled=enabled;GUI.EndScrollView();}
  InterfaceMenu.Paragraph(new(rect.x,rect.yMax-58,rect.width,56),notice,InterfaceMenu.Accent,13);
 }
}
