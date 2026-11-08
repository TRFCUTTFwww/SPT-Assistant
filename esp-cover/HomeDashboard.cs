using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.Counters;
using EFT.Quests;
using UnityEngine;
namespace LocalSpt5Esp;

// Only the open dashboard polls. Native inventory enumeration is split across frames;
// publish the completed snapshot atomically so GUI layout and repaint see the same data.
internal static class HomeDashboard {
 sealed class Snapshot {
  internal string Id="",Name="",Side="",Created="—";
  internal int Level,Experience,Started,Ready,Done;
  internal long Sessions,Survived,Kills,Deaths;
  internal bool MoneyReady,StatsReady,QuestsReady;
  internal long[] Money=new long[3];
 }
 static Snapshot? saved;
 static IEnumerator<int>? scan;
 static string owner="";static long pointer;static float nextRead;
 static Vector2 scroll;
 static TarkovApplication? application;
 static Profile? Current(){
  application??=Singleton<TarkovApplication>.Instance??Singleton<AbstractApplication>.Instance?.TryCast<TarkovApplication>()??UnityEngine.Object.FindObjectOfType<TarkovApplication>();
  var session=application?.GetClientBackEndSession()?.TryCast<ClientBackendSession>()??application?.Session?.TryCast<ClientBackendSession>();
  // The backend PMC remains the overview owner during Scav raids.
  var pmc=session?.Profile;
  return pmc?.Side==EPlayerSide.Savage?null:pmc;
 }
 internal static void Reset(){scan?.Dispose();scan=null;saved=null;owner="";pointer=0;nextRead=0;application=null;}
 internal static void Tick(){
  if(!InterfaceMenu.HomePage){scan?.Dispose();scan=null;nextRead=0;return;}
  try{
   var pmc=Current();
   if(pmc==null){scan?.Dispose();scan=null;saved=null;owner="";pointer=0;return;}
   if(!HomeOverviewRules.SameOwner(owner,pointer,pmc.Id,pmc.Pointer.ToInt64())){scan?.Dispose();scan=null;saved=null;owner=pmc.Id;pointer=pmc.Pointer.ToInt64();nextRead=0;}
   if(scan==null&&Time.unscaledTime>=nextRead){nextRead=Time.unscaledTime+5;scan=Read(pmc).GetEnumerator();}
   if(scan!=null&&!scan.MoveNext()){scan.Dispose();scan=null;}
  }catch{scan?.Dispose();scan=null;saved=null;nextRead=Time.unscaledTime+5;}
 }
 static IEnumerable<int> Read(Profile pmc){
  var info=pmc.Info;var result=new Snapshot{Id=pmc.Id,Name=info.Nickname,Side=info.Side.ToString(),Level=info.Level,Experience=info.Experience};
  try{if(info.RegistrationDate>0)result.Created=DateTimeOffset.FromUnixTimeSeconds(info.RegistrationDate).ToLocalTime().ToString("yyyy-MM-dd");}catch(ArgumentOutOfRangeException){}
  var counters=pmc.EftStats?.OverallCounters;
  if(counters!=null){
   result.Sessions=Count(counters,"Sessions","Pmc");result.Survived=Count(counters,"ExitStatus","Survived","Pmc");result.Kills=Count(counters,"Kills");result.Deaths=Count(counters,"Deaths");result.StatsReady=true;
  }
  yield return 0;
  var quests=pmc.QuestsData;
  if(quests!=null){var seen=new HashSet<string>();int budget=0;foreach(var quest in quests){if(quest==null||!seen.Add(quest.Id.ToString()))continue;switch(quest.Status){case EQuestStatus.Started:result.Started++;break;case EQuestStatus.AvailableForFinish:result.Ready++;break;case EQuestStatus.Success:result.Done++;break;}if(++budget%64==0)yield return 0;}result.QuestsReady=true;}
  yield return 0;
  var inventory=pmc.Inventory;
  if(inventory!=null){var seen=new HashSet<string>();int budget=0;foreach(var item in inventory.AllRealPlayerItems){if(item!=null&&seen.Add(item.Id.ToString())){int currency=HomeOverviewRules.Currency(item.StringTemplateId);if(currency>=0)result.Money[currency]+=Math.Max(1,item.StackObjectsCount);}if(++budget%64==0)yield return 0;}result.MoneyReady=true;}
  saved=result;
 }
 static long Count(CountersCollection counters,params string[] keys){var values=new Il2CppSystem.Object[keys.Length];for(int i=0;i<keys.Length;i++)values[i]=(Il2CppSystem.String)keys[i];return counters.GetLong(values);}
 static Color Muted=>Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.43f);
 static void Card(Rect r){InterfaceMenu.Surface(r,Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.055f),10);}
 static void Heading(float x,float y,float width,string text)=>InterfaceMenu.Label(new Rect(x,y,width,28),text,InterfaceMenu.Accent,16);
 internal static void Draw(Rect rect){
  float width=Math.Max(100,rect.width-20);int moneyColumns=HomeOverviewRules.Columns(width,190,3),detailColumns=HomeOverviewRules.Columns(width,300,2),linkColumns=HomeOverviewRules.Columns(width,210,3);
  float moneyHeight=(float)Math.Ceiling(3d/moneyColumns)*94,detailHeight=detailColumns==2?226:464,linksHeight=(float)Math.Ceiling(6d/linkColumns)*60;
  float total=182+moneyHeight+48+detailHeight+48+linksHeight+50;
  scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,width,Math.Max(rect.height-1,total)));
  try{
   var s=saved;var hero=new Rect(0,0,width,164);Card(hero);InterfaceMenu.Surface(new Rect(0,12,3,140),InterfaceMenu.Accent,1);
   InterfaceMenu.Label(new Rect(20,12,width-124,24),"当前 PMC 存档",Muted,12);
   if(InterfaceMenu.Button(new Rect(width-94,14,76,28),"刷新"))nextRead=0;
   // User-chosen nicknames are rendered as data, outside the translation catalog.
   InterfaceMenu.Label(new Rect(20,43,Math.Max(80,width-40),36),s?.Name??I18n.Text(scan!=null?"正在读取存档…":"等待登录角色"),InterfaceMenu.Text,25,translate:false);
   InterfaceMenu.Label(new Rect(20,89,width-40,26),s==null?"登录角色后显示存档概览":$"{s.Side}  ·  Lv. {s.Level}  ·  XP {s.Experience:N0}",InterfaceMenu.Accent,17);
   InterfaceMenu.Paragraph(new Rect(20,126,width-40,34),s==null?"":I18n.Text("创建日期")+"  "+s.Created+"    /    "+I18n.Text(RaidClock.Active?(Singleton<GameWorld>.Instance?.MainPlayer?.Side==EPlayerSide.Savage?"当前出战：Scav":"当前出战：PMC"):"待命"),Muted,12);
   float y=182,cell=(width-12*(moneyColumns-1))/moneyColumns;string[] names={"卢布","美元","欧元"};string[] signs={"₽","$","€"};
   for(int i=0;i<3;i++){var r=new Rect(i%moneyColumns*(cell+12),y+i/moneyColumns*94,cell,82);Card(r);InterfaceMenu.Label(new Rect(r.x+16,r.y+9,r.width-32,23),names[i],Muted,12);InterfaceMenu.Label(new Rect(r.x+16,r.y+36,r.width-32,34),s?.MoneyReady==true?signs[i]+" "+s.Money[i].ToString("N0"):"—",InterfaceMenu.Text,22);}
   y+=moneyHeight;InterfaceMenu.Paragraph(new Rect(4,y,width-8,36),"资金统计当前已加载的 PMC 库存；不含邮件、未成交挂单及商城钱包。",Muted,11);y+=48;
   float detailWidth=detailColumns==2?(width-12)/2:width;
   Stats(new Rect(0,y,detailWidth,226),s);Quests(new Rect(detailColumns==2?detailWidth+12:0,y+(detailColumns==2?0:238),detailWidth,226),s);
   y+=detailHeight+16;Heading(4,y,width-8,"常用入口");y+=32;
   string[] titles={"任务地点定位","添加物品","角色数据","时间与环境","界面布局","服务端设置"};string[] sections={"传送与导航","物品与装备","数据","战局与物资","镜头与画面","服务端"};string[] pages={"任务定位","添加物品","技能","时间与环境","界面布局","配置参数"};
   float linkWidth=(width-12*(linkColumns-1))/linkColumns;
   for(int i=0;i<titles.Length;i++)if(InterfaceMenu.Button(new Rect(i%linkColumns*(linkWidth+12),y+i/linkColumns*60,linkWidth,48),titles[i]))InterfaceMenu.Navigate(sections[i],pages[i]);
   y+=linksHeight;InterfaceMenu.Paragraph(new Rect(4,y,width-8,38),scan!=null?"正在刷新存档概览…":s==null?"存档尚未就绪，可稍后点击刷新。":"打开主页时每 5 秒刷新；战局战绩以结算后的存档为准。",Muted,11);
  }finally{GUI.EndScrollView();}
 }
 static void Row(Rect r,int index,string key,string value,Color? color=null){float y=r.y+48+index*38;InterfaceMenu.Label(new Rect(r.x+18,y,r.width*.48f-18,30),key,Muted,13);InterfaceMenu.Label(new Rect(r.x+r.width*.48f,y,r.width*.52f-18,30),value,color??InterfaceMenu.Text,17);}
 static void Stats(Rect r,Snapshot? s){Card(r);Heading(r.x+18,r.y+12,r.width-36,"累计战绩");bool ready=s?.StatsReady==true;double? rate=ready?HomeOverviewRules.Survival(s!.Sessions,s.Survived):null;Row(r,0,"总场次",ready?s!.Sessions.ToString("N0"):"—");Row(r,1,"生还率",rate.HasValue?rate.Value.ToString("P1"):"—",InterfaceMenu.Accent);Row(r,2,"总击杀",ready?s!.Kills.ToString("N0"):"—");Row(r,3,"阵亡次数",ready?s!.Deaths.ToString("N0"):"—");}
 static void Quests(Rect r,Snapshot? s){Card(r);Heading(r.x+18,r.y+12,r.width-36,"任务进度");bool ready=s?.QuestsReady==true;Row(r,0,"进行中",ready?s!.Started.ToString("N0"):"—");Row(r,1,"可提交",ready?s!.Ready.ToString("N0"):"—",InterfaceMenu.Accent);Row(r,2,"已完成",ready?s!.Done.ToString("N0"):"—");if(InterfaceMenu.Button(new Rect(r.x+18,r.y+178,r.width-36,32),"查看剧情与任务"))InterfaceMenu.Navigate("剧情","商人任务");}
}
