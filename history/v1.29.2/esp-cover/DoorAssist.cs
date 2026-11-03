using System;
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
 internal static void Initialize(Plugin p){
  Enabled=CnConfig.Bind(p.Config,"17. Force doors","Enabled",true);
  try{
   Patch(p,"get_CanBeBreached",nameof(OfferBreach));
   Patch(p,"get_CanInteractWithBreach",nameof(OfferBreach));
   Patch(p,"IsBreachAngle",nameof(SucceedBreach));
   Patch(p,"BreachSuccessRoll",nameof(SucceedBreach));
   ready=true;p.Log.LogInfo("Native door breach override ready");
  }catch(Exception e){ready=false;p.Log.LogError("Native door breach override unavailable: "+e);}
 }
 static void Patch(Plugin p,string method,string postfix){
  var target=typeof(Door).GetMethod(method)??throw new MissingMethodException(typeof(Door).FullName,method);
  p.Patches.Patch(target,postfix:new HarmonyMethod(typeof(DoorAssist),postfix));
 }
 internal static void Tick(Player? player){
  try{inRaid=player!=null&&player.IsYourPlayer;if(inRaid)localPosition=player!.Position;}catch{inRaid=false;}
 }
 internal static void Reset(){inRaid=false;}
 static bool NearLocal(Door door){
  if(!ready||!Enabled.Value||!inRaid||door==null)return false;
  try{return Vector3.Distance(localPosition,door.transform.position)<=4f;}catch{return false;}
 }
 static void OfferBreach(Door __instance,ref bool __result){
  if(NearLocal(__instance)&&(__instance.DoorState==EDoorState.Locked||__instance.DoorState==EDoorState.Shut))__result=true;
 }
 static void SucceedBreach(Door __instance,ref bool __result){if(NearLocal(__instance))__result=true;}
}
