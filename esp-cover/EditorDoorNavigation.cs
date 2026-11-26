using EFT;
using EFT.Interactive;
using UnityEngine;
using UnityEngine.AI;

namespace LocalSpt5Esp;

internal static class EditorDoorNavigation {
 const string Marker="SPT Assistant locked doorway";
 sealed record Barrier(Door Door,GameObject Marker);
 static readonly Dictionary<IntPtr,Barrier> barriers=new();
 static IntPtr world;
 static float nextCheck;
 static readonly List<IntPtr> finished=new();
 static readonly List<Vector3> changed=new();

 internal static bool Blocks(Door? door)=>door!=null&&door.DoorState==EDoorState.Locked&&barriers.ContainsKey(door.Pointer);

 internal static void Add(Door door){
  if(barriers.ContainsKey(door.Pointer))return;
  var marker=new GameObject(Marker);
  marker.SetActive(false);
  try{
   marker.transform.SetParent(door.transform,false);
   var obstacle=marker.AddComponent<NavMeshObstacle>();
   obstacle.shape=NavMeshObstacleShape.Box;
   // Use the physical door leaf, including its pivot offset on hinged doors.
   var collider=door.CollisionColliders?.Where(c=>c!=null&&!c.isTrigger)
    .OrderByDescending(c=>c.bounds.size.sqrMagnitude).FirstOrDefault()??door.Collider;
   if(collider==null)throw new InvalidOperationException("这扇门缺少碰撞体，无法给 AI 封路。");
   var shape=collider.TryCast<BoxCollider>();
   var mesh=collider.TryCast<MeshCollider>()?.sharedMesh;
   if(shape!=null||mesh!=null){
    var bounds=shape!=null?new Bounds(shape.center,shape.size):mesh!.bounds;
    marker.transform.SetParent(collider.transform,false);
    obstacle.center=bounds.center;obstacle.size=bounds.size;
   }else{
    var bounds=collider.bounds;
    if(bounds.size.sqrMagnitude<.01f)throw new InvalidOperationException("这扇门的碰撞范围不可用。");
    marker.transform.SetParent(null,false);marker.transform.position=bounds.center;
    marker.transform.SetParent(door.transform,true);obstacle.size=bounds.size;
   }
   obstacle.carving=true;obstacle.carveOnlyStationary=false;
   marker.SetActive(true);
   barriers.Add(door.Pointer,new(door,marker));changed.Add(door.transform.position);
  }catch{UnityEngine.Object.Destroy(marker);throw;}
 }
 internal static void Remove(Door door){
  if(!barriers.Remove(door.Pointer,out var entry))return;
  Drop(entry);changed.Add(door.transform.position);
 }
 static void Drop(Barrier entry){
  if(entry.Marker==null)return;
  entry.Marker.SetActive(false);UnityEngine.Object.Destroy(entry.Marker);
 }
 internal static void Tick(GameWorld? current){
  if(world!=(current?.Pointer??IntPtr.Zero)){
   foreach(var entry in barriers.Values)Drop(entry);
   barriers.Clear();changed.Clear();world=current?.Pointer??IntPtr.Zero;nextCheck=0;
   if(current!=null&&RaidClock.Active){
    // Adopt markers left by the previous core during an in-raid hot reload.
    foreach(var obstacle in UnityEngine.Object.FindObjectsOfType<NavMeshObstacle>()){
     if(obstacle.name==Marker&&obstacle.GetComponentInParent<Door>() is {} door)
      barriers[door.Pointer]=new(door,obstacle.gameObject);
    }
   }
  }
  if(current==null||Time.unscaledTime<nextCheck||barriers.Count==0&&changed.Count==0)return;
  nextCheck=Time.unscaledTime+.25f;finished.Clear();
  foreach(var pair in barriers){
   var door=pair.Value.Door;
   if(door!=null&&door.DoorState==EDoorState.Locked)continue;
   if(door!=null)changed.Add(door.transform.position);
   Drop(pair.Value);finished.Add(pair.Key);
  }
  foreach(var key in finished)barriers.Remove(key);
  if(changed.Count==0)return;
  var bots=current.AllAlivePlayersList;
  if(bots!=null)foreach(var actor in bots){
   var mover=actor?.AIData?.BotOwner?.Mover;
   if(mover!=null&&mover.HasPathAndNoComplete&&changed.Any(p=>(actor!.Position-p).sqrMagnitude<=30*30))mover.RecalcWay();
  }
  changed.Clear();
 }
}
