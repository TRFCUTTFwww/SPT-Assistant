using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.NetworkPackets;
using HarmonyLib;
using Local.ModHost;
using UnityEngine;
using NativeTask=Il2CppSystem.Threading.Tasks.Task;
namespace ItemUseGuard;

// The usable model is a separate bundle from the loot/inventory model. Native
// CreateItemUsablePrefabAsync does a synchronous PopOrCreate and never loads it.
// Load Raid pools BEFORE Proceed removes the old controller and acquires locks.
public sealed class ItemUseModule:IHotModule {
 sealed record Request(Player Player,Item Item,IntPtr World,IntPtr Hands,Action Run,Action<string> Fail,float Started);
 static ItemUseModule? active;
 ModuleContext? context;
 readonly HashSet<string> ready=[];
 readonly HashSet<string> failed=[];
 readonly List<Item> batch=[];
 NativeTask? loading;
 Request? request;
 IntPtr world,player,loadWorld;
 bool replaying,recovering;
 float nextScan,nextStatus,orphanSince=-1,recoverySince;
 DateTime observedCheck;
 DateTime observedRepair;
 int loads,resumed,rejected,recoveries,errors;
 string status="starting";
 object? recoveryBefore;
 public int ApiVersion=>1;
 public string Version=>"1.0.2";
 public bool CanReload=>request==null&&(loading==null||loading.IsCompleted)&&!recovering;
 public void Initialize(ModuleContext context){
  this.context=context;active=this;context.OnShutdown(Cleanup);
  string check=Path.Combine(context.DataDirectory,"check.request");if(File.Exists(check))observedCheck=File.GetLastWriteTimeUtc(check);
  string repair=Path.Combine(context.DataDirectory,"recover-inventory.request");if(File.Exists(repair))observedRepair=File.GetLastWriteTimeUtc(repair);
  var harmony=new Harmony(context.PatchId);
  harmony.Patch(AccessTools.Method(typeof(Player),nameof(Player.Proceed),[typeof(Meds),typeof(OneAndList<EBodyPart>),typeof(Callback<IMedsController>),typeof(int),typeof(bool)]),prefix:new HarmonyMethod(typeof(ItemUseModule),nameof(Medicine)));
  harmony.Patch(AccessTools.Method(typeof(Player),nameof(Player.Proceed),[typeof(FoodDrink),typeof(float),typeof(Callback<IMedsController>),typeof(int),typeof(bool)]),prefix:new HarmonyMethod(typeof(ItemUseModule),nameof(Food)));
  context.Log(LogLevel.Info,"Item use guard ready: preload native use-prefab Raid pools before medicine/food Proceed; bounded callback completion and orphaned hands recovery.");
  WriteStatus();
 }
 static bool Medicine(Player __instance,Meds __0,OneAndList<EBodyPart> __1,Callback<IMedsController> __2,int __3,bool __4)=>Gate(__instance,__0,__2,()=>__instance.Proceed(__0,__1,__2,__3,__4));
 static bool Food(Player __instance,FoodDrink __0,float __1,Callback<IMedsController> __2,int __3,bool __4)=>Gate(__instance,__0,__2,()=>__instance.Proceed(__0,__1,__2,__3,__4));
 static bool Gate(Player p,Item item,Callback<IMedsController> callback,Action run){
  var a=active;if(a?.context?.IsActive!=true||a.replaying||!Local(p))return true;
  try{
   if(a.ready.Contains(item.StringTemplateId))return true;
   void Fail(string error)=>callback?.Invoke(new Result<IMedsController>(null!,error));
   if(a.request!=null){a.rejected++;Fail("物品资源正在加载，请稍候。");return false;}
   if(a.failed.Contains(item.StringTemplateId)){a.rejected++;Fail("物品使用模型加载失败，请查看 ItemUseGuard 状态。");return false;}
   a.request=new(p,item,Singleton<GameWorld>.Instance.Pointer,p.HandsController?.Pointer??IntPtr.Zero,run,Fail,Time.unscaledTime);
   a.status="loading requested use model: "+item.StringTemplateId;
   return false;
  }catch(Exception e){a.errors++;a.context.Log(LogLevel.Warning,"Item use admission: "+e);callback?.Invoke(new Result<IMedsController>(null!,"物品资源检查失败。"));return false;}
 }
 static bool Local(Player? p)=>p!=null&&!p.Destroyed&&p.IsYourPlayer&&p.HealthController?.IsAlive==true&&Singleton<GameWorld>.Instance?.MainPlayer?.Pointer==p.Pointer;
 static bool Owned(Player p,Item item)=>p.Profile?.Inventory?.Equipment?.GetAllItems().Any(i=>i.Pointer==item.Pointer&&i.Id==item.Id)==true;
 static bool Consumable(Item item)=>item.TryCast<Meds>()!=null||item.TryCast<FoodDrink>()!=null;
 void FailRequest(string error){var old=request;request=null;if(old!=null){rejected++;try{old.Fail(error);}catch(Exception e){context?.Log(LogLevel.Warning,"Item use cancellation callback: "+e.Message);}}status=error;}
 void Load(IEnumerable<Item> items){
  var factory=Singleton<ObjectsFactory>.Instance??throw new InvalidOperationException("ObjectsFactory unavailable");
  batch.Clear();batch.AddRange(items);var keys=new Il2CppSystem.Collections.Generic.List<ResourceKey>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var item in batch){
   foreach(var key in item.Template.AllResources)if(key!=null&&!key.IsEmpty()&&seen.Add(key.path))keys.Add(key);
   // Explicitly include UsePrefab even if a modded template omits it from AllResources.
   var use=item.Template.UsePrefab;if(use!=null&&!use.IsEmpty()&&seen.Add(use.path))keys.Add(use);
  }
  if(keys.Count==0)throw new InvalidOperationException("No model resources in consumable template");
  var yield=Diz.Jobs.JobYieldPriority.General;
  if(Diz.Jobs.JobYieldPriority.Priority(yield)!=Diz.Jobs.EJobPriority.General)throw new InvalidOperationException("Invalid native loader priority");
  loadWorld=world;
  loading=factory.LoadBundlesAndCreatePools(ObjectsFactory.PoolsCategory.Raid,ObjectsFactory.AssemblyType.Local,keys.Cast<Il2CppSystem.Collections.Generic.ICollection<ResourceKey>>(),yield,null,factory.PoolsCancellationToken);
  loads++;context!.Log(LogLevel.Info,"Loading consumable models: "+string.Join(", ",batch.Select(i=>i.StringTemplateId))+"; resources="+keys.Count);
 }
 public void Tick(){
  if(context?.IsActive!=true)return;
  try{Update();}catch(Exception e){errors++;context.Log(LogLevel.Warning,"Item use guard: "+e);FailRequest("物品使用准备失败："+e.Message);if(loading?.IsCompleted!=false){foreach(var item in batch)failed.Add(item.StringTemplateId);batch.Clear();loading=null;}}
  if(Time.unscaledTime>=nextStatus){nextStatus=Time.unscaledTime+2;WriteStatus();}
 }
 void Update(){
  var current=Singleton<GameWorld>.Instance;var p=current?.MainPlayer;
  if(world!=(current?.Pointer??IntPtr.Zero)||player!=(p?.Pointer??IntPtr.Zero)){
   FailRequest("战局或角色已改变，已取消物品使用。");world=current?.Pointer??IntPtr.Zero;player=p?.Pointer??IntPtr.Zero;ready.Clear();failed.Clear();orphanSince=-1;nextScan=0;
  }
  if(loading!=null){
   if(!loading.IsCompleted){if(request!=null&&Time.unscaledTime-request.Started>=30)FailRequest("物品资源加载超时，已取消使用。");return;}
   if(loadWorld!=world){status="discarded completed load from previous world";}
   else if(loading.IsFaulted||loading.IsCanceled){foreach(var item in batch)failed.Add(item.StringTemplateId);status="native loader failed: "+loading.Exception;context!.Log(LogLevel.Warning,status);errors++;}
   else{foreach(var item in batch)ready.Add(item.StringTemplateId);status="consumable models ready";}
   loading=null;batch.Clear();
  }
  if(!Local(p)){FailRequest("角色当前不可使用物品。");return;}
  if(recovering&&Time.unscaledTime-recoverySince>15){recovering=false;errors++;status="native hands recovery callback timed out";context!.Log(LogLevel.Warning,status);}
  RecoverOrphan(p!);
  string repair=Path.Combine(context!.DataDirectory,"recover-inventory.request");
  if(File.Exists(repair)&&File.GetLastWriteTimeUtc(repair)!=observedRepair){
   observedRepair=File.GetLastWriteTimeUtc(repair);
   if(request==null&&p!.ProcessStatus==Player.EProcessStatus.None&&p.ScheduledProcess==null&&p._inventoryController?._loadProcess==null&&p._inventoryController?._checkMagazineProcess==null){
    recoveryBefore=Snapshot();ReleaseInventoryLock(p);status="released inventory lock left by failed use";WriteStatus();
   }else context.Log(LogLevel.Warning,"Inventory repair deferred by live native operation; request it again after operation completes.");
  }
  var job=request;
  if(job!=null){
   if(!UseRules.Resume(world.ToInt64(),job.World.ToInt64(),p!.Pointer.ToInt64(),job.Player.Pointer.ToInt64(),Local(p),Owned(p,job.Item),(p.HandsController?.Pointer??IntPtr.Zero)==job.Hands,p.ProcessStatus==Player.EProcessStatus.None&&!recovering)){
    FailRequest("角色或手持状态已改变，已取消等待的物品使用。");return;
   }
   if(failed.Contains(job.Item.StringTemplateId)){FailRequest("物品使用模型加载失败。");return;}
   if(!ready.Contains(job.Item.StringTemplateId)){Load([job.Item]);return;}
   request=null;try{replaying=true;job.Run();resumed++;status="native use resumed: "+job.Item.StringTemplateId;}catch{job.Fail("原生物品使用失败。");throw;}finally{replaying=false;}
  }
  if(Time.unscaledTime<nextScan||recovering)return;nextScan=Time.unscaledTime+1;
  var items=p!.Profile?.Inventory?.Equipment?.GetAllItems().Where(i=>Consumable(i)&&!ready.Contains(i.StringTemplateId)&&!failed.Contains(i.StringTemplateId)).GroupBy(i=>i.StringTemplateId).Select(g=>g.First()).Take(16).ToArray();
  if(items?.Length>0)Load(items);
  string check=Path.Combine(context!.DataDirectory,"check.request");
  if(loading==null&&request==null&&!recovering&&File.Exists(check)&&File.GetLastWriteTimeUtc(check)!=observedCheck){observedCheck=File.GetLastWriteTimeUtc(check);CheckModels(p);}
 }
 void RecoverOrphan(Player p){
  bool candidate=p.HandsController==null&&p.ScheduledProcess==null&&p.ProcessStatus==Player.EProcessStatus.Scheduled&&!recovering&&request==null;
  if(!candidate){orphanSince=-1;return;}if(orphanSince<0)orphanSince=Time.unscaledTime;
  if(!UseRules.Orphan(p.IsYourPlayer,p.HealthController?.IsAlive==true,false,false,true,loading!=null,Time.unscaledTime-orphanSince))return;
  recoveryBefore=Snapshot();recovering=true;recoverySince=Time.unscaledTime;recoveries++;orphanSince=-1;status="recovering missing hands after failed native creation";
  context!.Log(LogLevel.Warning,"Recovering orphaned local hands (Scheduled, no controller, no queued process for 5 seconds). No inventory items changed.");
  // No live controller exists to dispose. Restore the native empty controller,
  // then let the native inventory choose the currently owned weapon/knife.
  p._processStatus=Player.EProcessStatus.None;
  ReleaseInventoryLock(p);
  try{p.SetEmptyHands((Action<Result<IEmptyHandsController>>)(result=>{
   if(context?.IsActive!=true||!Local(p)){recovering=false;return;}
   status="empty hands recovery: "+result.Succeed;
   if(result.Succeed)p.SetFirstAvailableItem((Action<Result<IHandsController>>)(draw=>{recovering=false;if(context?.IsActive!=true||!Local(p))return;status="native hands restored: "+draw.Succeed;WriteStatus();}));
   else recovering=false;
   WriteStatus();
  }));}catch{recovering=false;throw;}
 }
 void ReleaseInventoryLock(Player p){
  // This flag is set by the inventory use caller before native Proceed. Native
  // controller-creation failure skips its completion callback, so restoring
  // hands alone cannot release the backpack/use lock. Never run for normal use.
  if(p._inventoryController?._nextProcessLocked==true){p._inventoryController.SetNextProcessLocked(false);context!.Log(LogLevel.Info,"Released orphaned inventory next-process lock.");}
 }
 void CheckModels(Player p){
  var before=Snapshot();var rows=new List<object>();var factory=Singleton<ObjectsFactory>.Instance;
  foreach(var item in p.Profile.Inventory.Equipment.GetAllItems().Where(Consumable).GroupBy(i=>i.StringTemplateId).Select(g=>g.First())){
   GameObject? model=null;try{
    model=factory.CreateItemUsablePrefab(item,p.Cast<IPlayer>());
    if(model==null||model.GetComponent<WeaponPrefab>()==null)throw new InvalidOperationException("Missing usable WeaponPrefab");
    rows.Add(new{template=item.StringTemplateId,use=item.Template.UsePrefab?.path,model=model.name,passed=true,error=""});
   }catch(Exception e){rows.Add(new{template=item.StringTemplateId,use=item.Template.UsePrefab?.path,model="",passed=false,error=e.Message});}
   finally{if(model!=null)EFT.AssetsManager.AssetPoolObject.ReturnToPool(model,true);}
  }
  File.WriteAllText(Path.Combine(context!.DataDirectory,"model-check.json"),System.Text.Json.JsonSerializer.Serialize(new{utc=DateTime.UtcNow,before,after=Snapshot(),rows},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
 }
 object Snapshot(){var p=Singleton<GameWorld>.Instance?.MainPlayer;return new{player=p!=null,alive=p?.HealthController?.IsAlive,process=p?.ProcessStatus.ToString(),scheduled=p?.ScheduledProcess!=null,hands=p?.HandsController?.GetIl2CppType().FullName,destroyed=p?.HandsController?.Destroyed,item=p?.HandsController?.Item?.StringTemplateId,inventoryNextProcessLocked=p?._inventoryController?._nextProcessLocked,inventoryLocked=p?._inventoryController?.Locked,inventoryItemInHands=p?.InventoryController?.ItemInHands?.StringTemplateId,setHandsCallback=p?._setInHandsCallback!=null,removeHandsCallback=p?._removeFromHandsCallback!=null};}
 void WriteStatus(){if(context==null)return;try{File.WriteAllText(Path.Combine(context.DataDirectory,"status.json"),System.Text.Json.JsonSerializer.Serialize(new{version=Version,utc=DateTime.UtcNow,processId=Environment.ProcessId,status,loads,resumed,rejected,recoveries,errors,ready=ready.ToArray(),failed=failed.ToArray(),loading=loading?.Status.ToString(),pending=request?.Item.StringTemplateId,state=Snapshot(),recoveryBefore},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){context.Log(LogLevel.Warning,"Item use status: "+e.Message);}}
 public void LateTick(){}
 public void Draw(){}
 public void Shutdown()=>Cleanup();
 void Cleanup(){FailRequest("物品使用保护正在重新加载。");if(active==this)active=null;ready.Clear();failed.Clear();batch.Clear();loading=null;context=null;}
}
