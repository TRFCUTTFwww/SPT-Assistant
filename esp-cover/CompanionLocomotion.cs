using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
// Observation only. BotMover, steering, avoidance and animation remain native.
internal static class CompanionLocomotion {
 internal static void Initialize(Plugin plugin)=>plugin.Log.LogInfo("Companion native locomotion: no movement, route, stop, pause, sprint or steering interception");
 internal static void AfterBrain(BotOwner owner){
  var actor=owner.GetPlayer;if(!CompanionSquad.TryMember(actor,out var member)||Time.unscaledTime<member.NextMovementLog)return;
  member.NextMovementLog=Time.unscaledTime+10;var mover=owner.Mover;
  Plugin.Self.Log.LogInfo($"Companion native movement: profile={actor.ProfileId}; order={member.Order}; layer={owner.Brain?.ActiveLayerName()}; decision={owner.Brain?.LastDecision}; request={owner.BotRequestController?.CurRequest?.GetType().Name}; nativeMoving={mover?.IsMoving}; paused={mover?.Pause}; advanced={Vector3.Distance(actor.Position,member.AuditPosition):F2}; targetDistance={(member.Target==null?-1:Vector3.Distance(actor.Position,member.Target.Position)):F1}; pickup={member.Work.LastPickup}");
  member.AuditPosition=actor.Position;
 }
}
