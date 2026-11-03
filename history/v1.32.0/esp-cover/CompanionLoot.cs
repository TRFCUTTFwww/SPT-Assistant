using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using UnityEngine;
using Object=UnityEngine.Object;
namespace LocalSpt5Esp;
internal static class CompanionLoot {
 internal sealed record Source(IntPtr Pointer,string Id,Item Root,Vector3 Point,EBotLootSource Kind,LootItem? Loose,LootableContainer? Container);
 internal sealed class Work {
  internal Il2CppSystem.Threading.Tasks.Task<bool>? Task;internal Il2CppSystem.Threading.CancellationTokenSource? Cancellation;
  internal float Started,NextSearch,NextDrop;internal int CargoCount;internal bool Returning;
  internal string Claimed="",DropId="",DropError="";internal IntPtr DropPointer;internal bool DropPending,DropSucceeded;internal volatile bool DropDone;internal Callback? Callback;internal string[] DropContents=Array.Empty<string>();
  internal readonly Dictionary<string,float> Tried=new(StringComparer.Ordinal);
  internal Source? Target;
 }
 sealed record Retired(Il2CppSystem.Threading.Tasks.Task<bool> Task,Il2CppSystem.Threading.CancellationTokenSource? Cancellation);
 static readonly List<Retired> retired=new();static readonly List<Source> sources=new();static readonly Dictionary<string,string> claims=new(StringComparer.Ordinal);static readonly HashSet<string> delivered=new(StringComparer.Ordinal);static IntPtr world;static float nextScan;
 static IEnumerable<Item> Items(Item root,int depth=0){if(depth>16)yield break;yield return root;foreach(var child in LootDisplay.Children(root))foreach(var item in Items(child,depth+1))yield return item;}
 internal static void CaptureOriginal(CompanionSquad.Member member,Player actor){var root=actor.Profile?.Inventory?.Equipment;if(root==null)return;foreach(var item in Items(root).Take(8192))member.Original.Add(item.Id);}
 static IEnumerable<Item> Stored(Player actor){var equipment=actor.Profile?.Inventory?.Equipment;if(equipment==null)yield break;foreach(var slot in new[]{EquipmentSlot.Backpack,EquipmentSlot.TacticalVest,EquipmentSlot.Pockets}){var container=equipment.GetSlot(slot)?.ContainedItem?.TryCast<CompoundItem>();if(container?.Grids==null)continue;foreach(var grid in container.Grids)if(grid!=null)foreach(var item in grid.Items)if(item!=null)yield return item;}}
 static IEnumerable<Item> Cargo(CompanionSquad.Member member,Player actor){IEnumerable<Item> Walk(Item root,int depth){if(depth>16)yield break;bool originalChild=Items(root).Skip(1).Any(i=>member.Original.Contains(i.Id));if(CompanionRules.Deliverable(root.Id,member.Original,member.Delivered,actor.HandsController?.Item?.Pointer==root.Pointer,false,originalChild)){yield return root;yield break;}foreach(var item in LootDisplay.Children(root))foreach(var child in Walk(item,depth+1))yield return child;}foreach(var item in Stored(actor))foreach(var root in Walk(item,0))yield return root;}
 static void Unclaim(CompanionSquad.Member member){var work=member.Work;if(work.Claimed.Length>0&&claims.GetValueOrDefault(work.Claimed)==member.Actor.Profile)claims.Remove(work.Claimed);work.Claimed="";work.Target=null;}
 internal static void Cancel(CompanionSquad.Member member){try{member.Work.Cancellation?.Cancel();}catch{}if(member.Work.Task==null)Unclaim(member);member.Work.Returning=false;}
 internal static void CollectRetired(){for(int i=retired.Count-1;i>=0;i--)if(retired[i].Task.IsCompleted){try{retired[i].Cancellation?.Dispose();}catch{}retired.RemoveAt(i);}}
 internal static void Reset(){foreach(var entry in retired)try{entry.Cancellation?.Cancel();}catch{}CollectRetired();sources.Clear();claims.Clear();delivered.Clear();world=IntPtr.Zero;nextScan=0;}
 internal static void Retire(CompanionSquad.Member member){var work=member.Work;Cancel(member);if(work.Task!=null&&!work.Task.IsCompleted)retired.Add(new(work.Task,work.Cancellation));else try{work.Cancellation?.Dispose();}catch{}work.Task=null;work.Cancellation=null;Unclaim(member);}
 static void Scan(GameWorld current){if(world!=current.Pointer){sources.Clear();claims.Clear();delivered.Clear();world=current.Pointer;nextScan=0;}if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+5;sources.Clear();var list=current.LootItems;if(list!=null)for(int i=0;i<list.Count;i++){
   var loot=list.GetByIndex(i)?.TryCast<LootItem>();if(loot?.Item==null||loot.Item.QuestItem||delivered.Contains(loot.Item.Id)||loot.LastOwner?.ProfileId==current.MainPlayer?.ProfileId)continue;var corpse=loot.TryCast<Corpse>();if(corpse!=null&&!CompanionPlans.Corpses.Value||corpse==null&&!CompanionPlans.LooseLoot.Value)continue;
   sources.Add(new(loot.Pointer,loot.Item.Id,loot.Item,loot.transform.position,corpse!=null?EBotLootSource.DeadBody:EBotLootSource.WorldItem,loot,null));
  }
  if(CompanionPlans.Containers.Value)foreach(var container in Object.FindObjectsOfType<LootableContainer>()){if(container==null||container.DoorState==EDoorState.Locked||container.ItemOwner?.RootItem is not {} item)continue;sources.Add(new(container.Pointer,item.Id,item,container.transform.position,EBotLootSource.LootPointContainer,null,container));}
 }
 static bool Valid(Source source,GameWorld current){if(world!=current.Pointer||delivered.Contains(source.Id)||source.Root.Id!=source.Id)return false;if(source.Container!=null)return source.Container.DoorState!=EDoorState.Locked&&source.Container.ItemOwner?.RootItem?.Pointer==source.Root.Pointer;if(source.Loose==null||source.Loose.Item?.Pointer!=source.Root.Pointer)return false;var list=current.LootItems;if(list==null)return false;for(int i=0;i<list.Count;i++)if(list.GetByIndex(i)?.Pointer==source.Pointer)return true;return false;}
 internal static bool Pending(CompanionSquad.Member member,GameWorld current,Player actor){var work=member.Work;
  if(work.Task!=null){if(!work.Task.IsCompleted){if(Time.unscaledTime-work.Started>30){try{work.Cancellation?.Cancel();}catch{}member.Status="拾取超时，等待原生操作取消";}else member.Status="正在拾取物资";return true;}
   try{member.Status=work.Task.IsFaulted?"拾取失败："+work.Task.Exception?.GetBaseException()?.Message:work.Task.IsCanceled?"拾取已取消":work.Task.Result?"已拾取，继续搜索":"物资无法收纳或已被拿走";}finally{work.Task=null;try{work.Cancellation?.Dispose();}catch{}work.Cancellation=null;if(work.Claimed.Length>0)work.Tried[work.Claimed]=Time.unscaledTime+60;Unclaim(member);work.CargoCount=Cargo(member,actor).Count();}
  }
  if(work.DropPending){if(!work.DropDone&&Time.unscaledTime-work.Started<15){member.Status="正在丢下战利品";return true;}
   bool stillOwned=actor.InventoryController.TryFindItem(work.DropId,out var item)&&item?.Pointer==work.DropPointer;
   if(!stillOwned){member.Delivered.UnionWith(work.DropContents);delivered.UnionWith(work.DropContents);work.CargoCount=Cargo(member,actor).Count();member.Status="已丢下战利品";}
   else{member.Status=work.DropDone&&!work.DropSucceeded?"丢物资失败："+work.DropError:"丢物资尚未完成，保留物品并停止本次交付";work.Returning=false;member.Order=CompanionOrder.Follow;}
   work.DropPending=false;work.DropDone=false;work.Callback=null;work.NextDrop=Time.unscaledTime+.5f;
  }
  return false;
 }
 internal static void Search(CompanionSquad.Member member,GameWorld current,Player actor,Player local){var work=member.Work;work.CargoCount=Cargo(member,actor).Count();if(work.Returning||CompanionPlans.AutoDeliver.Value&&work.CargoCount>0){work.Returning=true;Deliver(member,current,actor,local);return;}
  if(work.Target!=null&&!Valid(work.Target,current))Unclaim(member);
  if(work.Target==null){if(Time.unscaledTime<work.NextSearch)return;work.NextSearch=Time.unscaledTime+2;Scan(current);float radius=CompanionPlans.LootRadius.Value;var choices=sources.Where(s=>!delivered.Contains(s.Id)&&!claims.ContainsKey(s.Id)&&work.Tried.GetValueOrDefault(s.Id)<=Time.unscaledTime&&(s.Point-local.Position).sqrMagnitude<=radius*radius).OrderBy(s=>(s.Point-actor.Position).sqrMagnitude).Take(16);foreach(var candidate in choices){var path=new UnityEngine.AI.NavMeshPath();if(!UnityEngine.AI.NavMesh.SamplePosition(candidate.Point,out var sample,3,UnityEngine.AI.NavMesh.AllAreas)||!UnityEngine.AI.NavMesh.CalculatePath(actor.Position,sample.position,UnityEngine.AI.NavMesh.AllAreas,path)||path.status!=UnityEngine.AI.NavMeshPathStatus.PathComplete){work.Tried[candidate.Id]=Time.unscaledTime+30;continue;}if(!Valid(candidate,current))continue;claims[candidate.Id]=member.Actor.Profile;work.Claimed=candidate.Id;work.Target=candidate;work.Started=Time.unscaledTime;break;}if(work.Target==null){member.Status="范围内暂无可达物资，等待搜索";return;}}
  var target=work.Target;if(Time.unscaledTime-work.Started>45){work.Tried[target.Id]=Time.unscaledTime+60;Unclaim(member);member.Status="物资路线超时，已更换目标";return;}
  if(!CompanionSquad.Navigate(member,actor,target.Point,2.5f)){member.Status="正在前往物资 · "+Vector3.Distance(actor.Position,target.Point).ToString("F0")+" 米";return;}
  if(Physics.Linecast(actor.Position+Vector3.up*.8f,target.Point+Vector3.up*.2f,LayerMask.GetMask("HighPolyCollider","Terrain","LowPolyCollider"),QueryTriggerInteraction.Ignore)){work.Tried[target.Id]=Time.unscaledTime+60;Unclaim(member);member.Status="物资被墙体遮挡，已跳过";return;}
  if(!Valid(target,current)){Unclaim(member);return;}CompanionSquad.StopRequest(member,actor);actor.AIData?.BotOwner?.StopMove();work.Cancellation=new();work.Started=Time.unscaledTime;
  try{var looting=actor.AIData?.BotOwner?.Looting??throw new ArgumentException("队友原生拾取控制器尚未就绪。");var items=new Il2CppSystem.Collections.Generic.List<Item>();
   if(target.Kind==EBotLootSource.WorldItem)items.Add(target.Root);
   else if(target.Root.TryCast<InventoryEquipment>() is {} equipment){foreach(var slot in new[]{EquipmentSlot.FirstPrimaryWeapon,EquipmentSlot.SecondPrimaryWeapon,EquipmentSlot.Holster,EquipmentSlot.Headwear,EquipmentSlot.ArmorVest,EquipmentSlot.TacticalVest,EquipmentSlot.Backpack,EquipmentSlot.Pockets}){var item=equipment.GetSlot(slot)?.ContainedItem;if(item!=null&&!item.QuestItem)items.Add(item);}}
   else foreach(var item in LootDisplay.Children(target.Root))if(!item.QuestItem)items.Add(item);
   var weighted=new Il2CppSystem.Collections.Generic.List<BotLootingData.WeightedItem>();looting.ResetLootState();looting.UpdateBotContainersCache(actor.Profile.Inventory.Equipment);BotLootingData.SortItemsByWeight(items.Cast<Il2CppSystem.Collections.Generic.IEnumerable<Item>>(),weighted,false);
   work.Task=looting.LootingRemainingItemsAsync(target.Root,weighted,work.Cancellation.Token);member.Status="正在拾取物资";
  }catch{work.Cancellation.Dispose();work.Cancellation=null;work.Tried[target.Id]=Time.unscaledTime+30;Unclaim(member);work.NextSearch=Time.unscaledTime+2;throw;}
 }
 internal static void Deliver(CompanionSquad.Member member,GameWorld current,Player actor,Player local){var work=member.Work;var cargo=Cargo(member,actor).ToArray();work.CargoCount=cargo.Length;if(cargo.Length==0){work.Returning=false;Unclaim(member);member.Status="没有可交付的新物资";if(member.Order==CompanionOrder.Deliver)member.Order=CompanionOrder.Follow;return;}if(!CompanionSquad.Navigate(member,actor,local.Position,3)){member.Status="正在把物资送回玩家身边";return;}if(Time.unscaledTime<work.NextDrop)return;
  CompanionSquad.StopRequest(member,actor);actor.AIData?.BotOwner?.StopMove();var item=cargo[0];if(actor.HandsController?.Item?.Pointer==item.Pointer||member.Original.Contains(item.Id))throw new ArgumentException("保留队友原有装备与手持物品。");var controller=actor.InventoryController.TryCast<Player.PlayerInventoryController>()??throw new ArgumentException("队友丢物品控制器尚未就绪。");work.DropId=item.Id;work.DropPointer=item.Pointer;work.DropPending=true;work.DropDone=false;work.DropSucceeded=false;work.DropError="";work.Started=Time.unscaledTime;string id=work.DropId;
  work.DropContents=Items(item).Select(i=>(string)i.Id).ToArray();work.Callback=(Action<IResult>)(result=>{if(work.DropId!=id)return;work.DropSucceeded=result.Succeed;work.DropError=result.Error??"";work.DropDone=true;});try{controller.ThrowItem(item,true,work.Callback);}catch{work.DropPending=false;work.Callback=null;work.NextDrop=Time.unscaledTime+5;throw;}
 }
}
