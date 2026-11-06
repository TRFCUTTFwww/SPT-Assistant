using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class ForceLoot {
 sealed record Source(Item Item,Component Anchor,IntPtr Root,string Id);
 sealed class Pending(Source source,long before,int count) {
  internal readonly string Template=source.Item.StringTemplateId;internal readonly Source Source=source;internal readonly long Before=before;internal readonly int Count=count;
  internal Callback? Callback;internal Il2CppSystem.Threading.Tasks.Task<IResult>? Task;
  internal bool Done,Success;internal string Error="";internal float Completed=-1,Started=Time.unscaledTime;
 }
 static ConfigEntry<bool> enabled=null!,containers=null!,corpses=null!;
 static ConfigEntry<float> radius=null!;static ConfigEntry<KeyCode> key=null!;
 static readonly Queue<Source> queue=new();static readonly List<Source> found=new();
 static readonly List<Pending> retired=new();static IEnumerator<Source?>? scanner;static Pending? pending;
 static IntPtr world,actor,controller;static string profile="",status="按按钮或绑定快捷键执行一次拾取。";
 static Vector2 scroll;static bool busy,cancelled;static int taken,skipped,failed;static float next,jobRadius;
 internal static void Initialize(Plugin plugin){const string section="55. 范围拾取";
  enabled=FeatureConfig.Bind(section,"Enable range pickup","启用范围拾取",true);
  radius=FeatureConfig.Bind(section,"Pickup radius","拾取半径（米）",10f,"按玩家当前位置检查三维距离；跳过已消失、被其他人拿走及超出范围的物品。",new AcceptableValueRange<float>(1,200));
  key=FeatureConfig.Bind(section,"Pickup hotkey","范围拾取快捷键",KeyCode.None,"默认未绑定；按一下执行一次。辅助菜单输入期间不触发。");
  containers=FeatureConfig.Bind(section,"Include container contents","包含容器内物品",true);
  corpses=FeatureConfig.Bind(section,"Include corpse contents","包含尸体装备与物资",true,"不取保险箱及任务专用物品，不触碰活人装备。");
  var method=AccessTools.Method(typeof(Player.PlayerInventoryController),"FindItemById",new[]{typeof(MongoID),typeof(bool),typeof(bool)});
  plugin.Patches.Patch(method,prefix:new HarmonyMethod(typeof(ForceLoot),nameof(FindForPickup)));
 }
 static Item? Root(Component anchor)=>anchor.TryCast<Corpse>()?.ItemOwner?.RootItem??anchor.TryCast<LootableContainer>()?.ItemOwner?.RootItem??anchor.TryCast<LootItem>()?.Item;
 static Vector3 Position(Component anchor)=>anchor.TryCast<Corpse>() is Corpse body?CorpseDisplay.Position(body):anchor.TryCast<LootableContainer>() is LootableContainer box?ContainerAnchor.Position(box):anchor.transform.position;
 static bool Owned(Player local,string id)=>local.Profile?.Inventory?.Equipment?.TryFindItem(id,out var item)==true&&item!=null;
 static bool Same(GameWorld? current,Player? local)=>current!=null&&local!=null&&current.Pointer==world&&local.Pointer==actor&&local.ProfileId==profile&&local.IsYourPlayer&&local.HealthController?.IsAlive==true;
 static bool Valid(Source source,GameWorld current,Player local){
  if(!Same(current,local)||source.Item==null||source.Anchor==null||!source.Anchor.gameObject.activeInHierarchy||source.Item.Id.ToString()!=source.Id||source.Item.QuestItem||Owned(local,source.Id)||!ForceLootRules.InRange((Position(source.Anchor)-local.Position).sqrMagnitude,jobRadius))return false;
  var root=Root(source.Anchor);if(root==null||root.Pointer!=source.Root)return false;
  if(root.Pointer==source.Item.Pointer)return true;
  return root.TryCast<ContainerCollection>() is {} collection&&collection.TryFindItem(source.Item.Id,out var item)&&item?.Pointer==source.Item.Pointer;
 }
 // Ignore the normal interaction-distance lookup only for this batch's exact item,
 // controller and raid. Ownership checks and native inventory restrictions stay active.
 static void FindForPickup(Player.PlayerInventoryController __instance,MongoID __0,ref bool __1){
  try{var current=Singleton<GameWorld>.Instance;var local=current?.MainPlayer;
   if(pending!=null&&__instance.Pointer==controller&&__0.ToString()==pending.Source.Id&&current!=null&&local!=null&&Valid(pending.Source,current,local))__1=false;
  }catch{}
 }
 static IEnumerable<Item> CorpseItems(Item root){
  var equipment=root.TryCast<InventoryEquipment>();if(equipment==null)yield break;
  foreach(var slot in new[]{EquipmentSlot.Backpack,EquipmentSlot.TacticalVest,EquipmentSlot.Pockets,EquipmentSlot.FirstPrimaryWeapon,EquipmentSlot.SecondPrimaryWeapon,EquipmentSlot.Holster,EquipmentSlot.Headwear,EquipmentSlot.ArmorVest,EquipmentSlot.FaceCover,EquipmentSlot.Earpiece,EquipmentSlot.Dogtag}){
   var item=equipment.GetSlot(slot)?.ContainedItem;if(item==null)continue;
   if(item.TryCast<CompoundItem>() is {} storage&&storage.Grids!=null)foreach(var grid in storage.Grids)if(grid!=null)foreach(var child in grid.Items)if(child!=null)yield return child;
   if(slot!=EquipmentSlot.Pockets)yield return item;
  }
 }
 static IEnumerable<Source?> Scan(GameWorld current,Player local){
  var seen=new HashSet<string>(StringComparer.Ordinal);var loot=current.LootItems;
  if(loot!=null)for(int i=0;i<loot.Count;i++){
   var anchor=loot.GetByIndex(i);if(anchor==null){yield return null;continue;}
   if(!ForceLootRules.InRange((Position(anchor)-local.Position).sqrMagnitude,jobRadius)){yield return null;continue;}
   var root=Root(anchor);if(root==null){yield return null;continue;}
   if(anchor.TryCast<Corpse>()!=null){if(corpses.Value)foreach(var item in CorpseItems(root)){if(!item.QuestItem&&seen.Add(item.Id))yield return new(item,anchor,root.Pointer,item.Id);else yield return null;}}
   else if(!root.QuestItem&&seen.Add(root.Id))yield return new(root,anchor,root.Pointer,root.Id);
   yield return null;
  }
  if(containers.Value)foreach(var box in UnityEngine.Object.FindObjectsOfType<LootableContainer>()){
   if(box==null||!box.IsInitialized||!ForceLootRules.InRange((Position(box)-local.Position).sqrMagnitude,jobRadius)){yield return null;continue;}
   var root=box.ItemOwner?.RootItem;if(root==null){yield return null;continue;}
   foreach(var item in LootDisplay.Children(root)){if(!item.QuestItem&&seen.Add(item.Id))yield return new(item,box,root.Pointer,item.Id);else yield return null;}
   yield return null;
  }
 }
 static IEnumerable<Item> Tree(Item root,HashSet<IntPtr> seen,int depth=0){if(depth>20||!seen.Add(root.Pointer))yield break;yield return root;foreach(var child in LootDisplay.Children(root))foreach(var item in Tree(child,seen,depth+1))yield return item;}
 static long Count(Player local,string template){var root=local.Profile?.Inventory?.Equipment;return root==null?0:Tree(root,new()).Where(i=>i.StringTemplateId==template).Sum(i=>(long)Math.Max(1,i.StackObjectsCount));}
 static void Start(){
  if(busy){FeatureToasts.Show("范围拾取正在执行，请等待或停止队列。");return;}
  var current=Singleton<GameWorld>.Instance;var local=current?.MainPlayer;
  if(!enabled.Value){status="请先启用范围拾取。";return;}
  if(!RaidClock.Active||current==null||local?.IsYourPlayer!=true||local.HealthController?.IsAlive!=true){status="请进入战局后执行。";FeatureToasts.Show(status);return;}
  if(!float.IsFinite(radius.Value)||radius.Value<=0){status="拾取半径须为有效正数。";return;}
  world=current.Pointer;actor=local.Pointer;profile=local.ProfileId;controller=local.InventoryController?.Pointer??IntPtr.Zero;
  jobRadius=radius.Value;taken=skipped=failed=0;cancelled=false;busy=true;found.Clear();queue.Clear();scanner=Scan(current,local).GetEnumerator();status="正在扫描范围内的真实物品…";
 }
 static void Stop(){scanner?.Dispose();scanner=null;queue.Clear();found.Clear();cancelled=true;if(pending==null)Finish();else status="已停止后续队列，等待当前原生库存事务结束。";}
 static void Finish(){busy=false;scanner?.Dispose();scanner=null;queue.Clear();found.Clear();status=$"{(cancelled?"已停止":"拾取完成")}：成功 {taken}，跳过 {skipped}，失败 {failed}。";FeatureToasts.Show(status);RaidLoot.Invalidate();}
 internal static void Tick(GameWorld? current,Player? local){try{
  retired.RemoveAll(p=>p.Done||p.Task?.IsCompleted==true);
  if(busy&&(!RaidClock.Active||!Same(current,local))){Reset();return;}
  if(!enabled.Value&&busy)Stop();
  if(enabled.Value&&!InterfaceMenu.InputBlocked&&key.Value!=KeyCode.None&&Input.GetKeyDown(key.Value))Start();
  if(!busy||current==null||local==null)return;
  if(pending!=null){
   var work=pending;if(!work.Done&&work.Task?.IsCompleted==true){work.Done=true;work.Success=!work.Task.IsFaulted&&!work.Task.IsCanceled&&work.Task.Result?.Succeed==true;}
   if(!work.Done){if(Time.unscaledTime-work.Started>30){cancelled=true;queue.Clear();status="库存事务尚未结束；已停止后续队列，等待游戏完成当前操作。";}return;}
   if(work.Completed<0)work.Completed=Time.unscaledTime;
   bool owned=Owned(local,work.Source.Id),present=Valid(work.Source,current,local);long gain=Count(local,work.Template)-work.Before;
   if(ForceLootRules.Confirmed(work.Success,owned,gain,work.Count,present&&work.Source.Item!=null?work.Source.Item.StackObjectsCount:0,present)){
    taken++;if(present&&work.Source.Item!=null&&work.Source.Item.StackObjectsCount<work.Count)queue.Enqueue(work.Source);
   }else if(work.Success&&Time.unscaledTime-work.Completed<.5f)return;else failed++;
   pending=null;next=Time.unscaledTime+.06f;status=$"正在拾取：成功 {taken}，跳过 {skipped}，失败 {failed}，排队 {queue.Count}。";
  }
  if(cancelled){Finish();return;}
  if(scanner!=null){for(int i=0;i<80;i++){if(scanner.MoveNext()){if(scanner.Current is {} source)found.Add(source);continue;}scanner.Dispose();scanner=null;foreach(var source in found.OrderBy(s=>(Position(s.Anchor)-local.Position).sqrMagnitude))queue.Enqueue(source);found.Clear();status=$"扫描完成，等待拾取 {queue.Count} 项。";break;}return;}
  if(Time.unscaledTime<next)return;
  if(queue.Count==0){Finish();return;}
  var item=queue.Dequeue();if(!Valid(item,current,local)){skipped++;return;}
  var inventory=local.InventoryController?.TryCast<Player.PlayerInventoryController>();if(inventory==null){Stop();return;}
  var targets=new Il2CppSystem.Collections.Generic.List<CompoundItem>();
  foreach(var slot in new[]{EquipmentSlot.Backpack,EquipmentSlot.TacticalVest,EquipmentSlot.Pockets}){var bag=local.Profile.Inventory.Equipment.GetSlot(slot)?.ContainedItem?.TryCast<CompoundItem>();if(bag?.Grids!=null)targets.Add(bag);}
  if(targets.Count==0){skipped++;return;}
  pending=new(item,Count(local,item.Item.StringTemplateId),item.Item.StackObjectsCount);var operation=ItemManipulator.QuickFindAppropriatePlace(item.Item,inventory,targets.Cast<Il2CppSystem.Collections.Generic.IEnumerable<CompoundItem>>(),ItemManipulator.EMoveItemOrder.PickUp,true);
  if(!operation.Succeeded){pending=null;skipped++;return;}
  var active=pending;active.Callback=(Action<IResult>)(result=>{active.Success=result.Succeed;active.Error=result.Error??"";active.Done=true;});
  active.Task=inventory.TryRunNetworkTransaction(operation,active.Callback);
 }catch(Exception error){failed++;status="范围拾取："+error.Message;Plugin.Self.Error(error);if(pending!=null){retired.Add(pending);pending=null;}Stop();}}
 internal static void Reset(){scanner?.Dispose();scanner=null;queue.Clear();found.Clear();if(pending!=null){retired.Add(pending);pending=null;}busy=cancelled=false;world=actor=controller=IntPtr.Zero;profile="";}
 internal static void Draw(Rect rect){scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-20,650));DrawBody(new Rect(4,4,rect.width-32,634));GUI.EndScrollView();}
 static void DrawBody(Rect rect){float y=rect.y;InterfaceMenu.Label(new(rect.x,y,rect.width,30),"拾取范围内物品",InterfaceMenu.Accent,20);y+=42;
  float half=(rect.width-12)/2;if(InterfaceMenu.Button(new(rect.x,y,half,34),busy?"正在执行…":"立即拾取"))Start();if(InterfaceMenu.Button(new(rect.x+half+12,y,half,34),"停止后续队列"))Stop();y+=46;
  foreach(var entry in new ConfigEntryBase[]{enabled,radius,key,containers,corpses}){InterfaceMenu.DrawEntry(entry,new(rect.x,y,rect.width,76));y+=84;}
  InterfaceMenu.Paragraph(new(rect.x,y,rect.width,70),status+"\n通过原生库存事务转移，忽略普通交互距离；背包、胸挂和口袋满了会跳过，原物保留。任务专用物品及保险箱不取。",null,12);
 }
}
