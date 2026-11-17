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
 static readonly HashSet<string> selected=new(){"stash"};static Row[] rows=Array.Empty<Row>();static Task<string>? request;
 static string action="",label="",picked="",revision="",preview="",previewText="",notice="";static bool loaded,restored;static Vector2 scroll;
 static void Invalidate(){preview=previewText="";}
 static void Begin(string operation){if(request!=null)return;if(ItemSpawner.InRaid()){notice="请退出战局后备份或还原。";return;}action=operation;request=CharacterDataPanel.Call("/local/gear/data","characterData",new(){{"action",operation},{"id",operation=="backupCreate"?label:picked},{"categories",selected.OrderBy(s=>s).ToArray()},{"revision",revision},{"preview",preview}});notice="处理中…";}
 internal static void Tick(){if(request?.IsCompleted!=true)return;var task=request;request=null;try{using var doc=JsonDocument.Parse(task.GetAwaiter().GetResult());var root=doc.RootElement;if(root.TryGetProperty("err",out var code)&&code.GetInt32()!=0)throw new ArgumentException(root.TryGetProperty("errmsg",out var message)?message.GetString():"备份操作失败。");var data=CharacterDataPanel.Data(root);
   if(data.TryGetProperty("backups",out var list)){rows=list.EnumerateArray().Select(e=>new Row(e.GetProperty("Id").GetString()!,e.GetProperty("Session").GetString()!,e.GetProperty("Nickname").GetString()!,e.GetProperty("Created").GetString()!,e.GetProperty("Label").GetString()!,e.GetProperty("Bytes").GetInt32())).ToArray();revision=data.GetProperty("revision").GetString()!;Invalidate();notice=action=="backupCreate"?"备份已写入 SQLite。":"已读取历史备份。";}
   if(data.TryGetProperty("preview",out var p)){preview=p.GetString()!;revision=data.GetProperty("revision").GetString()!;previewText="来源："+data.GetProperty("source").GetString()+" → 当前角色："+data.GetProperty("target").GetString()+"\n还原后 PMC 物品总数："+data.GetProperty("itemCount").GetInt32()+"。还原将替换所选类别，执行前自动备份当前存档。";notice="预览完成，请核对来源和还原范围。";}
   if(data.TryGetProperty("restored",out var result)&&result.GetBoolean()){restored=true;Invalidate();notice="已还原。请立即退出客户端并重新登录，再操作物品或进入战局。";}
  }catch(Exception e){notice=e.Message;Invalidate();}}
 internal static void Reset(){request=null;rows=Array.Empty<Row>();loaded=restored=false;picked=revision=preview=previewText=notice="";}
 internal static void Draw(Rect rect){
  Tick();if(!loaded){loaded=true;Begin("backupList");}float w=rect.width-26;var area=new Rect(rect.x,rect.y,rect.width,Math.Max(50,rect.height-68));float height=750+rows.Length*74;scroll=GUI.BeginScrollView(area,scroll,new Rect(0,0,rect.width-20,height));bool enabled=GUI.enabled;GUI.enabled=enabled&&request==null&&!ItemSpawner.InRaid()&&!restored;
  try{float y=0;InterfaceMenu.Label(new(0,y,w,30),"SQLite 存档备份与还原",InterfaceMenu.Accent,19);y+=44;
   InterfaceMenu.Paragraph(new(0,y,w,66),"每份备份保存完整游戏存档，可在当前或新建角色中按类别还原。数据库位于服务端 LocalGearMail/backups/profiles.sqlite，复制该文件即可搬迁历史备份。",null,12);y+=76;
   label=InterfaceMenu.Field(new(0,y,w-270,32),label);if(InterfaceMenu.Button(new(w-258,y,148,32),"备份当前角色"))Begin("backupCreate");if(InterfaceMenu.Button(new(w-100,y,100,32),"刷新列表"))Begin("backupList");y+=48;
   InterfaceMenu.Label(new(0,y,w,25),"还原范围",InterfaceMenu.Accent,15);y+=32;
   foreach(var choice in choices){bool on=selected.Contains(choice.Key);if(InterfaceMenu.Button(new(0,y,w,30),(on?"✓ ":"○ ")+I18n.Text(choice.Title),on)){if(choice.Key=="all"){selected.Clear();selected.Add("all");}else{selected.Remove("all");if(!selected.Remove(choice.Key))selected.Add(choice.Key);}Invalidate();}y+=36;}
   bool old=GUI.enabled;GUI.enabled=old&&picked.Length>0&&selected.Count>0;if(InterfaceMenu.Button(new(0,y,(w-12)/2,34),"预览还原结果"))Begin("backupPreview");GUI.enabled=old&&preview.Length>0;if(InterfaceMenu.Button(new((w+12)/2,y,(w-12)/2,34),"确认替换所选数据"))Begin("backupRestore");GUI.enabled=old;y+=46;
   InterfaceMenu.Paragraph(new(0,y,w,82),previewText.Length>0?previewText:"先选择下方的历史备份，再预览还原。全部还原保留新账号登录信息及角色身份；还原后必须重新登录客户端。",null,12);y+=94;
   foreach(var row in rows){string time=DateTime.TryParse(row.Created,out var stamp)?stamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"):row.Created;if(InterfaceMenu.Button(new(0,y,w,64),row.Name+" · "+row.Label+"\n"+time+" · "+(row.Bytes/1024)+" KB · "+row.Id[..8],picked==row.Id)){picked=row.Id;Invalidate();}y+=74;}
   if(rows.Length==0)InterfaceMenu.Paragraph(new(0,y,w,45),"还没有备份。点击“备份当前角色”创建第一份。",null,13);
  }finally{GUI.enabled=enabled;GUI.EndScrollView();}
  InterfaceMenu.Paragraph(new(rect.x,rect.yMax-58,rect.width,56),notice,InterfaceMenu.Accent,13);
 }
}
