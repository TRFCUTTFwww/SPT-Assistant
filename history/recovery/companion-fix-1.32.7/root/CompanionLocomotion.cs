using System;
using System.Linq;
using EFT;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.AI;
namespace LocalSpt5Esp;
// One movement owner: squad planner publishes a route, native BotMover executes
// it. Role-specific brains cannot replace/stop that route during squad travel.
internal static class CompanionLocomotion {
 [ThreadStatic]static IntPtr issuing;
 static void Issue(BotMover mover,Action command){var previous=issuing;issuing=mover.Pointer;try{command();}finally{issuing=previous;}}
 static bool Controlled(BotMover? mover,out CompanionSquad.Member member){member=null!;try{return mover!=null&&issuing!=mover.Pointer&&CompanionSquad.NavigationOwner(mover._player,out member);}catch{return false;}}
 internal static void Initialize(Plugin plugin){
  foreach(var method in typeof(BotMover).GetMethods().Where(m=>m.DeclaringType==typeof(BotMover)&&m.ReturnType==typeof(NavMeshPathStatus)&&m.Name is "GoToPoint" or "CurrentStateGoToPoint"))plugin.Patches.Patch(method,prefix:new HarmonyMethod(typeof(CompanionLocomotion),nameof(RouteRequest)));
  foreach(var name in new[]{"GoToByWay","GoToPointNoWay"})plugin.Patches.Patch(typeof(BotMover).GetMethod(name),prefix:new HarmonyMethod(typeof(CompanionLocomotion),nameof(RouteWrite)));
  plugin.Patches.Patch(typeof(BotMover).GetMethod("Stop"),prefix:new HarmonyMethod(typeof(CompanionLocomotion),nameof(StopRequest)));
  plugin.Patches.Patch(typeof(BotMover).GetMethod("MovementPause"),prefix:new HarmonyMethod(typeof(CompanionLocomotion),nameof(PauseRequest)));
  plugin.Patches.Patch(typeof(BotMover).GetMethod("Sprint"),prefix:new HarmonyMethod(typeof(CompanionLocomotion),nameof(SprintRequest)));
  plugin.Patches.Patch(typeof(BotMover).GetMethod("MovePlayer"),prefix:new HarmonyMethod(typeof(CompanionLocomotion),nameof(BeforeNativeMovement)));
  plugin.Log.LogInfo("Companion-only locomotion takeover ready: exclusive route writes, native movement execution; other AI unchanged");
 }
 static bool RouteRequest(BotMover __instance,ref NavMeshPathStatus __result){if(!Controlled(__instance,out var member))return true;member.BlockedNativeOrders++;__result=member.Moving?NavMeshPathStatus.PathComplete:NavMeshPathStatus.PathInvalid;return false;}
 static bool RouteWrite(BotMover __instance){if(!Controlled(__instance,out var member))return true;member.BlockedNativeOrders++;return false;}
 static bool StopRequest(BotMover __instance){if(!Controlled(__instance,out var member)||!member.Moving)return true;member.BlockedNativeOrders++;return false;}
 static bool PauseRequest(BotMover __instance)=>!Controlled(__instance,out var member)||!member.Moving;
 static void SprintRequest(BotMover __instance,ref bool __0){if(Controlled(__instance,out var member)&&member.Moving)__0=(member.Goal-__instance._player.Position).sqrMagnitude>144&&__instance._player.AIData?.BotOwner?.Memory?.GoalEnemy?.IsVisible!=true;}
 static void BeforeNativeMovement(BotMover __instance){if(!Controlled(__instance,out var member))return;var actor=__instance._player;var d=actor.Position-member.Goal;if(member.Moving&&CompanionRules.Near(d.x,d.y,d.z,member.Reach))member.Moving=false;if(!member.Moving&&__instance.IsMoving)Stop(actor);}
 internal static void Publish(Player actor,Vector3[] corners,float reach){var mover=actor.AIData?.BotOwner?.Mover??throw new ArgumentException("队友移动器尚未就绪。");Issue(mover,()=>{mover.MovementResume();mover.SetTargetMoveSpeed(1);mover.GoToByWay(new Il2CppStructArray<Vector3>(corners),reach);mover.Sprint((corners[^1]-actor.Position).sqrMagnitude>144,false);});}
 internal static void Stop(Player actor){var mover=actor.AIData?.BotOwner?.Mover;if(mover!=null)Issue(mover,mover.Stop);}
 internal static void AfterBrain(BotOwner owner){var actor=owner.GetPlayer;if(actor==null||!CompanionSquad.NavigationOwner(actor,out var member))return;if(!member.Moving){if(owner.Mover?.IsMoving==true)Stop(actor);return;}if(owner.Memory?.GoalEnemy?.IsVisible!=true)owner.Steering?.LookToMovingDirection();
  if(member.NextMovementLog==0){member.NextMovementLog=Time.unscaledTime+5;member.AuditPosition=actor.Position;}else if(Time.unscaledTime>=member.NextMovementLog){member.NextMovementLog=Time.unscaledTime+5;var mover=owner.Mover;Plugin.Self.Log.LogInfo($"Companion movement audit: profile={actor.ProfileId}; order={member.Order}; nativeMoving={mover?.IsMoving}; paused={mover?.Pause}; speed={mover?.MoveSpeed}; advanced={Vector3.Distance(actor.Position,member.AuditPosition):F2}; remaining={Vector3.Distance(actor.Position,member.Goal):F1}; blockedNativeOrders={member.BlockedNativeOrders}");member.BlockedNativeOrders=0;member.AuditPosition=actor.Position;}
 }
}
