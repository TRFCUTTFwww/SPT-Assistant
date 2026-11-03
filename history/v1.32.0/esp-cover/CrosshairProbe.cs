using System;
using System.Collections.Generic;
using EFT;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
namespace LocalSpt5Esp;
internal enum CrosshairState { Empty, Target, Blocked, Overflow }
internal readonly record struct CrosshairContact(CrosshairState State,Player? Actor,BodyPartCollider? Body,Vector3 Point,float Distance);
internal static class CrosshairProbe {
 sealed record Entry(Player Actor,BodyPartCollider Body,Collider Collider);
 static readonly Il2CppStructArray<RaycastHit> hits=new(512);
 static readonly Dictionary<IntPtr,Entry> bodies=new();static IntPtr world;static float nextRefresh,nextDiagnostic;static int bodyMask;static bool reported;
 internal static string Diagnostic {get;private set;}="not scanned";
 internal static void Reset(){bodies.Clear();world=IntPtr.Zero;nextRefresh=nextDiagnostic=0;bodyMask=0;reported=false;Diagnostic="not scanned";}
 static void Refresh(GameWorld current){
  if(world!=current.Pointer){Reset();world=current.Pointer;}
  if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.5f;bodies.Clear();bodyMask=0;
  var all=current.AllAlivePlayersList;if(all==null)return;
  for(int i=0;i<all.Count;i++){
   var actor=all[i];if(actor==null||actor.HealthController?.IsAlive!=true||actor._hitColliders is not {} parts)continue;
   for(int p=0;p<parts.Length;p++){var part=parts[p];var collider=part?.Collider;if(part==null||collider==null)continue;bodies[collider.Pointer]=new(actor,part,collider);int layer=collider.gameObject.layer;if(layer is >=0 and <32)bodyMask|=1<<layer;}
  }
  if(!reported&&bodies.Count>0){reported=true;Plugin.Self.Log.LogInfo($"Crosshair collider map ready: native body colliders={bodies.Count}, bodyMask={bodyMask}; native GunshotMask + body layers");}
 }
 internal static bool LocalCollider(GameWorld current,Player local,Collider collider){
  if(collider.transform.IsChildOf(local.transform))return true;
  if(bodies.TryGetValue(collider.Pointer,out var entry))return entry.Actor.Pointer==local.Pointer;
  return current.PlayersColliders?.TryGetValue(collider,out var registered)==true&&registered?.ProfileId==local.ProfileId;
 }
 internal static CrosshairContact Read(GameWorld current,Player local,Camera camera,float range){
  Refresh(current);var ray=camera.ViewportPointToRay(new Vector3(.5f,.5f));
  int nativeMask=EFTHardSettings.Instance!=null?(int)EFTHardSettings.Instance.GunshotMask:0;int mask=nativeMask|bodyMask;if(mask==0)mask=~0;
  int count=Physics.RaycastNonAlloc(ray.origin,ray.direction,hits,range,mask,QueryTriggerInteraction.Collide);
  if(count>=hits.Length)return new(CrosshairState.Overflow,null,null,default,0);
  float nearest=float.MaxValue;CrosshairContact result=new(CrosshairState.Empty,null,null,default,0);
  Collider? nearestCollider=null;int exactHits=0,proxies=0;
  // Query the native hit shapes directly. Their owning player comes from the
  // world's hit-collider roster, independently of component/proxy lookup and
  // the global physics mask. The broad-phase bounds check avoids shape casts
  // against bodies which are nowhere near the crosshair.
  foreach(var entry in bodies.Values){
   var actor=entry.Actor;var collider=entry.Collider;
   if(actor.Pointer==local.Pointer||actor.IsYourPlayer||actor.HealthController?.IsAlive!=true||collider==null||!collider.enabled||!collider.gameObject.activeInHierarchy)continue;
   if(!collider.bounds.IntersectRay(ray,out float boundDistance)||boundDistance>range||boundDistance>nearest)continue;
   if(!collider.Raycast(ray,out var hit,Math.Min(range,nearest)))continue;
   exactHits++;nearest=hit.distance;nearestCollider=collider;
   bool ai=actor.IsAI||actor.AIData?.BotOwner!=null;
   result=ai?new(CrosshairState.Target,actor,entry.Body,hit.point,hit.distance):new(CrosshairState.Blocked,null,null,hit.point,hit.distance);
  }
  for(int i=0;i<count;i++){
   var hit=hits[i];var collider=hit.collider;if(collider==null||hit.distance>=nearest||LocalCollider(current,local,collider))continue;
   bodies.TryGetValue(collider.Pointer,out var entry);
   var part=entry?.Body??collider.GetComponent<BodyPartCollider>()??collider.GetComponent<BaseBallistic>()?.Get(hit.point)?.TryCast<BodyPartCollider>();
   var actor=entry?.Actor??part?.Player?.TryCast<Player>();
   if(actor?.Pointer==local.Pointer||part?.PlayerProfileID==local.ProfileId)continue;
   if(part!=null&&actor!=null&&(actor.IsAI||actor.AIData?.BotOwner!=null)&&!actor.IsYourPlayer&&actor.HealthController?.IsAlive==true){nearest=hit.distance;nearestCollider=collider;result=new(CrosshairState.Target,actor,part,hit.point,hit.distance);continue;}
   if(part==null){
    // Movement capsules and interaction proxies aren't bullet hit shapes.
    // Real player hit shapes, including human blockers, were checked above.
    if(collider.isTrigger)continue;
    if(current.PlayersColliders?.ContainsKey(collider)==true||collider.GetComponentInParent<Player>()!=null){proxies++;continue;}
   }
   nearest=hit.distance;nearestCollider=collider;result=new(CrosshairState.Blocked,null,null,hit.point,hit.distance);
  }
  if(Time.unscaledTime>=nextDiagnostic){nextDiagnostic=Time.unscaledTime+1;var actor=result.Actor;Diagnostic=$"physics={count}, shapes={bodies.Count}, exact={exactHits}, proxies={proxies}, nearest={nearestCollider?.name??"none"}, layer={(nearestCollider!=null?LayerMask.LayerToName(nearestCollider.gameObject.layer):"none")}, distance={result.Distance:F3}, actor={actor?.Profile?.Nickname??"none"}, isAI={actor?.IsAI}, botOwner={actor?.AIData?.BotOwner!=null}, ray={ray.origin}/{ray.direction}";}
  return result;
 }
 internal static Player? Target(GameWorld current,Player local,Camera camera,float range,out BodyPartCollider? body){var hit=Read(current,local,camera,range);body=hit.Body;return hit.Actor;}
}
