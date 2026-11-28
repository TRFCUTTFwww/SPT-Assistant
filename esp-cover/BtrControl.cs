using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Vehicle;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class BtrControl {
 static ConfigEntry<bool> disabled=null!,customWait=null!,keepFriendly=null!;
 static ConfigEntry<float> wait=null!;
 static ConfigEntry<KeyCode> key=null!;
 static readonly Dictionary<IntPtr,(GameObject Object,bool Active)> hidden=new();
 static IntPtr world;
 static string selected="",notice="";
 static Vector2 scroll;
 static readonly Queue<string> pendingRoute=new();
 static string requestedStop="";
 static BTRVehicle? stopped;
 static bool wasMoving,extracting;
 static float nextFriendlyCheck;
 internal static bool Disabled=>disabled!=null&&disabled.Value;
 internal static bool KeepFriendly=>keepFriendly!=null&&keepFriendly.Value;

 internal static void Initialize(Plugin plugin){
  BtrServices.Initialize(plugin);
  const string section="18. BTR";
  disabled=FeatureConfig.Bind(section,"Disable BTR","关闭 BTR",false,"隐藏并停用本局 BTR 车辆与射手。");
  key=FeatureConfig.Bind(section,"Disable BTR key","关闭 BTR 切换键",KeyCode.None);
  keepFriendly=FeatureConfig.Bind(section,"Keep BTR friendly","BTR 始终友好（忽略误伤）",false,"阻止误击或爆炸触发 BTR 对本地玩家的敌对，并持续清除对玩家的敌对状态。");
  customWait=FeatureConfig.Bind(section,"Custom stop duration","自定义每站停留时间",false);
  wait=FeatureConfig.Bind(section,"Stop duration","每站停留秒数",60f,"在下一次到站时应用；也可点击立即停留。",new AcceptableValueRange<float>(0,3600));
  plugin.Patches.Patch(typeof(BtrController).GetMethod("OnUpdate",new[]{typeof(float)}),prefix:new HarmonyMethod(typeof(BtrControl),nameof(AllowUpdate)));
  plugin.Patches.Patch(typeof(VehicleBase).GetMethod("IncomingToDestination",new[]{typeof(PathDestination)}),postfix:new HarmonyMethod(typeof(BtrControl),nameof(RouteArrived)));
  plugin.Patches.Patch(typeof(BtrController).GetMethod("OnIncomingToDestination"),postfix:new HarmonyMethod(typeof(BtrControl),nameof(Arrived)));
  if(AccessTools.Method(typeof(BtrController),"HitFromPlayer") is {} hitPlayer)
   plugin.Patches.Patch(hitPlayer,prefix:new HarmonyMethod(typeof(BtrControl),nameof(BeforeHitFromPlayer)));
  if(AccessTools.Method(typeof(BtrController),"ExplosionCheck") is {} explosionCheck)
   plugin.Patches.Patch(explosionCheck,prefix:new HarmonyMethod(typeof(BtrControl),nameof(BeforeExplosionCheck)));
  if(AccessTools.TypeByName("ShooterBTRLayersStrategy") is {} btrBrain&&AccessTools.Method(btrBrain,"ProcessBTRBeingHit") is {} processHit)
   plugin.Patches.Patch(processHit,prefix:new HarmonyMethod(typeof(BtrControl),nameof(BeforeProcessBeingHit)));
  if(AccessTools.TypeByName("BotBTRService") is {} btrService){
   if(AccessTools.Method(btrService,"AddBetrayer") is {} addBetrayer)
    plugin.Patches.Patch(addBetrayer,prefix:new HarmonyMethod(typeof(BtrControl),nameof(BeforeAddBetrayer)));
   if(AccessTools.Method(btrService,"OnBTRSupportInterruptedByBetrayer") is {} interrupted)
    plugin.Patches.Patch(interrupted,prefix:new HarmonyMethod(typeof(BtrControl),nameof(BeforeAddBetrayer)));
  }
 }

 static bool IsProtectedLocal(IPlayer? candidate){
  if(!KeepFriendly||!RaidClock.Active||candidate==null)return false;
  var local=Singleton<GameWorld>.Instance?.MainPlayer;
  if(local==null)return false;
  if(candidate.ProfileId==local.ProfileId)return true;
  string? localGroup=local.Profile?.Info?.GroupId;
  return !string.IsNullOrEmpty(localGroup)&&candidate.GroupId==localGroup;
 }

 static bool BeforeHitFromPlayer(Player __0)=>!IsProtectedLocal(__0);
 static bool BeforeExplosionCheck(int __2){
  if(!KeepFriendly||!RaidClock.Active)return true;
  var local=Singleton<GameWorld>.Instance?.MainPlayer;
  return local==null||local.PlayerId!=__2;
 }
 static bool BeforeProcessBeingHit(IPlayer __0)=>!IsProtectedLocal(__0);
 static bool BeforeAddBetrayer(IPlayer __0)=>!IsProtectedLocal(__0);

 static bool AllowUpdate()=>!Disabled;
 static void Arrived(BtrController __instance){if(!Disabled&&customWait.Value&&RaidClock.Active&&Singleton<GameWorld>.Instance?.BtrController?.Pointer==__instance.Pointer)__instance.BtrVehicle?.MovePauseOn(wait.Value);}
 static void RouteArrived(VehicleBase __instance){
  var controller=Singleton<GameWorld>.Instance?.BtrController;
  if(Disabled||!RaidClock.Active||requestedStop.Length==0||controller?.BtrVehicle?.Pointer!=__instance.Pointer)return;
  try{AdvanceRoute(controller);}catch(Exception e){CancelRoute();notice=e.Message;Plugin.Self.Log.LogWarning("BTR destination: "+e.Message);}
 }
 static void CancelRoute(){pendingRoute.Clear();requestedStop="";}
 static void Hide(GameObject? obj){if(obj==null)return;if(!hidden.ContainsKey(obj.Pointer))hidden[obj.Pointer]=(obj,obj.activeSelf);obj.SetActive(false);}
 static void Restore(){bool current=RaidClock.Active&&Singleton<GameWorld>.Instance?.Pointer==world;foreach(var entry in hidden.Values)try{if(current&&entry.Object!=null)entry.Object.SetActive(entry.Active);}catch{}hidden.Clear();try{if(current&&stopped!=null&&wasMoving)stopped.MoveEnable();}catch{}stopped=null;extracting=false;}

 static List<BotOwner> FindBtrOwners(GameWorld current){
  var owners=new List<BotOwner>();
  var seen=new HashSet<IntPtr>();
  void Add(BotOwner? owner){if(owner!=null&&seen.Add(owner.Pointer))owners.Add(owner);}
  Add(current.BtrController?.BotShooterBtr);
  var all=current.AllAlivePlayersList;
  if(all!=null){
   for(int i=0;i<all.Count;i++){
    var actor=all[i];
    if(actor==null||actor.IsYourPlayer)continue;
    var owner=actor.AIData?.BotOwner;
    if(owner==null)continue;
    if(actor.Profile?.Info?.Settings?.Role==WildSpawnType.shooterBTR)Add(owner);
   }
  }
  return owners;
 }

 internal static int ClearHostility(GameWorld? current,bool manual=true){
  if(current==null||!RaidClock.Active||current.MainPlayer is not {} local)
   throw new ArgumentException("请先进入战局。");

  var owners=FindBtrOwners(current);
  if(owners.Count==0&&manual&&current.BtrController==null)
   throw new ArgumentException("当前地图没有可用 BTR。");

  string? localGroup=local.Profile?.Info?.GroupId;
  int cleared=0;
  foreach(var owner in owners){
   try{
    AiActorControl.Forget(owner,local);
    var enemies=owner.EnemiesController?.EnemyInfos;
    if(enemies!=null){
     var toRemove=new List<IPlayer>();
     foreach(var kv in enemies)
      if(kv.Key!=null&&(kv.Key.ProfileId==local.ProfileId||(!string.IsNullOrEmpty(localGroup)&&kv.Key.GroupId==localGroup)))
       toRemove.Add(kv.Key);
     foreach(var target in toRemove)enemies.Remove(target);
    }
    var groupState=owner.BotsGroup?.BotGroupHostility;
    if(groupState!=null){
     if(groupState.Enemies.ContainsKey(local)){groupState.RemoveEnemyAction(local);cleared++;}
     groupState.Warns.Remove(local);
     if(!groupState.Allies.ContainsKey(local)&&!groupState.Neutrals.ContainsKey(local))
      groupState.AddNeutralAction(local);
     groupState.LastHostilityStatus.Remove(local.RaidId);
     groupState.LastHostilityStatus[local.RaidId]=groupState.Allies.ContainsKey(local)?BotHostilityStatus.Ally:BotHostilityStatus.Neutral;
    }
    var btrService=owner.BotsController?.BotTradersServices?.BTRServices;
    if(btrService?._betrayers is {} betrayers){
     for(int i=betrayers.Count-1;i>=0;i--){
      var b=betrayers[i];
      if(b==null||b.ProfileId==local.ProfileId||(!string.IsNullOrEmpty(localGroup)&&b.GroupId==localGroup)){
       betrayers.RemoveAt(i);
       cleared++;
      }
     }
    }
    owner.Memory?.CheckIsPeace();
    cleared++;
   }catch(Exception error){
    if(manual)Plugin.Self.Log.LogWarning("BTR clear hostility: "+error.Message);
   }
  }
  if(manual){
   notice=owners.Count>0?"已清除 BTR 对玩家的敌对状态。":"当前地图 BTR 射手尚未生成，已重置保护状态。";
   FeatureToasts.Show(notice);
  }
  return cleared;
 }

 internal static void Tick(GameWorld? current){
  if(current==null){Reset();return;}if(world!=current.Pointer){Reset();world=current.Pointer;}
  if(!InterfaceMenu.InputBlocked&&key.Value!=KeyCode.None&&Input.GetKeyDown(key.Value))disabled.Value=!disabled.Value;
  if(!Disabled){
   Restore();
   if(KeepFriendly&&RaidClock.Active&&Time.unscaledTime>=nextFriendlyCheck){
    nextFriendlyCheck=Time.unscaledTime+.4f;
    try{ClearHostility(current,false);}catch{}
   }
   return;
  }
  pendingRoute.Clear();requestedStop="";
  var btr=current.BtrController;if(btr==null)return;var vehicle=btr.BtrVehicle;if(vehicle!=null&&stopped==null){stopped=vehicle;wasMoving=vehicle._moveByPath;vehicle.MoveDisable();}if(vehicle?.Passengers?.Count>0){if(!extracting){extracting=true;vehicle.ExtractPassengers();}notice="正在让 BTR 乘客下车，随后停用车辆。";return;}Hide(vehicle?.gameObject);Hide(btr.BtrView?.gameObject);Hide(btr.BotShooterBtr?.gameObject);
 }
 internal static void Reset(){Restore();world=IntPtr.Zero;selected="";pendingRoute.Clear();requestedStop="";nextFriendlyCheck=0;}
 static void Action(Action action){try{notice="";action();if(notice.Length==0)notice="已提交 BTR 操作。";}catch(Exception e){CancelRoute();notice=e.Message;}}
 internal static void RequestDestination(GameWorld current,string target){
  if(!RaidClock.Active||Disabled||current.Pointer!=Singleton<GameWorld>.Instance?.Pointer)throw new ArgumentException("请在启用 BTR 的战局中设置站点。");
  var controller=current.BtrController??throw new ArgumentException("当前地图 BTR 尚未就绪。");var vehicle=controller.BtrVehicle??throw new ArgumentException("BTR 车辆尚未就绪。");var map=controller.MapPathsConfiguration??throw new ArgumentException("BTR 路线尚未就绪。");
  if(vehicle.VehicleRouteState==EVehicleRouteState.OnDepot)throw new ArgumentException("请等待 BTR 驶入地图后再设置站点。");
  var stops=map.PathDestinations?.Where(stop=>stop!=null&&!string.IsNullOrWhiteSpace(stop.id)).ToArray()??Array.Empty<PathDestination>();if(!stops.Any(stop=>stop.id==target))throw new ArgumentException("站点已失效，请重新选择。");
  bool moving=vehicle.BtrState is not (EBtrState.Stop or EBtrState.StopPaid);string start=(moving?vehicle._targetPathDestination:vehicle._currentPathDestination)?.id??"";
  if(!stops.Any(stop=>stop.id==start))throw new ArgumentException("BTR 当前行车站点尚未确定，请稍后重试。");
  var edges=new List<(string From,string To)>();foreach(var from in stops)foreach(var to in stops)if(from.id!=to.id&&map.GetPathSplineByPoints(from.id,to.id)!=null)edges.Add((from.id,to.id));
  var plan=BtrRouteRules.Plan(start,target,edges);pendingRoute.Clear();foreach(string stop in plan)pendingRoute.Enqueue(stop);requestedStop=target;
  if(moving){notice="已安排到站后前往："+BtrDisplay.StopName(current,target);return;}
  AdvanceRoute(controller);
 }
 internal static void CallNearest(GameWorld current){
  if(!RaidClock.Active||current.MainPlayer==null)throw new ArgumentException("请在战局中叫车。");
  var controller=current.BtrController??throw new ArgumentException("当前地图没有可用 BTR。");
  var vehicle=controller.BtrVehicle??throw new ArgumentException("BTR 车辆尚未就绪。");
  var map=controller.MapPathsConfiguration??throw new ArgumentException("BTR 路线尚未就绪。");
  bool moving=vehicle.BtrState is not (EBtrState.Stop or EBtrState.StopPaid);
  string start=(moving?vehicle._targetPathDestination:vehicle._currentPathDestination)?.id??"";
  var stops=map.PathDestinations?.Where(s=>s!=null&&!string.IsNullOrWhiteSpace(s.id)).ToArray()??Array.Empty<PathDestination>();
  var edges=new List<(string From,string To)>();foreach(var from in stops)foreach(var to in stops)if(from.id!=to.id&&map.GetPathSplineByPoints(from.id,to.id)!=null)edges.Add((from.id,to.id));
  var target=stops.OrderBy(s=>{var delta=s.transform.position-current.MainPlayer.Position;return delta.x*delta.x+delta.z*delta.z;}).FirstOrDefault(s=>BtrRouteRules.Reachable(start,s.id,edges));
  if(target==null)throw new ArgumentException("没有可达的 BTR 站点。");
  RequestDestination(current,target.id);selected=target.id;if(!moving&&vehicle._currentPathDestination?.id==target.id)vehicle.MovePauseOn(customWait.Value?wait.Value:60);else vehicle.MovePauseOff();
 }
 static void AdvanceRoute(BtrController controller){
  var vehicle=controller.BtrVehicle??throw new ArgumentException("BTR 车辆尚未就绪。");string here=vehicle._currentPathDestination?.id??"";
  if(here==requestedStop){pendingRoute.Clear();requestedStop="";notice="BTR 已到达所选站点。";return;}
  if(pendingRoute.Count==0)throw new ArgumentException("BTR 待执行路线已失效，请重新选择。");string next=pendingRoute.Peek();var map=controller.MapPathsConfiguration;
  if(string.IsNullOrWhiteSpace(here)||map?.GetPathSplineByPoints(here,next)==null)throw new ArgumentException("所选站点没有可用的 BTR 行车路线。");
  bool paid=vehicle.IsPaid;vehicle.IsPaid=true;
  try{vehicle.MoveToDestination(next);if(vehicle._targetPathDestination?.id!=next)throw new InvalidOperationException("BTR 未接受目标站点，请重新选择。");}catch{vehicle.IsPaid=paid;throw;}
  pendingRoute.Dequeue();notice="BTR 下一站已设置："+BtrDisplay.StopName(Singleton<GameWorld>.Instance!,next);
  Plugin.Self.Log.LogInfo("BTR destination applied: current="+here+"; next="+next+"; remaining="+pendingRoute.Count);
 }
 internal static object Diagnostics(){var current=Singleton<GameWorld>.Instance;var controller=current?.BtrController;var vehicle=controller?.BtrVehicle;return new{active=RaidClock.Active,disabled=Disabled,keepFriendly=KeepFriendly,vehicle=vehicle!=null,current=vehicle?._currentPathDestination?.id,target=vehicle?._targetPathDestination?.id,moving=vehicle?._moveByPath,paused=vehicle?._pauseOn,state=vehicle?.BtrState.ToString(),requestedStop,pending=pendingRoute.ToArray(),notice};}
 internal static void Draw(Rect rect){
  var current=Singleton<GameWorld>.Instance;var btr=current?.BtrController;var stops=btr?.MapPathsConfiguration?.PathDestinations;
  scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-20,1400+(stops?.Count??0)*44));float x=8,y=8,w=rect.width-36,half=(w-12)/2;
  InterfaceMenu.Label(new Rect(x,y,w,32),"BTR 管理",InterfaceMenu.Accent,21);y+=46;
  InterfaceMenu.DrawEntry(disabled,new Rect(x,y,half,84));InterfaceMenu.DrawEntry(key,new Rect(x+half+12,y,half,84));y+=96;
  InterfaceMenu.DrawEntry(BtrDisplay.Esp,new Rect(x,y,half,84));InterfaceMenu.DrawEntry(keepFriendly,new Rect(x+half+12,y,half,84));y+=96;
  bool enabled=GUI.enabled;
  GUI.enabled=enabled&&RaidClock.Active&&!Disabled&&current?.MainPlayer!=null;
  if(InterfaceMenu.Button(new Rect(x,y,w,38),"清除 BTR 对玩家的敌对状态"))Action(()=>ClearHostility(current!,true));
  GUI.enabled=enabled;y+=48;
  InterfaceMenu.Paragraph(new Rect(x,y,w,62),BtrDisplay.Status.Length>0?BtrDisplay.Status:"当前地图 BTR 尚未就绪。",InterfaceMenu.Accent,13);y+=74;
  InterfaceMenu.DrawEntry(customWait,new Rect(x,y,half,84));InterfaceMenu.DrawEntry(wait,new Rect(x+half+12,y,half,84));y+=96;
  GUI.enabled=enabled&&!Disabled&&btr?.BtrVehicle!=null;
  if(InterfaceMenu.Button(new Rect(x,y,half,36),"立即停留指定时间"))Action(()=>btr!.BtrVehicle.MovePauseOn(wait.Value));
  if(InterfaceMenu.Button(new Rect(x+half+12,y,half,36),"立即发车"))Action(()=>btr!.BtrVehicle.MovePauseOff());y+=48;GUI.enabled=enabled;
  y=BtrServices.Draw(x,y,w);
  GUI.enabled=enabled&&!Disabled&&btr?.BtrVehicle!=null;
  if(InterfaceMenu.Button(new Rect(x,y,w,38),"派遣 BTR 至最近站点"))Action(()=>CallNearest(current!));y+=50;GUI.enabled=enabled;
  InterfaceMenu.Label(new Rect(x,y,w,30),"选择下一站",InterfaceMenu.Accent,16);y+=40;
  if(stops!=null)foreach(var stop in stops){if(stop==null||string.IsNullOrWhiteSpace(stop.id))continue;if(InterfaceMenu.Button(new Rect(x,y,w,34),BtrDisplay.StopName(current!,stop.id),selected==stop.id))selected=stop.id;y+=42;}
  GUI.enabled=enabled&&!Disabled&&btr?.BtrVehicle!=null&&selected.Length>0;
  if(InterfaceMenu.Button(new Rect(x,y,w,38),"设置下一站："+(selected.Length>0?BtrDisplay.StopName(current!,selected):"未选择")))Action(()=>{if(stops==null||!stops.Any(s=>s!=null&&s.id==selected))throw new ArgumentException("站点已失效，请重新选择。");RequestDestination(current!,selected);});GUI.enabled=enabled;y+=48;
  if(notice.Length>0)InterfaceMenu.Paragraph(new Rect(x,y,w,48),notice,InterfaceMenu.Accent,12);GUI.EndScrollView();
 }
}
