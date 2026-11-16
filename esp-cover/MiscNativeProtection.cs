using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.BufferZone;
using EFT.Interactive;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class MiscNativeProtection {
 sealed record Immunity(PlayersWithImmuneToSniperFire Component,int RaidId);
 static readonly List<Immunity> added=new();static IntPtr world;static float next;
 internal static void Initialize(Plugin plugin){
  plugin.Patches.Patch(AccessTools.Method(typeof(BorderZone),"ProcessIncomingPacket"),prefix:new HarmonyMethod(typeof(MiscNativeProtection),nameof(Packet)));
  plugin.Patches.Patch(AccessTools.Method(typeof(BorderZone),"InvokePlayerShotEvent"),prefix:new HarmonyMethod(typeof(MiscNativeProtection),nameof(ShotEvent)));
  plugin.Patches.Patch(AccessTools.Method(typeof(BufferInnerZone),"GetUserRemainingUsageTime"),prefix:new HarmonyMethod(typeof(MiscNativeProtection),nameof(VisitTime)));
  plugin.Patches.Patch(AccessTools.Method(typeof(BufferInnerZone),"ReduceCurrentCustomerUsageTime"),prefix:new HarmonyMethod(typeof(MiscNativeProtection),nameof(VisitTick)));
 }
 static bool Packet(BorderZone __instance,IObserverToPlayerBridge __0)=>__instance.TryCast<SniperFiringZone>()==null||!MiscPanel.SniperProtected(__0?.TryCast<Player>());
 static bool ShotEvent(BorderZone __instance,IObserverToPlayerBridge __0)=>Packet(__instance,__0);
 static bool Lightkeeper=>MiscPanel.Options.UnlimitedLightkeeperVisit&&RaidClock.Active&&string.Equals(Singleton<GameWorld>.Instance?.LocationId,"lighthouse",StringComparison.OrdinalIgnoreCase);
 static bool VisitTime(int __0,ref float __result){var player=Singleton<GameWorld>.Instance?.MainPlayer;if(!Lightkeeper||player==null||player.RaidId!=__0)return true;__result=604800;return false;}
 static bool VisitTick(BufferInnerZone __instance){var player=Singleton<GameWorld>.Instance?.MainPlayer;if(!Lightkeeper||player==null||!__instance.IsPlayerInZone(player))return true;__instance.SetUserRemainingUsageTime(player.RaidId,604800);return false;}
 internal static void Tick(){
  var current=Singleton<GameWorld>.Instance;var local=current?.MainPlayer;
  if(!MiscPanel.Options.SniperBlind||!RaidClock.Active||local==null){Reset();return;}
  if(world!=current!.Pointer){Reset();world=current.Pointer;}
  if(Time.unscaledTime<next)return;next=Time.unscaledTime+2;
  foreach(var zone in UnityEngine.Object.FindObjectsOfType<SniperFiringZone>()){
   var immune=zone._playersWithImmuneToFire;if(immune==null||immune.IsPlayerImmuneForFire(local.RaidId))continue;
   immune.AddImmunePlayer(local.RaidId);added.Add(new(immune,local.RaidId));
  }
 }
 internal static void Reset(){foreach(var entry in added)try{entry.Component?.RemoveImmunePlayer(entry.RaidId);}catch{}added.Clear();world=IntPtr.Zero;next=0;}
}
