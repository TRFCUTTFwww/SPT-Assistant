using System;
using System.Linq;
using EFT;
using EFT.Interactive;
using UnityEngine;
using UnityEngine.AI;
namespace LocalSpt5Esp;
internal static class CompanionNavigation {
 static int Mask=>LayerMask.GetMask("HighPolyCollider","Terrain","LowPolyCollider");
 static bool BodyClear(Vector3 from,Vector3 to){var d=to-from;return d.sqrMagnitude<.01f||!Physics.SphereCast(from+Vector3.up*.85f,.22f,d.normalized,out _,d.magnitude,Mask,QueryTriggerInteraction.Ignore);}
 internal static bool ClearSegment(Vector3 from,Vector3 to)=>Math.Abs(from.y-to.y)<1.1f&&!NavMesh.Raycast(from,to,out _,NavMesh.AllAreas)&&BodyClear(from,to);
 internal static Vector3[] Detour(Vector3 from,Vector3 to,Vector3[] original,int attempt){
  var forward=to-from;forward.y=0;if(forward.sqrMagnitude<.1f)return original;forward.Normalize();var right=new Vector3(forward.z,0,-forward.x);
  for(int i=0;i<4;i++){float side=(i%2==0?1:-1)*(attempt%2==0?1:-1);var proposed=from+right*(side*(1.1f+i/2*.8f))+forward*.25f;
   if(!NavMesh.SamplePosition(proposed,out var hit,.6f,NavMesh.AllAreas)||Math.Abs(hit.position.y-from.y)>1||!ClearSegment(from,hit.position))continue;
   var first=new NavMeshPath();var rest=new NavMeshPath();if(!NavMesh.CalculatePath(from,hit.position,NavMesh.AllAreas,first)||first.status!=NavMeshPathStatus.PathComplete||!NavMesh.CalculatePath(hit.position,to,NavMesh.AllAreas,rest)||rest.status!=NavMeshPathStatus.PathComplete)continue;
   return first.corners.ToArray().Concat(rest.corners.ToArray().Skip(1)).ToArray();
  }return original;
 }
 internal static Vector3 Formation(CompanionSquad.Member member,Player actor,Player local){
  if(Time.unscaledTime<member.NextFormation&&(local.Position-member.FormationAnchor).sqrMagnitude<4)return member.FormationPoint;
  member.NextFormation=Time.unscaledTime+1;member.FormationAnchor=local.Position;float distance=CompanionPlans.FollowDistance.Value+member.Index/8*1.5f;float angle=member.Index*137.5f*Mathf.Deg2Rad;
  if(!NavMesh.SamplePosition(actor.Position,out var origin,2,NavMesh.AllAreas))return member.FormationPoint=local.Position;
  for(int ring=0;ring<2;ring++)for(int i=0;i<8;i++){float a=angle+(i%2==0?1:-1)*((i+1)/2)*Mathf.PI/4;var candidate=local.Position+new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*distance*(ring==0?1:.55f);
   if(!NavMesh.SamplePosition(candidate,out var sample,1,NavMesh.AllAreas)||Math.Abs(sample.position.y-local.Position.y)>1.5f||!BodyClear(local.Position,sample.position))continue;
   var path=new NavMeshPath();if(NavMesh.CalculatePath(origin.position,sample.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete)return member.FormationPoint=sample.position;
  }return member.FormationPoint=local.Position;
 }
 internal static bool OpeningDoor(Player actor)=>actor.AIData?.BotOwner?.DoorOpener?.Interacting==true;
 internal static void AssistDoor(CompanionSquad.Member member,Player actor){
  if(!member.Moving||OpeningDoor(actor)||Time.unscaledTime<member.NextDoor||member.Corner>=member.Corners.Length)return;
  var toward=member.Corners[member.Corner]-actor.Position;toward.y=0;if(toward.sqrMagnitude<.01f)return;
  if(!Physics.Raycast(actor.Position+Vector3.up*.85f,toward.normalized,out var hit,Math.Min(2,toward.magnitude),Mask,QueryTriggerInteraction.Ignore))return;
  var door=hit.collider?.GetComponentInParent<Door>();if(door==null||door.DoorState!=EDoorState.Shut)return;
  member.NextDoor=Time.unscaledTime+2.5f;actor.AIData?.BotOwner?.DoorOpener?.Interact(door,EInteractionType.Open);member.Status="正在开门通过";
 }
}
