using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using CommonAssets.Scripts.Game;
using EFT;
using EFT.Interactive;
using EFT.MovingPlatforms;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal enum ExfilPanelMode { Off, Hold, Always }
internal enum ExfilMarkerMode { Off, Nearest, All }
internal static class ExfilDisplay {
 static ConfigEntry<bool> Enabled=null!,ShowSpecial=null!,ShowLeverMarkers=null!,Force=null!,IgnoreRequirements=null!;
 static ConfigEntry<ExfilPanelMode> PanelMode=null!;
 static ConfigEntry<ExfilMarkerMode> MarkerMode=null!;
 static ConfigEntry<KeyCode> ForceKey=null!,IgnoreKey=null!,PanelKey=null!;
 static ConfigEntry<int> Lines=null!,PanelWidth=null!;
 static readonly List<Row> rows=new();
 static readonly Dictionary<IntPtr,Original> originals=new();
 static readonly Dictionary<IntPtr,(ExfiltrationPoint Point,EExfiltrationStatus Status)> conditionStatuses=new();
 static readonly Dictionary<IntPtr,(ExfiltrationPoint Point,int RaidId)> queuedCars=new();
 static readonly HashSet<IntPtr> seenPresent=new();
 static readonly List<LeverMarker> leverMarkers=new();
 static readonly Dictionary<IntPtr,List<Switch>> linkedSwitches=new();
 static float nextScan,nextError;
 static float nextSwitchScan;
 static IntPtr worldPointer;
 static bool lastForce,lastIgnore,hooksReady,conditionsReady,loggedDuplicateScan;
 static readonly HashSet<IntPtr> permissionOverrides=new();
 static string notice="";
 static bool layoutDirty=true;
 static GUIStyle? panelStyle;
 static int layoutWidth,layoutHeight,layoutFont,pageIndex,layoutLimit;
 static string nearestCaption="";
 static readonly List<(string Text,Color Color)> headers=new(),captions=new();
 static readonly List<float> headerHeights=new(),captionHeights=new();
 static List<PanelPages.Page> pages=new();
 static float headerHeight;
 static int transitRevision=-1,btrRevision=-1;
 sealed record Original(ExfiltrationPoint Point,Il2CppStringArray Entries,bool AddedScavId,string ProfileId);
 internal sealed record NavigationExit(string Id,string Name,string State,bool Available,float Distance,Vector3 Position);
 static NavigationExit RouteExit(Row row,Vector3 origin){var collider=row.Point._collider??row.Point.ExtendedCollider;var position=collider!=null?collider.ClosestPoint(origin):row.Position;return new(row.Point.Pointer.ToInt64().ToString(),row.Name,row.State,row.Available,Vector3.Distance(origin,position),position);}
 internal static IReadOnlyList<NavigationExit> NavigationRows {get{var origin=Singleton<GameWorld>.Instance?.MainPlayer?.Position??Vector3.zero;return rows.Select(r=>RouteExit(r,origin)).ToArray();}}
 internal static bool TryNavigationExit(string id,out NavigationExit? exit){exit=null;var row=rows.FirstOrDefault(r=>r.Point.Pointer.ToInt64().ToString()==id);var player=Singleton<GameWorld>.Instance?.MainPlayer;if(row==null||player==null)return false;exit=RouteExit(row,player.Position);return true;}
 sealed record Row(ExfiltrationPoint Point,string Name,string Kind,string State,bool Available,float Distance,Vector3 Position,bool Forced,bool InPanel,string TrainMarker,ExfilFaction Faction);
 sealed record LeverMarker(Switch Switch,string ExitName,Vector3 Position,float Distance,bool Open);
 internal static void DrawQuickActions(Rect rect){float width=(rect.width-16)/3;
  if(InterfaceMenu.Button(new Rect(rect.x,rect.y,width,40),"显示全部撤离点",Enabled.Value&&MarkerMode.Value==ExfilMarkerMode.All)){Enabled.Value=true;MarkerMode.Value=ExfilMarkerMode.All;}
  if(InterfaceMenu.Button(new Rect(rect.x+width+8,rect.y,width,40),"标记最近可用撤离点",Enabled.Value&&MarkerMode.Value==ExfilMarkerMode.Nearest)){Enabled.Value=true;MarkerMode.Value=ExfilMarkerMode.Nearest;}
  if(InterfaceMenu.Button(new Rect(rect.x+2*(width+8),rect.y,width,40),"关闭空间标记",MarkerMode.Value==ExfilMarkerMode.Off))MarkerMode.Value=ExfilMarkerMode.Off;
 }
 internal static void Initialize(Plugin p){
  Enabled=CnConfig.Bind(p.Config,"14. Exfiltration","Enabled",true);
  PanelMode=CnConfig.Bind(p.Config,"14. Exfiltration","Panel mode",ExfilPanelMode.Hold);
  PanelKey=CnConfig.Bind(p.Config,"14. Exfiltration","Panel hold key",KeyCode.Plus);
  PanelWidth=CnConfig.Bind(p.Config,"14. Exfiltration","Panel width",440,new ConfigDescription("",new AcceptableValueRange<int>(300,600)));
  MarkerMode=CnConfig.Bind(p.Config,"14. Exfiltration","Exit marker mode",ExfilMarkerMode.All);
  ShowLeverMarkers=CnConfig.Bind(p.Config,"14. Exfiltration","Show lever markers",true);
  ShowSpecial=CnConfig.Bind(p.Config,"14. Exfiltration","Show conditional exits",true);
  Lines=CnConfig.Bind(p.Config,"14. Exfiltration","Maximum exits",8,new ConfigDescription("",new AcceptableValueRange<int>(3,20)));
  Force=CnConfig.Bind(p.Config,"14. Exfiltration","Force unassigned exits",false);
  ForceKey=CnConfig.Bind(p.Config,"14. Exfiltration","Force toggle key",KeyCode.F10);
  IgnoreRequirements=CnConfig.Bind(p.Config,"14. Exfiltration","Ignore exit requirements",false);
  IgnoreKey=CnConfig.Bind(p.Config,"14. Exfiltration","Ignore requirements toggle key",KeyCode.I);
  try{
   foreach(var type in new[]{typeof(ExfiltrationPoint),typeof(ScavExfiltrationPoint),typeof(SharedExfiltrationPoint)}){
    var method=type.GetMethods().Single(m=>m.DeclaringType==type&&m.Name=="InfiltrationMatch");p.Patches.Patch(method,postfix:new HarmonyMethod(typeof(ExfilDisplay),nameof(MatchOverride)));
   }
   foreach(var method in typeof(ExfiltrationController).GetMethods().Where(m=>m.DeclaringType==typeof(ExfiltrationController)&&m.Name=="EligiblePoints")){
    var prefix=method.GetParameters()[0].ParameterType==typeof(Profile)?nameof(EligibleProfile):nameof(EligibleEntryPoint);
    p.Patches.Patch(method,postfix:new HarmonyMethod(typeof(ExfilDisplay),prefix));
   }
   hooksReady=true;p.Log.LogInfo("Exfiltration permissions ready: same-faction unassigned-exit hooks");
  }catch(Exception e){hooksReady=false;p.Log.LogError("Exfiltration permission hooks unavailable: "+e);}
  try{
   // IL2CPP shares several Met native bodies with unrelated methods. Hook the
   // point-level check to avoid intercepting those unrelated calls.
   var met=typeof(ExfiltrationPoint).GetMethod("HasMetRequirements",new[]{typeof(string)})??throw new MissingMethodException("HasMetRequirements");
   p.Patches.Patch(met,postfix:new HarmonyMethod(typeof(ExfilDisplay),nameof(HasMetOverride)));
   conditionsReady=true;p.Log.LogInfo("Exit requirement hook ready: HasMetRequirements");
  }catch(Exception e){conditionsReady=false;p.Log.LogError("Exit requirement hooks unavailable: "+e);}
 }
 internal static void Keys(){if(Hotkeys.Pressed(ForceKey.Value)){Force.Value=!Force.Value;nextScan=0;}if(Hotkeys.Pressed(IgnoreKey.Value)){IgnoreRequirements.Value=!IgnoreRequirements.Value;nextScan=0;}}
 internal static void Reset(){RestoreConditions();rows.Clear();leverMarkers.Clear();linkedSwitches.Clear();originals.Clear();seenPresent.Clear();permissionOverrides.Clear();nextScan=nextSwitchScan=0;worldPointer=IntPtr.Zero;notice="";lastForce=lastIgnore=false;layoutDirty=true;pageIndex=0;}
 static bool ConditionsActive=>conditionsReady&&IgnoreRequirements.Value;

 static void HasMetOverride(ExfiltrationPoint __instance,string __0,ref bool __result){
  if(!ConditionsActive||__result)return;
  try{var player=Singleton<GameWorld>.Instance?.MainPlayer;if(player!=null&&player.IsYourPlayer&&__0==player.ProfileId&&CanBypass(__instance))__result=true;}catch{}
 }
 static bool CanBypass(ExfiltrationPoint point)=>point.TryCast<EFT.Interactive.SecretExfiltrations.SecretExfiltrationPoint>()==null&&!Lever(point)&&!Train(point)&&point.Status==EExfiltrationStatus.RegularMode;
 static void MatchOverride(ExfiltrationPoint __instance,Player __0,ref bool __result){
  if(hooksReady&&Force.Value&&__0!=null&&__0.IsYourPlayer&&permissionOverrides.Contains(__instance.Pointer))__result=true;
 }
 static void EligibleProfile(Profile __0,ref Il2CppReferenceArray<ExfiltrationPoint> __result){var player=Singleton<GameWorld>.Instance?.MainPlayer;if(player!=null&&__0!=null&&__0.Id.ToString()==player.ProfileId)MergeEligible(ref __result);}
 static void EligibleEntryPoint(string __0,ref Il2CppReferenceArray<ExfiltrationPoint> __result){
  var player=Singleton<GameWorld>.Instance?.MainPlayer;
  if(player==null||!player.IsYourPlayer)return;
  if(string.Equals(__0,player.Infiltration,StringComparison.OrdinalIgnoreCase)||
     string.Equals(__0,player.Profile?.Info?.EntryPoint,StringComparison.OrdinalIgnoreCase))MergeEligible(ref __result);
 }
 static void MergeEligible(ref Il2CppReferenceArray<ExfiltrationPoint> result){
  if(!hooksReady||!Force.Value)return;
  var list=new List<ExfiltrationPoint>();var seen=new HashSet<IntPtr>();
  if(result!=null)foreach(var p in result)if(p!=null&&seen.Add(p.Pointer))list.Add(p);
  foreach(var original in originals.Values)if(original.Point!=null&&permissionOverrides.Contains(original.Point.Pointer)&&seen.Add(original.Point.Pointer))list.Add(original.Point);
  result=new Il2CppReferenceArray<ExfiltrationPoint>(list.ToArray());
 }
 static bool Money(ExfiltrationPoint point){
  var requirement=point.TransferItemRequirement;if(requirement==null)return false;
  return requirement.Id=="5449016a4bdc2d6f028b456f"||requirement.Id=="5696686a4bdc2da3298b456a"||requirement.Id=="569668774bdc2da2298b4568";
 }
 static bool Train(ExfiltrationPoint point){var reqs=point.Requirements;if(reqs!=null)foreach(var r in reqs)if(r!=null&&r.Requirement==ERequirementState.Train)return true;return false;}
 static (string Panel,string Marker) TrainSchedule(ExfiltrationPoint point){
  try{
   TrainRequirement? requirement=null;
   var requirements=point.Requirements;
   if(requirements!=null)foreach(var item in requirements){
    if(item==null||item.Requirement!=ERequirementState.Train)continue;
    requirement=item.TryCast<TrainRequirement>();break;
   }
   var locomotive=requirement?.Locomotive??UnityEngine.Object.FindObjectOfType<Locomotive>();
   if(locomotive==null)return("列车时间未同步","时间未同步");
   var timing=locomotive._travelStateTiming;
   int arrival=(int)Locomotive.ETravelState.Arrived,departure=(int)Locomotive.ETravelState.Departing;
   if(timing==null||timing.Length<=departure||!float.IsFinite(timing[arrival])||!float.IsFinite(timing[departure])||timing[departure]<=timing[arrival])return("列车时间未同步","时间未同步");
   long start=locomotive._depart.Ticks,now=EFT.DateTimeExtensions.UtcNow.Ticks;
   if(start<=0)return("列车时间未同步","时间未同步");
   double elapsed=(now-start)/(double)TimeSpan.TicksPerSecond;
   double untilArrival=timing[arrival]-elapsed,untilDeparture=timing[departure]-elapsed;
   if(untilDeparture<=0)return("已发车","已发车");
   if(untilArrival<=0)return($"已到站 · 发车倒计时 {Clock(untilDeparture)}",$"发车 {Clock(untilDeparture)}");
   return($"到站倒计时 {Clock(untilArrival)} · 发车倒计时 {Clock(untilDeparture)}",$"到站 {Clock(untilArrival)} / 发车 {Clock(untilDeparture)}");
  }catch{return("列车时间未同步","时间未同步");}
 }
 static string Clock(double seconds){var left=TimeSpan.FromSeconds(Math.Ceiling(Math.Max(0,seconds)));return $"{(int)left.TotalMinutes:00}:{left.Seconds:00}";}
 static bool Lever(ExfiltrationPoint point){
  if(point.Switch!=null)return true;
  var reqs=point.Requirements;if(reqs!=null)foreach(var r in reqs)if(r!=null&&(r.Requirement==ERequirementState.WorldEvent||r.Requirement==ERequirementState.Reference))return true;
  return false;
 }
 static void Restore(){
  foreach(var original in originals.Values){
   try{
    var point=original.Point;if(point==null)continue;
    point.EligibleEntryPoints=original.Entries;
    if(original.AddedScavId){var scav=point.TryCast<ScavExfiltrationPoint>();if(scav!=null&&scav.EligibleIds!=null)scav.EligibleIds.Remove(original.ProfileId);}
   }catch(Exception e){Warn(e);}
  }
  originals.Clear();permissionOverrides.Clear();
 }
 static void RestoreConditions(){
  foreach(var entry in queuedCars.Values){
   try{if(entry.Point!=null&&entry.Point.Status==EExfiltrationStatus.RegularMode)entry.Point.QueuedPlayers?.Remove(entry.RaidId);}catch(Exception e){Warn(e);}
  }
  queuedCars.Clear();
  foreach(var entry in conditionStatuses.Values){
   try{if(entry.Point!=null&&entry.Point.Status==EExfiltrationStatus.RegularMode)entry.Point.ExternalSetStatus(entry.Status,"SPT5 restore exit requirements");}catch(Exception e){Warn(e);}
  }
  conditionStatuses.Clear();
 }
 internal static void Shutdown(){RestoreConditions();Restore();Reset();}
 static void UnlockPoint(ExfiltrationPoint point,Player player,bool assigned){
  if(!hooksReady||!Force.Value||point.TryCast<EFT.Interactive.SecretExfiltrations.SecretExfiltrationPoint>()!=null)return;
  var scav=point.TryCast<ScavExfiltrationPoint>();bool playerScav=player.Profile?.Side==EPlayerSide.Savage;
  if(!ExfilPolicy.SameFaction(playerScav,scav!=null)||Money(point)||Train(point))return;
  if(assigned&&point.Status!=EExfiltrationStatus.NotPresent)return;
  if(originals.ContainsKey(point.Pointer))return;
  var entries=point.EligibleEntryPoints;string entry=player.Profile?.Info?.EntryPoint??"";
  bool addScav=scav!=null&&scav.EligibleIds!=null&&!scav.EligibleIds.Contains(player.ProfileId);
  originals[point.Pointer]=new(point,entries,addScav,player.ProfileId);permissionOverrides.Add(point.Pointer);
  if(addScav)scav!.EligibleIds!.Add(player.ProfileId);
  var expanded=new List<string>();if(entries!=null)foreach(var e in entries)expanded.Add(e);
  if(!string.IsNullOrEmpty(entry)&&!expanded.Contains(entry))expanded.Add(entry);point.EligibleEntryPoints=new Il2CppStringArray(expanded.ToArray());
 }
 static void BuildLeverMarkers(Player player){
  leverMarkers.Clear();
  if(!ShowLeverMarkers.Value)return;
  try{
   if(Time.unscaledTime>=nextSwitchScan){
    nextSwitchScan=Time.unscaledTime+20f;linkedSwitches.Clear();
    foreach(var candidate in UnityEngine.Object.FindObjectsOfType<Switch>()){
     try{
      var point=candidate?.ExfiltrationPoint;if(point==null)continue;
      if(!linkedSwitches.TryGetValue(point.Pointer,out var list))linkedSwitches[point.Pointer]=list=new List<Switch>();
      list.Add(candidate!);
     }catch(Exception error){Warn(error);}
    }
   }
   var seen=new HashSet<IntPtr>();
   foreach(var row in rows){
    try{
     var point=row.Point;if(point==null||!Lever(point))continue;
     Add(point.Switch,row.Name,player,seen);
     var chained=point._switches;if(chained!=null)foreach(var linked in chained)Add(linked,row.Name,player,seen);
     if(linkedSwitches.TryGetValue(point.Pointer,out var associated))foreach(var candidate in associated)Add(candidate,row.Name,player,seen);
    }catch(Exception error){Warn(error);}
   }
  }catch(Exception error){Warn(error);}
 }
 static void Add(Switch? lever,string exitName,Player player,HashSet<IntPtr> seen){
  if(lever==null||!seen.Add(lever.Pointer))return;
  try{
   var position=lever.transform.position;
   if(leverMarkers.Any(marker=>marker.ExitName==exitName&&Vector3.Distance(marker.Position,position)<1f))return;
   leverMarkers.Add(new(lever,exitName,position,Vector3.Distance(player.Position,position),lever.DoorState==EDoorState.Open));
  }catch(Exception error){Warn(error);}
 }
 internal static void Tick(GameWorld? world,Player? player){
  if(world==null||player==null){if(worldPointer!=IntPtr.Zero)Reset();return;}
  if(worldPointer!=world.Pointer){Reset();worldPointer=world.Pointer;}
  if(Time.unscaledTime<nextScan&&lastForce==Force.Value&&lastIgnore==IgnoreRequirements.Value)return;nextScan=Time.unscaledTime+1f;
  try{
   if((lastForce!=Force.Value)&&originals.Count>0)Restore();lastForce=Force.Value;
   if(lastIgnore!=IgnoreRequirements.Value&&!IgnoreRequirements.Value)RestoreConditions();lastIgnore=IgnoreRequirements.Value;
   rows.Clear();leverMarkers.Clear();layoutDirty=true;if(!Enabled.Value&&!Force.Value&&!IgnoreRequirements.Value&&!GroundNavigation.WantsExitData&&!TeleportControl.WantsExitData)return;
   var controller=ExfiltrationController.Instance;if(controller==null){notice="撤离点正在加载";return;}
   var assigned=new HashSet<IntPtr>();var eligible=controller.EligiblePoints(player.Profile);
   if(eligible!=null)foreach(var point in eligible)if(point!=null)assigned.Add(point.Pointer);
   var secretEligible=controller.SecretEligiblePoints();
   if(secretEligible!=null)foreach(var point in secretEligible)if(point!=null)assigned.Add(point.Pointer);
   var points=new Dictionary<IntPtr,ExfiltrationPoint>();
   var normal=controller.ExfiltrationPoints;if(normal!=null)foreach(var point in normal)if(point!=null)points[point.Pointer]=point;
   var scavPoints=controller.ScavExfiltrationPoints;if(scavPoints!=null)foreach(var point in scavPoints)if(point!=null)points[point.Pointer]=point;
   var secretPoints=controller.SecretExfiltrationPoints;if(secretPoints!=null)foreach(var point in secretPoints)if(point!=null)points[point.Pointer]=point;
   if(eligible!=null)foreach(var point in eligible)if(point!=null)points[point.Pointer]=point;
   if(secretEligible!=null)foreach(var point in secretEligible)if(point!=null)points[point.Pointer]=point;
   bool banned=controller.BannedPlayers!=null&&controller.BannedPlayers.Contains(player.RaidId);notice="";
   foreach(var point in points.Values){
    try{
     bool isAssigned=assigned.Contains(point.Pointer);
     if(Force.Value)UnlockPoint(point,player,isAssigned);
     bool forced=originals.ContainsKey(point.Pointer);
     if(forced)isAssigned=point.InfiltrationMatch(player);
     string key=point.Settings?.Name??point.name;
     string name=LocalizationExtensions.Localized(key,EStringCase.None);if(string.IsNullOrWhiteSpace(name))name=key;
     bool car=Money(point),lever=Lever(point),train=Train(point),secret=point.TryCast<EFT.Interactive.SecretExfiltrations.SecretExfiltrationPoint>()!=null;string kind=secret?"暗号":train?"列车":ExfilPolicy.Kind(name+" "+key,car,lever);
     bool inPanel=secret||train||ShowSpecial.Value||isAssigned||kind=="普通";
     bool bypass=ConditionsActive&&CanBypass(point);
     bool met=true;var unmet=new List<string>();var requirements=point.Requirements;
     if(!bypass&&requirements!=null)foreach(var req in requirements){
      if(req==null||req.Met(player,point))continue;met=false;
      string tip=req.GetLocalizedTip(player.ProfileId);if(!string.IsNullOrWhiteSpace(tip))unmet.Add(tip.Replace('\n',' '));
     }
     bool queued=point.QueuedPlayers!=null&&point.QueuedPlayers.Contains(player.RaidId);
     if(ConditionsActive&&car&&isAssigned&&!banned&&!queued&&point.Status==EExfiltrationStatus.RegularMode){
      var carPosition=point.ExtendedCollider!=null?point.ExtendedCollider.bounds.center:point.transform.position;
      if(Vector3.Distance(player.Position,carPosition)<=5f&&point.QueuedPlayers!=null){point.QueuedPlayers.Add(player.RaidId);queuedCars[point.Pointer]=(point,player.RaidId);queued=true;}
     }
     var state=ExfilPolicy.Evaluate(point.Status.ToString(),isAssigned,met,banned,car,queued);
     string text=ExfilPolicy.Evaluate(point.Status.ToString(),true,met,banned,car,queued).Text;
     if(point.Status!=EExfiltrationStatus.NotPresent&&point.Status!=EExfiltrationStatus.Hidden)seenPresent.Add(point.Pointer);
     if(car&&point.Status==EExfiltrationStatus.NotPresent)text=seenPresent.Contains(point.Pointer)?"已离开":"未出现／已离开";
     if(kind=="绿烟")text=point.Status==EExfiltrationStatus.NotPresent?"未开启":(point.Status==EExfiltrationStatus.RegularMode?"已开启 · ":"")+text;
     if(lever&&point.Switch!=null)text+=" · 开关"+(point.Switch.DoorState==EDoorState.Open?"已开启":"未开启");
     if(unmet.Count>0&&!bypass)text+=" · "+string.Join(" / ",unmet.Take(2));
     if(car&&point.Status!=EExfiltrationStatus.NotPresent&&!met&&!ConditionsActive){
      var payment=point.TransferItemRequirement;int amount;
      if(payment!=null)text+=payment.CanTransferItems(player,out amount)?" · 可付款":" · 未付款／资金不足";
     }
     string trainMarker="";
     if(train){var schedule=TrainSchedule(point);text+=" · "+schedule.Panel;trainMarker=schedule.Marker;}
     if(forced)text+=" · 强制";
     if(bypass&&isAssigned)text+=" · 免条件";
     if(!isAssigned)text+=" · 未分配";
     var position=point.ExtendedCollider!=null?point.ExtendedCollider.bounds.center:point.transform.position;
     var faction=ExfilPolicy.Faction(point.TryCast<ScavExfiltrationPoint>()!=null,point.TryCast<SharedExfiltrationPoint>()!=null);
     var secretPoint=point.TryCast<EFT.Interactive.SecretExfiltrations.SecretExfiltrationPoint>();
     if(secretPoint!=null)faction=(secretPoint.EligibleForPmc?ExfilFaction.Pmc:ExfilFaction.Unknown)|(secretPoint.EligibleForScav?ExfilFaction.Scav:ExfilFaction.Unknown);
     rows.Add(new(point,name,kind,text,state.Available,Vector3.Distance(player.Position,position),position,forced,inPanel,trainMarker,faction));
    }catch(Exception e){Warn(e);}
   }
   // Several trigger objects can represent the same logical exit. Native pointers
   // only deduplicate the same object, not these separate triggers.
   var uniqueRows=rows.GroupBy(r=>r.Name.Trim(),StringComparer.OrdinalIgnoreCase)
    .Select(group=>group.OrderBy(r=>r.Available?0:1).ThenBy(r=>r.Distance).ThenBy(r=>r.Point.Pointer.ToInt64()).First() with {Faction=ExfilPolicy.MergeFactions(group.Select(r=>r.Faction))}).ToList();
   if(uniqueRows.Count<rows.Count&&!loggedDuplicateScan){loggedDuplicateScan=true;Plugin.Self.Log.LogInfo($"Exfil display merged duplicate triggers: {rows.Count} -> {uniqueRows.Count}");}
   rows.Clear();rows.AddRange(uniqueRows);
   rows.Sort((a,b)=>a.Distance.CompareTo(b.Distance));
   BuildLeverMarkers(player);

  }catch(Exception e){notice="撤离状态读取失败";Warn(e);}
 }
 internal static void Draw(Camera camera,Player player,GUIStyle style){
  if(Enabled.Value)DrawMarkers(camera,style);
  if(!InterfaceMenu.Editing&&(PanelMode.Value==ExfilPanelMode.Off||PanelMode.Value==ExfilPanelMode.Hold&&!PanelKeyHeld()))return;
  bool transit=TransitDisplay.Enabled.Value&&TransitDisplay.Rows.Count>0;
  bool btr=BtrDisplay.Status.Length>0;
  if(!Enabled.Value&&!transit&&!btr)return;
  var nearest=Enabled.Value?rows.FirstOrDefault(r=>r.Available):null;
  string heading=!Enabled.Value?"":nearest!=null?$"最近：{nearest.Name} [{ExfilPolicy.FactionLabel(nearest.Faction)}] · {nearest.Distance:F0}米 · {Direction(camera,nearest.Position-player.Position)}":"最近："+(string.IsNullOrEmpty(notice)?"无可用撤离点":notice);
  int width=(int)Math.Min(PanelWidth.Value,Screen.width-24);float line=Math.Max(21,style.fontSize+5);
  if(panelStyle==null){panelStyle=new GUIStyle(style);panelStyle.alignment=TextAnchor.UpperLeft;panelStyle.wordWrap=true;panelStyle.richText=false;}
  if(layoutDirty||transitRevision!=TransitDisplay.Revision||btrRevision!=BtrDisplay.Revision||layoutWidth!=width||layoutHeight!=Screen.height||layoutFont!=style.fontSize||layoutLimit!=Lines.Value||nearestCaption!=heading){
   transitRevision=TransitDisplay.Revision;
   btrRevision=BtrDisplay.Revision;
   layoutDirty=false;layoutWidth=width;layoutHeight=Screen.height;layoutFont=style.fontSize;layoutLimit=Lines.Value;nearestCaption=heading;panelStyle.fontSize=style.fontSize;
   headers.Clear();captions.Clear();headerHeights.Clear();captionHeights.Clear();headerHeight=0;
   if(!string.IsNullOrEmpty(heading))headers.Add((heading,nearest!=null?Color.green:Color.yellow));
   headers.Add(($"强制：{CnConfig.OnOff(Force.Value)} · 免条件：{(IgnoreRequirements.Value?(conditionsReady?"开":"不可用"):"关")}",Color.cyan));
   if(Enabled.Value)foreach(var row in rows.Where(r=>r.InPanel).OrderBy(r=>r.Available?0:r.Kind!="普通"?1:2).ThenBy(r=>r.Distance))captions.Add(($"[{row.Kind} · {ExfilPolicy.FactionLabel(row.Faction)}] {row.Name} {row.Distance:F0}米 · {row.State}",row.Available?Color.green:new Color(1,0.8f,0.3f)));
   if(transit){var closest=TransitDisplay.Rows.FirstOrDefault(r=>r.Available);if(closest.Available)headers.Add(($"转移：{closest.Destination} · {closest.Distance:F0}米 · {Direction(camera,closest.Position-player.Position)}",Color.cyan));foreach(var row in TransitDisplay.Rows)captions.Add(($"[转移] {row.Destination} {row.Distance:F0}米 · {row.State}",row.Available?Color.cyan:Color.gray));}
   if(btr)headers.Add((BtrDisplay.Status,Color.cyan));
   foreach(var text in headers){float height=Math.Max(line,panelStyle.CalcHeight(new GUIContent(text.Text),width-16)+4);headerHeights.Add(height);headerHeight+=height;}
   foreach(var text in captions)captionHeights.Add(Math.Max(line,panelStyle.CalcHeight(new GUIContent(text.Text),width-16)+4));
   pages=PanelPages.Build(captionHeights,Lines.Value,Math.Max(line,Screen.height-122-headerHeight-line-12));
  }
  if(Hotkeys.Pressed(KeyCode.PageDown))pageIndex++;
  if(Hotkeys.Pressed(KeyCode.PageUp))pageIndex--;
  pageIndex=Math.Clamp(pageIndex,0,pages.Count-1);var page=pages[pageIndex];
  float panelHeight=headerHeight+line+page.Height+12,x=Screen.width-width-12,y=Math.Max(110,Screen.height-panelHeight-12);
  var panel=InterfaceMenu.Place("exfil",new Rect(x,y,width,panelHeight));x=panel.x;y=panel.y;
  var oldColor=GUI.color;
  try{
   InterfaceMenu.Panel(panel);float top=y+6;
   for(int i=0;i<headers.Count;i++){GUI.color=InterfaceMenu.Tint(headers[i].Color);GUI.Label(new Rect(x+8,top,width-16,headerHeights[i]),headers[i].Text,panelStyle);top+=headerHeights[i];}
   GUI.color=InterfaceMenu.Accent;GUI.Label(new Rect(x+8,top,width-16,line),$"撤离／转移 {pageIndex+1}/{pages.Count}",panelStyle);top+=line;
   for(int i=page.Start;i<page.Start+page.Count;i++){GUI.color=InterfaceMenu.Tint(captions[i].Color);float height=Math.Min(captionHeights[i],Screen.height-top-18);GUI.Label(new Rect(x+8,top,width-16,height),captions[i].Text,panelStyle);top+=captionHeights[i];}
  }finally{GUI.color=oldColor;}
 }
 static bool PanelKeyHeld(){
  if(Input.GetKey(PanelKey.Value))return true;
  return PanelKey.Value==KeyCode.Plus&&(Input.GetKey(KeyCode.KeypadPlus)||
   (Input.GetKey(KeyCode.Equals)&&(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift))));
 }
 static void DrawMarkers(Camera camera,GUIStyle style){
  if(rows.Count==0&&leverMarkers.Count==0)return;
  IEnumerable<Row> candidates=MarkerMode.Value switch{
   ExfilMarkerMode.All=>rows.OrderBy(r=>r.Name,StringComparer.Ordinal).ThenBy(r=>r.Point.Pointer.ToInt64()),
   ExfilMarkerMode.Nearest=>rows.Where(r=>r.Available).Take(1),
   _=>Array.Empty<Row>()
  };
  var occupied=new List<Rect>();
  var seen=new HashSet<string>();
  foreach(var row in candidates){
   if(!seen.Add(row.Name.Trim()))continue;
   float markerWidth=Math.Min(Screen.width-8,row.TrainMarker.Length>0?470:340);
   var (rect,arrow)=PlaceMarker(camera,row.Position,markerWidth,occupied);
   GUI.color=row.Available?Color.green:row.Forced?Color.cyan:new Color(1f,0.8f,0.3f);
   string label=row.TrainMarker.Length>0?$"{arrow}[列车 · {ExfilPolicy.FactionLabel(row.Faction)}] {row.Distance:F0}米 · {row.TrainMarker}":$"{arrow}[{row.Kind} · {ExfilPolicy.FactionLabel(row.Faction)}] {row.Name} {row.Distance:F0}米";
   GUI.Label(rect,label,style);
  }
  if(ShowLeverMarkers.Value)foreach(var lever in leverMarkers){
   if(!OnScreen(camera,lever.Position))continue;
   var (rect,arrow)=PlaceMarker(camera,lever.Position,Math.Min(Screen.width-8,310),occupied);
   GUI.color=lever.Open?Color.green:new Color(1f,0.7f,0.2f);
   GUI.Label(rect,$"{arrow}[拉闸] {lever.ExitName} · {lever.Distance:F0}米 · {(lever.Open?"已开启":"未开启")}",style);
  }
  GUI.color=Color.white;
 }
 static bool OnScreen(Camera camera,Vector3 position){var p=camera.WorldToViewportPoint(position);return p.z>0&&p.x>=0&&p.x<=1&&p.y>=0&&p.y<=1;}
 static (Rect Rect,string Arrow) PlaceMarker(Camera camera,Vector3 position,float width,List<Rect> occupied){
  var screen=OverlayProjection.ScreenPoint(camera,position);
  if(screen.z<=0){screen.x=Screen.width-screen.x;screen.y=Screen.height-screen.y;}
  float rawY=Screen.height-screen.y,half=width/2;
  float x=Math.Clamp(screen.x,half,Screen.width-half);
  float y=Math.Clamp(rawY,68,Screen.height-32);
  string arrow=screen.x<half?"◀ ":screen.x>Screen.width-half?"▶ ":rawY<68?"▲ ":rawY>Screen.height-32?"▼ ":"● ";
  var rect=new Rect(x-half,y-14,width,28);
  // Preserve the projected position; don't wrap a crowded marker across the screen.
  if(occupied.Any(other=>other.Overlaps(rect)))return(new Rect(0,0,0,0),arrow);
  occupied.Add(rect);
  return(rect,arrow);
 }
 static string Direction(Camera camera,Vector3 delta){
  float angle=Vector3.SignedAngle(new Vector3(camera.transform.forward.x,0,camera.transform.forward.z),new Vector3(delta.x,0,delta.z),Vector3.up);
  if(Math.Abs(angle)<22.5f)return "前方";if(Math.Abs(angle)>157.5f)return "后方";
  if(angle>0)return angle<67.5f?"右前方":angle<112.5f?"右侧":"右后方";
  return angle>-67.5f?"左前方":angle>-112.5f?"左侧":"左后方";
 }
 static void Warn(Exception e){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+10;Plugin.Self.Error(e);}
}
