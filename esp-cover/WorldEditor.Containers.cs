using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using LocalSpt.WorldEditing;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class WorldEditor {
 sealed record ContainerCopy(ItemCopy Contents,string Template,Quaternion Rotation,bool Opened);
 sealed record ContainerMove(ContainerScene.Reference Reference,Vector3 Origin,Quaternion Rotation);
 sealed record ContainerJob(IntPtr World,IntPtr Player,string Profile,string Map,string Template,Vector3 Position,Quaternion Rotation,bool Opened,ContainerLootOptions? Options,ContainerCopy? Copy,ContainerScene.Reference? Target,string ExpectedRoot);
 static readonly List<ContainerScene.Reference> selectedContainers=new();
 static readonly List<ContainerMove> moveContainers=new();
 static readonly Queue<ContainerJob> containerJobs=new();
 static ContainerJob? containerJob;static Task<JsonElement>? containerResponse;static Item? containerRoot;
 static Il2CppSystem.Threading.Tasks.Task? containerLoading;static bool containerDraining;
 static string containerStatus="",containerRolls="6",containerLootQuery="";
 static readonly List<ContainerLootChoice> containerChoices=new();
 static bool containerFir=true,createContainer;
 static void InitializeContainerContext(){
  var migrated=FeatureConfig.Bind("63. 场景编辑","Container menu migration","容器菜单已初始化",false,"内部迁移标记：仅首次加入新增容器菜单，之后保留自定义显隐。");
  if(migrated.Value)return;contextOrder.Value=string.Join(",",EditorContextRules.Read(contextOrder.Value).Concat(new[]{"rotate","loot-server","loot-custom","container-open"}).Distinct());migrated.Value=true;
 }
 static ContainerLootOptions LootOptions(string mode)=>new(){Mode=mode,Rolls=ItemSpawnRules.Integer(containerRolls,0,100,"抽取次数"),FoundInRaid=containerFir,Items=containerChoices.Select(i=>new ContainerLootChoice{Template=i.Template,Weight=i.Weight}).ToList()};
 static ContainerCopy CaptureContainer(LootableContainer c)=>new(CaptureItem(c.ItemOwner?.RootItem??throw new ArgumentException("容器库存尚未就绪。")),ContainerScene.Template(c),ContainerScene.Root(c).rotation,ContainerScene.Open(c));
 static void PlaceContainerCopy(ContainerCopy copy,Vector3 position)=>QueueContainer(copy.Template,position,copy.Rotation,copy.Opened,null,copy,null);
 static void QueueContainer(string template,Vector3 position,Quaternion rotation,bool opened,ContainerLootOptions? options,ContainerCopy? copy,ContainerScene.Reference? target){
  var current=Current();if(containerDraining)throw new ArgumentException("正在更新，请稍候。");
  if(containerJobs.Count>=32)throw new ArgumentException("容器操作队列已满，请等待完成。");
  if(target==null&&ContainerScene.Prototype(current,template)==null)throw new ArgumentException("当前地图未加载此类容器模型，请选择标为可用的容器。");
  options?.Validate();var p=current.MainPlayer;string expected=target?.Resolve().ItemOwner?.RootItem?.Id.ToString()??"";
  containerJobs.Enqueue(new(current.Pointer,p.Pointer,p.ProfileId,current.LocationId,template,position,rotation,opened,options,copy,target,expected));containerStatus="已排队 · "+(containerJobs.Count+(containerJob==null?0:1));
 }
 static GameWorld ResolveContainerJob(ContainerJob job){
  var current=Singleton<GameWorld>.Instance;var p=current?.MainPlayer;
  if(!RaidClock.Active||current?.Pointer!=job.World||p?.Pointer!=job.Player||p.ProfileId!=job.Profile||p.HealthController?.IsAlive!=true)throw new ArgumentException("战局已改变，已取消容器操作。");
  if(job.Target is {} reference){var target=reference.Resolve();if(target.ItemOwner?.RootItem?.Id.ToString()!=job.ExpectedRoot)throw new ArgumentException("容器库存已改变，请重新选择后操作。");if(target.InteractingPlayer!=null)throw new ArgumentException("容器正在使用中，已取消替换。");}
  return current;
 }
 internal static bool PrepareContainersForReload(){StopContainerBuild();containerDraining=true;containerJobs.Clear();return containerLoading==null||containerLoading.IsCompleted;}
 internal static void TickContainers(){
  if(containerDraining)return;
  if(containerJob==null&&containerJobs.Count>0)containerJob=containerJobs.Dequeue();var job=containerJob;if(job==null)return;
  try{
   var current=ResolveContainerJob(job);
   if(containerRoot==null){
    if(job.Copy!=null)containerRoot=CloneItem(job.Copy.Contents);
    else if(job.Options?.Mode=="empty"){var factory=Singleton<ItemFactory>.Instance;containerRoot=factory.CreateItem(factory.NextId.ToString(),job.Template,null);}
    else{
     containerResponse??=WorldEditorClient.Call("loot",job.Map,job.Template,job.Options);
     if(!containerResponse.IsCompleted)return;
     var data=containerResponse.GetAwaiter().GetResult();containerRoot=CloneItem(new(data.GetProperty("items").GetRawText(),data.GetProperty("root").GetString()!));
     foreach(var item in containerRoot.GetAllItems())item.SpawnedInSession=job.Options?.FoundInRaid??true;
    }
   }
   if(containerLoading==null){
    var factory=Singleton<ObjectsFactory>.Instance??throw new ArgumentException("物品模型工厂尚未就绪。");
    var resources=new Il2CppSystem.Collections.Generic.List<ResourceKey>();var seen=new HashSet<string>();
    foreach(var item in containerRoot.GetAllItems())foreach(var key in item.Template.AllResources)if(key!=null&&!key.IsEmpty()&&seen.Add(key.path))resources.Add(key);
    if(resources.Count>0)containerLoading=factory.LoadBundlesAndCreatePools(ObjectsFactory.PoolsCategory.Raid,ObjectsFactory.AssemblyType.Local,resources.Cast<Il2CppSystem.Collections.Generic.ICollection<ResourceKey>>(),Diz.Jobs.JobYieldPriority.General,null,factory.PoolsCancellationToken);
   }
   if(containerLoading!=null){if(!containerLoading.IsCompleted)return;if(containerLoading.IsCanceled||containerLoading.IsFaulted)throw new ArgumentException("容器物资资源加载失败，原库存未替换。");}
   current=ResolveContainerJob(job);
   if(job.Target is {} target)ContainerScene.Replace(current,target.Resolve(),containerRoot);
   else ContainerScene.Create(current,ContainerScene.Prototype(current,job.Template)??throw new ArgumentException("原模型已不可用。"),containerRoot,job.Position,job.Rotation,job.Opened);
   containerStatus=job.Target==null?"容器已放置。":"容器物资已重新生成。";FeatureToasts.Show(containerStatus);ClearContainerJob();
  }catch(Exception e){containerStatus="容器操作失败："+e.GetBaseException().Message;Plugin.Self.Log.LogWarning(containerStatus);FeatureToasts.Show(containerStatus);ClearContainerJob();}
 }
 static void ClearContainerJob(){containerJob=null;containerResponse=null;containerRoot=null;containerLoading=null;}
 static void ResetContainerJobs(){containerJobs.Clear();if(containerResponse is {} pending)_=pending.ContinueWith(t=>{_ = t.Exception;},TaskContinuationOptions.OnlyOnFaulted);ClearContainerJob();containerDraining=false;}
 static void RegenerateContainers(bool custom){var options=LootOptions(custom?"custom":"server");foreach(var reference in selectedContainers.ToArray()){var c=reference.Resolve();QueueContainer(ContainerScene.Template(c),Vector3.zero,Quaternion.identity,false,options,null,reference);}CompleteOperation();}
 static void SetContainersOpen(bool value){foreach(var reference in selectedContainers.ToArray())ContainerScene.SetOpen(reference.Resolve(),value);CompleteOperation();}
 static void OpenContainerLoot(){Modal("container-loot");}
 static void DrawContainerLoot(Rect rect){
  float x=rect.x,y=rect.y,w=rect.width;
  InterfaceMenu.Paragraph(new(x,y,w,50),"替换所选容器的全部库存。按权重抽取物品，使用游戏原生装箱规则；容器容量会限制实际生成数量。",null,13);y+=58;
  InterfaceMenu.Label(new(x,y,110,30),"抽取次数");containerRolls=InterfaceMenu.Field(new(x+120,y,110,30),containerRolls);if(InterfaceMenu.Button(new(x+242,y,w-242,30),containerFir?"物品：战局中找到 ✓":"物品：非战局中找到",containerFir))containerFir=!containerFir;y+=40;
  containerLootQuery=InterfaceMenu.Field(new(x,y,w-100,30),containerLootQuery);if(InterfaceMenu.Button(new(x+w-92,y,92,30),"清空候选"))containerChoices.Clear();y+=42;
  var found=ItemSpawner.FindTemplates(containerLootQuery).Take(20).ToArray();float viewHeight=Math.Max(90,rect.yMax-y-100);
  scroll=GUI.BeginScrollView(new(x,y,w,viewHeight),scroll,new Rect(0,0,w-22,(containerChoices.Count+found.Length)*42+30));
  try{float cy=0,cw=w-28;foreach(var choice in containerChoices.ToArray()){
   InterfaceMenu.Label(new(0,cy,Math.Max(60,cw-174),32),ItemSpawner.TemplateName(choice.Template),null,12);
   string text=InterfaceMenu.Field(new(cw-170,cy,82,30),choice.Weight.ToString(CultureInfo.InvariantCulture));if(double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out double weight)&&double.IsFinite(weight)&&weight>0&&weight<=1000000)choice.Weight=weight;
   if(InterfaceMenu.Button(new(cw-80,cy,80,30),"移除"))containerChoices.Remove(choice);cy+=42;
  }foreach(var item in found){if(InterfaceMenu.Button(new(0,cy,cw,32),"+ "+item.Name))Run(()=>{if(containerChoices.Count>=100)throw new ArgumentException("候选物品最多 100 项。");if(!containerChoices.Any(i=>i.Template==item.Id))containerChoices.Add(new(){Template=item.Id});});cy+=42;}}
  finally{GUI.EndScrollView();}
  if(InterfaceMenu.Button(new(x,rect.yMax-84,w,34),selectedContainers.Count>0?"按自定义配置重新生成 · "+selectedContainers.Count+" 个容器":"保存配置并返回容器目录"))Run(()=>{if(selectedContainers.Count>0)RegenerateContainers(true);else{LootOptions("custom").Validate();createContainer=true;Modal("create");}});
  InterfaceMenu.Paragraph(new(x,rect.yMax-42,w,40),"候选项右侧为相对权重。空列表配合 0 次抽取可清空容器。",null,12);
 }
 static readonly List<ContainerMove> rotateContainers=new();static string containerAngle="0";
 static void BeginContainerRotation(){rotateContainers.Clear();foreach(var r in selectedContainers){var t=ContainerScene.Root(r.Resolve());rotateContainers.Add(new(r,t.position,t.rotation));}containerAngle="0";Modal("container-rotation");}
 static void CancelContainerRotation(){foreach(var value in rotateContainers)try{ContainerScene.Root(value.Reference.Resolve()).SetPositionAndRotation(value.Origin,value.Rotation);}catch{}rotateContainers.Clear();}
 static void DrawContainerRotation(Rect rect){
  InterfaceMenu.Paragraph(new(rect.x,rect.y,rect.width,54),"绕各容器原点旋转；正数顺时针。点击应用保存，关闭或 Esc 恢复原方向。",null,13);
  containerAngle=InterfaceMenu.Field(new(rect.x,rect.y+64,rect.width,32),containerAngle);
  if(float.TryParse(containerAngle,NumberStyles.Float,CultureInfo.InvariantCulture,out float angle)&&float.IsFinite(angle)){foreach(var value in rotateContainers)ContainerScene.Root(value.Reference.Resolve()).rotation=Quaternion.AngleAxis(ContainerBuildRules.Yaw(angle),Vector3.up)*value.Rotation;}
  if(InterfaceMenu.Button(new(rect.x,rect.y+110,rect.width,34),"应用旋转"))Run(()=>{if(!float.TryParse(containerAngle,NumberStyles.Float,CultureInfo.InvariantCulture,out float a)||!float.IsFinite(a))throw new ArgumentException("角度无效。");rotateContainers.Clear();CompleteOperation();});
 }
}
