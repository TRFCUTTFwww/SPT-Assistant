using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using NativeTask=Il2CppSystem.Threading.Tasks.Task;
using NativeTaskStatus=Il2CppSystem.Threading.Tasks.TaskStatus;

namespace LocalSpt5Esp;

/// <summary>
/// Maintains raid-scoped pre-warmed donor <see cref="Profile"/> seeds whose 3D AssetBundles and
/// <see cref="ObjectsFactory"/> Raid ObjectPools are 100% resident in memory, enabling sub-100ms
/// zero-HTTP, zero-disk-I/O <see cref="ProfileDescriptor"/> deep-clone spawning while silently
/// replenishing varied loadouts in the background.
/// </summary>
internal static class AiWarmProfilePool {
 const int MaxSeedsPerRole=4;
 const int DefaultWarmTarget=2;

 sealed class WarmSeed {
  internal Profile Profile=null!;
  internal int UseCount;
 }

 sealed record WarmRequest(AiSpawnOption Option,int Difficulty,int TargetCount);
 sealed record RetiredWarmTask(NativeTask Task,Il2CppSystem.Threading.CancellationTokenSource? Cancellation,BotCreationData? Data);

 static readonly Dictionary<string,List<WarmSeed>> seedsByRole=new(StringComparer.Ordinal);
 static readonly Dictionary<string,int> roleCursors=new(StringComparer.Ordinal);
 static readonly HashSet<string> seenBackupProfileIds=new(StringComparer.Ordinal);
 static readonly HashSet<IntPtr> seenLiveActorPointers=new();
 static readonly HashSet<string> verifiedNoPoolPaths=new(StringComparer.OrdinalIgnoreCase);
 static readonly LinkedList<WarmRequest> warmQueue=new();
 static readonly List<RetiredWarmTask> retiredTasks=new();

 static IntPtr currentWorldPtr;
 static string lastHintRole="";
 static int lastHintDifficulty=-1;
 static bool startupRolesQueued;
 static float raidReadyAt=-1f,nextHarvestAt,nextWarmAttemptAt;
 static int accountCounter=100000,variationCounter;

 static WarmRequest? activeWarm;
 static BotCreationData? warmData;
 static Il2CppSystem.Threading.CancellationTokenSource? warmCts;
 static Il2CppSystem.Threading.Tasks.Task<Profile>? warmGenerating;
 static Profile? warmPendingProfile;
 static NativeTask? warmLoadingPools;
 static float warmStartedAt;

 internal static Diz.Jobs.YieldDelegate GeneralYield(){
  var value=Diz.Jobs.JobYieldPriority.General;
  if(Diz.Jobs.JobYieldPriority.Priority(value)!=Diz.Jobs.EJobPriority.General)
   throw new InvalidOperationException("Native warm loading priority mismatch.");
  return value;
 }

 internal static void Reset(){
  CancelActiveWarm();
  CollectRetired();
  seedsByRole.Clear();
  roleCursors.Clear();
  seenBackupProfileIds.Clear();
  seenLiveActorPointers.Clear();
  warmQueue.Clear();
  currentWorldPtr=IntPtr.Zero;
  lastHintRole="";
  lastHintDifficulty=-1;
  startupRolesQueued=false;
  raidReadyAt=-1f;
  nextHarvestAt=0f;
  nextWarmAttemptAt=0f;
 }

 static void CancelActiveWarm(){
  try{warmData?.StopSpawn();warmCts?.Cancel();}catch{}
  RaidSpawnControl.ForgetManual(warmData);
  NativeTask? running=warmLoadingPools??(NativeTask?)warmGenerating;
  if(running!=null&&!running.IsCompleted)
   retiredTasks.Add(new(running,warmCts,warmData));
  else
   try{warmCts?.Dispose();}catch{}
  activeWarm=null;
  warmData=null;
  warmCts=null;
  warmGenerating=null;
  warmPendingProfile=null;
  warmLoadingPools=null;
 }

 static void CollectRetired(){
  for(int i=retiredTasks.Count-1;i>=0;i--){
   var entry=retiredTasks[i];
   if(!entry.Task.IsCompleted)continue;
   try{entry.Data?.StopSpawn();entry.Cancellation?.Dispose();}catch{}
   retiredTasks.RemoveAt(i);
  }
 }

 internal static void HintSelection(AiSpawnOption? option,int difficulty){
  if(option==null||!option.Enabled||!RaidClock.Active||!AiSpawnService.GameStarted)return;
  int clamped=Math.Clamp(difficulty,0,3);
  if(lastHintRole==option.Role&&lastHintDifficulty==clamped)return;
  lastHintRole=option.Role;
  lastHintDifficulty=clamped;
  EnqueueWarm(option,clamped,DefaultWarmTarget,front:true);
 }

 internal static void PreloadNow(AiSpawnOption option,int difficulty,int count){
  if(!option.Enabled||!RaidClock.Active||!AiSpawnService.GameStarted)return;
  EnqueueWarm(option,Math.Clamp(difficulty,0,3),Math.Clamp(count,1,MaxSeedsPerRole),front:true);
 }

 internal static void RequestReplenish(AiSpawnOption option,int difficulty){
  if(!option.Enabled||!RaidClock.Active||!AiSpawnService.GameStarted)return;
  EnqueueWarm(option,Math.Clamp(difficulty,0,3),MaxSeedsPerRole,front:false);
 }

 static void EnqueueWarm(AiSpawnOption option,int difficulty,int targetCount,bool front){
  if(!Enum.TryParse<WildSpawnType>(option.Role,out _))return;
  int have=ReadySeedCount(option.Role);
  if(have>=targetCount)return;
  if(activeWarm?.Option.Role==option.Role)return;
  for(var node=warmQueue.First;node!=null;node=node.Next){
   if(node.Value.Option.Role!=option.Role)continue;
   int mergedTarget=Math.Max(node.Value.TargetCount,targetCount);
   if(front&&node!=warmQueue.First){
    warmQueue.Remove(node);
    warmQueue.AddFirst(new WarmRequest(option,difficulty,mergedTarget));
   }else{
    node.Value=new WarmRequest(option,difficulty,mergedTarget);
   }
   return;
  }
  var req=new WarmRequest(option,difficulty,targetCount);
  if(front)warmQueue.AddFirst(req);
  else warmQueue.AddLast(req);
 }

 static int ReadySeedCount(string role){
  var factory=Singleton<ObjectsFactory>.Instance;
  if(factory==null||!seedsByRole.TryGetValue(role,out var list)||list.Count==0)return 0;
  int ready=0;
  for(int i=0;i<list.Count;i++)
   if(list[i].Profile!=null&&AreAllProfileResourcesPooled(factory,list[i].Profile))
    ready++;
  return ready;
 }

 internal static void Tick(GameWorld? current,Player? local){
  CollectRetired();
  if(current==null||local?.HealthController?.IsAlive!=true||!RaidClock.Active){
   if(currentWorldPtr!=IntPtr.Zero)Reset();
   return;
  }
  if(currentWorldPtr!=current.Pointer){
   Reset();
   currentWorldPtr=current.Pointer;
  }
  if(!AiSpawnService.GameStarted)return;

  float now=Time.unscaledTime;
  if(raidReadyAt<0f)raidReadyAt=now;

  var spawner=Singleton<AbstractGame>.Instance?.TryCast<IBotGame>()?.BotsController?.GetSpawner();
  if(spawner?._botCreator==null)return;

  if(now>=nextHarvestAt){
   nextHarvestAt=now+0.75f;
   HarvestRaidDonors(current,spawner,targetRoleFilter:null);
  }

  if(!startupRolesQueued&&now-raidReadyAt>=1.5f&&spawner._botCreator.StartProfilesLoaded){
   startupRolesQueued=true;
   foreach(string defaultRole in new[]{"assault","pmcUSEC","pmcBEAR"}){
    var opt=AiSpawnCatalog.Options.FirstOrDefault(o=>o.Enabled&&o.Role==defaultRole);
    if(opt!=null)EnqueueWarm(opt,1,1,front:false);
   }
  }

  TickBackgroundWarm(spawner,now);
 }

 static void TickBackgroundWarm(BotSpawner spawner,float now){
  try{
   if(warmGenerating!=null){
    if(warmGenerating.IsFaulted||warmGenerating.IsCanceled){
     CancelActiveWarm();
     nextWarmAttemptAt=now+1.5f;
     return;
    }
    if(!warmGenerating.IsCompleted){
     if(now-warmStartedAt>90f)CancelActiveWarm();
     return;
    }
    var generated=warmGenerating.Result;
    warmGenerating=null;
    if(generated==null||activeWarm==null){
     CancelActiveWarm();
     return;
    }
    string actualRole=generated.Info?.Settings?.Role.ToString()??"";
    string actualSide=generated.Side.ToString();
    if(!AiFastSpawnRules.AcceptsDonorRole(activeWarm.Option.Role,activeWarm.Option.Side,actualRole,actualSide)){
     CancelActiveWarm();
     return;
    }
    warmPendingProfile=generated;
    var missing=CollectMissingProfileResources(new[]{generated});
    var factory=Singleton<ObjectsFactory>.Instance;
    if(missing.Count>0&&factory!=null){
     warmCts??=new();
     warmLoadingPools=factory.LoadBundlesAndCreatePools(
      ObjectsFactory.PoolsCategory.Raid,ObjectsFactory.AssemblyType.Local,missing,GeneralYield(),null,warmCts.Token);
     warmStartedAt=now;
     return;
    }
    CompleteWarmSeed();
    return;
   }

   if(warmLoadingPools!=null){
    if(warmLoadingPools.IsFaulted||warmLoadingPools.IsCanceled){
     CancelActiveWarm();
     nextWarmAttemptAt=now+1.5f;
     return;
    }
    if(!warmLoadingPools.IsCompleted){
     if(now-warmStartedAt>90f)CancelActiveWarm();
     return;
    }
    warmLoadingPools=null;
    CompleteWarmSeed();
    return;
   }

   // Do not start a new background warm HTTP/bundle job while a foreground cold spawn is preparing
   if(AiSpawnService.PreparingCold||retiredTasks.Count>0||now<nextWarmAttemptAt||warmQueue.Count==0)
    return;
   if(!spawner._botCreator.StartProfilesLoaded)return;

   while(warmQueue.First is {} head){
    warmQueue.RemoveFirst();
    if(ReadySeedCount(head.Value.Option.Role)>=head.Value.TargetCount)continue;
    StartBackgroundWarm(spawner,head.Value,now);
    break;
   }
  }catch(Exception error){
   Plugin.Self.Log.LogWarning("AI warm profile pool: "+error.Message);
   CancelActiveWarm();
   nextWarmAttemptAt=now+3f;
  }
 }

 static void StartBackgroundWarm(BotSpawner spawner,WarmRequest req,float now){
  var loadingSide=Enum.Parse<EPlayerSide>(AiPmcProfileRules.LoadingSide(req.Option.Role,req.Option.Side));
  var roleEnum=Enum.Parse<WildSpawnType>(req.Option.Role);
  var parameters=new BotSpawnParams{
   TriggerType=SpawnTriggerType.none,
   ShallBeGroup=new ShallBeGroupParams(false,req.Option.Boss,1),
   Id_spawn="warm-"+Guid.NewGuid().ToString("N")
  };
  var profileParams=new GetProfileDataParams(loadingSide,roleEnum,(BotDifficulty)req.Difficulty,0,parameters,true);
  activeWarm=req;
  warmData=BotCreationData.CreateWithoutProfile(profileParams.Cast<IGetProfileData>());
  warmData._botCreator=spawner._botCreator;
  warmData._tokenGetter=spawner.Cast<ITokenGetter>();
  warmCts=new();
  warmStartedAt=now;
  warmGenerating=spawner._botCreator.GenerateProfile(warmData,warmCts.Token,true);
 }

 static void CompleteWarmSeed(){
  var req=activeWarm;
  var profile=warmPendingProfile;
  CancelActiveWarm();
  if(req==null||profile==null)return;
  StoreWarmSeed(req.Option.Role,profile,evictReused:true);
  if(ReadySeedCount(req.Option.Role)<req.TargetCount)
   warmQueue.AddLast(req);
 }

 internal static void StoreWarmSeed(string role,Profile? source,bool evictReused=false){
  if(string.IsNullOrEmpty(role)||source==null)return;
  var factory=Singleton<ObjectsFactory>.Instance;
  if(factory==null||!AreAllProfileResourcesPooled(factory,source))return;
  if(!Enum.TryParse<WildSpawnType>(role,out var roleEnum))return;
  if(!seedsByRole.TryGetValue(role,out var list)){
   list=new List<WarmSeed>(MaxSeedsPerRole);
   seedsByRole[role]=list;
  }
  if(list.Count>=MaxSeedsPerRole&&!evictReused)return;
  try{
   var side=source.Side;
   var diff=source.Info?.Settings?.BotDifficulty??BotDifficulty.normal;
   var detached=CloneProfile(source,side,roleEnum,diff,varyNickname:false,restoreFullHealth:true);
   if(list.Count>=MaxSeedsPerRole){
    int replaceIdx=0;
    for(int i=1;i<list.Count;i++)
     if(list[i].UseCount>list[replaceIdx].UseCount)
      replaceIdx=i;
    list[replaceIdx]=new WarmSeed{Profile=detached,UseCount=0};
   }else{
    list.Add(new WarmSeed{Profile=detached,UseCount=0});
   }
  }catch(Exception error){
   Plugin.Self.Log.LogWarning($"AI warm seed snapshot ({role}) skipped: {error.Message}");
  }
 }

 static void HarvestRaidDonors(GameWorld current,BotSpawner spawner,string? targetRoleFilter){
  var factory=Singleton<ObjectsFactory>.Instance;
  if(factory==null)return;

  // 1. Harvest ready profiles from native ABotProfileCreator._simpleProfiles backup cache
  var backupList=spawner._botCreator?.TryCast<ABotProfileCreator>()?._simpleProfiles;
  if(backupList!=null){
   for(int i=0;i<backupList.Count;i++){
    var p=backupList[i];
    if(p==null||string.IsNullOrEmpty(p.Id))continue;
    string role=p.Info?.Settings?.Role.ToString()??"";
    if(role.Length==0||(targetRoleFilter!=null&&role!=targetRoleFilter))continue;
    if(seedsByRole.TryGetValue(role,out var existing)&&existing.Count>=MaxSeedsPerRole)continue;
    if(!seenBackupProfileIds.Add(p.Id))continue;
    if(!HasUsableLoadout(p,role)||!AreAllProfileResourcesPooled(factory,p))continue;
    StoreWarmSeed(role,p,evictReused:false);
   }
  }

  // 2. Harvest newly spawned live map AI while their equipment is untouched
  var alive=current.AllAlivePlayersList;
  if(alive!=null){
   for(int i=0;i<alive.Count;i++){
    var actor=alive[i];
    if(actor==null||actor.IsYourPlayer||!(actor.IsAI||actor.AIData?.BotOwner!=null)||actor.HealthController?.IsAlive!=true)
     continue;
    var p=actor.Profile;
    string role=p?.Info?.Settings?.Role.ToString()??"";
    if(p==null||role.Length==0)continue;
    if(targetRoleFilter!=null&&role!=targetRoleFilter)continue;
    if(!seenLiveActorPointers.Add(actor.Pointer))continue;
    if(seedsByRole.TryGetValue(role,out var existing)&&existing.Count>=MaxSeedsPerRole)continue;
    if(!HasUsableLoadout(p,role)||!AreAllProfileResourcesPooled(factory,p))continue;
    StoreWarmSeed(role,p,evictReused:false);
   }
  }
 }

 static bool HasUsableLoadout(Profile profile,string role){
  var eq=profile.Inventory?.Equipment;
  if(eq==null)return false;
  if(AiFastSpawnRules.AllowsUnarmedDonor(role))return true;
  foreach(var slot in new[]{EquipmentSlot.FirstPrimaryWeapon,EquipmentSlot.SecondPrimaryWeapon,EquipmentSlot.Holster})
   if(eq.GetSlot(slot)?.ContainedItem?.TryCast<Weapon>()!=null)
    return true;
  return eq.GetSlot(EquipmentSlot.Scabbard)?.ContainedItem!=null&&
   (role.Contains("Tagilla",StringComparison.OrdinalIgnoreCase));
 }

 internal static bool TryCreateInstantBatch(
  GameWorld current,
  BotSpawner spawner,
  AiSpawnOption option,
  int difficulty,
  int wanted,
  out List<Profile> clones){
  clones=new List<Profile>(wanted);
  var factory=Singleton<ObjectsFactory>.Instance;
  if(factory==null||wanted<1||!Enum.TryParse<WildSpawnType>(option.Role,out var roleEnum)||!Enum.TryParse<EPlayerSide>(option.Side,out var sideEnum))
   return false;

  if(ReadySeedCount(option.Role)==0)
   HarvestRaidDonors(current,spawner,targetRoleFilter:option.Role);

  if(!seedsByRole.TryGetValue(option.Role,out var seeds)||seeds.Count==0)
   return false;

  var readySeeds=new List<WarmSeed>(seeds.Count);
  for(int i=0;i<seeds.Count;i++){
   var s=seeds[i];
   if(s.Profile!=null&&AreAllProfileResourcesPooled(factory,s.Profile))
    readySeeds.Add(s);
  }
  if(readySeeds.Count==0)return false;

  roleCursors.TryGetValue(option.Role,out int cursor);
  var diffEnum=(BotDifficulty)Math.Clamp(difficulty,0,3);
  try{
   for(int i=0;i<wanted;i++){
    int slot=AiFastSpawnRules.SelectSeedSlot(readySeeds.Count,cursor++);
    var chosen=readySeeds[slot];
    bool vary=chosen.UseCount>0;
    var cloned=CloneProfile(chosen.Profile,sideEnum,roleEnum,diffEnum,varyNickname:vary,restoreFullHealth:true);
    chosen.UseCount++;
    clones.Add(cloned);
   }
   roleCursors[option.Role]=cursor;
   return true;
  }catch(Exception error){
   Plugin.Self.Log.LogWarning($"Instant AI clone fallback to cold path ({option.Role}): {error.Message}");
   clones.Clear();
   return false;
  }
 }

 internal static Profile CloneProfile(
  Profile source,
  EPlayerSide targetSide,
  WildSpawnType targetRole,
  BotDifficulty targetDifficulty,
  bool varyNickname,
  bool restoreFullHealth){
  var itemFactory=Singleton<ItemFactory>.Instance??throw new InvalidOperationException("物品工厂未就绪。");
  var desc=new ProfileDescriptor(source,FullySearchedSearchController.Instance);
  desc.Id=MongoID.Generate(true);
  desc.AccountId=(9000000+(++accountCounter)).ToString(System.Globalization.CultureInfo.InvariantCulture);
  desc.PetId=new Il2CppSystem.Nullable<MongoID>();

  if(desc.Info!=null){
   desc.Info.Side=targetSide;
   desc.Info.GroupId=null;
   desc.Info.TeamId=null;
   if(desc.Info.Settings!=null){
    desc.Info.Settings.Role=targetRole;
    desc.Info.Settings.BotDifficulty=targetDifficulty;
   }
   if(varyNickname){
    desc.Info.Nickname=AiFastSpawnRules.GenerateVariedNickname(
     targetRole.ToString(),targetSide.ToString(),desc.Info.Nickname??"",++variationCounter);
   }
  }

  var idMap=new Dictionary<string,MongoID>(StringComparer.Ordinal);
  var seenPtrs=new HashSet<IntPtr>();
  void RemapTree(ItemDescriptor? node,int depth){
   if(node==null||depth>28||seenPtrs.Count>=8192||!seenPtrs.Add(node.Pointer))return;
   string oldId=node.Id.ToString();
   MongoID freshId=itemFactory.NextId;
   node.Id=freshId;
   if(!string.IsNullOrEmpty(oldId))idMap[oldId]=freshId;
   if(node.Slots!=null)
    foreach(var slot in node.Slots)
     if(slot?.ContainedItem!=null)RemapTree(slot.ContainedItem,depth+1);
   if(node.Grids!=null)
    foreach(var grid in node.Grids)
     if(grid?.ContainedItems!=null)
      foreach(var entry in grid.ContainedItems)
       if(entry?.Item!=null)RemapTree(entry.Item,depth+1);
   if(node.StackSlots!=null)
    foreach(var stack in node.StackSlots)
     if(stack?.ContainedItems!=null)
      foreach(var child in stack.ContainedItems)
       if(child!=null)RemapTree(child,depth+1);
  }

  var inv=desc.Inventory;
  if(inv!=null){
   var eq=inv.Equipment;
   var questRaid=inv.QuestRaidItems;
   var questStash=inv.QuestStashItems;
   var stash=inv.Stash;
   var sorting=inv.SortingTable;
   var hideoutCustom=inv.HideoutCustomizationStash;

   RemapTree(eq,0);
   RemapTree(questRaid,0);
   RemapTree(questStash,0);
   RemapTree(stash,0);
   RemapTree(sorting,0);
   RemapTree(hideoutCustom,0);
   if(inv.HideoutAreaStashes!=null)
    foreach(var kv in inv.HideoutAreaStashes)
     if(kv.Value!=null)RemapTree(kv.Value,0);

   inv._inventory=null;
   if(eq!=null)inv._equipmentId=eq.Id;
   if(stash!=null)inv._stashId=new Il2CppSystem.Nullable<MongoID>(stash.Id);
   if(questRaid!=null)inv._questRaidItemsId=new Il2CppSystem.Nullable<MongoID>(questRaid.Id);
   if(questStash!=null)inv._questStashItemsId=new Il2CppSystem.Nullable<MongoID>(questStash.Id);
   if(sorting!=null)inv._sortingTableId=new Il2CppSystem.Nullable<MongoID>(sorting.Id);
   if(hideoutCustom!=null)inv._hideoutCustomizationStashId=new Il2CppSystem.Nullable<MongoID>(hideoutCustom.Id);

   if(inv.FastAccess!=null){
    var fast=new Il2CppSystem.Collections.Generic.Dictionary<EBoundItem,MongoID>();
    foreach(var kv in inv.FastAccess)
     if(idMap.TryGetValue(kv.Value.ToString(),out var mapped))
      fast[kv.Key]=mapped;
    inv.FastAccess=fast;
   }
   if(inv.FavoriteItemsStorage!=null)
    inv.FavoriteItemsStorage=new Il2CppSystem.Collections.Generic.List<MongoID>();
  }

  if(desc.CheckedMagazines!=null){
   var mags=new Il2CppSystem.Collections.Generic.Dictionary<MongoID,int>();
   foreach(var kv in desc.CheckedMagazines)
    if(idMap.TryGetValue(kv.Key.ToString(),out var mapped))
     mags[mapped]=kv.Value;
   desc.CheckedMagazines=mags;
  }
  if(desc.CheckedChambers!=null){
   var chambers=new Il2CppSystem.Collections.Generic.List<MongoID>();
   foreach(var oldChamber in desc.CheckedChambers)
    if(idMap.TryGetValue(oldChamber.ToString(),out var mapped))
     chambers.Add(mapped);
   desc.CheckedChambers=chambers;
  }

  var cloned=new Profile(desc);
  if(restoreFullHealth&&cloned.Health?.BodyParts is {} parts){
   foreach(var kv in parts){
    if(kv.Value?.Health is {} h&&h.Maximum>0f)
     h.Current=h.Maximum;
    if(kv.Value!=null)
     kv.Value.Effects=new Il2CppSystem.Collections.Generic.Dictionary<string,Profile.HealthInfo.EffectInfo>();
   }
  }
  return cloned;
 }

 internal static bool AreAllProfileResourcesPooled(ObjectsFactory factory,Profile profile){
  var raidPools=factory.GetPools(ObjectsFactory.PoolsCategory.Raid);
  var poolsDict=raidPools?.PoolsDictionary;
  if(raidPools==null||poolsDict==null)return false;
  var loadedBundles=factory._loadedBundles;
  var templates=ObjectsFactory.CurrentItemTemplates;
  var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);

  bool IsReady(ResourceKey? rk){
   if(rk==null||rk.IsEmpty())return true;
   string path=rk.path;
   if(string.IsNullOrEmpty(path)||!seen.Add(path))return true;
   if(poolsDict.TryGetValue(rk,out var poolData)&&poolData!=null){
    var task=poolData.Source?.Task;
    return task!=null&&task.Status==NativeTaskStatus.RanToCompletion;
   }
   if(verifiedNoPoolPaths.Contains(path))return true;
   if(templates!=null&&loadedBundles!=null&&loadedBundles.TryGetValue(rk,out var tokens)&&tokens!=null&&tokens.Count>0){
    try{
     var info=templates.GetResourceTypeByBundlePath(path);
     if((int)info.ResourceType==0x200){verifiedNoPoolPaths.Add(path);return true;}
    }catch{}
   }
   return false;
  }

  var prefabs=profile.GetAllPrefabPaths(false);
  if(prefabs!=null){
   var enumerator=prefabs.GetEnumerator();
   try{
    while(enumerator.MoveNext())
     if(!IsReady(enumerator.Current))return false;
   }finally{
    try{enumerator.TryCast<Il2CppSystem.IDisposable>()?.Dispose();}catch{}
   }
  }

  var eq=profile.Inventory?.Equipment;
  if(eq!=null){
   foreach(var item in eq.GetAllItems()){
    if(item?.Template==null)continue;
    var all=item.Template.AllResources;
    if(all!=null)foreach(var rk in all)if(!IsReady(rk))return false;
    if(!IsReady(item.Template.Prefab)||!IsReady(item.Template.UsePrefab))return false;
   }
  }
  return seen.Count>0;
 }

 internal static Il2CppSystem.Collections.Generic.ICollection<ResourceKey> CollectMissingProfileResources(IEnumerable<Profile> profiles){
  var list=new Il2CppSystem.Collections.Generic.List<ResourceKey>();
  var factory=Singleton<ObjectsFactory>.Instance;
  var raidPools=factory?.GetPools(ObjectsFactory.PoolsCategory.Raid);
  var poolsDict=raidPools?.PoolsDictionary;
  if(factory==null||raidPools==null||poolsDict==null)return list.Cast<Il2CppSystem.Collections.Generic.ICollection<ResourceKey>>();
  var loadedBundles=factory._loadedBundles;
  var templates=ObjectsFactory.CurrentItemTemplates;
  var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);

  void Inspect(ResourceKey? rk){
   if(rk==null||rk.IsEmpty())return;
   string path=rk.path;
   if(string.IsNullOrEmpty(path)||!seen.Add(path))return;
   if(poolsDict.TryGetValue(rk,out var poolData)&&poolData!=null){
    var task=poolData.Source?.Task;
    if(task!=null&&task.Status==NativeTaskStatus.RanToCompletion)return;
    list.Add(rk);
    return;
   }
   if(verifiedNoPoolPaths.Contains(path))return;
   if(templates!=null&&loadedBundles!=null&&loadedBundles.TryGetValue(rk,out var tokens)&&tokens!=null&&tokens.Count>0){
    try{
     var info=templates.GetResourceTypeByBundlePath(path);
     if((int)info.ResourceType==0x200){verifiedNoPoolPaths.Add(path);return;}
    }catch{}
   }
   list.Add(rk);
  }

  foreach(var profile in profiles){
   if(profile==null)continue;
   var prefabs=profile.GetAllPrefabPaths(false);
   if(prefabs!=null){
    var enumerator=prefabs.GetEnumerator();
    try{
     while(enumerator.MoveNext())Inspect(enumerator.Current);
    }finally{
     try{enumerator.TryCast<Il2CppSystem.IDisposable>()?.Dispose();}catch{}
    }
   }
   var eq=profile.Inventory?.Equipment;
   if(eq!=null){
    foreach(var item in eq.GetAllItems()){
     if(item?.Template==null)continue;
     var all=item.Template.AllResources;
     if(all!=null)foreach(var rk in all)Inspect(rk);
     Inspect(item.Template.Prefab);
     Inspect(item.Template.UsePrefab);
    }
   }
  }
  return list.Cast<Il2CppSystem.Collections.Generic.ICollection<ResourceKey>>();
 }
}
