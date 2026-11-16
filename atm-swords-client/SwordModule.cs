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
 public string Version=>"1.3.5";
 // We only observe factory tasks. They own no module assets or callbacks and
 // must not block unloading if the native factory stalls or a UI request dies.
 public bool CanReload=>true;
 public void Initialize(ModuleContext context){
  this.context=context;context.OnShutdown(Cleanup);active=this;
  InitializeSettings();
  foreach(var sword in Swords.Renderable){var asset=new SwordAssets(sword,Path.Combine(context.ModuleDirectory,"Assets"),context);asset.SetSize(sizePercent.Value);asset.SetAppearance(brightnessPercent.Value,opacityPercent.Value);assets.Add(sword.Id,asset);}
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
  InstallGuard(h);
  InitializeInventoryIcons(h);
  InitializeHandsLifecycle(h);
  context.Log(LogLevel.Info,"Sword assets loaded using the 1.3.2 model factory path. Repeated rig traversal is cached; diagnostics are opt-in.");
 }
 static void Creating(Item __0,out string? __state){__state=creating;creating=__0?.StringTemplateId;}
 static void CreationEnded(string? __state){creating=__state;}
 static void Popped(GameObject __result){var a=active;if(a?.context?.IsActive!=true||creating==null||__result==null)return;try{a.Apply(creating,__result);}catch(Exception e){a.context.Log(LogLevel.Warning,"Sword pool model: "+e.Message);}}
 static void Created(Item __0,GameObject __result)=>active?.TryApply(__0,__result);
 static void CreatedAsync(Item __0,GameTask __result){var a=active;if(a?.context?.IsActive!=true||__0==null||(Swords.Find(__0.StringTemplateId)==null&&!Swords.Original(__0.StringTemplateId))||__result==null)return;if(__result.IsCompleted){if(!__result.IsFaulted&&!__result.IsCanceled)a.TryApply(__0,__result.Result);}else if(!a.pending.Any(p=>p.Task.Pointer==__result.Pointer))a.pending.Add(new(__0,__result,Time.unscaledTime));}
 static void Spawned(Player.KnifeController __instance)=>active?.TryApply(__instance.GetItem(),__instance.ControllerGameObject);
 void TryApply(Item? item,GameObject? root){if(context?.IsActive!=true||item==null||root==null)return;try{Apply(item.StringTemplateId,root);}catch(Exception e){if(failures++<5)context.Log(LogLevel.Warning,"Sword model: "+e);}}
 void Apply(string id,GameObject root){
  assets.TryGetValue(id,out var sword);
  if(sword==null&&!Swords.Original(id))return;
  int applied=0;
  foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)){
   // Never touch arm meshes, damage/collision meshes, or a shared imported Mesh.
   if(!Swords.Generic(id)&&id==Swords.BaseTemplate&&!filter.name.StartsWith("weapon_apoc_wasteland_gladius_LOD",StringComparison.Ordinal))continue;
   if(sword!=null&&!Swords.Generic(id)&&!filter.name.StartsWith("weapon_apoc_wasteland_gladius_LOD",StringComparison.Ordinal))continue;
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
  foreach(var asset in assets.Values){asset.SetSize(sizePercent.Value);asset.SetAppearance(brightnessPercent.Value,opacityPercent.Value);}
  RestoreToolPose();TickGuard();
  RefreshInventoryIcons();if(diagnosticsEnabled.Value){Diagnose();ObserveInventoryPresentation();}
  for(int i=pending.Count-1;i>=0;i--){var p=pending[i];if(!p.Task.IsCompleted){if(Time.unscaledTime-p.Started>30){pending.RemoveAt(i);context.Log(LogLevel.Warning,"Sword native model request timed out: "+p.Item.StringTemplateId);}continue;}pending.RemoveAt(i);if(!p.Task.IsCanceled&&!p.Task.IsFaulted)TryApply(p.Item,p.Task.Result);else context.Log(LogLevel.Warning,"Sword native model request failed: "+p.Item.StringTemplateId+" "+p.Task.Exception);}
  if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+.5f;
  foreach(var key in swaps.Where(p=>p.Value.Filter==null).Select(p=>p.Key).ToArray())swaps.Remove(key);
  // Keep the fallback for async hands creation and late joining actors, but
  // traverse a rig only when that actor changes hands. Factory hooks are 1.3.2.
  var world=Singleton<GameWorld>.Instance;
  if(world?.Pointer!=scannedWorld){scannedHands.Clear();scannedWorld=world?.Pointer??IntPtr.Zero;}
  var players=world?.AllAlivePlayersList;if(players==null)return;
  liveActors.Clear();
  for(int i=0;i<players.Count;i++){
   var player=players[i];if(player==null)continue;liveActors.Add(player.Pointer);
   var hands=player.HandsController?.TryCast<Player.KnifeController>();var root=hands?.ControllerGameObject;
   if(hands==null||root==null){scannedHands.Remove(player.Pointer);continue;}
   string item=hands.GetItem()?.Id.ToString()??"";var key=(hands.Pointer,root.Pointer,item);
   if(scannedHands.TryGetValue(player.Pointer,out var previous)&&previous==key)continue;
   TryApply(hands.GetItem(),root);scannedHands[player.Pointer]=key;
  }
  foreach(var actor in scannedHands.Keys.Where(p=>!liveActors.Contains(p)).ToArray())scannedHands.Remove(actor);
 }
 public void LateTick(){ApplyGuardPose();ApplyToolPose();}
 public void Draw(){}
 public void Shutdown(){Cleanup();}
 IntPtr scannedWorld;
 readonly Dictionary<IntPtr,(IntPtr Hands,IntPtr Root,string Item)> scannedHands=[];
 readonly HashSet<IntPtr> liveActors=[];
 void Cleanup(){RestoreToolPose();ResetGuard();ClearToolMeshes();scannedHands.Clear();liveActors.Clear();if(active==this)active=null;RestoreInventoryIcons();foreach(var swap in swaps.Values)Restore(swap);swaps.Clear();pending.Clear();assets.Clear();modelApplications.Clear();creating=null;context=null;}
}
