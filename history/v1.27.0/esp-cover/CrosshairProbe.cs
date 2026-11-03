using System;
using EFT;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class CrosshairProbe {
 static readonly Il2CppStructArray<RaycastHit> hits=new(128);
 internal static Player? Target(GameWorld world,Player local,Camera camera,float range,out BodyPartCollider? body){
  body=null;var ray=camera.ScreenPointToRay(new Vector3(Screen.width*.5f,Screen.height*.5f));
  int count=Physics.RaycastNonAlloc(ray.origin,ray.direction,hits,range,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Collide);
  if(count>=hits.Length)return null;
  float nearest=float.MaxValue;
  for(int i=0;i<count;i++){
   var hit=hits[i];var collider=hit.collider;if(collider==null||hit.distance>=nearest||collider.transform.IsChildOf(local.transform))continue;
   var part=collider.GetComponent<BodyPartCollider>();if(part?.PlayerProfileID==local.ProfileId)continue;
   if(part==null&&(collider.isTrigger||world.PlayersColliders?.ContainsKey(collider)==true))continue;
   nearest=hit.distance;body=part;
  }
  if(body==null)return null;var all=world.AllAlivePlayersList;if(all==null)return null;
  for(int i=0;i<all.Count;i++){var actor=all[i];if(actor?.ProfileId==body.PlayerProfileID&&actor.IsAI&&!actor.IsYourPlayer&&actor.HealthController?.IsAlive==true)return actor;}
  return null;
 }
}
