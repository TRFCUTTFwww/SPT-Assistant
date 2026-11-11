using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using UnityEngine.AI;
namespace LocalSpt5Esp;
// Exact-coordinate placement for editing tools. Air anchors own physics and pause
// independently of editor mode; moving onto navigation or releasing restores both.
internal static class AiFreePlacement {
 sealed record Anchor(LiveActorReference Reference,Player Player,Vector3 Point,bool WasPaused);
 static readonly Dictionary<IntPtr,Anchor> anchors=new();
 static bool Match(Player player,out Anchor value)=>anchors.TryGetValue(player.Pointer,out value!)&&value.Reference.World==Singleton<GameWorld>.Instance?.Pointer&&value.Reference.Profile==player.ProfileId;
 internal static bool Pinned(Player player)=>Match(player,out _);
 internal static bool PriorPaused(Player player)=>Match(player,out var value)?value.WasPaused:AiTestControl.IsPaused(player);
 internal static void Release(Player player){if(!Match(player,out var value))return;anchors.Remove(player.Pointer);try{value.Reference.Resolve();AiTestControl.SetPaused(new[]{player},value.WasPaused);}catch{}}
 internal static void Place(GameWorld current,Player actor,Vector3 point,bool? previousPause=null){
  new LiveActorReference(current,actor).Resolve();if(actor.IsYourPlayer||actor.AIData?.BotOwner==null||actor.Profile?.Info?.Settings?.Role==WildSpawnType.shooterBTR)throw new ArgumentException("只允许编辑本机控制的普通 AI。");
  if(!TeleportRules.Finite(new(point.x,point.y,point.z)))throw new ArgumentException("目标坐标无效。");
  bool paused=previousPause??PriorPaused(actor);var bot=actor.AIData.BotOwner;var origin=actor.Position;bool originalPause=AiTestControl.IsPaused(actor);anchors.TryGetValue(actor.Pointer,out var originalAnchor);
  // Do not route through BotMover.Teleport: it requires navigation at the goal.
  try{
   bot.StopMove();bot.Mover?.AllowTeleport();actor.Teleport(point,true);
   bool ground=NavMesh.SamplePosition(point,out var nav,.4f,NavMesh.AllAreas)&&Vector3.Distance(nav.position,point)<=.35f;
   anchors.Remove(actor.Pointer);
   if(!ground)anchors[actor.Pointer]=new(new(current,actor),actor,point,paused);
   AiTestControl.SetPaused(new[]{actor},!ground||paused);
   if(ground)bot.Mover?.RecalcWay();
  }catch{if(originalAnchor!=null)anchors[actor.Pointer]=originalAnchor;else anchors.Remove(actor.Pointer);try{actor.Teleport(origin,true);AiTestControl.SetPaused(new[]{actor},originalPause);}catch{}throw;}
 }
 internal static void FilterMotion(Il2CppObjectBase controller,ref Vector3 motion){
  foreach(var value in anchors.Values)if(value.Player.CharacterController?.Pointer==controller.Pointer&&Match(value.Player,out _)&&value.Player.HealthController?.IsAlive==true){motion=Vector3.zero;return;}
 }
 internal static void Tick(GameWorld? current){
  foreach(var value in anchors.Values.ToArray())try{if(current?.Pointer!=value.Reference.World||!RaidClock.Active){anchors.Remove(value.Reference.Pointer);continue;}var actor=value.Reference.Resolve();if(WorldEditor.Holding(actor))continue;if(!AiTestControl.IsPaused(actor))AiTestControl.SetPaused(new[]{actor},true);if((actor.Position-value.Point).sqrMagnitude>.0001f)actor.Teleport(value.Point,true);}catch{anchors.Remove(value.Reference.Pointer);}
 }
 internal static void Reset(){foreach(var value in anchors.Values.ToArray())Release(value.Player);anchors.Clear();}
}
