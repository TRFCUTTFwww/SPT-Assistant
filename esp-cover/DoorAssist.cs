using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using EFT;
using EFT.Interactive;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class DoorAssist {
 static ConfigEntry<bool> Enabled=null!;
 static bool ready;
 static bool inRaid;
 static Vector3 localPosition;
 sealed record Flags(Door Door,bool Breachable,bool Interactable);
 static readonly Dictionary<IntPtr,Flags> changed=new();
 static Door[] doors=Array.Empty<Door>();static float nextScan;
 internal static void Initialize(Plugin p){
  Enabled=CnConfig.Bind(p.Config,"17. Force doors","Enabled",true);
  try{
   Patch(p,"IsBreachAngle",nameof(SucceedBreach));
   Patch(p,"BreachSuccessRoll",nameof(SucceedBreach));
   ready=true;p.Log.LogInfo("Native door breach override ready: reversible nearby door fields and native angle/roll prefixes");
  }catch(Exception e){ready=false;p.Log.LogError("Native door breach override unavailable: "+e);}
 }
 static void Patch(Plugin p,string method,string postfix){
  var target=typeof(Door).GetMethod(method)??throw new MissingMethodException(typeof(Door).FullName,method);
  p.Patches.Patch(target,prefix:new HarmonyMethod(typeof(DoorAssist),postfix));
 }
 internal static void Tick(Player? player){
  try{
   inRaid=player!=null&&player.IsYourPlayer;if(inRaid)localPosition=player!.Position;
   if(!inRaid||!ready||!Enabled.Value){Restore();return;}
   if(Time.unscaledTime>=nextScan){nextScan=Time.unscaledTime+3;doors=UnityEngine.Object.FindObjectsOfType<Door>().ToArray();}
   foreach(var key in new List<IntPtr>(changed.Keys)){var saved=changed[key];if(saved.Door==null||!NearLocal(saved.Door)){Restore(saved);changed.Remove(key);}}
   foreach(var door in doors){if(!NearLocal(door)||door.DoorState!=EDoorState.Locked&&door.DoorState!=EDoorState.Shut)continue;
    if(!changed.ContainsKey(door.Pointer))changed[door.Pointer]=new(door,door.CanBeBreached,door.CanInteractWithBreach);
    door.CanBeBreached=true;door.CanInteractWithBreach=true;
   }
  }catch(Exception e){Reset();Plugin.Self.Log.LogWarning("Door breach update: "+e.Message);}
 }
 static void Restore(Flags saved){if(saved.Door==null)return;saved.Door.CanBeBreached=saved.Breachable;saved.Door.CanInteractWithBreach=saved.Interactable;}
 static void Restore(){foreach(var saved in changed.Values)try{Restore(saved);}catch{}changed.Clear();}
 internal static void Reset(){inRaid=false;Restore();doors=Array.Empty<Door>();nextScan=0;}
 static bool NearLocal(Door door){
  if(!ready||!Enabled.Value||!inRaid||door==null)return false;
  try{return Vector3.Distance(localPosition,door.transform.position)<=4f;}catch{return false;}
 }
 static bool SucceedBreach(Door __instance,ref bool __result){if(!NearLocal(__instance))return true;__result=true;return false;}
}
