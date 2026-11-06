using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class StoryPanel {
 static Task<string>? request;static JsonElement data,preview;static bool requested,ready,confirm;
 static string status="",search="",selected="",selectedName="",action="",lastPage="";static int state=2;static Vector2 scroll;
 static readonly string[] stateNames={"未开启","可接取","进行中","可交付","已完成","已失败","可重试","失败","已过期","等待中","自动接取"};
 static string State(int value)=>value>=0&&value<stateNames.Length?stateNames[value]:"未知";
 static void Begin(string name,string id="",int target=2){if(request!=null)return;request=CharacterDataPanel.Call("/local/gear/story","storyManagement",new(){{"action",name},{"id",id},{"state",target},{"revision",ready?data.GetProperty("revision").GetString():""}});status="处理中…";}
 internal static void Draw(Rect rect,string page){
  if(ItemSpawner.InRaid()){InterfaceMenu.Label(rect,"请退出战局后再管理剧情。",InterfaceMenu.Accent);return;}
  if(!requested){requested=true;Begin("status");}
  if(lastPage!=page){lastPage=page;scroll=Vector2.zero;search="";confirm=false;}
  if(request?.IsCompleted==true){try{using var doc=JsonDocument.Parse(request.GetAwaiter().GetResult());var root=doc.RootElement;
   if(root.TryGetProperty("err",out var err)&&err.TryGetInt32(out int code)&&code!=0)status=root.TryGetProperty("errmsg",out var message)?message.GetString()??"操作失败":"操作失败";
   else{var result=CharacterDataPanel.Data(root);if(result.TryGetProperty("preview",out _)){preview=result.Clone();confirm=true;status="请查看受影响的任务。";}else{data=result.Clone();ready=true;confirm=false;status="已读取剧情进度；保存后重新登录角色生效。";}}
  }catch{status="服务响应无法读取，请刷新。";}request=null;}
  InterfaceMenu.Label(new Rect(rect.x,rect.y,rect.width-90,30),page,InterfaceMenu.Accent,18);bool previous=GUI.enabled;GUI.enabled=previous&&request==null;
  if(InterfaceMenu.Button(new Rect(rect.xMax-80,rect.y,80,30),"刷新")){confirm=false;Begin("status");}
  var body=new Rect(rect.x,rect.y+48,rect.width,Math.Max(40,rect.height-104));
  if(ready){if(confirm)Confirm(body);else if(page=="等待时间")Waits(body);else if(page=="结局")Endings(body);else Quests(body,page);}
  GUI.enabled=previous;InterfaceMenu.Paragraph(new Rect(rect.x,rect.yMax-46,rect.width,46),status,null,12);
 }
 static void Choose(string id,string name,string operation,int target=2){selected=id;selectedName=name;action=operation;state=target;Begin("preview",id);}
 static void Quests(Rect r,string page){
  search=InterfaceMenu.Field(new Rect(r.x,r.y,r.width,30),search);bool story=page!="商人任务";
  var rows=data.GetProperty("quests").EnumerateArray().Where(q=>page=="剧情走向"?q.GetProperty("alternatives").GetArrayLength()>0||q.GetProperty("story").GetBoolean()&&q.GetProperty("state").GetInt32()>0:q.GetProperty("story").GetBoolean()==story).Where(q=>search.Length==0||q.GetProperty("name").GetString()!.Contains(search,StringComparison.OrdinalIgnoreCase)||q.GetProperty("id").GetString()!.Contains(search,StringComparison.OrdinalIgnoreCase)).ToArray();
  var active=data.GetProperty("chapters").EnumerateArray().Where(c=>c.GetProperty("state").GetInt32() is 2 or 3).Select(c=>c.GetProperty("name").GetString()).ToArray();
  InterfaceMenu.Label(new Rect(r.x,r.y+36,r.width,26),"当前主线："+(active.Length==0?"暂无进行中的章节":string.Join("、",active)),InterfaceMenu.Accent,12);
  var view=new Rect(r.x,r.y+76,r.width,Math.Max(30,r.height-76));float width=view.width-25;int height=page=="剧情走向"?112:78;
  scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,view.width-18,rows.Length*height));
  for(int i=Math.Max(0,(int)(scroll.y/height)-1);i<Math.Min(rows.Length,(int)((scroll.y+view.height)/height)+2);i++){var row=rows[i];float y=i*height;string id=row.GetProperty("id").GetString()!,name=row.GetProperty("name").GetString()!;int value=row.GetProperty("state").GetInt32();InterfaceMenu.Paragraph(new Rect(0,y,width-155,45),name,null,14);InterfaceMenu.Label(new Rect(width-145,y,145,28),State(value),InterfaceMenu.Accent,12);
   if(page=="剧情走向"){foreach(var option in new[]{(Label:"重走此线",Value:2),(Label:"设为完成",Value:4),(Label:"设为失败",Value:5)}){int index=option.Value==2?0:option.Value==4?1:2;if(InterfaceMenu.Button(new Rect(index*(width+8)/3,y+53,(width-16)/3,31),option.Label))Choose(id,name,"state",option.Value);}y+=112;}
   else{if(InterfaceMenu.Button(new Rect(width-145,y+33,145,29),story?"重新开始":"重做任务"))Choose(id,name,"replay");y+=78;}
  }GUI.EndScrollView();
 }
 static void Confirm(Rect r){
  InterfaceMenu.Paragraph(new Rect(r.x,r.y,r.width,45),"目标："+selectedName,InterfaceMenu.Accent,15);
  InterfaceMenu.Paragraph(new Rect(r.x,r.y+50,r.width,70),action=="ending"?"切换当前结局和展示结局，保留已有奖励。":"会重置相关后续任务和对话，互斥任务将设为失败。已有物品和商人好感保留。",null,13);
  var affected=preview.GetProperty("affected").EnumerateArray().ToArray();var view=new Rect(r.x,r.y+125,r.width,Math.Max(30,r.height-180));scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,view.width-18,affected.Length*33));for(int i=0;i<affected.Length;i++)InterfaceMenu.Label(new Rect(0,i*33,view.width-25,30),affected[i].GetProperty("name").GetString()!,null,12);GUI.EndScrollView();
  if(InterfaceMenu.Button(new Rect(r.x,r.yMax-42,(r.width-12)/2,34),"应用修改",true))Begin(action,selected,state);
  if(InterfaceMenu.Button(new Rect(r.x+(r.width+12)/2,r.yMax-42,(r.width-12)/2,34),"取消"))confirm=false;
 }
 static void Waits(Rect r){InterfaceMenu.Paragraph(new Rect(r.x,r.y,r.width,68),"清除当前主线的接取等待时间。任务目标、等级及其他前置条件仍需满足。",null,14);if(InterfaceMenu.Button(new Rect(r.x,r.y+85,240,36),"跳过主线等待时间",true))Begin("waits");}
 static void Endings(Rect r){var rows=data.GetProperty("endings").EnumerateArray().ToArray();scroll=GUI.BeginScrollView(r,scroll,new Rect(0,0,r.width-18,rows.Length*118));for(int i=0;i<rows.Length;i++){var row=rows[i];string id=row.GetProperty("id").GetString()!,key=row.GetProperty("name").GetString()!;string name=CharacterDataPanel.Name(key+" name",key);float y=i*118;InterfaceMenu.Paragraph(new Rect(0,y,r.width-180,65),name,InterfaceMenu.Accent,14);if(InterfaceMenu.Button(new Rect(r.width-165,y,140,32),row.GetProperty("current").GetBoolean()?"当前结局":"切换至此结局")){selected=id;selectedName=name;action="ending";confirm=true;using var doc=JsonDocument.Parse("{\"affected\":[]}");preview=doc.RootElement.Clone();}InterfaceMenu.Label(new Rect(0,y+72,r.width-25,28),"切换结局不重复发放奖励。",null,12);}GUI.EndScrollView();}
}
