using System;
using EFT;
using EFT.Interactive;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
namespace LocalSpt5Esp;
internal static class ContainerLoadCompatibility {
 internal static void Initialize(Plugin plugin){
#if EFT_48558
  plugin.Patches.Patch(AccessTools.Method(typeof(GameWorld),"DestroyContainers"),prefix:new HarmonyMethod(typeof(ContainerLoadCompatibility),nameof(BeforeDestroy)));
  plugin.Log.LogInfo("Container load compatibility ready: missing destruction targets are removed before native cleanup");
#endif
 }
#if EFT_48558
 static void BeforeDestroy(ref Il2CppReferenceArray<IStaticLoot> __1){
  if(__1==null)return;
  foreach(var source in __1){
   var container=source?.TryCast<LootableContainer>();if(container==null)continue;
   var targets=container.GameObjectsToDestroy;if(targets==null)continue;
   int removed=0;for(int i=targets.Count-1;i>=0;i--)if(targets[i]==null){targets.RemoveAt(i);removed++;}
   if(removed>0)Plugin.Self.Log.LogWarning($"Container cleanup: removed {removed} missing destruction target(s); id={container.Id}; template={container.Template}; scene={container.gameObject.scene.name}; name={container.name}");
  }
 }
#endif
}
