using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using HarmonyLib;

namespace LocalSpt5Esp;

internal static class BushMovement {
 static ConfigEntry<bool> Enabled=null!,SwampEnabled=null!;
 static MovementContext? current;
 static bool applied,swampApplied,restoring;
 static float nextError;
 sealed class ObstacleState {
  internal ObstacleCollider Obstacle=null!;
  internal float Speed;
  internal bool SprintDisabled;
 }
 internal static void Initialize(Plugin plugin){
  Enabled=FeatureConfig.Bind("20. 体力与移动","No bush slowdown","草丛不减速",false);
  SwampEnabled=FeatureConfig.Bind("20. 体力与移动","No swamp slowdown","沼泽不减速",false);
  var refresh=typeof(MovementContext).GetMethod(nameof(MovementContext.RefreshObstacleRestrictions),Type.EmptyTypes)??throw new MissingMethodException(nameof(MovementContext),nameof(MovementContext.RefreshObstacleRestrictions));
  plugin.Patches.Patch(refresh,
   prefix:new HarmonyMethod(typeof(BushMovement),nameof(BeforeRefresh)),finalizer:new HarmonyMethod(typeof(BushMovement),nameof(FinishRefresh)));
 }
 static bool Local(MovementContext context){
  var player=Singleton<GameWorld>.Instance?.MainPlayer;
  return RaidClock.Active&&player?.IsYourPlayer==true&&!player.Destroyed&&player.HealthController?.IsAlive==true&&player.MovementContext?.Pointer==context.Pointer;
 }
 static void BeforeRefresh(MovementContext __instance,out List<ObstacleState>? __state){
  __state=null;
  try{
   if(restoring||(!Enabled.Value&&!SwampEnabled.Value)||!Local(__instance))return;
   if(!(__instance._bushZoneCount>0?Enabled.Value:SwampEnabled.Value))return;
   var obstacles=__instance._enteredObstacles;if(obstacles==null)return;
   foreach(var obstacle in obstacles){
    if(obstacle==null||!obstacle._hasSwampSpeedLimit)continue;
    (__state??=new()).Add(new(){Obstacle=obstacle,Speed=obstacle.speedMlp,SprintDisabled=obstacle._disablesSprint});
    obstacle.speedMlp=1;obstacle._disablesSprint=false;
   }
  }catch(Exception error){Warn(error);}
 }
 static void FinishRefresh(List<ObstacleState>? __state){
  if(__state==null)return;
  foreach(var state in __state)try{if(state.Obstacle!=null){state.Obstacle.speedMlp=state.Speed;state.Obstacle._disablesSprint=state.SprintDisabled;}}catch(Exception error){Warn(error);}
 }
 internal static void Tick(Player? player){
  try{
   if(!RaidClock.Active||player?.IsYourPlayer!=true||player.Destroyed||player.HealthController?.IsAlive!=true||player.MovementContext==null){Reset();return;}
   var context=player.MovementContext;
   if(current?.Pointer!=context.Pointer){Reset();current=context;}
   if(applied==Enabled.Value&&swampApplied==SwampEnabled.Value)return;
   applied=Enabled.Value;swampApplied=SwampEnabled.Value;context.RefreshObstacleRestrictions();
  }catch(Exception error){Warn(error);}
 }
 internal static void Reset(){
  var previous=current;bool refresh=applied||swampApplied;current=null;applied=false;swampApplied=false;
  if(previous==null||!refresh)return;
  restoring=true;
  try{previous.RefreshObstacleRestrictions();}catch(Exception error){Warn(error);}finally{restoring=false;}
 }
 static void Warn(Exception error){if(UnityEngine.Time.unscaledTime<nextError)return;nextError=UnityEngine.Time.unscaledTime+20;Plugin.Self.Error(error);}
}
