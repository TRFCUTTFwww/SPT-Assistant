using System;
using System.Collections.Generic;
using EFT.Interactive;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class ContainerAnchor {
 sealed record Anchor(LootableContainer Owner,Collider? Collider,Renderer? Renderer,float Retry);
 static readonly Dictionary<IntPtr,Anchor> cache=new();
 internal static void Reset()=>cache.Clear();
 internal static Vector3 Position(LootableContainer container){
  var fallback=container.transform.position;
  if(cache.TryGetValue(container.Pointer,out var saved)&&saved.Owner!=null&&Time.unscaledTime<saved.Retry){
   if(saved.Collider!=null)return saved.Collider.bounds.center;
   if(saved.Renderer!=null)return saved.Renderer.bounds.center;
   return fallback;
  }
  try{
   var collider=container.Collider;
   if(collider!=null){var owner=collider.GetComponentInParent<LootableContainer>();var bounds=collider.bounds;if(owner!=null&&owner.Pointer==container.Pointer&&bounds.size.sqrMagnitude>0.000001f){cache[container.Pointer]=new(container,collider,null,Time.unscaledTime+10);return bounds.center;}}
  }catch{}
  // Handle is a native convenience getter that throws on containers without a handle.
  // Prefer a collider owned by this exact drawer, never a neighbouring drawer.
  try{
   foreach(var collider in container.GetComponentsInChildren<Collider>(true)){
    if(collider==null)continue;var owner=collider.GetComponentInParent<LootableContainer>();
    if(owner==null||owner.Pointer!=container.Pointer)continue;
    var bounds=collider.bounds;if(bounds.size.sqrMagnitude>0.000001f){cache[container.Pointer]=new(container,collider,null,Time.unscaledTime+10);return bounds.center;}
   }
  }catch{}
  try{var renderer=container.GetComponent<Renderer>();if(renderer!=null){cache[container.Pointer]=new(container,null,renderer,Time.unscaledTime+10);return renderer.bounds.center;}}catch{}
  cache[container.Pointer]=new(container,null,null,Time.unscaledTime+3);
  return fallback;
 }
}
