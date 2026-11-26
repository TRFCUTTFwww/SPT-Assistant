using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using Il2CppInterop.Runtime;
using LocalMiscShared;
using UnityEngine;

namespace LocalSpt5Esp;

internal static class ScavDiplomacy {
 static readonly HashSet<IntPtr> seen=new();
 static readonly Dictionary<IntPtr,string> protectedOwners=new();
 static IntPtr world;
 static bool active;
 static float next;
 static string localId="";

 internal static bool Protect(BotOwner? owner,IPlayer? target){
  if(!active||!MiscPanel.Options.DiplomaticImmunity||owner==null||target==null||target.ProfileId!=localId)return false;
  return protectedOwners.TryGetValue(owner.Pointer,out var id)&&owner.GetPlayer?.ProfileId==id;
 }

 internal static bool Protect(BotsGroup group,IPlayer target){
  bool found=false;
  var members=group._members;
  if(members==null)return false;
  foreach(var member in members){
   if(member==null)continue;
   if(!Protect(member,target))return false;
   found=true;
  }
  return found;
 }

 static bool InvalidateHostility(BotsGroup group,Player local){
  try{
   var statuses=group.BotGroupHostility?.LastHostilityStatus;
   if(statuses==null)return false;
   // Invalidate only our target. The stock AI update owns group-wide recalculation.
   statuses.Remove(local.RaidId);
   return true;
  }catch(Il2CppException error) when(error.Message.Contains("System.NullReferenceException",StringComparison.Ordinal)){
   return false;
  }
 }

 internal static void Tick(){
  var current=Singleton<GameWorld>.Instance;
  var local=current?.MainPlayer;
  if(current==null||local==null||!RaidClock.Active){Reset();return;}
  if(world!=current.Pointer){
   seen.Clear();
   protectedOwners.Clear();
   active=false;
   next=0;
   world=current.Pointer;
  }
  bool enabled=MiscPanel.Options.DiplomaticImmunity&&local.Side==EPlayerSide.Savage;
  if(Time.unscaledTime<next&&enabled==active)return;
  next=Time.unscaledTime+.5f;
  if(!enabled){
   if(active)Reset();
   return;
  }

  var players=current.AllAlivePlayersList;
  if(players==null)return;
  localId=local.ProfileId;
  protectedOwners.Clear();
  active=true;
  foreach(var actor in players){
   if(actor==null)continue;
   var owner=actor.AIData?.BotOwner;
   var group=owner?.BotsGroup;
   if(owner==null||group==null)continue;
   if(!MiscSettings.Diplomacy(true,true,true,true,!actor.IsYourPlayer,actor.Side==EPlayerSide.Savage,actor.Profile?.Info?.Settings?.Role.ToString()??""))continue;
   protectedOwners[owner.Pointer]=actor.ProfileId;
   if(!seen.Contains(group.Pointer)&&InvalidateHostility(group,local))seen.Add(group.Pointer);
   AiActorControl.Forget(owner,local);
  }
 }

 internal static void Reset(){
  active=false;
  protectedOwners.Clear();
  localId="";
  next=0;
  try{
   if(seen.Count==0)return;
   var current=Singleton<GameWorld>.Instance;
   if(current==null||current.Pointer!=world||current.MainPlayer is not {} local||current.AllAlivePlayersList is not {} players)return;
   foreach(var actor in players){
    var group=actor?.AIData?.BotOwner?.BotsGroup;
    if(group!=null&&seen.Contains(group.Pointer)&&InvalidateHostility(group,local))seen.Remove(group.Pointer);
   }
  }finally{
   seen.Clear();
   world=IntPtr.Zero;
  }
 }
}
