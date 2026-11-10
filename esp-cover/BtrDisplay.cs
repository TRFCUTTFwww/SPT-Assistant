using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.Vehicle;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class BtrDisplay {
 internal static ConfigEntry<bool> Enabled=null!;
 internal static ConfigEntry<bool> Esp=null!;
 internal static string Status {get;private set;}="";
 internal static int Revision {get;private set;}
 static IntPtr worldPointer;
 static float nextScan,nextError;
 static string lastStopId="",requestedId="",incomingId="",lastDiagnostic="";
 internal static void Initialize(Plugin plugin){
  Enabled=CnConfig.Bind(plugin.Config,"18. BTR","Enabled",true);
  Esp=FeatureConfig.Bind("18. BTR","BTR ESP","BTR 透视定位",true,"显示 BTR 的方框、位置、距离和当前路线状态，独立于路线信息窗。");
  Hook(plugin,typeof(BtrController),"MoveToDestination",new[]{typeof(string),typeof(int)},nameof(AfterMove));
  Hook(plugin,typeof(BtrController),"OnIncomingToDestination",new[]{typeof(PathDestination),typeof(bool),typeof(bool),typeof(bool)},nameof(AfterIncoming));
  Hook(plugin,typeof(VehicleBase),"ToNextDestination",Type.EmptyTypes,nameof(AfterVehicleAdvance));
  plugin.Log.LogInfo("BTR display uses live vehicle current/target destinations with event fallback");
 }
 static void Hook(Plugin plugin,Type type,string name,Type[] parameters,string postfix){try{var method=type.GetMethod(name,parameters)??throw new MissingMethodException(type.Name,name);plugin.Patches.Patch(method,postfix:new HarmonyMethod(typeof(BtrDisplay),postfix));}catch(Exception e){plugin.Log.LogWarning("BTR optional observation unavailable: "+name+" "+e.Message);}}
 static bool OwnController(BtrController controller){var world=Singleton<GameWorld>.Instance;var local=world?.BtrController;if(world==null||local==null||local.Pointer!=controller.Pointer)return false;Bind(world);return true;}
 static void Bind(GameWorld world){if(worldPointer==world.Pointer)return;Reset();worldPointer=world.Pointer;}
 static void AfterMove(BtrController __instance,string __0){try{if(!OwnController(__instance)||string.IsNullOrWhiteSpace(__0))return;requestedId=__0;nextScan=0;}catch(Exception e){Warn(e);}}
 static void AfterIncoming(BtrController __instance,PathDestination __0){try{if(!OwnController(__instance)||__0==null||string.IsNullOrWhiteSpace(__0.id))return;incomingId=lastStopId=__0.id;if(requestedId.Equals(lastStopId,StringComparison.OrdinalIgnoreCase))requestedId="";nextScan=0;}catch(Exception e){Warn(e);}}
 static void AfterVehicleAdvance(VehicleBase __instance){try{var world=Singleton<GameWorld>.Instance;var own=world?.BtrController?.BtrVehicle;if(world!=null&&own!=null&&own.Pointer==__instance.Pointer){Bind(world);nextScan=0;}}catch(Exception e){Warn(e);}}
 internal static void Reset(){Status="";worldPointer=IntPtr.Zero;lastStopId=requestedId=incomingId=lastDiagnostic="";nextScan=0;Revision++;}
 internal static void Tick(GameWorld? world){
  if(world==null){if(worldPointer!=IntPtr.Zero)Reset();return;}Bind(world);
  if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+1;string updated="";
  try{if((Enabled.Value||Esp.Value)&&BtrControl.Disabled)updated="BTR：已关闭";else if(Enabled.Value||Esp.Value){var controller=world.BtrController;var view=controller?.BtrView;var vehicle=controller?.BtrVehicle;if(view!=null){
   bool stopped=(vehicle!=null?vehicle.BtrState:view.BtrState) is EBtrState.Stop or EBtrState.StopPaid;
   bool depot=(vehicle!=null?vehicle.VehicleRouteState:view.RouteState)==EVehicleRouteState.OnDepot;
   string nativeCurrent=vehicle?._currentPathDestination?.id??"",nativeTarget=vehicle?._targetPathDestination?.id??"";
   if(stopped){string current=!string.IsNullOrWhiteSpace(nativeCurrent)?nativeCurrent:NearestStop(controller!,view.transform.position);if(string.IsNullOrEmpty(current))current=incomingId;if(!string.IsNullOrEmpty(current))lastStopId=current;}
   else if(!string.IsNullOrWhiteSpace(nativeCurrent))lastStopId=nativeCurrent;
   if(requestedId.Equals(lastStopId,StringComparison.OrdinalIgnoreCase))requestedId="";
   var next=BtrRouteRules.Resolve(lastStopId,nativeTarget,requestedId,stopped,depot,vehicle!=null);string nextName=next.Id.Length>0?StopName(world,next.Id):next.Pending;
   if(depot)updated="BTR：停驻／返回车库 · 下一站 "+nextName;
   else if(stopped){int raw=view.TimeToEndPause;string remaining=raw<0?"计时未就绪":raw==0?"即将发车":PauseClock(raw);updated=$"BTR：当前站 {StopName(world,lastStopId)} · 剩余停留 {remaining} · 下一站 {nextName}";}
   else{string previous=lastStopId.Length==0?"行驶中":$"行驶中（上站 {StopName(world,lastStopId)}）";updated=$"BTR：{previous} · 下一站 {nextName}";}
   string diagnostic=$"current={lastStopId}; target={nativeTarget}; request={requestedId}; stopped={stopped}; depot={depot}; source={next.Source}";if(lastDiagnostic!=diagnostic){lastDiagnostic=diagnostic;Plugin.Self.Log.LogInfo("BTR route: "+diagnostic);}
  }}}
  catch(Exception e){updated="BTR：路线读取失败";Warn(e);}if(updated!=Status){Status=updated;Revision++;}
 }
 static string NearestStop(BtrController controller,Vector3 position){var destinations=controller.MapPathsConfiguration?.PathDestinations;if(destinations==null)return "";float best=70f*70f;string id="";foreach(var destination in destinations){if(destination==null||string.IsNullOrWhiteSpace(destination.id))continue;var delta=destination.transform.position-position;float distance=delta.x*delta.x+delta.z*delta.z;if(distance>=best)continue;best=distance;id=destination.id;}return id;}
 internal static void DrawMarker(Camera camera,Player player){
  foreach(var marker in Markers(player))VehicleEsp.Draw(camera,marker);
 }
 internal static IEnumerable<VehicleEsp.Marker> Markers(Player player){
  if(!Esp.Value||BtrControl.Disabled)yield break;var controller=Singleton<GameWorld>.Instance?.BtrController;var view=controller?.BtrView;if(view==null||!view.gameObject.activeInHierarchy)yield break;
  Vector3 position=view.transform.position;
  yield return new(position+Vector3.up*1.5f,new Vector3(3.2f,3,7.5f),"BTR · "+Vector3.Distance(player.Position,position).ToString("F0")+" m",Status,new Color(.35f,.85f,1));
 }
 internal static string StopName(GameWorld world,string id){if(string.IsNullOrWhiteSpace(id))return "站点未识别";string map=world.LocationId?.ToLowerInvariant() switch{"tarkovstreets" or "streets"=>"TarkovStreets","woods"=>"Woods","lighthouse"=>"Lighthouse",_=>""};foreach(var key in new[]{map.Length>0?$"Trading/Dialog/PlayerTaxi/{map}/{id}/Name":"",$"Trading/Dialog/PlayerTaxi/{id}/Name"}){if(key.Length==0)continue;string localized=LocalizationExtensions.Localized(key,EStringCase.None);if(!string.IsNullOrWhiteSpace(localized)&&localized!=key)return localized;}return id;}
 static string PauseClock(int raw){double seconds=raw>300?raw/1000d:raw;if(seconds>600)return "计时未就绪";int rounded=(int)Math.Ceiling(seconds);return $"{rounded/60:00}:{rounded%60:00}";}
 static void Warn(Exception e){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+10;Plugin.Self.Error(e);}
}
