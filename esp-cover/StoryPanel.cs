using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using UnityEngine;

namespace LocalSpt5Esp;

internal static class StoryPanel {
 static Task<string>? request;
 static JsonElement data,preview;
 static bool requested,ready,confirm,showLiveSync;
 static string status="",search="",selected="",selectedName="",action="",lastPage="",requestAction="",traderFilter="";
 static int state=2,stateGroup;
 static Vector2 scroll;
 static readonly string[] stateNames={"未开启","可接取","进行中","可交付","已完成","已失败","可重试","失败","已过期","等待中","自动接取"};
 static readonly string[] groupLabels={"全部状态","未开启","可接取／等待","进行中／可交","已完成／失败"};

 static string State(int value)=>value>=0&&value<stateNames.Length?stateNames[value]:"未知";
 static string TraderId(JsonElement q)=>q.TryGetProperty("traderId",out var t)&&t.ValueKind==JsonValueKind.String?t.GetString()??"":"";
 static string TraderName(string id)=>string.IsNullOrEmpty(id)?"未知商人":CharacterDataPanel.Name(id+" Nickname",id);
 static bool MatchesStateGroup(int s)=>stateGroup switch{
  1=>s==0,
  2=>s is 1 or 6 or 9 or 10,
  3=>s is 2 or 3,
  4=>s is 4 or 5 or 7 or 8,
  _=>true
 };

 static void Begin(string name,string id="",int target=2){
  if(request!=null)return;
  showLiveSync=false;requestAction=name;
  string rev=ready&&data.ValueKind==JsonValueKind.Object&&data.TryGetProperty("revision",out var r)?r.GetString()??"":"";
  request=CharacterDataPanel.Call("/local/gear/story","storyManagement",new(){{"action",name},{"id",id},{"state",target},{"revision",rev}});
  status="处理中…";
 }

 internal static void Draw(Rect rect,string page){
  if(ItemSpawner.InRaid()){InterfaceMenu.Label(rect,"请退出战局后再管理剧情。",InterfaceMenu.Accent);return;}
  if(!requested){requested=true;Begin("status");}
  if(lastPage!=page){lastPage=page;scroll=Vector2.zero;search="";traderFilter="";stateGroup=0;confirm=false;}
  if(request?.IsCompleted==true){
   try{
    using var doc=JsonDocument.Parse(request.GetAwaiter().GetResult());var root=doc.RootElement;
    if(root.TryGetProperty("err",out var err)&&err.TryGetInt32(out int code)&&code!=0){
     string msg=root.TryGetProperty("errmsg",out var message)?message.GetString()??"操作失败":"操作失败";
     if(msg=="未知剧情操作。")msg="请点击右上角“热更新服务端”以启用忽略前置与实时生效功能。";
     status=msg;
    }else if(requestAction=="reload"){
     status="服务端管理模块已热更新。";
     request=null;Begin("status");return;
    }else{
     var result=CharacterDataPanel.Data(root);
     if(result.TryGetProperty("preview",out _)){preview=result.Clone();confirm=true;status="请查看受影响的任务。";}
     else{
      data=result.Clone();ready=true;confirm=false;
      bool mutated=requestAction!="status";
      status=mutated?"修改已保存，正在实时同步到游戏任务列表…":"已读取剧情与商人任务进度；修改保存后自动实时生效，无须重新登录。";
      showLiveSync=true;
      QuestWaitSync.Queue(result,fullSync:mutated);
     }
    }
   }catch{status="服务响应无法读取，请刷新。";}
   request=null;
  }

  InterfaceMenu.Label(new Rect(rect.x,rect.y,Math.Max(80,rect.width-240),30),page,InterfaceMenu.Accent,18);
  bool previous=GUI.enabled;GUI.enabled=previous&&request==null;
  if(InterfaceMenu.Button(new Rect(rect.xMax-226,rect.y,138,30),"热更新服务端"))
  {
   requestAction="reload";
   request=CharacterDataPanel.Call("/local/gear/reload","adminHotReload",new(){{"action","reload"}});
   status="正在热更新服务端管理模块…";
  }
  if(InterfaceMenu.Button(new Rect(rect.xMax-80,rect.y,80,30),"刷新")){confirm=false;Begin("status");}
  var body=new Rect(rect.x,rect.y+42,rect.width,Math.Max(40,rect.height-92));
  if(ready){
   if(confirm)Confirm(body);
   else if(page=="等待时间")Waits(body);
   else if(page=="结局")Endings(body);
   else Quests(body,page);
  }
  GUI.enabled=previous;
  string footer=request==null&&showLiveSync&&QuestWaitSync.Notice.Length>0?QuestWaitSync.Notice:status;
  InterfaceMenu.Paragraph(new Rect(rect.x,rect.yMax-44,rect.width,44),footer,InterfaceMenu.Accent,12);
 }

 static void Choose(string id,string name,string operation,int target=2){selected=id;selectedName=name;action=operation;state=target;Begin("preview",id);}

 static void Quests(Rect r,string page){
  bool story=page!="商人任务";
  var pageBase=data.GetProperty("quests").EnumerateArray().Where(q=>
   page=="剧情走向"?q.GetProperty("alternatives").GetArrayLength()>0||q.GetProperty("story").GetBoolean()&&q.GetProperty("state").GetInt32()>0
   :q.GetProperty("story").GetBoolean()==story).ToArray();

  float x=r.x,y=r.y,w=r.width;
  search=InterfaceMenu.Field(new Rect(x,y,w,28),search);
  if(search.Length==0)InterfaceMenu.Label(new Rect(x+8,y,w-20,28),"搜索任务名称、商人或任务编号…",Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.45f),11);
  y+=34;

  var traderGroups=pageBase.GroupBy(TraderId).Where(g=>g.Key.Length>0).Select(g=>(Id:g.Key,Name:TraderName(g.Key),Count:g.Count())).OrderByDescending(g=>g.Count).ThenBy(g=>g.Name).ToArray();
  string[] traders=new[]{$"全部商人 ({pageBase.Length})"}.Concat(traderGroups.Select(t=>$"{t.Name} ({t.Count})")).ToArray();
  int currentTrader=Array.FindIndex(traderGroups,t=>t.Id==traderFilter)+1;
  float half=(w-8)/2;
  int chosenTrader=ClickSelect.Draw(new(x,y,half,28),"story-trader",traders[currentTrader],traders,currentTrader);
  if(chosenTrader>=0){traderFilter=chosenTrader==0?"":traderGroups[chosenTrader-1].Id;scroll=Vector2.zero;}
  int chosenGroup=ClickSelect.Draw(new(x+half+8,y,half,28),"story-status",groupLabels[stateGroup],groupLabels,stateGroup);
  if(chosenGroup>=0){stateGroup=chosenGroup;scroll=Vector2.zero;}
  y+=32;

  if(story){
   var active=data.GetProperty("chapters").EnumerateArray().Where(c=>c.GetProperty("state").GetInt32() is 2 or 3).Select(c=>c.GetProperty("name").GetString()).ToArray();
   InterfaceMenu.Label(new Rect(x,y,w,24),"当前主线："+(active.Length==0?"暂无进行中的章节":string.Join("、",active)),InterfaceMenu.Accent,12);
   y+=28;
  }

  var rows=pageBase.Where(q=>{
   string tid=TraderId(q);
   if(traderFilter.Length>0&&tid!=traderFilter)return false;
   if(!MatchesStateGroup(q.GetProperty("state").GetInt32()))return false;
   if(search.Length==0)return true;
   string qName=q.GetProperty("name").GetString()??"",qId=q.GetProperty("id").GetString()??"",tName=TraderName(tid);
   return qName.Contains(search,StringComparison.OrdinalIgnoreCase)||qId.Contains(search,StringComparison.OrdinalIgnoreCase)||tName.Contains(search,StringComparison.OrdinalIgnoreCase);
  }).ToArray();

  var view=new Rect(x,y,w,Math.Max(30,r.yMax-y));
  float width=view.width-24;
  int columns=Math.Max(1,Math.Min(4,(int)((width-14)/126)));
  int rowHeight=54+(int)Math.Ceiling(4d/columns)*34+6;
  scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,view.width-18,Math.Max(view.height,rows.Length*rowHeight)));
  try{
   if(rows.Length==0)InterfaceMenu.Paragraph(new Rect(8,8,width-16,48),"没有符合当前商人／状态筛选的任务。",null,13);
   int first=Math.Max(0,(int)(scroll.y/rowHeight)-1),end=Math.Min(rows.Length,(int)((scroll.y+view.height)/rowHeight)+2);
   for(int i=first;i<end;i++){
    var row=rows[i];float ry=i*rowHeight;
    string id=row.GetProperty("id").GetString()!,name=row.GetProperty("name").GetString()!,tid=TraderId(row);
    int value=row.GetProperty("state").GetInt32();
    long waitSec=row.TryGetProperty("wait",out var wp)&&wp.TryGetInt64(out long wv)?wv:0;
    InterfaceMenu.Surface(new Rect(0,ry,width,rowHeight-6),Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.035f),8);
    string badge=tid.Length>0?$"[{TraderName(tid)}]  ":"";
    InterfaceMenu.Paragraph(new Rect(10,ry+6,width-180,46),badge+name,null,13);
    string stateLabel=State(value)+(waitSec>0?$" ({waitSec/60}m)":"");
    InterfaceMenu.Label(new Rect(width-166,ry+8,156,26),stateLabel,InterfaceMenu.Accent,12,translate:false);

    bool branching=page=="剧情走向";
    string[] actions=branching?["忽略前置启用","重走此线","设为完成","设为失败"]:["忽略前置启用","忽略前置可接","设为完成",story?"重置重开":"重做任务"];
    int operation=ActionButtons.Draw(new(10,ry+48,width-20,28),actions,out _);
    if(operation==0)Begin("forceStart",id);
    if(operation==1){if(branching)Choose(id,name,"state",2);else Begin("forceUnlock",id);}
    if(operation==2)Choose(id,name,"state",4);
    if(operation==3)Choose(id,name,branching?"state":"replay",branching?5:2);
   }
  }finally{GUI.EndScrollView();}
 }

 static void Confirm(Rect r){
  InterfaceMenu.Paragraph(new Rect(r.x,r.y,r.width,45),"目标："+selectedName,InterfaceMenu.Accent,15);
  InterfaceMenu.Paragraph(new Rect(r.x,r.y+50,r.width,70),action=="ending"?"切换当前结局和展示结局，保留已有奖励。":"会重置相关后续任务和对话，互斥任务将设为失败。已有物品和商人好感保留，保存后实时生效。",null,13);
  var affected=preview.GetProperty("affected").EnumerateArray().ToArray();
  var view=new Rect(r.x,r.y+125,r.width,Math.Max(30,r.height-180));
  scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,view.width-18,affected.Length*33));
  for(int i=0;i<affected.Length;i++)InterfaceMenu.Label(new Rect(0,i*33,view.width-25,30),affected[i].GetProperty("name").GetString()!,null,12);
  GUI.EndScrollView();
  if(InterfaceMenu.Button(new Rect(r.x,r.yMax-42,(r.width-12)/2,34),"应用修改",true))Begin(action,selected,state);
  if(InterfaceMenu.Button(new Rect(r.x+(r.width+12)/2,r.yMax-42,(r.width-12)/2,34),"取消"))confirm=false;
 }

 static void Waits(Rect r){
  InterfaceMenu.Paragraph(new Rect(r.x,r.y,r.width,44),"清除当前主线或支线／商人任务的接取倒计时（含隐藏等待）。",null,14);
  if(InterfaceMenu.Button(new Rect(r.x,r.y+58,240,36),"跳过主线等待时间",true))Begin("waits");
  if(InterfaceMenu.Button(new Rect(r.x,r.y+106,240,36),"跳过支线等待时间",true))Begin("sidewaits");
  InterfaceMenu.Paragraph(new Rect(r.x,r.y+158,r.width,44),"保存后自动实时同步到当前角色；重新打开商人任务页即可查看。",null,13);
 }

 static void Endings(Rect r){
  var rows=data.GetProperty("endings").EnumerateArray().ToArray();
  scroll=GUI.BeginScrollView(r,scroll,new Rect(0,0,r.width-18,rows.Length*118));
  for(int i=0;i<rows.Length;i++){
   var row=rows[i];string id=row.GetProperty("id").GetString()!,key=row.GetProperty("name").GetString()!;
   string name=CharacterDataPanel.Name(key+" name",key);float y=i*118;
   InterfaceMenu.Paragraph(new Rect(0,y,r.width-180,65),name,InterfaceMenu.Accent,14);
   if(InterfaceMenu.Button(new Rect(r.width-165,y,140,32),row.GetProperty("current").GetBoolean()?"当前结局":"切换至此结局")){
    selected=id;selectedName=name;action="ending";confirm=true;
    using var doc=JsonDocument.Parse("{\"affected\":[]}");preview=doc.RootElement.Clone();
   }
   InterfaceMenu.Label(new Rect(0,y+72,r.width-25,28),"切换结局不重复发放奖励。",null,12);
  }
  GUI.EndScrollView();
 }
}
