using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Game.Spawning;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.AI;
using NativeTask=Il2CppSystem.Threading.Tasks.Task;
namespace LocalSpt5Esp;
internal static class AiSpawnService {
 sealed class Job {
  internal bool SceneRestore;internal IntPtr World;internal Vector3 Position;internal AiSpawnOption Option=null!;internal int Difficulty;internal AiBirthPreset Preset=null!;internal SpawnLedger Ledger=null!;
  internal readonly Dictionary<string,Profile> Expected=new(StringComparer.Ordinal);internal int BatchWanted,BeforeBatch,Failures,Serial;internal float Started,PhaseStart,LastProgress,Completed=-1;
  internal string Label="",Phase="",CompanionKey="";internal int PresetFailures;internal GetProfileDataParams? RequestedProfile;
 }
 sealed record Retired(NativeTask Task,Il2CppSystem.Threading.CancellationTokenSource? Cancellation,BotCreationData? Data);
 static readonly Queue<Job> queue=new();static readonly List<Retired> retired=new();static readonly ConcurrentQueue<(Job Job,int Serial,BotOwner Bot)> callbacks=new();
 static Job? active;static BotSpawner? spawner;static BotZone? zone;static BotCreationData? data;
 static Il2CppSystem.Threading.Tasks.Task<Profile>? generatingProfile;static Il2CppSystem.Threading.Tasks.Task<BotCreationData>? generating;static NativeTask? activating;
 static Il2CppSystem.Threading.CancellationTokenSource? cancellation;static Il2CppSystem.Action<BotOwner>? callback;
 static int serial;static float nextStatus,nextWarning;internal static string Status{get;private set;}="尚无生成请求。";internal static int Revision{get;private set;}
 internal static bool Busy=>active!=null||queue.Count>0||retired.Any(entry=>!entry.Task.IsCompleted);
 internal static int QueueCount=>queue.Count;
 static void Report(string message,bool toast=false){if(Status!=message){Status=message;Revision++;}if(toast)FeatureToasts.Show(message);}
 static BotsController Controller()=>Singleton<AbstractGame>.Instance?.TryCast<IBotGame>()?.BotsController??throw new ArgumentException("当前战局 AI 控制器尚未就绪。");
 internal static Vector3 Ground(Vector3 point,Player player,bool sceneRestore=false){
  if(!float.IsFinite(point.x)||!float.IsFinite(point.y)||!float.IsFinite(point.z))throw new ArgumentException("生成坐标无效。");
  if(Physics.Raycast(point+Vector3.up*1.8f,Vector3.down,out var floor,6,LayerMask.GetMask("HighPolyCollider","Terrain","LowPolyCollider"),QueryTriggerInteraction.Ignore))point=floor.point;
  if(!NavMesh.SamplePosition(point,out var hit,3,NavMesh.AllAreas)||Math.Abs(hit.position.y-point.y)>2)throw new ArgumentException("弹着点附近没有可行走地面，请对准有效地面。");

  return hit.position;
 }
 internal static void Enqueue(GameWorld current,Vector3 position,AiSpawnOption option,int difficulty,int count,AiBirthPreset preset,string label,bool sceneRestore=false,string companionKey=""){
  if(!RaidClock.Active||current.MainPlayer?.HealthController?.IsAlive!=true)throw new ArgumentException("请进入战局且保持存活。");
  if(queue.Count>=16)throw new ArgumentException("生成队列已满，请等待或取消剩余请求。");
  if(count<1||difficulty is <0 or >3)throw new ArgumentException("生成参数无效。");
  position=Ground(position,current.MainPlayer,sceneRestore);var controller=Controller();var native=controller.GetSpawner()??throw new ArgumentException("AI 生成器尚未就绪。");
  var area=native.GetClosestZone(position,out _)??throw new ArgumentException("目标位置没有原生 AI 区域。");
  if(!area.HaveFreeSpace(count))throw new ArgumentException("该区域当前空位不足；区域原生容量为 "+area.MaxPersons+"，请减少数量或更换位置。");
  if(!AiLimitCompatibility.HasNavigation(controller.CoversData,position)||AICorePointHolder.GetClosest(position)==null)throw new ArgumentException("目标缺少完整 AI 导航节点，请换到原生 AI 活动区域。");
  queue.Enqueue(new(){SceneRestore=sceneRestore,World=current.Pointer,Position=position,Option=option,Difficulty=difficulty,Preset=preset,Ledger=new(count),Label=label,CompanionKey=companionKey});
  Report("已排队："+option.Name+" ×"+count+"；等待请求 "+queue.Count,true);
 }
 internal static void Preload(AiSpawnOption option,int difficulty,int count){var creator=Controller().GetSpawner()?._botCreator??throw new ArgumentException("AI 资料加载器尚未就绪。");creator.AddToTargetBackup((BotDifficulty)difficulty,Enum.Parse<WildSpawnType>(option.Role),Math.Clamp(count,1,8));Report("已请求预加载："+option.Name+"；只准备资料和资源，不生成角色。",true);}
 static void CollectRetired(){
  for(int i=retired.Count-1;i>=0;i--){var entry=retired[i];if(!entry.Task.IsCompleted)continue;try{entry.Data?.StopSpawn();if(entry.Task.TryCast<Il2CppSystem.Threading.Tasks.Task<BotCreationData>>() is {} generated&&!generated.IsFaulted&&!generated.IsCanceled)generated.Result?.StopSpawn();entry.Cancellation?.Dispose();}catch{}retired.RemoveAt(i);}
 }
 static void ReleaseBatch(bool stop){
  try{if(stop){data?.StopSpawn();cancellation?.Cancel();}}catch{}
  RaidSpawnControl.ForgetManual(data);
  NativeTask? task=activating??(NativeTask?)generating??generatingProfile;
  if(task!=null&&!task.IsCompleted)retired.Add(new(task,cancellation,data));else try{cancellation?.Dispose();}catch{}
  generating=null;generatingProfile=null;activating=null;data=null;cancellation=null;callback=null;
 }
 internal static void Cancel(bool notify){
  if(notify)SceneTemplates.CancelRestore();
  int made=active?.Ledger.Created.Count??0,wanted=active?.Ledger.Wanted??0;if(active?.CompanionKey.Length>0)CompanionSquad.RequestCancelled(active.CompanionKey);foreach(var job in queue)if(job.CompanionKey.Length>0)CompanionSquad.RequestCancelled(job.CompanionKey);serial++;queue.Clear();ReleaseBatch(true);active=null;spawner=null;zone=null;callbacks.Clear();
  if(notify)Report($"已取消剩余生成；已出现 {made}/{wanted} 名 AI 保留。",true);
 }
 internal static void Reset(){Cancel(false);CollectRetired();}
 static void Finish(string reason){var job=active;if(job==null)return;if(job.SceneRestore)SceneTemplates.SceneJobFinished(job.Ledger.Created.Count,job.Ledger.Wanted);if(job.CompanionKey.Length>0)CompanionSquad.RequestFinished(job.CompanionKey,job.Ledger.Created.Count,job.Ledger.Wanted,reason);ReleaseBatch(false);active=null;spawner=null;zone=null;Report($"{job.Option.Name}：实际生成 {job.Ledger.Created.Count}/{job.Ledger.Wanted}，用时 {Time.unscaledTime-job.Started:F1} 秒。"+reason+(job.PresetFailures>0?$" {job.PresetFailures} 名 AI 的部分出生设置失败，见日志。":""),true);}
 static void BeginBatch(GameWorld current){
  var job=active!;if(job.Ledger.Missing==0){Finish("已完成。");return;}
  var controller=Controller();spawner=controller.GetSpawner()??throw new ArgumentException("AI 生成器尚未就绪。");
  zone=spawner.GetClosestZone(job.Position,out _)??throw new ArgumentException("目标 AI 区域不可用。");
  job.BatchWanted=Math.Min(job.Ledger.Missing,job.Option.Side=="Savage"?4:1);
  while(job.BatchWanted>0&&!zone.HaveFreeSpace(job.BatchWanted))job.BatchWanted--;
  if(job.BatchWanted==0)throw new ArgumentException("该区域已无可用空位，未生成的数量保留在结果中。");
  job.BeforeBatch=job.Ledger.Created.Count;job.Expected.Clear();job.Completed=-1;job.Serial=++serial;
  job.Phase="资料与资源准备";job.PhaseStart=job.LastProgress=Time.unscaledTime;
  var parameters=new BotSpawnParams{TriggerType=SpawnTriggerType.none,ShallBeGroup=new ShallBeGroupParams(job.BatchWanted>1,job.Option.Boss,job.BatchWanted),Id_spawn="assistant-"+Guid.NewGuid().ToString("N")};
  var profile=new GetProfileDataParams(Enum.Parse<EPlayerSide>(job.Option.Side),Enum.Parse<WildSpawnType>(job.Option.Role),(BotDifficulty)job.Difficulty,0,parameters,true);
  if(job.Option.Side!="Savage"){
   job.RequestedProfile=profile;
   var loadingSide=Enum.Parse<EPlayerSide>(AiPmcProfileRules.LoadingSide(job.Option.Role,job.Option.Side));
   var loadingProfile=new GetProfileDataParams(loadingSide,Enum.Parse<WildSpawnType>(job.Option.Role),(BotDifficulty)job.Difficulty,0,parameters,true);
   data=BotCreationData.CreateWithoutProfile(loadingProfile.Cast<IGetProfileData>());data._botCreator=spawner._botCreator;data._tokenGetter=spawner.Cast<ITokenGetter>();cancellation=new();generatingProfile=spawner._botCreator.GenerateProfile(data,cancellation.Token,true);
   Plugin.Self.Log.LogInfo($"PMC profile selection: requested={job.Option.Role}/{job.Option.Side}; backendLoadingSide={loadingSide}; difficulty={(BotDifficulty)job.Difficulty}");
  }else generating=BotCreationData.Create(profile.Cast<IGetProfileData>(),spawner._botCreator,job.BatchWanted,spawner.Cast<ITokenGetter>());
  Plugin.Self.Log.LogInfo($"Manual AI batch requested: {job.Option.Role}, wanted={job.BatchWanted}, missing={job.Ledger.Missing}, source={job.Label}");
 }
 static void Activate(GameWorld current){
  var job=active!;if(generating!=null){data=generating.Result;generating=null;}
  if(data?.Profiles==null||data.Profiles.Count==0){RetryPreparation("加载器返回的 AI 资料为空。");return;}
  while(data.Profiles.Count>job.BatchWanted){int before=data.Profiles.Count;data.RemoveProfile(data.Profiles[before-1]);if(data.Profiles.Count>=before)throw new InvalidOperationException("原生资料集合未能缩减，已停止激活以避免超额生成。");}
  var positions=new Il2CppSystem.Collections.Generic.List<ISpawnPoint>();
  for(int i=0;i<data.Profiles.Count;i++){
   var profile=data.Profiles[i];if(profile==null||job.Expected.ContainsKey(profile.Id))throw new ArgumentException("加载器返回了重复或无效的 AI 身份。");
   AiSpawnPresets.Prepare(profile,job.Preset);job.Expected[profile.Id]=profile;
   int index=job.Ledger.Created.Count+i;float radius=index==0?0:2.1f*Mathf.Sqrt(index);var point=Ground(job.Position+new Vector3(Mathf.Cos(index*2.4f),0,Mathf.Sin(index*2.4f))*radius,current.MainPlayer,job.SceneRestore);
   if(!AiLimitCompatibility.HasNavigation(Controller().CoversData,point))throw new ArgumentException("分散生成点缺少导航数据。");
   var core=AICorePointHolder.GetClosest(point)??throw new ArgumentException("生成点附近没有 AI 核心节点。");
   var spawn=new SpawnPoint{Id="assistant-"+Guid.NewGuid().ToString("N"),Name="SPT Assistant",Position=point,Rotation=job.Preset.Scene?.Enabled==true?Quaternion.Euler(0,job.Preset.Scene.Yaw,0):Quaternion.identity,BotZone=zone,CorePointId=core.Id,Sides=(EPlayerSideMask)(-1),Categories=(ESpawnCategoryMask)(-1)};positions.Add(spawn.Cast<ISpawnPoint>());
  }
  int expectedSerial=job.Serial;callback=DelegateSupport.ConvertDelegate<Il2CppSystem.Action<BotOwner>>(new Action<BotOwner>(bot=>{if(bot!=null)callbacks.Enqueue((job,expectedSerial,bot));}));
  Plugin.Self.Log.LogInfo($"Manual AI profiles ready: returned={job.Expected.Count}/{job.BatchWanted}, prepareSeconds={Time.unscaledTime-job.PhaseStart:F2}, bundles={spawner!._botCreator.BundlesLoading}");
  job.Phase="地图激活";job.PhaseStart=job.LastProgress=Time.unscaledTime;cancellation??=new();RaidSpawnControl.AllowManual(data);
  activating=spawner!.ActivateBots(positions,zone!,data,callback,new BotSpawnBossAndGroupParams(job.Option.Boss,job.BatchWanted>1),cancellation.Token);
 }
 static void Confirm(GameWorld current,Player actor){
  var job=active!;if(!job.Expected.ContainsKey(actor.ProfileId)||job.Ledger.Created.Contains(actor.ProfileId)||actor.HealthController==null)return;
  if(!actor.HealthController.IsAlive){if(job.Ledger.Confirm(actor.ProfileId)){job.LastProgress=Time.unscaledTime;Plugin.Self.Log.LogWarning("Spawned AI died before birth settings could be applied; counted once and will not be replaced: "+actor.ProfileId);}return;}
  if(actor.ActiveHealthController==null||actor.AIData?.BotOwner==null)return;
  try{new LiveActorReference(current,actor).Resolve();}catch{return;}
  if(!job.Ledger.Confirm(actor.ProfileId))return;
  Plugin.Self.Log.LogInfo($"Manual AI identity: requested={job.Option.Role}; actual={actor.Profile?.Info?.Settings?.Role}; nickname={actor.Profile?.Nickname}; side={actor.Profile?.Side}; profile={actor.ProfileId}");
  job.LastProgress=Time.unscaledTime;
  try{string problems=AiSpawnPresets.Apply(current,actor,job.Preset);if(problems.Length>0){job.PresetFailures++;Plugin.Self.Log.LogWarning("AI birth settings "+actor.ProfileId+": "+problems);}}catch(Exception error){job.PresetFailures++;Plugin.Self.Log.LogWarning("AI birth settings: "+error);}if(job.SceneRestore)SceneTemplates.Restored(job.Preset.SceneKey,actor);if(job.CompanionKey.Length>0)CompanionSquad.Spawned(current,actor,job.CompanionKey);
 }
 static void Reconcile(GameWorld current){var all=current.AllAlivePlayersList;if(all==null)return;for(int i=0;i<all.Count;i++){var actor=all[i];if(actor?.IsAI==true&&!actor.IsYourPlayer&&actor.HealthController?.IsAlive==true)Confirm(current,actor);}}
 static void RetryPreparation(string reason){var job=active!;ReleaseBatch(true);job.Failures++;Plugin.Self.Log.LogWarning("Manual AI preparation: "+reason);if(job.Failures>2)Finish("资料准备补尝试两次后仍失败："+reason);else Report("资料准备未成功，将进行第 "+job.Failures+" 次补尝试；"+reason);}
 internal static void Tick(GameWorld? current,Player? player){
  CollectRetired();
  if(current==null||player?.HealthController?.IsAlive!=true){if(active!=null||queue.Count>0)Cancel(false);return;}
  if(active!=null&&active.World!=current.Pointer){Cancel(false);return;}
  if(active==null){if(retired.Count>0){if(queue.Count>0)Report("等待上次取消的原生加载结束，避免重复生成。");return;}if(queue.Count==0)return;active=queue.Dequeue();if(active.World!=current.Pointer){Cancel(false);return;}active.Started=Time.unscaledTime;}
  try{
   while(callbacks.TryDequeue(out var message))if(ReferenceEquals(message.Job,active)&&message.Serial==active.Serial&&message.Bot?.GetPlayer is {} actor)Confirm(current,actor);
   if(generating==null&&generatingProfile==null&&activating==null)BeginBatch(current);
   if(active==null)return;var job=active;
   if(generatingProfile!=null){
    if(generatingProfile.IsFaulted||generatingProfile.IsCanceled){var error=generatingProfile.Exception;Plugin.Self.Log.LogWarning("PMC direct profile preparation failed: "+error);RetryPreparation(error?.GetBaseException()?.Message??"PMC 资料准备已取消");return;}
    if(generatingProfile.IsCompleted){
     var profile=generatingProfile.Result??throw new ArgumentException("PMC 资料加载器未返回角色。");string actualRole=profile.Info?.Settings?.Role.ToString()??"",actualSide=profile.Side.ToString();
     if(!AiPmcProfileRules.Accepts(job.Option.Role,job.Option.Side,actualRole,actualSide))throw new ArgumentException($"PMC 资料不匹配：请求 {job.Option.Role}/{job.Option.Side}，返回 {actualRole}/{actualSide}，未用 Scav 代替。");
     profile.Info!.Side=Enum.Parse<EPlayerSide>(job.Option.Side);data!._profileData=job.RequestedProfile!.Cast<IGetProfileData>();data.AddProfile(profile);
     Plugin.Self.Log.LogInfo($"PMC profile selected: role={actualRole}; backendSide={actualSide}; activatedSide={profile.Side}; profile={profile.Id}");
     generatingProfile=null;Activate(current);
    }else if(Time.unscaledTime-job.PhaseStart>180){ReleaseBatch(true);Finish("PMC 资料／资源准备超时。");return;}
   }
   if(generating!=null){
    if(generating.IsFaulted||generating.IsCanceled){RetryPreparation(generating.Exception?.ToString()??"资料准备已取消");return;}
    if(generating.IsCompleted)Activate(current);else if(Time.unscaledTime-job.PhaseStart>180){ReleaseBatch(true);Finish("资料／资源准备超时；未继续叠加请求。");return;}
   }
   if(activating!=null){
    Reconcile(current);
    if(activating.IsCompleted){
     if(job.Completed<0)job.Completed=Time.unscaledTime;
     if(Time.unscaledTime-job.Completed>=.75f){
      int made=job.Ledger.Created.Count-job.BeforeBatch;bool fault=activating.IsFaulted||activating.IsCanceled;
      if(made<job.Expected.Count&&spawner!._botCreator.BotsLoading>0){if(Time.unscaledTime-job.Completed>30){ReleaseBatch(true);Finish("原生激活仍未收尾，已停止后续请求以避免重复角色。");return;}}
      else{
       string error=fault?activating.Exception?.Message??"原生激活已取消":"";
       Plugin.Self.Log.LogInfo($"Manual AI activation settled: created={made}/{job.BatchWanted}, seconds={Time.unscaledTime-job.PhaseStart:F2}, error={error}");
       ReleaseBatch(fault);
       if(made<job.BatchWanted){job.Failures++;if(job.Failures>2){Finish("补生成两次后仍有缺额。"+error);return;}}else job.Failures=0;
       if(job.Ledger.Missing==0){Finish("已完成。");return;}
      }
     }
    }else if(Time.unscaledTime-job.LastProgress>90){ReleaseBatch(true);Finish("地图激活长时间无进展；已取消剩余激活并保留已出现的 AI。");return;}
   }
   if(active!=null&&Time.unscaledTime>=nextStatus){nextStatus=Time.unscaledTime+.5f;Report($"{job.Option.Name} · {job.Phase} · 已出现 {job.Ledger.Created.Count}/{job.Ledger.Wanted} · 本阶段 {Time.unscaledTime-job.PhaseStart:F0}s · 资源加载 {spawner?._botCreator?.BundlesLoading??0} · 等待 {queue.Count}" );}
  }catch(Exception error){if(Time.unscaledTime>=nextWarning){nextWarning=Time.unscaledTime+5;Plugin.Self.Log.LogWarning("Manual AI generation: "+error);}ReleaseBatch(true);Finish(error.Message);}
 }
}
