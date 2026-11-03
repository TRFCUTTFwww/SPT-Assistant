using System;
using BepInEx.Configuration;
using EFT;
using EFT.Interactive;
using EFT.Vehicle;
using HarmonyLib;
using UnityEngine;

namespace LocalSpt5Esp;

internal static class BtrDisplay {
 internal static ConfigEntry<bool> Enabled=null!;
 internal static string Status { get; private set; }="";
 internal static int Revision { get; private set; }
 static IntPtr worldPointer;
 static float nextScan,nextError;
 static string lastStopId="",targetId="",incomingId="";

 internal static void Initialize(Plugin plugin){
  Enabled=CnConfig.Bind(plugin.Config,"18. BTR","Enabled",true);
  try{
   var move=typeof(BtrController).GetMethod("MoveToDestination",new[]{typeof(string),typeof(int)})??throw new MissingMethodException("BtrController.MoveToDestination");
   plugin.Patches.Patch(move,postfix:new HarmonyMethod(typeof(BtrDisplay),nameof(AfterMove)));
   var incoming=typeof(BtrController).GetMethod("OnIncomingToDestination",new[]{typeof(PathDestination),typeof(bool),typeof(bool),typeof(bool)})??throw new MissingMethodException("BtrController.OnIncomingToDestination");
   plugin.Patches.Patch(incoming,postfix:new HarmonyMethod(typeof(BtrDisplay),nameof(AfterIncoming)));
   plugin.Log.LogInfo("BTR route observation ready");
  }catch(Exception e){plugin.Log.LogWarning("BTR route observation unavailable: "+e);}
 }
 static void AfterMove(string __0){
  if(string.IsNullOrWhiteSpace(__0))return;
  targetId=__0;Revision++;
 }
 static void AfterIncoming(PathDestination __0){
  if(__0==null||string.IsNullOrWhiteSpace(__0.id))return;
  incomingId=__0.id;targetId=incomingId;Revision++;
 }
 internal static void Reset(){
  Status="";worldPointer=IntPtr.Zero;lastStopId=targetId=incomingId="";nextScan=0;Revision++;
 }
 internal static void Tick(GameWorld? world){
  if(world==null){if(worldPointer!=IntPtr.Zero)Reset();return;}
  if(worldPointer!=world.Pointer){if(worldPointer!=IntPtr.Zero)Reset();worldPointer=world.Pointer;}
  if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+1;
  string updated="";
  try{
   if(Enabled.Value){
    var controller=world.BtrController;var view=controller?.BtrView;
    if(view!=null){
     bool stopped=view.BtrState==EBtrState.Stop||view.BtrState==EBtrState.StopPaid;
     if(stopped){
      string current=NearestStop(controller!,view.transform.position);
      if(string.IsNullOrEmpty(current))current=incomingId;
      if(!string.IsNullOrEmpty(current))lastStopId=current;
      if(targetId==lastStopId)targetId="";
      string stopName=StopName(world,lastStopId);
      int raw=view.TimeToEndPause;
      string remaining=raw<0?"待同步":raw==0?"即将发车":PauseClock(raw);
      updated=$"BTR：当前站 {stopName} · 剩余停留 {remaining} · 下一站 {NextName(world)}";
     }else{
      string previous=string.IsNullOrEmpty(lastStopId)?"行驶中":$"行驶中（上站 {StopName(world,lastStopId)}）";
      updated=$"BTR：{previous} · 下一站 {NextName(world)}";
     }
    }
   }
  }catch(Exception e){Warn(e);}
  if(updated!=Status){Status=updated;Revision++;}
 }
 static string NextName(GameWorld world)=>string.IsNullOrEmpty(targetId)?"待路线同步":StopName(world,targetId);
 static string NearestStop(BtrController controller,Vector3 position){
  var destinations=controller.MapPathsConfiguration?.PathDestinations;
  if(destinations==null)return "";
  float best=70f*70f;string id="";
  foreach(var destination in destinations){
   if(destination==null||string.IsNullOrWhiteSpace(destination.id))continue;
   var delta=destination.transform.position-position;
   float distance=delta.x*delta.x+delta.z*delta.z;
   if(distance>=best)continue;best=distance;id=destination.id;
  }
  return id;
 }
 static string StopName(GameWorld world,string id){
  if(string.IsNullOrWhiteSpace(id))return "待同步";
  string map=world.LocationId?.ToLowerInvariant() switch{
   "tarkovstreets" or "streets"=>"TarkovStreets",
   "woods"=>"Woods",
   "lighthouse"=>"Lighthouse",
   _=>""
  };
  foreach(var key in new[]{map.Length>0?$"Trading/Dialog/PlayerTaxi/{map}/{id}/Name":"",$"Trading/Dialog/PlayerTaxi/{id}/Name"}){
   if(key.Length==0)continue;
   string localized=LocalizationExtensions.Localized(key,EStringCase.None);
   if(!string.IsNullOrWhiteSpace(localized)&&localized!=key)return localized;
  }
  return id;
 }
 static string PauseClock(int raw){
  // The view may expose milliseconds or seconds depending on the sync path.
  double seconds=raw>300?raw/1000d:raw;
  if(seconds>600)return "待同步";
  int rounded=(int)Math.Ceiling(seconds);
  return $"{rounded/60:00}:{rounded%60:00}";
 }
 static void Warn(Exception e){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+10;Plugin.Self.Error(e);}
}
