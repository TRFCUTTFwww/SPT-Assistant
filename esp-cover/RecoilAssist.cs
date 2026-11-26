using System;
using System.Reflection;
using BepInEx.Configuration;
using EFT;
using EFT.Animations;
using EFT.Animations.NewRecoil;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;

internal static class RecoilAssist {
 internal static ConfigEntry<bool> Enabled=null!;
 internal static ConfigEntry<KeyCode> ToggleKey=null!;
 static ConfigEntry<bool> steadyAim=null!;
 static bool ready;
 static float nextSteadyAimError;
 static long oldShots,newShots,steadyPasses,finishedSteadyPasses,steadyErrors;
 // Owned by one ProcessEffectors invocation, never retained across frames or weapons.
 struct SteadyFrame {
  internal BreathEffector? Breath;
  internal Spring? Rotation;
  internal float BreathIntensity,RotationIntensity;
 }
 internal static void Initialize(Plugin p){
  Enabled=CnConfig.Bind(p.Config,"10. Recoil assist","No recoil",true,"Suppress local-player shot recoil and recoil camera rotation in both recoil pipelines. Independent of aim mode. Disabling restores normal recoil for subsequent shots.");
  ToggleKey=CnConfig.Bind(p.Config,"10. Recoil assist","Toggle key",KeyCode.F5,"Toggle no recoil.");
  steadyAim=FeatureConfig.Bind("10. Recoil assist","Steady aim","开镜不抖动",false,
   "减轻本地玩家开镜时的呼吸和持枪晃动。可与无后座独立开关。");
  int patched=0;
  try{
   Patch(p,typeof(OldRecoilShotEffect),"AddRecoilForce",nameof(OldForce));patched++;
   Patch(p,typeof(NewRecoilShotEffect),"AddRecoilForce",nameof(NewForce));patched++;
   Patch(p,typeof(ProceduralWeaponAnimation),"AddHandRecoilRotateToCamera",nameof(CameraRecoil));patched++;
   Patch(p,typeof(ProceduralWeaponAnimation),"ProcessEffectors",nameof(BeforeEffectors),nameof(FinishEffectors));patched++;
   ready=true;p.Log.LogInfo($"No recoil ready: {patched} hooks; shot and current-local camera recoil suppressed, native camera interpolation retained; scoped steady aim; enabled={Enabled.Value}; toggle={ToggleKey.Value}");
  }catch(Exception e){ready=false;p.Log.LogError($"No recoil unavailable after {patched} hooks: {e}");}
 }
 static void Patch(Plugin p,Type type,string name,string prefix,string? finalizer=null){
  var methods=type.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);
  MethodInfo? target=null;
  foreach(var method in methods)if(method.Name==name){if(target!=null)throw new InvalidOperationException($"Ambiguous {type.Name}.{name}");target=method;}
  if(target==null)throw new MissingMethodException(type.FullName,name);
  p.Patches.Patch(target,prefix:new HarmonyMethod(typeof(RecoilAssist),prefix),finalizer:finalizer==null?null:new HarmonyMethod(typeof(RecoilAssist),finalizer));
 }
 static bool Active=>ready&&Enabled!=null&&Enabled.Value;
 static bool Local(Player.FirearmController? controller){
  if(controller==null||controller.Destroyed)return false;
  var player=controller._player;
  return player!=null&&!player.Destroyed&&player.IsYourPlayer&&player.HandsController?.Pointer==controller.Pointer;
 }
 // Resolve the current local controller at each hook instead of retaining old rig IDs.
 static bool OldForce(OldRecoilShotEffect? __instance){
  if(!Active||!Local(__instance?._firearmController))return true;
  oldShots++;return false;
 }
 static bool NewForce(NewRecoilShotEffect? __instance){
  if(!Active||!Local(__instance?._firearmController))return true;
  newShots++;return false;
 }
 static bool CameraRecoil(ProceduralWeaponAnimation? __instance)=>!(Active&&Local(__instance?._firearmController));
 static void BeforeEffectors(ProceduralWeaponAnimation __instance,out SteadyFrame __state){
  __state=default;
  try{
   if(!ready||steadyAim?.Value!=true||!Local(__instance?._firearmController)||!__instance!.IsAiming)return;
   var breath=__instance.Breath;
   if(breath!=null&&float.IsFinite(breath.Intensity)&&breath.Intensity!=0f){
    __state.Breath=breath;__state.BreathIntensity=breath.Intensity;breath.Intensity=0f;
   }
   var rotation=__instance.HandsContainer?.HandsRotation;
   if(rotation!=null&&float.IsFinite(rotation.InputIntensity)&&rotation.InputIntensity!=0f){
    __state.Rotation=rotation;__state.RotationIntensity=rotation.InputIntensity;rotation.InputIntensity=0f;
   }
   if(__state.Breath!=null||__state.Rotation!=null)steadyPasses++;
  }catch(Exception e){ReportSteadyAimError(e);}
 }
 // Harmony finalizer also runs on exceptions. Do not replace a native writer's
 // nonzero value or reset spring position, velocity, draw/stow or camera state.
 static void FinishEffectors(SteadyFrame __state){
  try{if(__state.Breath is {} breath&&breath.Intensity==0f)breath.Intensity=__state.BreathIntensity;}
  catch(Exception e){ReportSteadyAimError(e);}
  try{if(__state.Rotation is {} rotation&&rotation.InputIntensity==0f)rotation.InputIntensity=__state.RotationIntensity;}
  catch(Exception e){ReportSteadyAimError(e);}
  if(__state.Breath!=null||__state.Rotation!=null)finishedSteadyPasses++;
 }
 static void ReportSteadyAimError(Exception error){
  steadyErrors++;
  if(Time.unscaledTime<nextSteadyAimError)return;
  nextSteadyAimError=Time.unscaledTime+10f;
  Plugin.Self.Log.LogWarning("Steady aim could not update: "+error.Message);
 }
 internal static object Diagnostics(){
  var method=AccessTools.Method(typeof(ProceduralWeaponAnimation),"AddHandRecoilRotateToCamera");
  var patches=Harmony.GetPatchInfo(method);
  var effectors=Harmony.GetPatchInfo(AccessTools.Method(typeof(ProceduralWeaponAnimation),"ProcessEffectors"));
  var player=Comfort.Common.Singleton<GameWorld>.Instance?.MainPlayer;
  var animation=player?.ProceduralWeaponAnimation;
  return new{ready,noRecoil=Enabled?.Value==true,steadyAim=steadyAim?.Value==true,oldShots,newShots,steadyPasses,finishedSteadyPasses,steadyErrors,
   currentLocalHands=Local(animation?._firearmController),aiming=animation?.IsAiming,
   breathIntensity=animation?.Breath?.Intensity,rotationIntensity=animation?.HandsContainer?.HandsRotation?.InputIntensity,
   scopedSteadyAimPatched=effectors?.Owners.Contains("local.spt5.esp")==true,
   cameraRecoilSuppressionPatched=patches?.Owners.Contains("local.spt5.esp")==true};
 }
 internal static void Keys(){
  if(Hotkeys.Pressed(ToggleKey.Value)){Enabled.Value=!Enabled.Value;Plugin.Self.Log.LogInfo("No recoil: "+Enabled.Value);}
 }
 internal static string Status=>"无后座："+(!ready?"不可用":CnConfig.OnOff(Enabled.Value));
}
