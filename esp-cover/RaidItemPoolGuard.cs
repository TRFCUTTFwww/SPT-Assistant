using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.NetworkPackets;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using NativeTask=Il2CppSystem.Threading.Tasks.Task;
using NativeTaskStatus=Il2CppSystem.Threading.Tasks.TaskStatus;

namespace LocalSpt5Esp;

/// <summary>
/// Ensures 3D AssetBundles and Raid ObjectPools exist in <see cref="ObjectsFactory"/> before
/// weapons, mods, magazines, ammo, throwables, knives or consumables from <see cref="RaidStash"/>
/// (or in-raid spawners) are instantiated on body slots or drawn into hands via <c>Player.Proceed</c>.
/// </summary>
internal static class RaidItemPoolGuard {
 sealed class DeferredProceed {
  internal Player Actor=null!;
  internal Item Item=null!;
  internal IntPtr WorldPointer;
  internal IntPtr ActorPointer;
  internal IntPtr ItemPointer;
  internal string ItemId="";
  internal Action Replay=null!;
  internal Action<string> Fail=null!;
  internal float StartedAt;
 }

 static readonly Dictionary<IntPtr,DeferredProceed> pendingProceeds=new();
 static readonly HashSet<(IntPtr Actor,IntPtr Item)> replaying=new();
 static readonly Queue<Item> warmQueue=new();
 static readonly HashSet<IntPtr> queuedItemPointers=new();
 static readonly HashSet<string> verifiedNoPoolPaths=new(StringComparer.OrdinalIgnoreCase);
 static readonly Dictionary<string,float> failedPathsUntil=new(StringComparer.OrdinalIgnoreCase);
 static readonly List<string> activeBatchPaths=new();

 static Item? pendingHoverItem;
 static NativeTask? activeLoadTask;
 static IntPtr currentWorldPtr,loadWorldPtr,localPlayerPtr;
 static bool draining,recoveringHands,immediateScanRequested;
 static float nextEquipmentScan,orphanSince=-1f,recoveryStartedAt;
 static int loadsStarted,loadsCompleted,deferredCount,replayedCount,slotViewsRefreshed,orphanRecoveries,loadErrors;

 internal static void Initialize(Plugin plugin){
  var p=typeof(Player);
  plugin.Patches.Patch(
   AccessTools.Method(p,nameof(Player.TryProceed),[typeof(Item),typeof(Callback<IHandsController>),typeof(bool)]),
   prefix:new HarmonyMethod(typeof(RaidItemPoolGuard),nameof(BeforeTryProceed)));
  plugin.Patches.Patch(
   AccessTools.Method(p,nameof(Player.Proceed),[typeof(Weapon),typeof(Callback<IFirearmHandsController>),typeof(bool)]),
   prefix:new HarmonyMethod(typeof(RaidItemPoolGuard),nameof(BeforeProceedWeapon)));
  plugin.Patches.Patch(
   AccessTools.Method(p,nameof(Player.Proceed),[typeof(ThrowWeap),typeof(Callback<IGrenadeController>),typeof(bool)]),
   prefix:new HarmonyMethod(typeof(RaidItemPoolGuard),nameof(BeforeProceedGrenade)));
  plugin.Patches.Patch(
   AccessTools.Method(p,nameof(Player.Proceed),[typeof(ThrowWeap),typeof(Callback<IQuickGrenadeThrowController>),typeof(bool)]),
   prefix:new HarmonyMethod(typeof(RaidItemPoolGuard),nameof(BeforeProceedQuickGrenade)));
  plugin.Patches.Patch(
   AccessTools.Method(p,nameof(Player.Proceed),[typeof(KnifeComponent),typeof(Callback<IKnifeController>),typeof(bool)]),
   prefix:new HarmonyMethod(typeof(RaidItemPoolGuard),nameof(BeforeProceedKnife)));
  plugin.Patches.Patch(
   AccessTools.Method(p,nameof(Player.Proceed),[typeof(KnifeComponent),typeof(Callback<IQuickKnifeKickController>),typeof(bool)]),
   prefix:new HarmonyMethod(typeof(RaidItemPoolGuard),nameof(BeforeProceedQuickKnife)));
  plugin.Patches.Patch(
   AccessTools.Method(p,nameof(Player.Proceed),[typeof(Meds),typeof(OneAndList<EBodyPart>),typeof(Callback<IMedsController>),typeof(int),typeof(bool)]),
   prefix:new HarmonyMethod(typeof(RaidItemPoolGuard),nameof(BeforeProceedMeds)));
  plugin.Patches.Patch(
   AccessTools.Method(p,nameof(Player.Proceed),[typeof(FoodDrink),typeof(float),typeof(Callback<IMedsController>),typeof(int),typeof(bool)]),
   prefix:new HarmonyMethod(typeof(RaidItemPoolGuard),nameof(BeforeProceedFood)));
  plugin.Patches.Patch(
   AccessTools.Method(p,nameof(Player.Proceed),[typeof(RecorderItem),typeof(Callback<IRecorderController>),typeof(bool)]),
   prefix:new HarmonyMethod(typeof(RaidItemPoolGuard),nameof(BeforeProceedRecorder)));

  plugin.Patches.Patch(
   AccessTools.Method(typeof(ItemView),nameof(ItemView.OnPointerEnter),[typeof(PointerEventData)]),
   postfix:new HarmonyMethod(typeof(RaidItemPoolGuard),nameof(AfterItemPointerEnter)));
  plugin.Patches.Patch(
   AccessTools.Method(typeof(ItemView),nameof(ItemView.OnBeginDrag),[typeof(PointerEventData)]),
   postfix:new HarmonyMethod(typeof(RaidItemPoolGuard),nameof(AfterItemBeginDrag)));
 }

 static Diz.Jobs.YieldDelegate NativeYield(){
  var value=Diz.Jobs.JobYieldPriority.General;
  if(Diz.Jobs.JobYieldPriority.Priority(value)!=Diz.Jobs.EJobPriority.General)
   throw new InvalidOperationException("Native item loading priority mismatch.");
  return value;
 }

 static bool InActiveRaid(){
  if(!RaidClock.Active)return false;
  var game=Singleton<AbstractGame>.Instance;
  return game!=null&&game.InRaid&&game.TryCast<HideoutGame>()==null&&Singleton<GameWorld>.Instance!=null;
 }

 static void AfterItemPointerEnter(ItemView __instance){
  try{
   if(!draining&&RaidClock.Active&&__instance?.Item is {} item)
    pendingHoverItem=item;
  }catch{}
 }

 static void AfterItemBeginDrag(ItemView __instance){
  try{
   if(!draining&&RaidClock.Active&&__instance?.Item is {} item)
    Warm(item);
  }catch{}
 }

 internal static void Warm(Item? item){
  if(draining||item==null||!InActiveRaid())return;
  if(warmQueue.Count<64&&queuedItemPointers.Add(item.Pointer))
   warmQueue.Enqueue(item);
 }

 internal static void RequestImmediateScan(){
  if(!draining)immediateScanRequested=true;
 }

 static bool BeforeProceedWeapon(Player __instance,Weapon __0,Callback<IFirearmHandsController> __1,bool __2)=>
  GateProceed(__instance,__0,__1,()=>__instance.Proceed(__0,__1,__2));

 static bool BeforeProceedGrenade(Player __instance,ThrowWeap __0,Callback<IGrenadeController> __1,bool __2)=>
  GateProceed(__instance,__0,__1,()=>__instance.Proceed(__0,__1,__2));

 static bool BeforeProceedQuickGrenade(Player __instance,ThrowWeap __0,Callback<IQuickGrenadeThrowController> __1,bool __2)=>
  GateProceed(__instance,__0,__1,()=>__instance.Proceed(__0,__1,__2));

 static bool BeforeProceedKnife(Player __instance,KnifeComponent __0,Callback<IKnifeController> __1,bool __2)=>
  GateProceed(__instance,__0?.Item,__1,()=>__instance.Proceed(__0!,__1,__2));

 static bool BeforeProceedQuickKnife(Player __instance,KnifeComponent __0,Callback<IQuickKnifeKickController> __1,bool __2)=>
  GateProceed(__instance,__0?.Item,__1,()=>__instance.Proceed(__0!,__1,__2));

 static bool BeforeProceedMeds(Player __instance,Meds __0,OneAndList<EBodyPart> __1,Callback<IMedsController> __2,int __3,bool __4)=>
  GateProceed(__instance,__0,__2,()=>__instance.Proceed(__0,__1,__2,__3,__4));

 static bool BeforeProceedFood(Player __instance,FoodDrink __0,float __1,Callback<IMedsController> __2,int __3,bool __4)=>
  GateProceed(__instance,__0,__2,()=>__instance.Proceed(__0,__1,__2,__3,__4));

 static bool BeforeProceedRecorder(Player __instance,RecorderItem __0,Callback<IRecorderController> __1,bool __2)=>
  GateProceed(__instance,__0,__1,()=>__instance.Proceed(__0,__1,__2));

 static bool BeforeTryProceed(Player __instance,Item __0,Callback<IHandsController> __1,bool __2)=>
  GateProceed(__instance,__0,__1,()=>__instance.TryProceed(__0,__1,__2));

 static bool GateProceed<TController>(Player? actor,Item? item,Callback<TController>? callback,Action replay){
  if(draining||actor==null||item==null||actor.Destroyed||actor.HealthController?.IsAlive!=true||!InActiveRaid())
   return true;
  var key=(actor.Pointer,item.Pointer);
  if(replaying.Contains(key))return true;
  try{
   var factory=Singleton<ObjectsFactory>.Instance;
   if(factory==null)return true;
   var missing=CollectMissingResources(factory,item,out bool blockedByFailedPath);
   if(missing.Count==0&&!blockedByFailedPath)return true;

   void FailCallback(string reason){
    ReleaseInventoryLock(actor);
    try{callback?.Invoke(new Result<TController>(default!,reason));}catch{}
   }

   if(blockedByFailedPath){
    FailCallback("武器或配件模型资源加载失败。");
    if(actor.IsYourPlayer)FeatureToasts.Show("武器或配件模型资源加载失败。");
    return false;
   }

   var world=Singleton<GameWorld>.Instance!;
   if(pendingProceeds.TryGetValue(actor.Pointer,out var prev)){
    if(prev.ItemPointer==item.Pointer){
     prev.Replay=replay;
     prev.Fail=FailCallback;
     return false;
    }
    pendingProceeds.Remove(actor.Pointer);
    prev.Fail("已切换手持物品。");
   }

   pendingProceeds[actor.Pointer]=new DeferredProceed{
    Actor=actor,
    Item=item,
    WorldPointer=world.Pointer,
    ActorPointer=actor.Pointer,
    ItemPointer=item.Pointer,
    ItemId=item.Id.ToString(),
    Replay=replay,
    Fail=FailCallback,
    StartedAt=Time.unscaledTime
   };
   deferredCount++;
   Plugin.Self.Log.LogInfo($"Deferring Proceed until Raid pools load: actor={actor.ProfileId}, local={actor.IsYourPlayer}, template={item.StringTemplateId}, missingResources={missing.Count}");
   TryStartNextLoad(factory,world.Pointer);
   return false;
  }catch(Exception error){
   loadErrors++;
   Plugin.Self.Log.LogWarning("RaidItemPoolGuard admission check: "+error);
   return true;
  }
 }

 static List<ResourceKey> CollectMissingResources(ObjectsFactory factory,Item root,out bool hasFailedPath){
  bool failedFlag=false;
  var result=new List<ResourceKey>();
  var raidPools=factory.GetPools(ObjectsFactory.PoolsCategory.Raid);
  var poolsDict=raidPools?.PoolsDictionary;
  if(raidPools==null||poolsDict==null){hasFailedPath=false;return result;}
  var loadedBundles=factory._loadedBundles;
  var templates=ObjectsFactory.CurrentItemTemplates;
  float now=Time.unscaledTime;
  var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);

  void InspectKey(ResourceKey? rk){
   if(rk==null||rk.IsEmpty())return;
   string path=rk.path;
   if(string.IsNullOrEmpty(path)||!seen.Add(path))return;
   if(poolsDict.TryGetValue(rk,out var poolData)&&poolData!=null){
    var task=poolData.Source?.Task;
    if(task!=null&&task.Status==NativeTaskStatus.RanToCompletion)return;
    if(task!=null&&(task.IsFaulted||task.IsCanceled)){failedFlag=true;return;}
    result.Add(rk);
    return;
   }
   if(verifiedNoPoolPaths.Contains(path))return;
   if(templates!=null&&loadedBundles!=null&&loadedBundles.TryGetValue(rk,out var tokens)&&tokens!=null&&tokens.Count>0){
    try{
     var info=templates.GetResourceTypeByBundlePath(path);
     if((int)info.ResourceType==0x200){verifiedNoPoolPaths.Add(path);return;}
    }catch{}
   }
   if(failedPathsUntil.TryGetValue(path,out float retryAt)){
    if(now<retryAt){failedFlag=true;return;}
    failedPathsUntil.Remove(path);
   }
   result.Add(rk);
  }

  foreach(var child in root.GetAllItems()){
   if(child?.Template==null)continue;
   var all=child.Template.AllResources;
   if(all!=null)foreach(var rk in all)InspectKey(rk);
   InspectKey(child.Template.Prefab);
   InspectKey(child.Template.UsePrefab);
  }
  hasFailedPath=failedFlag;
  return result;
 }

 static bool OwnedByActor(Player actor,Item item,string expectedId){
  var equip=actor.Profile?.Inventory?.Equipment;
  if(equip==null)return false;
  foreach(var child in equip.GetAllItems())
   if(child!=null&&child.Pointer==item.Pointer&&child.Id.ToString()==expectedId)
    return true;
  return false;
 }

 static void ReleaseInventoryLock(Player actor){
  try{
   if(actor._inventoryController?._nextProcessLocked==true)
    actor._inventoryController.SetNextProcessLocked(false);
  }catch{}
 }

 static void RefreshMissingSlotViews(Player? local){
  if(local==null||local.Destroyed||local.HealthController?.IsAlive!=true)return;
  var factory=Singleton<ObjectsFactory>.Instance;
  var views=local.PlayerBody?.SlotViews;
  if(factory==null||views==null)return;
  IntPtr heldPtr=local.HandsController?.Item?.Pointer??IntPtr.Zero;
  for(int i=0;i<views.Count;i++){
   var sv=views.GetByIndex(i);
   if(sv==null)continue;
   var contained=sv._slot?.ContainedItem;
   if(contained==null||contained.Pointer==heldPtr)continue;
   if(sv.Model!=null)continue;
   if(sv.LoadingJob!=null&&!sv.LoadingJob.IsCompleted)continue;
   if(CollectMissingResources(factory,contained,out bool failed).Count==0&&!failed){
    try{sv.LoadItem();slotViewsRefreshed++;}catch{}
   }
  }
 }

 static void TryStartNextLoad(ObjectsFactory factory,IntPtr worldPtr){
  if(draining||activeLoadTask!=null)return;
  var keys=new Il2CppSystem.Collections.Generic.List<ResourceKey>();
  var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);

  void AppendMissing(Item candidate){
   var missing=CollectMissingResources(factory,candidate,out _);
   for(int i=0;i<missing.Count;i++){
    var rk=missing[i];
    if(seen.Add(rk.path))keys.Add(rk);
   }
  }

  // 1. Highest priority: local player's deferred Proceed, then any AI deferred Proceed
  foreach(var job in pendingProceeds.Values.OrderByDescending(j=>j.Actor?.IsYourPlayer==true)){
   if(job.Actor!=null&&!job.Actor.Destroyed)AppendMissing(job.Item);
  }

  // 2. Explicit warm queue (dragged items, newly added/equipped items)
  if(keys.Count==0){
   while(warmQueue.Count>0&&keys.Count<48){
    var next=warmQueue.Dequeue();
    queuedItemPointers.Remove(next.Pointer);
    AppendMissing(next);
   }
  }

  // 3. Latest hovered item in inventory/RaidStash
  if(keys.Count==0&&pendingHoverItem is {} hovered){
   pendingHoverItem=null;
   AppendMissing(hovered);
  }

  if(keys.Count==0)return;

  activeBatchPaths.Clear();
  foreach(var rk in keys)activeBatchPaths.Add(rk.path);
  loadWorldPtr=worldPtr;
  activeLoadTask=factory.LoadBundlesAndCreatePools(
   ObjectsFactory.PoolsCategory.Raid,
   ObjectsFactory.AssemblyType.Local,
   keys.Cast<Il2CppSystem.Collections.Generic.ICollection<ResourceKey>>(),
   NativeYield(),
   null,
   factory.PoolsCancellationToken);
  loadsStarted++;
  Plugin.Self.Log.LogInfo($"RaidItemPoolGuard loading {keys.Count} missing bundle/pool resource(s)");
 }

 static void CompleteActiveLoad(ObjectsFactory? factory,IntPtr worldPtr,Player? local){
  if(activeLoadTask==null||!activeLoadTask.IsCompleted)return;
  var finished=activeLoadTask;
  activeLoadTask=null;
  if(loadWorldPtr==worldPtr){
   if(finished.IsFaulted||finished.IsCanceled){
    loadErrors++;
    float retryAt=Time.unscaledTime+10f;
    foreach(string path in activeBatchPaths)failedPathsUntil[path]=retryAt;
    Plugin.Self.Log.LogWarning("RaidItemPoolGuard bundle load failed: "+finished.Exception);
   }else{
    loadsCompleted++;
    var poolsDict=factory?.GetPools(ObjectsFactory.PoolsCategory.Raid)?.PoolsDictionary;
    if(poolsDict!=null){
     var pooledPaths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
     foreach(var kv in poolsDict)if(kv.Key!=null&&!string.IsNullOrEmpty(kv.Key.path))pooledPaths.Add(kv.Key.path);
     foreach(string path in activeBatchPaths)if(!pooledPaths.Contains(path))verifiedNoPoolPaths.Add(path);
    }
    RefreshMissingSlotViews(local);
   }
  }
  activeBatchPaths.Clear();
 }

 static void ProcessDeferredProceeds(ObjectsFactory factory,IntPtr worldPtr){
  if(pendingProceeds.Count==0)return;
  float now=Time.unscaledTime;
  foreach(var pair in pendingProceeds.ToArray()){
   var job=pair.Value;
   var actor=job.Actor;
   bool actorValid=actor!=null&&!actor.Destroyed&&actor.HealthController?.IsAlive==true;
   bool owned=actorValid&&OwnedByActor(actor!,job.Item,job.ItemId);
   if(!actorValid||!owned||worldPtr!=job.WorldPointer||now-job.StartedAt>25f){
    pendingProceeds.Remove(pair.Key);
    job.Fail(now-job.StartedAt>25f?"武器模型资源加载超时。":"角色或武器状态已改变。");
    continue;
   }
   var missing=CollectMissingResources(factory,job.Item,out bool failed);
   if(failed){
    pendingProceeds.Remove(pair.Key);
    job.Fail("武器或配件模型资源加载失败。");
    if(actor!.IsYourPlayer)FeatureToasts.Show("武器或配件模型资源加载失败。");
    continue;
   }
   if(missing.Count>0)continue;
   if(actor!.IsYourPlayer&&recoveringHands)continue;
   bool handsHealthy=!(actor.HandsController==null&&actor.ProcessStatus==Player.EProcessStatus.Scheduled);
   bool alreadyHeld=actor.HandsController?.Item?.Pointer==job.ItemPointer;
   if(alreadyHeld){
    pendingProceeds.Remove(pair.Key);
    continue;
   }
   if(!RaidItemPoolRules.CanReplayProceed(worldPtr.ToInt64(),job.WorldPointer.ToInt64(),actor.Pointer.ToInt64(),job.ActorPointer.ToInt64(),true,true,false,handsHealthy))
    continue;

   pendingProceeds.Remove(pair.Key);
   var token=(actor.Pointer,job.ItemPointer);
   replaying.Add(token);
   try{
    job.Replay();
    replayedCount++;
    Plugin.Self.Log.LogInfo($"Replayed deferred Proceed after Raid pool load: actor={actor.ProfileId}, template={job.Item.StringTemplateId}");
   }catch(Exception error){
    loadErrors++;
    Plugin.Self.Log.LogWarning("Deferred Proceed replay failed: "+error);
    job.Fail("切换武器失败："+error.Message);
   }finally{
    replaying.Remove(token);
   }
  }
 }

 static void CheckOrphanedLocalHands(Player local){
  if(recoveringHands&&Time.unscaledTime-recoveryStartedAt>12f){
   recoveringHands=false;
   Plugin.Self.Log.LogWarning("RaidItemPoolGuard orphaned hands recovery timed out");
  }
  bool candidate=local.HandsController==null&&local.ScheduledProcess==null&&local.ProcessStatus==Player.EProcessStatus.Scheduled&&!recoveringHands&&!pendingProceeds.ContainsKey(local.Pointer);
  if(!candidate){orphanSince=-1f;return;}
  if(orphanSince<0f)orphanSince=Time.unscaledTime;
  if(!RaidItemPoolRules.ShouldRecoverOrphanedHands(local.IsYourPlayer,local.HealthController?.IsAlive==true,false,false,true,activeLoadTask!=null,Time.unscaledTime-orphanSince))
   return;

  recoveringHands=true;
  recoveryStartedAt=Time.unscaledTime;
  orphanSince=-1f;
  orphanRecoveries++;
  Plugin.Self.Log.LogWarning("RaidItemPoolGuard recovering orphaned local hands (HandsController==null, Scheduled)");
  local._processStatus=Player.EProcessStatus.None;
  ReleaseInventoryLock(local);
  try{
   local.SetEmptyHands((Action<Result<IEmptyHandsController>>)(emptyRes=>{
    if(draining||local.Destroyed||local.HealthController?.IsAlive!=true){recoveringHands=false;return;}
    if(emptyRes.Succeed){
     local.SetFirstAvailableItem((Action<Result<IHandsController>>)(_=>{recoveringHands=false;}));
    }else{
     recoveringHands=false;
    }
   }));
  }catch(Exception error){
   recoveringHands=false;
   Plugin.Self.Log.LogWarning("Orphaned hands recovery failed: "+error);
  }
 }

 internal static void Tick(){
  if(draining)return;
  try{
   var world=Singleton<GameWorld>.Instance;
   var local=world?.MainPlayer;
   IntPtr worldPtr=InActiveRaid()?(world?.Pointer??IntPtr.Zero):IntPtr.Zero;
   IntPtr playerPtr=local?.Pointer??IntPtr.Zero;

   if(worldPtr!=currentWorldPtr||playerPtr!=localPlayerPtr){
    FailAllPending("战局或角色已改变。");
    currentWorldPtr=worldPtr;
    localPlayerPtr=playerPtr;
    warmQueue.Clear();
    queuedItemPointers.Clear();
    verifiedNoPoolPaths.Clear();
    failedPathsUntil.Clear();
    pendingHoverItem=null;
    orphanSince=-1f;
    recoveringHands=false;
    nextEquipmentScan=0f;
   }

   var factory=Singleton<ObjectsFactory>.Instance;
   CompleteActiveLoad(factory,worldPtr,local);
   if(worldPtr==IntPtr.Zero||factory==null||local==null)return;

   if(local.IsYourPlayer&&local.HealthController?.IsAlive==true)
    CheckOrphanedLocalHands(local);

   ProcessDeferredProceeds(factory,worldPtr);

   bool inventoryOpen=CommonUI.Instance?.InventoryScreen?.gameObject.activeInHierarchy==true;
   float scanInterval=inventoryOpen?0.12f:0.4f;
   if(immediateScanRequested||Time.unscaledTime>=nextEquipmentScan){
    immediateScanRequested=false;
    nextEquipmentScan=Time.unscaledTime+scanInterval;
    var equip=local.Profile?.Inventory?.Equipment;
    if(equip!=null){
     foreach(var item in equip.GetAllItems()){
      if(item==null||queuedItemPointers.Contains(item.Pointer))continue;
      if(CollectMissingResources(factory,item,out bool failed).Count>0&&!failed)
       Warm(item);
     }
    }
    RefreshMissingSlotViews(local);
   }

   TryStartNextLoad(factory,worldPtr);
  }catch(Exception error){
   loadErrors++;
   Plugin.Self.Log.LogWarning("RaidItemPoolGuard tick: "+error);
  }
 }

 static void FailAllPending(string reason){
  foreach(var job in pendingProceeds.Values.ToArray())
   try{job.Fail(reason);}catch{}
  pendingProceeds.Clear();
  replaying.Clear();
 }

 internal static bool PrepareForReload(){
  draining=true;
  warmQueue.Clear();
  queuedItemPointers.Clear();
  pendingHoverItem=null;
  return pendingProceeds.Count==0&&(activeLoadTask==null||activeLoadTask.IsCompleted)&&!recoveringHands;
 }

 internal static void Reset(){
  FailAllPending("辅助模块正在重载。");
  warmQueue.Clear();
  queuedItemPointers.Clear();
  verifiedNoPoolPaths.Clear();
  failedPathsUntil.Clear();
  activeBatchPaths.Clear();
  pendingHoverItem=null;
  activeLoadTask=null;
  currentWorldPtr=loadWorldPtr=localPlayerPtr=IntPtr.Zero;
  draining=recoveringHands=immediateScanRequested=false;
  orphanSince=-1f;
  nextEquipmentScan=0f;
 }

 internal static object Diagnostics()=>new{
  loadsStarted,
  loadsCompleted,
  deferredCount,
  replayedCount,
  slotViewsRefreshed,
  orphanRecoveries,
  loadErrors,
  pendingProceeds=pendingProceeds.Count,
  warmQueued=warmQueue.Count,
  loading=activeLoadTask?.Status.ToString(),
  recoveringHands
 };
}
