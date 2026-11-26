using System;
using Comfort.Common;
using CommonAssets.Scripts.Game;
using EFT;
using EFT.Interactive;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
namespace LocalSpt5Esp;

internal static partial class ExfilDisplay {
 sealed record RequirementScope(ExfiltrationPoint Point,Il2CppReferenceArray<ExfiltrationRequirement> Original,Il2CppReferenceArray<ExfiltrationRequirement> Temporary);
 static void InitializeRequirementHooks(Plugin plugin){
  // Requirement.Met implementations share/in-line IL2CPP native bodies. Patch
  // concrete point entry points instead, keeping every unrelated call intact.
  void Patch(string name,Type argument,string prefix,string? finalizer=null)=>plugin.Patches.Patch(AccessTools.Method(typeof(ExfiltrationPoint),name,new[]{argument})??throw new MissingMethodException(name),prefix:new HarmonyMethod(typeof(ExfilDisplay),prefix),finalizer:finalizer==null?null:new HarmonyMethod(typeof(ExfilDisplay),finalizer));
  Patch("UnmetRequirements",typeof(Player),nameof(UnmetPlayer));
  Patch("UnmetRequirements",typeof(string),nameof(UnmetProfile));
  Patch("GetTips",typeof(string),nameof(RequirementTips));
  Patch("OnPlayerEnter",typeof(Player),nameof(BeforeRequirementPlayer),nameof(AfterRequirementScope));
  var proceed=AccessTools.Method(typeof(ExfiltrationPoint),"Proceed",new[]{typeof(Player),typeof(bool)})??throw new MissingMethodException("Proceed");
  plugin.Patches.Patch(proceed,prefix:new HarmonyMethod(typeof(ExfilDisplay),nameof(BeforeRequirementPlayer)),finalizer:new HarmonyMethod(typeof(ExfilDisplay),nameof(AfterRequirementScope)));
 }
 static bool LocalBypass(ExfiltrationPoint point,string profile){
  if(!ConditionsActive||!RaidClock.Active||point==null)return false;
  var player=Singleton<GameWorld>.Instance?.MainPlayer;var controller=ExfiltrationController.Instance;
  if(player==null||controller==null)return false;
  return ExfilRequirementRules.Local(ConditionsActive,RaidClock.Active,player.HealthController?.IsAlive==true,player.IsYourPlayer,profile,player.ProfileId,point.InfiltrationMatch(player),controller.BannedPlayers?.Contains(player.RaidId)==true)&&CanBypass(point);
 }
 static bool UnmetPlayer(ExfiltrationPoint __instance,Player __0,ref Il2CppSystem.Collections.Generic.IEnumerable<ExfiltrationRequirement> __result){
  if(__0==null||!__0.IsYourPlayer||Singleton<GameWorld>.Instance?.MainPlayer?.Pointer!=__0.Pointer)return true;
  return UnmetProfile(__instance,__0.ProfileId,ref __result);
 }
 static bool UnmetProfile(ExfiltrationPoint __instance,string __0,ref Il2CppSystem.Collections.Generic.IEnumerable<ExfiltrationRequirement> __result){
  try{if(!LocalBypass(__instance,__0))return true;__result=new Il2CppSystem.Collections.Generic.List<ExfiltrationRequirement>().Cast<Il2CppSystem.Collections.Generic.IEnumerable<ExfiltrationRequirement>>();return false;}catch(Exception error){Warn(error);return true;}
 }
 static bool RequirementTips(ExfiltrationPoint __instance,string __0,ref Il2CppStringArray __result){
  try{if(!LocalBypass(__instance,__0))return true;__result=new Il2CppStringArray(0);return false;}catch(Exception error){Warn(error);return true;}
 }
 static RequirementScope? BeginRequirementScope(ExfiltrationPoint point,string profile){
  if(!LocalBypass(point,profile))return null;
  var requirements=point.Requirements;if(requirements==null||requirements.Length==0)return null;
  var empty=new Il2CppReferenceArray<ExfiltrationRequirement>(0);var scope=new RequirementScope(point,requirements,empty);point.Requirements=empty;return scope;
 }
 static void BeforeRequirementPlayer(ExfiltrationPoint __instance,Player __0,out RequirementScope? __state){
  __state=null;try{if(__0!=null&&__0.IsYourPlayer&&Singleton<GameWorld>.Instance?.MainPlayer?.Pointer==__0.Pointer)__state=BeginRequirementScope(__instance,__0.ProfileId);}catch(Exception error){Warn(error);}
 }
 static void AfterRequirementScope(RequirementScope? __state){
  // Finalizer runs after exceptions as well. Nested scopes see an empty array
  // and leave restoration to the outer owner. Never erase another mod's update.
  if(__state==null)return;
  try{if(__state.Point!=null&&__state.Point.Requirements?.Pointer==__state.Temporary.Pointer)__state.Point.Requirements=__state.Original;}catch(Exception error){Warn(error);}
 }
 static void RefreshEnteredRequirements(ExfiltrationPoint point,Player player,bool bypass,bool car){
  if(!bypass||point.Entered==null)return;
  foreach(var actor in point.Entered){
   if(actor==null||actor.Pointer!=player.Pointer)continue;
   bool addedToQueue=car&&QueueLocalCar(point,player);
   // Proceed populates the list that both extraction and its HUD read.
   // A failed attempt can retry on the next scan without resetting a running timer.
   if(addedToQueue||point._playersMetAllRequirements?.Contains(player.ProfileId)==false){
    point.Proceed(player,false);
    if(point._playersMetAllRequirements?.Contains(player.ProfileId)==true)
     Plugin.Self.Log.LogInfo("Local exit requirements accepted: "+(point.Settings?.Name??point.name));
   }
   return;
  }
 }
 static bool QueueLocalCar(ExfiltrationPoint point,Player player){
  if(point.Status!=EExfiltrationStatus.RegularMode||point.QueuedPlayers is not {} queue||queue.Contains(player.RaidId))return false;
  queue.Add(player.RaidId);queuedCars[point.Pointer]=(point,player.RaidId);
  return true;
 }
}
