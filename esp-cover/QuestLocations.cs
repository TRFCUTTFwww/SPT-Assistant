using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.Quests;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class QuestLocations {
 sealed record Anchor(TriggerWithId Trigger,Vector3 Position);
 internal sealed record Row(string Key,string Quest,string Objective,Vector3 Position);
 static readonly Dictionary<string,List<Anchor>> anchors=new(StringComparer.Ordinal);
 static readonly List<Row> rows=new();
 static readonly List<Rect> occupied=new();
 static ConfigEntry<bool> enabled=null!;
 static ConfigEntry<float> range=null!;
 static ConfigEntry<int> maxLabels=null!;
 static IntPtr world;
 static float nextRead,nextIndex,nextError;
 static IEnumerator<TriggerWithId>? scanner;
 static bool indexed;
 static string filter="",status="",rangeDraft="",labelsDraft="",settingsError="";
 static Vector2 scroll;
 static int spatial,matched;
 internal static void Initialize(){
  enabled=FeatureConfig.Bind("60. 任务定位","Quest location markers","显示任务地点",true,"显示已接且未完成任务在当前地图的地点、目标和距离。");
  range=FeatureConfig.Bind("60. 任务定位","Quest location distance","任务地点显示距离",2000f,"米",new AcceptableValueRange<float>(10,5000));
  maxLabels=FeatureConfig.Bind("60. 任务定位","Maximum quest labels","最多任务地点标签",15,"优先显示最近地点。",new AcceptableValueRange<int>(1,60));
  rangeDraft=range.Value.ToString(CultureInfo.InvariantCulture);labelsDraft=maxLabels.Value.ToString(CultureInfo.InvariantCulture);
 }
 internal static void Reset(){scanner?.Dispose();scanner=null;anchors.Clear();rows.Clear();world=IntPtr.Zero;nextRead=nextIndex=0;indexed=false;spatial=matched=0;status="";}
 static bool Page=>InterfaceMenu.QuestLocationsPage;
 internal static void Tick(GameWorld? current,Player? player){
  if(!RaidClock.Active||current==null||player==null){if(world!=IntPtr.Zero)Reset();return;}
  if(world!=current.Pointer){Reset();world=current.Pointer;}
  if(!enabled.Value&&!Page&&!GroundNavigation.WantsQuestData)return;
  try{
   if(scanner==null&&Time.unscaledTime>=nextIndex){
    nextIndex=Time.unscaledTime+30;indexed=false;anchors.Clear();
    scanner=((IEnumerable<TriggerWithId>)UnityEngine.Object.FindObjectsOfType<TriggerWithId>(true).ToArray()).GetEnumerator();
   }
   if(scanner!=null){
    for(int i=0;i<32;i++){
     if(!scanner.MoveNext()){scanner.Dispose();scanner=null;indexed=true;nextRead=0;break;}
     var trigger=scanner.Current;if(trigger==null)continue;
     var collider=trigger.GetComponent<Collider>();var position=collider!=null&&collider.bounds.size.sqrMagnitude>.0001f?collider.bounds.center:trigger.transform.position;
     if(!TeleportRules.Finite(new(position.x,position.y,position.z)))continue;
     var ids=new HashSet<string>(StringComparer.Ordinal);if(!string.IsNullOrWhiteSpace(trigger.Id))ids.Add(trigger.Id);
     var place=trigger.TryCast<PlaceItemTrigger>();if(place!=null&&!string.IsNullOrWhiteSpace(place.TriggerId))ids.Add(place.TriggerId);
     foreach(string id in ids){if(!anchors.TryGetValue(id,out var list))anchors[id]=list=new();list.Add(new(trigger,position));}
    }
   }
   if(indexed&&Time.unscaledTime>=nextRead){nextRead=Time.unscaledTime+1;Read(player);}
  }catch(Exception error){nextRead=Time.unscaledTime+10;nextIndex=Time.unscaledTime+30;scanner?.Dispose();scanner=null;Warn(error);}
 }
 static void Read(Player player){
  rows.Clear();spatial=matched=0;var seen=new HashSet<string>(StringComparer.Ordinal);var quests=new List<Quest>();
  if(player.QuestController?.Quests!=null)foreach(var quest in player.QuestController.Quests)if(quest!=null&&seen.Add(quest.Id.ToString()))quests.Add(quest);
  if(player.Profile?.QuestsData!=null)foreach(var data in player.Profile.QuestsData)if(data?.Quest!=null&&seen.Add(data.Quest.Id.ToString()))quests.Add(data.Quest);
  var keys=new HashSet<string>(StringComparer.Ordinal);
  foreach(var quest in quests){
   if(quest.QuestStatus!=EQuestStatus.Started||!quest.Conditions.TryGetValue(EQuestStatus.AvailableForFinish,out var conditions))continue;
   foreach(var condition in conditions)if(condition!=null)Walk(quest,condition,"",keys,0);
  }
  status=spatial==0?"当前没有未完成的地点目标。":$"当前地图匹配 {matched} / {spatial} 个地点目标；其他目标可能位于别的地图，或没有原生坐标。";
  var origin=player.Position;rows.Sort((a,b)=>(a.Position-origin).sqrMagnitude.CompareTo((b.Position-origin).sqrMagnitude));
 }
 static void Walk(Quest quest,Condition condition,string inherited,HashSet<string> keys,int depth){
  if(depth>8||!QuestLocationRules.Eligible(quest.QuestStatus==EQuestStatus.Started,quest.IsConditionDone(condition)))return;
  var counter=condition.TryCast<ConditionCounterCreator>();
  if(counter!=null){if(counter.Conditions!=null)foreach(var child in counter.Conditions)if(child!=null)Walk(quest,child,Description(condition,inherited),keys,depth+1);return;}
  string[] ids;
  var zone=condition.TryCast<ConditionZone>();var inside=condition.TryCast<ConditionInZone>();
  if(zone!=null)ids=QuestLocationRules.Zones("zone",null,zone.zoneIds?.ToArray());
  else if(inside!=null)ids=QuestLocationRules.Zones("in-zone",null,inside.zoneIds?.ToArray());
  else if(condition.TryCast<ConditionVisitPlace>() is {} visit)ids=QuestLocationRules.Zones("visit",visit.target,null);
  else if(condition.TryCast<ConditionLocationTrigger>() is {} trigger)ids=QuestLocationRules.Zones("trigger",trigger.target,null);
  else return;
  if(ids.Length==0)return;spatial++;bool found=false;
  foreach(string id in ids){if(!anchors.TryGetValue(id,out var list))continue;
   foreach(var anchor in list){if(anchor.Trigger==null)continue;found=true;string key=quest.Id+"/"+condition.id+"/"+anchor.Trigger.Pointer.ToInt64();if(!keys.Add(key))continue;
    rows.Add(new(key,Clean(quest.Name),Description(condition,inherited),anchor.Position));
   }
  }
  if(found)matched++;
 }
 static string Clean(string value)=>value.Replace('\n',' ').Replace('\r',' ').Trim();
 static string Description(Condition condition,string fallback){try{var text=Clean(condition.FormattedDescription??"");if(text.Length>0)return text.Length>180?text[..177]+"…":text;}catch{}return fallback.Length>0?fallback:"任务地点";}
 static void Warn(Exception error){status="任务地点读取暂不可用："+error.Message;if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+20;Plugin.Self.Log.LogWarning(status);}
 internal static void DrawMarkers(Camera camera,Player local){
  if(!enabled.Value||!RaidClock.Active||Event.current.type!=EventType.Repaint)return;
  int count=0;occupied.Clear();
  foreach(var row in rows){
   float distance=Vector3.Distance(local.Position,row.Position);if(distance>range.Value)continue;
   var point=OverlayProjection.ScreenPoint(camera,row.Position);if(point.z<=.05f||point.x<0||point.x>Screen.width||point.y<0||point.y>Screen.height)continue;
   float x=Math.Clamp(MathF.Round(point.x-170),0,Math.Max(0,Screen.width-340)),y=MathF.Round(Screen.height-point.y-25);
   var rect=new Rect(x,y,340,46);int shifts=0;while(Overlaps(rect)&&shifts++<6)rect.y-=48;
   if(rect.y<0||rect.yMax>Screen.height||Overlaps(rect))continue;occupied.Add(rect);
   InterfaceMenu.Label(new Rect(rect.x,rect.y,rect.width,22),I18n.Text("任务地点")+" · "+row.Quest+" · "+distance.ToString("F0")+" m",new Color(.4f,1,.76f),13);
   InterfaceMenu.Label(new Rect(rect.x,rect.y+22,rect.width,22),row.Objective,Color.white,12);
   if(++count>=maxLabels.Value)break;
  }
 }
 static bool Overlaps(Rect rect){foreach(var other in occupied)if(other.Overlaps(rect))return true;return false;}
 internal static bool TryNavigationTarget(string key,out Vector3 position,out string name){
  position=default;name="";
  if(!RaidClock.Active||world==IntPtr.Zero||Singleton<GameWorld>.Instance?.Pointer!=world)return false;
  var row=rows.FirstOrDefault(row=>row.Key==key);if(row==null)return false;
  position=row.Position;name=row.Quest+" · "+row.Objective;return true;
 }
 internal static void Draw(Rect rect){
  InterfaceMenu.Label(new Rect(rect.x,rect.y,rect.width,28),"任务地点定位",InterfaceMenu.Accent,18);
  float col=(rect.width-24)/3;
  InterfaceMenu.Label(new Rect(rect.x+(col+12)*2,rect.y+6,col,24),"筛选任务",null,12);
  if(InterfaceMenu.Button(new Rect(rect.x,rect.y+38,col,32),enabled.Value?"任务地点 · 开启":"任务地点 · 关闭",enabled.Value))enabled.Value=!enabled.Value;
  if(InterfaceMenu.Button(new Rect(rect.x+col+12,rect.y+38,col,32),"刷新任务地点")){nextIndex=nextRead=0;}
  filter=InterfaceMenu.Field(new Rect(rect.x+(col+12)*2,rect.y+38,col,32),filter);
  InterfaceMenu.Label(new Rect(rect.x,rect.y+86,96,24),"显示距离（米）",null,12);rangeDraft=InterfaceMenu.Field(new Rect(rect.x+100,rect.y+80,Math.Max(45,col-100),32),rangeDraft);
  InterfaceMenu.Label(new Rect(rect.x+col+12,rect.y+86,84,24),"最多标签",null,12);labelsDraft=InterfaceMenu.Field(new Rect(rect.x+col+100,rect.y+80,Math.Max(45,col-88),32),labelsDraft);
  if(InterfaceMenu.Button(new Rect(rect.x+(col+12)*2,rect.y+80,col,32),"应用显示设置")){
   if(float.TryParse(rangeDraft,NumberStyles.Float,CultureInfo.InvariantCulture,out var distance)&&float.IsFinite(distance)&&distance>=10&&int.TryParse(labelsDraft,out var labels)&&labels>=1&&labels<=60){range.Value=distance;maxLabels.Value=labels;settingsError="";}
   else settingsError="距离至少 10 米，标签数量为 1 至 60。";
  }
  InterfaceMenu.Paragraph(new Rect(rect.x,rect.y+122,rect.width,48),settingsError.Length>0?settingsError:!RaidClock.Active?"进入战局后读取已接任务的地点。":!indexed?"正在索引当前地图的任务地点…":status,null,12);
  var local=Singleton<GameWorld>.Instance?.MainPlayer;var visible=rows.Where(row=>RaidRules.Match(row.Quest+" "+row.Objective,"",filter)).OrderBy(row=>local==null?0:(row.Position-local.Position).sqrMagnitude).ToArray();
  var view=new Rect(rect.x,rect.y+180,rect.width,Math.Max(40,rect.height-235));scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,view.width-18,Math.Max(view.height,visible.Length*86)));
  int first=Math.Max(0,(int)(scroll.y/86)),last=Math.Min(visible.Length,first+(int)(view.height/86)+2);
  for(int i=first;i<last;i++){var row=visible[i];float y=i*86;
   InterfaceMenu.Label(new Rect(0,y,view.width-164,24),row.Quest+ (local==null?"":" · "+Vector3.Distance(local.Position,row.Position).ToString("F0")+" m"),InterfaceMenu.Accent,14);
   InterfaceMenu.Paragraph(new Rect(0,y+27,view.width-164,50),row.Objective,null,12);
   bool navigating=GroundNavigation.IsQuestTarget(row.Key);
   if(InterfaceMenu.Button(new Rect(view.width-145,y+22,117,33),navigating?"当前目标":"导航到这里",navigating))GroundNavigation.ToQuest(row.Key);
  }
  GUI.EndScrollView();InterfaceMenu.Paragraph(new Rect(rect.x,rect.yMax-42,rect.width,40),"仅显示当前地图中能匹配原生坐标的未完成目标；任务物品可在「任务物资」中高亮。",null,12);
 }
 internal static object Diagnostics()=>new{enabled=enabled.Value,indexed,indexing=scanner!=null,zones=anchors.Count,spatial,matched,markers=rows.Count,status};
}
