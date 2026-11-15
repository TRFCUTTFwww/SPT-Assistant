using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Local.ModHost;
using UnityEngine;
using System.Text.Json;
using RenderResult=IconCreatorBase<EFT.InventoryLogic.Item,ItemIcon>.RenderModelResult;
using SpriteFactory=IconCreatorBase<EFT.InventoryLogic.Item,ItemIcon>.SpriteFactory;
using RenderTask=Il2CppSystem.Threading.Tasks.Task<IconCreatorBase<EFT.InventoryLogic.Item,ItemIcon>.RenderModelResult>;
using ResultSource=Il2CppSystem.Threading.Tasks.TaskCompletionSource<IconCreatorBase<EFT.InventoryLogic.Item,ItemIcon>.RenderModelResult>;

namespace IconRenderGuard;
public sealed class IconRenderModule:IHotModule {
 sealed class Job(ItemIconCreator creator,Item item,SpriteFactory factory){
  internal readonly ItemIconCreator Creator=creator;internal readonly Item Item=item;
  internal readonly SpriteFactory Factory=factory;internal readonly ResultSource Source=new();
  internal SpriteFactory? Wrapped;internal string Model="";internal string[] Meshes=[];
 }
 sealed record Repair(string Template,ItemIcon Icon,string BeforeFile);
 static IconRenderModule? active;[ThreadStatic]static bool invoking;
 ModuleContext? context;readonly Queue<Job> jobs=new();readonly List<object> captures=[];readonly List<Repair> repairs=[];
 Job? current;RenderTask? task;
 DateTime repairStamp;float nextPoll,settleAt;int started,completed,failed,maximumQueued,invalidations;
 bool repairPending;
 public int ApiVersion=>1;public string Version=>"1.0.1";
 public bool CanReload=>current==null&&jobs.Count==0&&!repairPending;
 public void Initialize(ModuleContext context){
  this.context=context;context.OnShutdown(Cleanup);active=this;
  string request=Path.Combine(context.DataDirectory,"repair.request.json");
  // A prior one-shot repair must never repeat on reload or game startup.
  if(File.Exists(request))repairStamp=File.GetLastWriteTimeUtc(request);
  new Harmony(context.PatchId).Patch(AccessTools.Method(typeof(ItemIconCreator),nameof(ItemIconCreator.RenderModel)),prefix:new HarmonyMethod(typeof(IconRenderModule),nameof(Enqueue)));
  context.Log(LogLevel.Info,"Item icon render isolation ready: native model creation, capture and pool return run one at a time.");
  WriteStatus();
 }
 static bool Enqueue(ItemIconCreator __instance,Item __0,SpriteFactory __1,ref RenderTask __result){
  var a=active;if(invoking||a?.context?.IsActive!=true)return true;
  var job=new Job(__instance,__0,__1);a.jobs.Enqueue(job);a.maximumQueued=Math.Max(a.maximumQueued,a.jobs.Count);
  __result=job.Source.Task;return false;
 }
 public void Tick(){
  if(context?.IsActive!=true)return;
  // Complete the original caller only after native RenderModel has also
  // returned its preview model to the pool. The whole native task is isolated,
  // including async bundle/mip waits, rather than only Camera.Render itself.
  if(current!=null&&task?.IsCompleted==true){
   var finished=current;var result=task;current=null;task=null;
   if(result.IsCanceled){finished.Source.TrySetCanceled();failed++;}
   else if(result.IsFaulted){finished.Source.TrySetException(result.Exception);failed++;}
   else{finished.Source.TrySetResult(result.Result);completed++;}
   if(captures.Count<100)captures.Add(new{item=finished.Item.StringTemplateId,prefab=finished.Item.Prefab?.path,model=finished.Model,meshes=finished.Meshes,cancelled=result.IsCanceled,faulted=result.IsFaulted});
  }
  if(current==null&&jobs.Count>0){
   var job=jobs.Dequeue();current=job;started++;
   // Observe the actual model passed to the native sprite factory without
   // editing its transforms, meshes, layers, materials or native task state.
   job.Wrapped=DelegateSupport.ConvertDelegate<SpriteFactory>(new Func<GameObject,PreviewPivot,Il2CppSystem.Threading.Tasks.Task<Sprite>>((model,pivot)=>{
    job.Model=model?.name??"<null>";
    job.Meshes=model==null?[]:model.GetComponentsInChildren<MeshFilter>(true).Take(20).Select(f=>f.sharedMesh?.name??"<null>").ToArray();
    return job.Factory.Invoke(model!,pivot);
   }));
   try{invoking=true;task=job.Creator.RenderModel(job.Item,job.Wrapped);}
   catch(Exception error){failed++;job.Source.TrySetException(new Il2CppSystem.InvalidOperationException(error.Message));current=null;task=null;context.Log(LogLevel.Warning,"Icon render: "+error.Message);}
   finally{invoking=false;}
  }
  if(Time.unscaledTime<nextPoll)return;nextPoll=Time.unscaledTime+.5f;
  string request=Path.Combine(context.DataDirectory,"repair.request.json");
  if(File.Exists(request)&&!repairPending&&jobs.Count==0&&current==null){
   var stamp=File.GetLastWriteTimeUtc(request);if(stamp!=repairStamp){repairStamp=stamp;BeginRepair(request);}
  }
  if(repairPending&&jobs.Count==0&&current==null&&repairs.All(r=>r.Icon.Sprite!=null)){
   if(settleAt==0)settleAt=Time.unscaledTime+1;
   else if(Time.unscaledTime>=settleAt){FinishRepair();repairPending=false;settleAt=0;WriteStatus();}
  }
 }
 void BeginRepair(string path){
  using var doc=JsonDocument.Parse(File.ReadAllText(path));
  string[] templates=doc.RootElement.GetProperty("templates").EnumerateArray().Select(x=>x.GetString()??"").Distinct().ToArray();
  bool invalidateAll=doc.RootElement.TryGetProperty("invalidateAll",out var invalidate)&&invalidate.GetBoolean();
  if(templates.Length is <1 or >128||templates.Any(id=>id.Length!=24||id.Any(c=>!Uri.IsHexDigit(c))))throw new InvalidDataException("Expected 1-128 explicit item template IDs");
  var factory=Singleton<ItemFactory>.Instance??throw new InvalidOperationException("Item factory unavailable");var creator=Singleton<ItemIconCreator>.Instance??throw new InvalidOperationException("Icon creator unavailable");
  string backup=Path.Combine(context!.DataDirectory,"before-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));Directory.CreateDirectory(backup);
  if(File.Exists(creator._indexPath))File.Copy(creator._indexPath,Path.Combine(backup,"icon-index.backup"));
  repairs.Clear();captures.Clear();
  var samples=new List<(string Id,Item Item,string Before)>();
  // Force refresh only the requested raster entries, leaving the item/profile,
  // template database, other icon files and all equipped models untouched.
  foreach(string id in templates){
   var item=factory.CreateItem(factory.NextId.ToString(),id,null);int hash=IconsHash.GetItemHash(item);string old="";
   if(creator.TryGetIconPath(hash,out old)&&File.Exists(old))File.Copy(old,Path.Combine(backup,id+".png"));
   samples.Add((id,item,old));
  }
  if(invalidateAll){creator.ClearMemoryCache();creator._fileCacheIndex.Clear();invalidations++;}
  foreach(var sample in samples){var icon=ItemViewFactory.LoadItemIcon(sample.Item,1,true);repairs.Add(new(sample.Id,icon,sample.Before));}
  if(invalidateAll)foreach(var view in Resources.FindObjectsOfTypeAll<ItemView>())try{
   if(view!=null&&!view.IsKilled&&view.gameObject.activeInHierarchy&&view.Item!=null)view.RefreshIcon();
  }catch(Exception error){context.Log(LogLevel.Warning,"Refresh existing item icon: "+error.Message);}
  repairPending=true;settleAt=0;WriteStatus();
 }
 void FinishRepair(){
  var creator=Singleton<ItemIconCreator>.Instance!;var report=new List<object>();
  foreach(var repair in repairs){
   creator.TryGetIconPath(repair.Icon.Hash,out string path);
   if(File.Exists(path))File.Copy(path,Path.Combine(context!.DataDirectory,repair.Template+".png"),true);
   report.Add(new{template=repair.Template,hash=repair.Icon.Hash,before=repair.BeforeFile,after=path,sprite=repair.Icon.Sprite?.Pointer.ToInt64()});
  }
  var repaired=repairs.Select(r=>r.Template).ToHashSet(StringComparer.Ordinal);
  foreach(var view in Resources.FindObjectsOfTypeAll<ItemView>())if(view!=null&&!view.IsKilled&&view.gameObject.activeInHierarchy&&view.Item!=null&&repaired.Contains(view.Item.StringTemplateId))view.RefreshIcon();
  File.WriteAllText(Path.Combine(context!.DataDirectory,"repair-result.json"),JsonSerializer.Serialize(new{utc=DateTime.UtcNow,process=Environment.ProcessId,report,captures,started,completed,failed,maximumQueued,invalidations}));
  context.Log(LogLevel.Info,"Regenerated "+repairs.Count+" requested item icons with isolated native renders.");
 }
 void WriteStatus(){if(context!=null)File.WriteAllText(Path.Combine(context.DataDirectory,"render-status.json"),JsonSerializer.Serialize(new{utc=DateTime.UtcNow,version=Version,process=Environment.ProcessId,started,completed,failed,maximumQueued,queued=jobs.Count,busy=current!=null,repairPending,invalidations}));}
 void Cleanup(){if(active==this)active=null;while(jobs.Count>0)jobs.Dequeue().Source.TrySetCanceled();current?.Source.TrySetCanceled();current=null;task=null;repairs.Clear();captures.Clear();context=null;}
 public void LateTick(){}public void Draw(){}public void Shutdown()=>Cleanup();
}
