using System;
using System.Collections.Generic;
using Comfort.Common;
using BepInEx.Configuration;
using EFT;
using EFT.Interactive;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class DoorAssist {
 static ConfigEntry<bool> Enabled=null!;static bool ready;static float nextScan,nextError;
 sealed record Flags(Door Door,bool Breachable,bool Interactable);
 static readonly Dictionary<IntPtr,Flags> changed=new();
 static readonly Il2CppStructArray<RaycastHit> hits=new(32);
 static readonly HashSet<IntPtr> nearby=new();
 static IntPtr world;
 internal static void Initialize(Plugin p){
  Enabled=CnConfig.Bind(p.Config,"17. Force doors","Enabled",true);
  try{
   foreach(string name in new[]{"IsBreachAngle","IsOnBreachSide"})p.Patches.Patch(AccessTools.Method(typeof(Door),name),prefix:new HarmonyMethod(typeof(DoorAssist),nameof(SucceedBreach)));
   p.Patches.Patch(AccessTools.Method(typeof(MovementContext),"StartDoorBreak"),prefix:new HarmonyMethod(typeof(DoorAssist),nameof(BeforeBreak)),postfix:new HarmonyMethod(typeof(DoorAssist),nameof(AfterBreak)));
   p.Patches.Patch(AccessTools.Method(typeof(BreachDoorState),"ManualAnimatorMoveUpdate"),prefix:new HarmonyMethod(typeof(DoorAssist),nameof(BeforeKick)));
   ready=true;p.Log.LogInfo("Native door breach ready: local StartDoorBreak result and animation-time guard; bounded crosshair scan");
  }catch(Exception e){ready=false;p.Log.LogError("Native door breach unavailable: "+e);}
 }
 static bool LocalContext(MovementContext? context){var current=Singleton<GameWorld>.Instance;var player=current?.MainPlayer;return ready&&Enabled.Value&&RaidClock.Active&&player?.IsYourPlayer==true&&player.HealthController?.IsAlive==true&&context!=null&&player.MovementContext?.Pointer==context.Pointer;}
 static bool NearLocal(Door? door){var player=Singleton<GameWorld>.Instance?.MainPlayer;return ready&&Enabled.Value&&RaidClock.Active&&player?.IsYourPlayer==true&&door!=null&&(player.Position-door.transform.position).sqrMagnitude<=16;}
 static void Offer(Door door){if(!changed.ContainsKey(door.Pointer))changed[door.Pointer]=new(door,door.CanBeBreached,door.CanInteractWithBreach);door.CanBeBreached=true;door.CanInteractWithBreach=true;}
 internal static void Tick(Player? player){try{
  var current=Singleton<GameWorld>.Instance;
  if(!ready||!Enabled.Value||!RaidClock.Active||player?.IsYourPlayer!=true||current==null){Reset();return;}
  if(world!=current.Pointer)Reset();world=current.Pointer;
  if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+.25f;nearby.Clear();
  // Only the doors under the local interaction ray need menu flags. Never scan
  // every Door in a loaded map, or walk the entire door roster twice a second.
  var camera=OverlayProjection.MainCamera;if(camera!=null){var ray=camera.ViewportPointToRay(new Vector3(.5f,.5f));int count=Physics.RaycastNonAlloc(ray.origin,ray.direction,hits,4,~0,QueryTriggerInteraction.Collide);
   for(int i=0;i<count;i++){var door=hits[i].collider?.GetComponentInParent<Door>();if(!NearLocal(door)||door!.DoorState!=EDoorState.Locked&&door.DoorState!=EDoorState.Shut)continue;nearby.Add(door.Pointer);Offer(door);}
  }
  foreach(var key in new List<IntPtr>(changed.Keys)){var saved=changed[key];bool interacting=player.MovementContext?.InteractionDoor?.Pointer==key;if(!nearby.Contains(key)&&!interacting){Restore(saved);changed.Remove(key);}}
 }catch(Exception e){Reset();if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+10;Plugin.Self.Log.LogWarning("Door breach update: "+e.Message);}}}
 static void BeforeBreak(MovementContext __instance,WorldInteractiveObject __0){if(LocalContext(__instance)&&__0?.TryCast<Door>() is {} door&&NearLocal(door))Offer(door);}
 static void AfterBreak(MovementContext __instance,WorldInteractiveObject __0){if(!LocalContext(__instance)||__0?.TryCast<Door>() is not {} door||!NearLocal(door))return;__instance.NextBreachResult=true;Plugin.Self.Log.LogInfo("Local breach result forced: "+door.Id+"; state="+door.DoorState);}
 static void BeforeKick(BreachDoorState __instance){var context=__instance.MovementContext;if(LocalContext(context)&&__instance._door is {} door&&NearLocal(door))context.NextBreachResult=true;}
 static bool SucceedBreach(Door __instance,ref bool __result){if(!NearLocal(__instance))return true;__result=true;return false;}
 static void Restore(Flags saved){if(saved.Door==null)return;saved.Door.CanBeBreached=saved.Breachable;saved.Door.CanInteractWithBreach=saved.Interactable;}
 internal static void Reset(){foreach(var saved in changed.Values)try{Restore(saved);}catch{}changed.Clear();nearby.Clear();world=IntPtr.Zero;nextScan=0;}
}