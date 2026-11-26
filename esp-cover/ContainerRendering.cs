using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
namespace LocalSpt5Esp;

internal static class ContainerRendering {
 static IntPtr world;static bool recovered;
 static readonly List<(Transform Root,int Frame)> pending=new();
 internal static void Prepare(Transform root){
  // Scene MBOIT IDs refer to the source map's renderer registry. A new instance
  // must draw its own mesh instead of inheriting those disabled map renderers.
  foreach(var target in root.GetComponentsInChildren<LootableContainer>(true)){
   target._mboitRenderers=new Il2CppReferenceArray<WorldInteractiveObject.MBOITRenderer>(0);
   target._rendererToMBOITRenderer=new Il2CppSystem.Collections.Generic.Dictionary<Renderer,WorldInteractiveObject.MBOITRenderer>();
   target._shouldUpdateTransformWindowsManager=false;
  }
 }
 internal static void Repair(Transform root){
  Prepare(root);
  foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true)){
   if(renderer.GetComponent<MeshFilter>()?.sharedMesh==null)continue;
   renderer.enabled=true;renderer.forceRenderingOff=false;renderer.allowOcclusionWhenDynamic=false;
   renderer.lightmapIndex=-1;renderer.realtimeLightmapIndex=-1;
  }
  foreach(var lod in root.GetComponentsInChildren<LODGroup>(true)){lod.RecalculateBounds();lod.ForceLOD(0);}
 }
 internal static void Created(Transform root){Repair(root);pending.Add((root,Time.frameCount+2));}
 internal static void Tick(){
  var current=Singleton<GameWorld>.Instance;
  if(!RaidClock.Active||current==null){world=IntPtr.Zero;recovered=false;pending.Clear();return;}
  if(world!=current.Pointer){world=current.Pointer;recovered=false;pending.Clear();}
  ContainerScene.Bind(current);if(!recovered){recovered=true;var roots=new HashSet<IntPtr>();foreach(var container in UnityEngine.Object.FindObjectsByType<LootableContainer>(FindObjectsSortMode.None)){
   for(var root=container.transform;root!=null;root=root.parent)if(root.name.StartsWith("Assistant placed container ",StringComparison.Ordinal)){ContainerScene.Root(container);if(roots.Add(root.Pointer))Created(root);break;}
  }}
  for(int i=pending.Count-1;i>=0;i--){var entry=pending[i];if(Time.frameCount<entry.Frame)continue;pending.RemoveAt(i);if(entry.Root!=null)Repair(entry.Root);}
 }
 internal static void Reset(){pending.Clear();world=IntPtr.Zero;recovered=false;}
}
