using System;
using System.Collections.Generic;
using EFT;
using EFT.CameraControl;
using HarmonyLib;
using BepInEx.Logging;
namespace LocalSpt5Esp;
internal static class PlayerVisualGuard {
 const string OwnerKey="LocalSpt5Esp.VisualLifecycleGuard.Owner";
 static ManualLogSource log=null!;
 static long skipped;
 static readonly Dictionary<IntPtr,string> blocked=new();
 internal static void Initialize(Plugin plugin){
  // Keep this independent of feature unpatching: native players continue their
  // visual pass while the host replaces modules. Install the new guard before
  // releasing the old one, so only one previous module is retained temporarily.
  log=plugin.Log;string owner="local.spt5.esp.visual-lifecycle."+Guid.NewGuid().ToString("N");
  var method=AccessTools.Method(typeof(Player),"VisualPass");
  new Harmony(owner).Patch(method,prefix:new HarmonyMethod(typeof(PlayerVisualGuard),nameof(Ready)){priority=Priority.First});
  string? previous=AppDomain.CurrentDomain.GetData(OwnerKey) as string;
  AppDomain.CurrentDomain.SetData(OwnerKey,owner);
  if(previous!=null)new Harmony(previous).Unpatch(method,HarmonyPatchType.All,previous);
 }
 internal static bool Ready(Player __instance){
  var player=__instance;
  // VisualPass is also used by hideout/menu players. Do not gate it on InRaid
  // or health: corpses and live preview models still need their visual pass.
  string reason;
  if(player==null)reason="destroyed Unity player";
  else if(player.Destroyed)reason="disposed player";
  else if(player.MovementContext==null)reason="movement context unavailable";
  else if(player.PlayerBones==null)reason="visual skeleton unavailable";
  else if(!RaidClock.Active&&CameraManager.Instance?.Camera==null)reason="menu/hideout camera unloaded";
  else if(player.PointOfView==EPointOfView.ThirdPerson&&CameraManager.Instance==null)reason="third-person camera manager unavailable";
  else{blocked.Remove(player.Pointer);return true;}
  skipped++;
  if(player is not null&&(!blocked.TryGetValue(player.Pointer,out var previous)||previous!=reason)){
   if(blocked.Count>=32)blocked.Clear();blocked[player.Pointer]=reason;
   log.LogWarning($"Visual lifecycle guard: {reason}; player={player.Pointer}; destroyed={player.Destroyed}; movement={player.MovementContext!=null}; bones={player.PlayerBones!=null}; pov={player.PointOfView}; camera={CameraManager.Instance?.Camera!=null}");
  }
  return false;
 }
 internal static object Diagnostics()=>new{skipped,blocked=blocked.Count,reasons=new List<string>(blocked.Values)};
}
