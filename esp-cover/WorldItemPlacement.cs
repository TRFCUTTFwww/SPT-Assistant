using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class WorldItemPlacement {
 sealed record Request(IntPtr World,IntPtr Player,string Profile,Item Item,Vector3 Position,Action? Completed);
 static readonly Queue<Request> queue=new();static Request? active;
 static Il2CppSystem.Threading.Tasks.Task? loading;static bool draining;
 static readonly Diz.Jobs.YieldDelegate yield=(Func<Diz.Jobs.JobYield,Diz.Jobs.IJobAwaitable>)(job=>Diz.Jobs.JobScheduler.Yield(Diz.Jobs.EJobPriority.General,job));
 internal static bool Busy=>active!=null||queue.Count>0;
 internal static string Status{get;private set;}="";
 internal static void Enqueue(GameWorld current,Item item,Vector3 position,Action? completed=null){
  if(draining)throw new ArgumentException("正在热更新，请稍候。");
  if(queue.Count>=64)throw new ArgumentException("物品生成队列已满。");
  var player=current.MainPlayer;if(player?.HealthController?.IsAlive!=true)throw new ArgumentException("请进入战局且保持存活。");
  queue.Enqueue(new(current.Pointer,player.Pointer,player.ProfileId,item,position,completed));Status="正在加载物品资源…";
 }
 static GameWorld Resolve(Request job){var world=Singleton<GameWorld>.Instance;var player=world?.MainPlayer;if(!RaidClock.Active||world==null||world.Pointer!=job.World||player?.Pointer!=job.Player||player.ProfileId!=job.Profile||player.HealthController?.IsAlive!=true)throw new ArgumentException("战局已改变，已取消物品生成。");return world;}
 internal static bool PrepareForReload(){draining=true;queue.Clear();return loading==null||loading.IsCompleted;}
 internal static void Reset(){queue.Clear();active=null;loading=null;draining=false;Status="";}
 internal static void Tick(){
  if(draining)return;
  if(active==null&&queue.Count>0)active=queue.Dequeue();var job=active;if(job==null)return;
  try{
   if(loading!=null&&!loading.IsCompleted)return;
   var world=Resolve(job);
   if(loading==null){
    var factory=Singleton<ObjectsFactory>.Instance??throw new ArgumentException("物品模型工厂尚未就绪。");
    var resources=new Il2CppSystem.Collections.Generic.List<ResourceKey>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach(var child in job.Item.GetAllItems())foreach(var resource in child.Template.AllResources)if(resource!=null&&!resource.IsEmpty()&&seen.Add(resource.path))resources.Add(resource);
    if(resources.Count==0)throw new ArgumentException("物品没有可用模型资源。");
    loading=factory.LoadBundlesAndCreatePools(ObjectsFactory.PoolsCategory.Raid,ObjectsFactory.AssemblyType.Local,resources.Cast<Il2CppSystem.Collections.Generic.ICollection<ResourceKey>>(),yield,null,factory.PoolsCancellationToken);
   }
   if(!loading.IsCompleted)return;
   if(loading.IsCanceled)throw new ArgumentException("物品资源加载已取消。");
   if(loading.IsFaulted)throw new InvalidOperationException(loading.Exception?.ToString()??"物品资源加载失败。");
   world=Resolve(job);
   world.ThrowItem(job.Item,world.MainPlayer.Cast<IPlayer>(),job.Position+Vector3.up*.15f,Quaternion.identity,Vector3.zero,Vector3.zero,true,true,0);
   bool found=false;for(int i=0;i<world.LootItems.Count;i++)if(world.LootItems.GetByIndex(i)?.Item?.Id==job.Item.Id){found=true;break;}
   if(!found)throw new InvalidOperationException("游戏未能生成物品，请查看日志。");
   RaidLoot.Invalidate();Status="物品已生成。";active=null;loading=null;job.Completed?.Invoke();FeatureToasts.Show(Status);
  }catch(Exception error){Status=I18n.Text("物品生成失败：")+error.Message;Plugin.Self.Log.LogWarning("World item placement: "+error);FeatureToasts.Show(Status);active=null;loading=null;}
 }
}
