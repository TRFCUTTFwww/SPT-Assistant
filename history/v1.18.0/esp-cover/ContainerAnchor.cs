using EFT.Interactive;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class ContainerAnchor {
 internal static Vector3 Position(LootableContainer container){
  var fallback=container.transform.position;
  try{
   var collider=container.Collider;
   if(collider!=null){var bounds=collider.bounds;if(bounds.size.sqrMagnitude>0.000001f)return bounds.center;}
  }catch{}
  // Handle is a native convenience getter that throws on containers without a handle.
  // Prefer a collider owned by this exact drawer, never a neighbouring drawer.
  try{
   foreach(var collider in container.GetComponentsInChildren<Collider>(true)){
    if(collider==null)continue;var owner=collider.GetComponentInParent<LootableContainer>();
    if(owner==null||owner.Pointer!=container.Pointer)continue;
    var bounds=collider.bounds;if(bounds.size.sqrMagnitude>0.000001f)return bounds.center;
   }
  }catch{}
  try{var renderer=container.GetComponent<Renderer>();if(renderer!=null)return renderer.bounds.center;}catch{}
  return fallback;
 }
}
