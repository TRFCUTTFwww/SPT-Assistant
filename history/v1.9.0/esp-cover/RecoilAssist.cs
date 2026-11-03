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
 static bool ready;
 static IntPtr oldEffect,newEffect,animation,hands;
 static float nextRefresh;
 internal static void Initialize(Plugin p){
  Enabled=CnConfig.Bind(p.Config,"10. Recoil assist","No recoil",true,"Suppress local-player shot recoil and recoil camera rotation in both recoil pipelines. Independent of aim mode. Disabling restores normal recoil for subsequent shots.");
  ToggleKey=CnConfig.Bind(p.Config,"10. Recoil assist","Toggle key",KeyCode.F5,"Toggle no recoil.");
  int patched=0;
  try{
   Patch(p,typeof(OldRecoilShotEffect),"AddRecoilForce",nameof(OldForce));patched++;
   Patch(p,typeof(NewRecoilShotEffect),"AddRecoilForce",nameof(NewForce));patched++;
   Patch(p,typeof(ProceduralWeaponAnimation),"AddHandRecoilRotateToCamera",nameof(CameraRecoil));patched++;
   ready=true;p.Log.LogInfo($"No recoil ready: {patched} hooks; enabled={Enabled.Value}; toggle={ToggleKey.Value}");
  }catch(Exception e){ready=false;p.Log.LogError($"No recoil unavailable after {patched} hooks: {e}");}
 }
 static void Patch(Plugin p,Type type,string name,string prefix){
  var methods=type.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.DeclaredOnly);
  MethodInfo? target=null;
  foreach(var method in methods)if(method.Name==name){if(target!=null)throw new InvalidOperationException($"Ambiguous {type.Name}.{name}");target=method;}
  if(target==null)throw new MissingMethodException(type.FullName,name);
  p.Patches.Patch(target,prefix:new HarmonyMethod(typeof(RecoilAssist),prefix));
 }
 static bool Active=>ready&&Enabled!=null&&Enabled.Value;
 // The getter hooks ran for uninitialized effects, including on the main menu.
 // Only intercept shot impulses, using cached local-object identities.
 static bool OldForce(OldRecoilShotEffect? __instance)=>!(Active&&oldEffect!=IntPtr.Zero&&__instance?.Pointer==oldEffect);
 static bool NewForce(NewRecoilShotEffect? __instance)=>!(Active&&newEffect!=IntPtr.Zero&&__instance?.Pointer==newEffect);
 static bool CameraRecoil(ProceduralWeaponAnimation? __instance)=>!(Active&&animation!=IntPtr.Zero&&__instance?.Pointer==animation);
 internal static void Reset(){oldEffect=newEffect=animation=hands=IntPtr.Zero;nextRefresh=0;}
 internal static void Tick(Player? player){
  if(!Active){Reset();return;}
  try{
  if(player==null||!player.IsYourPlayer){Reset();return;}
  var current=player.HandsController;
  var pointer=current?.Pointer??IntPtr.Zero;
  if(hands==pointer&&Time.unscaledTime<nextRefresh)return;
  Reset();hands=pointer;nextRefresh=Time.unscaledTime+0.25f;
  if(current?.TryCast<Player.FirearmController>()==null)return;
   var procedural=player.ProceduralWeaponAnimation;if(procedural==null)return;
   animation=procedural.Pointer;var shooting=procedural.Shootingg;
   oldEffect=shooting?.OldShotRecoil?.Pointer??IntPtr.Zero;
   newEffect=shooting?.NewShotRecoil?.Pointer??IntPtr.Zero;
  }catch{Reset();nextRefresh=Time.unscaledTime+1;}
 }
 internal static void Keys(){
  if(Hotkeys.Pressed(ToggleKey.Value)){Enabled.Value=!Enabled.Value;Plugin.Self.Log.LogInfo("No recoil: "+Enabled.Value);}
 }
 internal static string Status=>"无后座："+(!ready?"不可用":CnConfig.OnOff(Enabled.Value));
}
