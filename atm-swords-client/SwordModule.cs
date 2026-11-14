using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using Local.ModHost;
using UnityEngine;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using GameTask=Il2CppSystem.Threading.Tasks.Task<UnityEngine.GameObject>;
namespace AllTheModium.Client;
public sealed partial class SwordModule:IHotModule {
 sealed record Swap(MeshFilter Filter,Mesh Original,Mesh Replacement,MeshRenderer Renderer,Il2CppReferenceArray<Material> Materials);
 sealed record Pending(Item Item,GameTask Task,float Started);
 static SwordModule? active;
 ModuleContext? context;
 readonly Dictionary<string,SwordAssets> assets=[];
 readonly Dictionary<IntPtr,Swap> swaps=[];
 readonly List<Pending> pending=[];
 float nextScan;
 int failures;
 [ThreadStatic] static string? creating;
 public int ApiVersion=>1;
 public string Version=>"1.0.1";
 // We only observe factory tasks. They own no module assets or callbacks and
 // must not block unloading if the native factory stalls or a UI request dies.
 public bool CanReload=>true;
 public void Initialize(ModuleContext context){
  this.context=context;context.OnShutdown(Cleanup);active=this;
  foreach(var sword in Swords.All)assets.Add(sword.Id,new(sword,Path.Combine(context.ModuleDirectory,"Assets"),context));
  var h=new Harmony(context.PatchId);
  foreach(var method in typeof(ObjectsFactory).GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Where(m=>!m.IsGenericMethod&&m.GetParameters().FirstOrDefault()?.ParameterType==typeof(Item))){
   if(method.ReturnType==typeof(GameObject)&&method.Name is "CreateItem" or "CreateItemUsablePrefab" or "CreateCleanLootPrefab" or "CreateLootPrefab")h.Patch(method,prefix:new HarmonyMethod(typeof(SwordModule),nameof(Creating)),postfix:new HarmonyMethod(typeof(SwordModule),nameof(Created)),finalizer:new HarmonyMethod(typeof(SwordModule),nameof(CreationEnded)));
   else if(method.ReturnType==typeof(GameTask)&&method.Name is "CreateItemAsync" or "CreateItemUsablePrefabAsync" or "CreateCleanLootPrefabAsync")h.Patch(method,prefix:new HarmonyMethod(typeof(SwordModule),nameof(Creating)),postfix:new HarmonyMethod(typeof(SwordModule),nameof(CreatedAsync)),finalizer:new HarmonyMethod(typeof(SwordModule),nameof(CreationEnded)));
  }
  // Observe the public pool boundary while the factory is creating the item.
  // Never detour the IL2CPP value-type async state machine (MoveNext): its
  // unboxed native this pointer is not a managed object pointer.
  h.Patch(AccessTools.Method(typeof(ObjectsFactory),nameof(ObjectsFactory.PopOrCreate)),postfix:new HarmonyMethod(typeof(SwordModule),nameof(Popped)));
  h.Patch(AccessTools.Method(typeof(Player.KnifeController),"Spawn"),postfix:new HarmonyMethod(typeof(SwordModule),nameof(Spawned)));
  context.Log(LogLevel.Info,"Sword assets loaded; model application is recorded in model-applications.json. Native gladius animation and hit registration retained.");
 }
 static void Creating(Item __0,out string? __state){__state=creating;creating=__0?.StringTemplateId;}
 static void CreationEnded(string? __state){creating=__state;}
 static void Popped(GameObject __result){var a=active;if(a?.context?.IsActive!=true||creating==null||__result==null)return;try{a.Apply(creating,__result);}catch(Exception e){a.context.Log(LogLevel.Warning,"Sword pool model: "+e.Message);}}
 static void Created(Item __0,GameObject __result)=>active?.TryApply(__0,__result);
 static void CreatedAsync(Item __0,GameTask __result){var a=active;if(a?.context?.IsActive!=true||__0==null||(Swords.Find(__0.StringTemplateId)==null&&__0.StringTemplateId!=Swords.BaseTemplate)||__result==null)return;if(__result.IsCompleted){if(!__result.IsFaulted&&!__result.IsCanceled)a.TryApply(__0,__result.Result);}else if(!a.pending.Any(p=>p.Task.Pointer==__result.Pointer))a.pending.Add(new(__0,__result,Time.unscaledTime));}
 static void Spawned(Player.KnifeController __instance)=>active?.TryApply(__instance.GetItem(),__instance.ControllerGameObject);
 void TryApply(Item? item,GameObject? root){if(context?.IsActive!=true||item==null||root==null)return;try{Apply(item.StringTemplateId,root);}catch(Exception e){if(failures++<5)context.Log(LogLevel.Warning,"Sword model: "+e);}}
 void Apply(string id,GameObject root){
  assets.TryGetValue(id,out var sword);
  if(sword==null&&id!=Swords.BaseTemplate)return;
  int applied=0;
  foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)){
   // Never touch arm meshes, damage/collision meshes, or a shared imported Mesh.
   if(!filter.name.StartsWith("weapon_apoc_wasteland_gladius_LOD",StringComparison.Ordinal))continue;
   if(swaps.TryGetValue(filter.Pointer,out var old)){
    if(old.Filter!=null&&old.Filter.Pointer==filter.Pointer){
     if(sword!=null&&old.Replacement.Pointer==sword.Mesh.Pointer&&filter.sharedMesh?.Pointer==sword.Mesh.Pointer)continue;
     Restore(old);
    }
    swaps.Remove(filter.Pointer);
   }
   if(sword==null)continue;
   var renderer=filter.GetComponent<MeshRenderer>();var original=filter.sharedMesh;if(renderer==null||original==null)continue;
   swaps.Add(filter.Pointer,new(filter,original,sword.Mesh,renderer,renderer.sharedMaterials));
   filter.sharedMesh=sword.Mesh;renderer.sharedMaterials=new Material[]{sword.Material};
   applied++;
  }
  RecordApplication(id,root,applied);
 }
 static void Restore(Swap swap){
  if(swap.Filter!=null&&swap.Filter.sharedMesh?.Pointer==swap.Replacement.Pointer){swap.Filter.sharedMesh=swap.Original;if(swap.Renderer!=null)swap.Renderer.sharedMaterials=swap.Materials;}
 }
 public void Tick(){
  if(context?.IsActive!=true)return;
  Diagnose();
  for(int i=pending.Count-1;i>=0;i--){var p=pending[i];if(!p.Task.IsCompleted){if(Time.unscaledTime-p.Started>30){pending.RemoveAt(i);context.Log(LogLevel.Warning,"Sword native model request timed out: "+p.Item.StringTemplateId);}continue;}pending.RemoveAt(i);if(!p.Task.IsCanceled&&!p.Task.IsFaulted)TryApply(p.Item,p.Task.Result);else context.Log(LogLevel.Warning,"Sword native model request failed: "+p.Item.StringTemplateId+" "+p.Task.Exception);}
  if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+.5f;
  foreach(var key in swaps.Where(p=>p.Value.Filter==null).Select(p=>p.Key).ToArray())swaps.Remove(key);
  // Covers hot reload, asynchronous native controller creation, and other players.
  var players=Singleton<GameWorld>.Instance?.AllAlivePlayersList;if(players==null)return;
  for(int i=0;i<players.Count;i++){var hands=players[i]?.HandsController?.TryCast<Player.KnifeController>();if(hands!=null)TryApply(hands.GetItem(),hands.ControllerGameObject);}
 }
 public void LateTick(){}
 public void Draw(){}
 public void Shutdown(){Cleanup();}
 void Cleanup(){if(active==this)active=null;foreach(var swap in swaps.Values)Restore(swap);swaps.Clear();pending.Clear();assets.Clear();modelApplications.Clear();creating=null;context=null;}
}
