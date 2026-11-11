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
 // The native loader identifies delegates by identity in JobYieldPriority.Priority.
 // A managed wrapper, even one calling JobScheduler.Yield, is rejected there.
 static Diz.Jobs.YieldDelegate NativeYield(){var value=Diz.Jobs.JobYieldPriority.General;if(Diz.Jobs.JobYieldPriority.Priority(value)!=Diz.Jobs.EJobPriority.General)throw new InvalidOperationException("Native item loading priority mismatch.");return value;}
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
    Plugin.Self.Log.LogInfo($"World item resources requested: template={job.Item.TemplateId}, resources={resources.Count}");
    loading=factory.LoadBundlesAndCreatePools(ObjectsFactory.PoolsCategory.Raid,ObjectsFactory.AssemblyType.Local,resources.Cast<Il2CppSystem.Collections.Generic.ICollection<ResourceKey>>(),NativeYield(),null,factory.PoolsCancellationToken);
   }
   if(!loading.IsCompleted)return;
   if(loading.IsCanceled)throw new ArgumentException("物品资源加载已取消。");
   if(loading.IsFaulted)throw new InvalidOperationException(loading.Exception?.ToString()??"物品资源加载失败。");
   world=Resolve(job);
   world.ThrowItem(job.Item,world.MainPlayer.Cast<IPlayer>(),job.Position+Vector3.up*.15f,Quaternion.identity,Vector3.zero,Vector3.zero,true,true,0);
   bool found=false;for(int i=0;i<world.LootItems.Count;i++)if(world.LootItems.GetByIndex(i)?.Item?.Id==job.Item.Id){found=true;break;}
   if(!found)throw new InvalidOperationException("游戏未能生成物品，请查看日志。");
   Plugin.Self.Log.LogInfo($"World item placement registered: template={job.Item.TemplateId}, item={job.Item.Id}, position={job.Position}");
   RaidLoot.Invalidate();Status="物品已生成。";active=null;loading=null;job.Completed?.Invoke();FeatureToasts.Show(Status);
  }catch(Exception error){Status=I18n.Text("物品生成失败：")+error.Message;Plugin.Self.Log.LogWarning("World item placement: "+error);FeatureToasts.Show(Status);active=null;loading=null;}
 }
 static string checkState="not run",checkTemplate="";
 internal static object Diagnostics()=>new{busy=Busy,status=Status,checkState,checkTemplate,loading=loading?.Status.ToString()};
 internal static void Check(string template){
  if(Busy)throw new ArgumentException("物品生成队列正在运行。");var current=Singleton<GameWorld>.Instance??throw new ArgumentException("请进入战局且保持存活。");
  var factory=Singleton<ItemFactory>.Instance;var item=factory.CreateItem(factory.NextId.ToString(),template,null);checkTemplate=template;checkState="pending";
  Enqueue(current,item,current.MainPlayer.Position,()=>{
   var world=Singleton<GameWorld>.Instance;if(world?.Pointer!=current.Pointer)throw new InvalidOperationException("Placement check world changed.");
   EFT.Interactive.LootItem? created=null;for(int i=0;i<world.LootItems.Count;i++){var loot=world.LootItems.GetByIndex(i);if(loot?.Item?.Pointer==item.Pointer&&loot.Item.Id==item.Id){created=loot;break;}}
   if(created==null)throw new InvalidOperationException("Placement check lost created item.");world.DestroyLoot(created.Cast<IKillable>());
   for(int i=0;i<world.LootItems.Count;i++)if(world.LootItems.GetByIndex(i)?.Pointer==created.Pointer)throw new InvalidOperationException("Placement check cleanup did not remove temporary loot.");
   checkState="passed: resources loaded, item registered, temporary loot removed";Plugin.Self.Log.LogInfo("World item check: "+checkState+"; template="+template);RaidLoot.Invalidate();
  });
 }
}
