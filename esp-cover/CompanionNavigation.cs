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
 internal static Vector3 Formation(CompanionSquad.Member member,Player actor,Player local){
  if(Time.unscaledTime<member.NextFormation&&(local.Position-member.FormationAnchor).sqrMagnitude<4)return member.FormationPoint;
  member.NextFormation=Time.unscaledTime+1;member.FormationAnchor=local.Position;float distance=CompanionPlans.FollowDistance.Value+member.Index/8*1.5f;float angle=member.Index*137.5f*Mathf.Deg2Rad;
  if(!NavMesh.SamplePosition(actor.Position,out var origin,2,NavMesh.AllAreas))return member.FormationPoint=actor.Position;
  for(int ring=0;ring<2;ring++)for(int i=0;i<8;i++){float a=angle+(i%2==0?1:-1)*((i+1)/2)*Mathf.PI/4;var candidate=local.Position+new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*distance*(ring==0?1:.55f);
   if(!NavMesh.SamplePosition(candidate,out var sample,1,NavMesh.AllAreas)||Math.Abs(sample.position.y-local.Position.y)>1.5f||!BodyClear(local.Position,sample.position)||CompanionSquad.Occupied(member,sample.position))continue;
   var path=new NavMeshPath();if(NavMesh.CalculatePath(origin.position,sample.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete)return member.FormationPoint=sample.position;
  }return member.FormationPoint=actor.Position;
 }
 internal static bool OpeningDoor(Player actor)=>actor.AIData?.BotOwner?.DoorOpener?.Interacting==true;
}
