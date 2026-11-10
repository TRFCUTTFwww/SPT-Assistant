using System;
using System.Collections.Generic;
using Comfort.Common;
using CommonAssets.Scripts.Game;
using EFT;
using EFT.Interactive;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
namespace LocalSpt5Esp;

internal static partial class ExfilDisplay {
 static readonly HashSet<IntPtr> resumedRequirements=new();
 sealed record RequirementScope(ExfiltrationPoint Point,Il2CppReferenceArray<ExfiltrationRequirement> Original,Il2CppReferenceArray<ExfiltrationRequirement> Temporary);
 static void InitializeRequirementHooks(Plugin plugin){
  // Requirement.Met implementations share/in-line IL2CPP native bodies. Patch
  // concrete point entry points instead, keeping every unrelated call intact.
  void Patch(string name,Type argument,string prefix,string? finalizer=null)=>plugin.Patches.Patch(AccessTools.Method(typeof(ExfiltrationPoint),name,new[]{argument})??throw new MissingMethodException(name),prefix:new HarmonyMethod(typeof(ExfilDisplay),prefix),finalizer:finalizer==null?null:new HarmonyMethod(typeof(ExfilDisplay),finalizer));
  var met=AccessTools.Method(typeof(ExfiltrationPoint),"HasMetRequirements",new[]{typeof(string)})??throw new MissingMethodException("HasMetRequirements");
  plugin.Patches.Patch(met,postfix:new HarmonyMethod(typeof(ExfilDisplay),nameof(HasMetOverride)));
  Patch("UnmetRequirements",typeof(Player),nameof(UnmetPlayer));
  Patch("UnmetRequirements",typeof(string),nameof(UnmetProfile));
  Patch("GetTips",typeof(string),nameof(RequirementTips));
  Patch("OnPlayerEnter",typeof(Player),nameof(BeforeRequirementPlayer),nameof(AfterRequirementScope));
  var proceed=AccessTools.Method(typeof(ExfiltrationPoint),"Proceed",new[]{typeof(Player),typeof(bool)})??throw new MissingMethodException("Proceed");
  plugin.Patches.Patch(proceed,prefix:new HarmonyMethod(typeof(ExfilDisplay),nameof(BeforeRequirementPlayer)),finalizer:new HarmonyMethod(typeof(ExfilDisplay),nameof(AfterRequirementScope)));
  var ui=AccessTools.Method(typeof(AbstractGame),"UpdateExfiltrationUi",new[]{typeof(ExfiltrationPoint),typeof(bool),typeof(bool)})??throw new MissingMethodException("UpdateExfiltrationUi");
  plugin.Patches.Patch(ui,prefix:new HarmonyMethod(typeof(ExfilDisplay),nameof(BeforeRequirementUi)),finalizer:new HarmonyMethod(typeof(ExfilDisplay),nameof(AfterRequirementScope)));
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
 static void BeforeRequirementUi(ExfiltrationPoint __0,out RequirementScope? __state){
  __state=null;try{var player=Singleton<GameWorld>.Instance?.MainPlayer;if(player!=null)__state=BeginRequirementScope(__0,player.ProfileId);}catch(Exception error){Warn(error);}
 }
 static void AfterRequirementScope(RequirementScope? __state){
  // Finalizer runs after exceptions as well. Nested scopes see an empty array
  // and leave restoration to the outer owner. Never erase another mod's update.
  if(__state==null)return;
  try{if(__state.Point!=null&&__state.Point.Requirements?.Pointer==__state.Temporary.Pointer)__state.Point.Requirements=__state.Original;}catch(Exception error){Warn(error);}
 }
 static void ResumeEnteredRequirements(ExfiltrationPoint point,Player player,bool bypass){
  bool entered=false;var players=point.Entered;if(players!=null)foreach(var actor in players)if(actor!=null&&actor.Pointer==player.Pointer){entered=true;break;}
  if(!bypass||!entered){resumedRequirements.Remove(point.Pointer);return;}
  // Toggling inside a blocked vent must retry the native extraction flow once;
  // it must not reset an existing countdown on every overlay refresh.
  if(!resumedRequirements.Add(point.Pointer))return;
  try{point.Proceed(player,false);Plugin.Self.Log.LogInfo("Local exit requirements refreshed: "+(point.Settings?.Name??point.name));}catch{resumedRequirements.Remove(point.Pointer);throw;}
 }
}
