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
internal static class BtrControl {
 static ConfigEntry<bool> disabled=null!,customWait=null!;static ConfigEntry<float> wait=null!;static ConfigEntry<KeyCode> key=null!;
 static readonly Dictionary<IntPtr,(GameObject Object,bool Active)> hidden=new();static IntPtr world;static string selected="",notice="";static Vector2 scroll;
 static readonly Queue<string> pendingRoute=new();static string requestedStop="";
 static BTRVehicle? stopped;static bool wasMoving,extracting;
 internal static bool Disabled=>disabled!=null&&disabled.Value;
 internal static void Initialize(Plugin plugin){
  const string section="18. BTR";
  disabled=FeatureConfig.Bind(section,"Disable BTR","关闭 BTR",false,"隐藏并停用本局 BTR 车辆、显示模型及射手；关闭此开关后恢复，地图原有路线与出现时机仍有效。");
  key=FeatureConfig.Bind(section,"Disable BTR key","关闭 BTR 切换键",KeyCode.None);
  customWait=FeatureConfig.Bind(section,"Custom stop duration","自定义每站停留时间",false);
  wait=FeatureConfig.Bind(section,"Stop duration","每站停留秒数",60f,"在下一次到站时应用；也可点击立即停留。",new AcceptableValueRange<float>(0,3600));
  plugin.Patches.Patch(typeof(BtrController).GetMethod("OnUpdate",new[]{typeof(float)}),prefix:new HarmonyMethod(typeof(BtrControl),nameof(AllowUpdate)));
  plugin.Patches.Patch(typeof(VehicleBase).GetMethod("IncomingToDestination",new[]{typeof(PathDestination)}),postfix:new HarmonyMethod(typeof(BtrControl),nameof(RouteArrived)));
  plugin.Patches.Patch(typeof(BtrController).GetMethod("OnIncomingToDestination"),postfix:new HarmonyMethod(typeof(BtrControl),nameof(Arrived)));
 }
 static bool AllowUpdate()=>!Disabled;
 static void Arrived(BtrController __instance){if(!Disabled&&customWait.Value&&RaidClock.Active&&Singleton<GameWorld>.Instance?.BtrController?.Pointer==__instance.Pointer)__instance.BtrVehicle?.MovePauseOn(wait.Value);}
 // The controller arrival callback runs inside IncomingToDestination, before the vehicle
 // finishes updating its own arrival state. Apply the next leg only after that completes.
 static void RouteArrived(VehicleBase __instance){
  var controller=Singleton<GameWorld>.Instance?.BtrController;
  if(Disabled||!RaidClock.Active||requestedStop.Length==0||controller?.BtrVehicle?.Pointer!=__instance.Pointer)return;
  try{AdvanceRoute(controller);}catch(Exception e){CancelRoute();notice=e.Message;Plugin.Self.Log.LogWarning("BTR destination: "+e.Message);}
 }
 static void CancelRoute(){pendingRoute.Clear();requestedStop="";}
 static void Hide(GameObject? obj){if(obj==null)return;if(!hidden.ContainsKey(obj.Pointer))hidden[obj.Pointer]=(obj,obj.activeSelf);obj.SetActive(false);}
 static void Restore(){bool current=RaidClock.Active&&Singleton<GameWorld>.Instance?.Pointer==world;foreach(var entry in hidden.Values)try{if(current&&entry.Object!=null)entry.Object.SetActive(entry.Active);}catch{}hidden.Clear();try{if(current&&stopped!=null&&wasMoving)stopped.MoveEnable();}catch{}stopped=null;extracting=false;}
 internal static void Tick(GameWorld? current){
  if(current==null){Reset();return;}if(world!=current.Pointer){Reset();world=current.Pointer;}
  if(!InterfaceMenu.InputBlocked&&key.Value!=KeyCode.None&&Input.GetKeyDown(key.Value))disabled.Value=!disabled.Value;
  if(!Disabled){Restore();return;}pendingRoute.Clear();requestedStop="";
  var btr=current.BtrController;if(btr==null)return;var vehicle=btr.BtrVehicle;if(vehicle!=null&&stopped==null){stopped=vehicle;wasMoving=vehicle._moveByPath;vehicle.MoveDisable();}if(vehicle?.Passengers?.Count>0){if(!extracting){extracting=true;vehicle.ExtractPassengers();}notice="正在让 BTR 乘客下车，随后停用车辆。";return;}Hide(vehicle?.gameObject);Hide(btr.BtrView?.gameObject);Hide(btr.BotShooterBtr?.gameObject);
 }
 internal static void Reset(){Restore();world=IntPtr.Zero;selected="";pendingRoute.Clear();requestedStop="";}
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
 static void AdvanceRoute(BtrController controller){
  var vehicle=controller.BtrVehicle??throw new ArgumentException("BTR 车辆尚未就绪。");string here=vehicle._currentPathDestination?.id??"";
  if(here==requestedStop){pendingRoute.Clear();requestedStop="";notice="BTR 已到达所选站点。";return;}
  if(pendingRoute.Count==0)throw new ArgumentException("BTR 待执行路线已失效，请重新选择。");string next=pendingRoute.Peek();var map=controller.MapPathsConfiguration;
  if(string.IsNullOrWhiteSpace(here)||map?.GetPathSplineByPoints(here,next)==null)throw new ArgumentException("所选站点没有可用的 BTR 行车路线。");
  // The controller overload consumes a paid taxi service ID such as map/stop.
  // Admin routing uses the actual vehicle spline and raw destination ID.
  bool paid=vehicle.IsPaid;vehicle.IsPaid=true;
  try{vehicle.MoveToDestination(next);if(vehicle._targetPathDestination?.id!=next)throw new InvalidOperationException("BTR 未接受目标站点，请重新选择。");}catch{vehicle.IsPaid=paid;throw;}
  pendingRoute.Dequeue();notice="BTR 下一站已设置："+BtrDisplay.StopName(Singleton<GameWorld>.Instance!,next);
  Plugin.Self.Log.LogInfo("BTR destination applied: current="+here+"; next="+next+"; remaining="+pendingRoute.Count);
 }
 internal static object Diagnostics(){var current=Singleton<GameWorld>.Instance;var controller=current?.BtrController;var vehicle=controller?.BtrVehicle;return new{active=RaidClock.Active,disabled=Disabled,vehicle=vehicle!=null,current=vehicle?._currentPathDestination?.id,target=vehicle?._targetPathDestination?.id,moving=vehicle?._moveByPath,paused=vehicle?._pauseOn,state=vehicle?.BtrState.ToString(),requestedStop,pending=pendingRoute.ToArray(),notice};}
 internal static void Draw(Rect rect){
  var current=Singleton<GameWorld>.Instance;var btr=current?.BtrController;var stops=btr?.MapPathsConfiguration?.PathDestinations;
  scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-20,820+(stops?.Count??0)*44));float x=8,y=8,w=rect.width-36,half=(w-12)/2;
  InterfaceMenu.Label(new Rect(x,y,w,32),"BTR 管理",InterfaceMenu.Accent,21);y+=46;InterfaceMenu.DrawEntry(disabled,new Rect(x,y,half,84));InterfaceMenu.DrawEntry(key,new Rect(x+half+12,y,half,84));y+=96;
  InterfaceMenu.DrawEntry(BtrDisplay.Esp,new Rect(x,y,w,84));y+=96;
  InterfaceMenu.Paragraph(new Rect(x,y,w,62),BtrDisplay.Status.Length>0?BtrDisplay.Status:"当前地图 BTR 尚未就绪。",InterfaceMenu.Accent,13);y+=74;
  InterfaceMenu.DrawEntry(customWait,new Rect(x,y,half,84));InterfaceMenu.DrawEntry(wait,new Rect(x+half+12,y,half,84));y+=96;
  bool enabled=GUI.enabled;GUI.enabled=enabled&&!Disabled&&btr?.BtrVehicle!=null;
  if(InterfaceMenu.Button(new Rect(x,y,half,36),"立即停留指定时间"))Action(()=>btr!.BtrVehicle.MovePauseOn(wait.Value));
  if(InterfaceMenu.Button(new Rect(x+half+12,y,half,36),"立即发车"))Action(()=>btr!.BtrVehicle.MovePauseOff());y+=48;GUI.enabled=enabled;
  InterfaceMenu.Label(new Rect(x,y,w,30),"选择下一站",InterfaceMenu.Accent,16);y+=40;
  if(stops!=null)foreach(var stop in stops){if(stop==null||string.IsNullOrWhiteSpace(stop.id))continue;if(InterfaceMenu.Button(new Rect(x,y,w,34),BtrDisplay.StopName(current!,stop.id),selected==stop.id))selected=stop.id;y+=42;}
  GUI.enabled=enabled&&!Disabled&&btr?.BtrVehicle!=null&&selected.Length>0;
  if(InterfaceMenu.Button(new Rect(x,y,w,38),"设置下一站："+(selected.Length>0?BtrDisplay.StopName(current!,selected):"未选择")))Action(()=>{if(stops==null||!stops.Any(s=>s!=null&&s.id==selected))throw new ArgumentException("站点已失效，请重新选择。");RequestDestination(current!,selected);});GUI.enabled=enabled;y+=52;
  InterfaceMenu.Paragraph(new Rect(x,y,w,64),notice+"\n行驶中先完成当前路段，再按地图道路前往所选站点；停站倒计时保留，可点击立即发车。",null,12);GUI.EndScrollView();
 }
}
