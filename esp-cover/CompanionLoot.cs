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
 internal sealed record Source(IntPtr Pointer,string Id,Item Root,Vector3 Point,EBotLootSource Kind,LootItem? Loose,LootableContainer? Container,bool Offered=false);
 internal sealed class Work {
  internal Il2CppSystem.Threading.Tasks.Task<bool>? Task,Draining;internal Il2CppSystem.Threading.CancellationTokenSource? Cancellation;
  internal float Started,NextSearch,NextDrop,VerifySince=-1;internal int CargoCount;internal bool Returning,RequestedOffer;
  internal string Claimed="",DropId="",DropError="";internal IntPtr DropPointer;internal bool DropPending,DropSucceeded;internal volatile bool DropDone;internal Callback? Callback;internal string[] DropContents=Array.Empty<string>();
  internal readonly Dictionary<string,float> Tried=new(StringComparer.Ordinal);
  internal readonly Dictionary<string,(string Template,string Name)> Requested=new(StringComparer.Ordinal);
  internal Dictionary<string,long> BeforeCounts=new(StringComparer.Ordinal);
  internal readonly HashSet<string> Gifts=new(StringComparer.Ordinal);
  internal string LastPickup="尚未确认拿到物品";
  internal Source? Target;internal Vector3 Approach,SourcePosition;
 }
 sealed record Retired(Il2CppSystem.Threading.Tasks.Task<bool> Task,Il2CppSystem.Threading.CancellationTokenSource? Cancellation);
 static readonly List<Retired> retired=new();static readonly List<Source> sources=new();static readonly Dictionary<string,string> claims=new(StringComparer.Ordinal);static readonly HashSet<string> delivered=new(StringComparer.Ordinal);static IntPtr world;static float nextScan,nextContainers;static LootableContainer[] containers=Array.Empty<LootableContainer>();
 static IEnumerable<Item> Items(Item root,int depth=0){if(depth>16)yield break;yield return root;foreach(var child in LootDisplay.Children(root))foreach(var item in Items(child,depth+1))yield return item;}
 internal static void CaptureOriginal(CompanionSquad.Member member,Player actor){var root=actor.Profile?.Inventory?.Equipment;if(root==null)return;foreach(var item in Items(root).Take(8192))member.Original.Add(item.Id);}
 static IEnumerable<Item> Stored(Player actor){var equipment=actor.Profile?.Inventory?.Equipment;if(equipment==null)yield break;foreach(var slot in new[]{EquipmentSlot.Backpack,EquipmentSlot.TacticalVest,EquipmentSlot.Pockets}){var container=equipment.GetSlot(slot)?.ContainedItem?.TryCast<CompoundItem>();if(container?.Grids==null)continue;foreach(var grid in container.Grids)if(grid!=null)foreach(var item in grid.Items)if(item!=null)yield return item;}}
 static Item[] StoredItems(Player actor)=>Stored(actor).SelectMany(root=>Items(root)).GroupBy(item=>(string)item.Id,StringComparer.Ordinal).Select(group=>group.First()).Take(8192).ToArray();
 static Dictionary<string,long> Counts(IEnumerable<Item> items)=>items.GroupBy(item=>item.StringTemplateId,StringComparer.Ordinal).ToDictionary(group=>group.Key,group=>group.Sum(item=>(long)Math.Max(1,item.StackObjectsCount)),StringComparer.Ordinal);
 static void BeforePickup(Work work,Player actor,Il2CppSystem.Collections.Generic.List<Item> items){work.RequestedOffer=work.Target?.Offered==true;work.VerifySince=-1;work.Requested.Clear();work.BeforeCounts=Counts(StoredItems(actor));foreach(var item in items)if(item!=null)work.Requested[item.Id]=(item.StringTemplateId,LootDisplay.ItemName(item,false));}
 static bool VerifyPickup(CompanionSquad.Member member,Player actor){var work=member.Work;var stored=StoredItems(actor);var ids=stored.Select(item=>(string)item.Id).ToHashSet(StringComparer.Ordinal);var counts=Counts(stored);int roots=work.Requested.Keys.Count(ids.Contains);long gain=work.Requested.Values.Select(item=>item.Template).Distinct(StringComparer.Ordinal).Sum(template=>Math.Max(0,counts.GetValueOrDefault(template)-work.BeforeCounts.GetValueOrDefault(template)));bool verified=CompanionRules.PickupConfirmed(roots,gain);
  if(verified){if(work.RequestedOffer)foreach(var item in stored)if(work.Requested.ContainsKey(item.Id)||work.Requested.Values.Any(request=>request.Template==item.StringTemplateId&&counts.GetValueOrDefault(request.Template)>work.BeforeCounts.GetValueOrDefault(request.Template)))work.Gifts.Add(item.Id);var names=work.Requested.Values.Where(item=>counts.GetValueOrDefault(item.Template)>work.BeforeCounts.GetValueOrDefault(item.Template)||work.Requested.Any(pair=>pair.Value.Template==item.Template&&ids.Contains(pair.Key))).Select(item=>item.Name).Distinct().Take(3);work.LastPickup="已拿到："+string.Join("、",names)+(work.RequestedOffer?" · 保留在队友背包":"");member.Status=work.LastPickup;}
  else{work.LastPickup="未确认物品入包：检查队友背包空间或物品是否仍在地上";member.Status=work.LastPickup;}
  Plugin.Self.Log.LogInfo($"Companion loot verified: actor={actor.ProfileId}; confirmed={verified}; ownedRoots={roots}; stackGain={gain}; requested={work.Requested.Count}");return verified;
 }
 static IEnumerable<Item> Cargo(CompanionSquad.Member member,Player actor){IEnumerable<Item> Walk(Item root,int depth){if(depth>16)yield break;bool originalChild=Items(root).Skip(1).Any(i=>member.Original.Contains(i.Id));if(CompanionRules.Deliverable(root.Id,member.Original,member.Delivered,actor.HandsController?.Item?.Pointer==root.Pointer,false,originalChild)){yield return root;yield break;}foreach(var item in LootDisplay.Children(root))foreach(var child in Walk(item,depth+1))yield return child;}foreach(var item in Stored(actor))foreach(var root in Walk(item,0))yield return root;}
 static IEnumerable<Item> AutoCargo(CompanionSquad.Member member,Player actor)=>Cargo(member,actor).Where(root=>!Items(root).Any(item=>member.Work.Gifts.Contains(item.Id)));
 static void Unclaim(CompanionSquad.Member member){var work=member.Work;if(work.Claimed.Length>0&&claims.GetValueOrDefault(work.Claimed)==member.Actor.Profile)claims.Remove(work.Claimed);work.Claimed="";work.Target=null;}
 internal static void Cancel(CompanionSquad.Member member){try{member.Work.Cancellation?.Cancel();}catch{}if(member.Work.Task==null)Unclaim(member);member.Work.Returning=false;}
 internal static void CollectRetired(){for(int i=retired.Count-1;i>=0;i--)if(retired[i].Task.IsCompleted){try{retired[i].Cancellation?.Dispose();}catch{}retired.RemoveAt(i);}}
 internal static void Reset(){foreach(var entry in retired)try{entry.Cancellation?.Cancel();}catch{}CollectRetired();sources.Clear();claims.Clear();delivered.Clear();containers=Array.Empty<LootableContainer>();world=IntPtr.Zero;nextScan=nextContainers=0;}
 internal static void Retire(CompanionSquad.Member member){var work=member.Work;Cancel(member);if(work.Task!=null&&!work.Task.IsCompleted)retired.Add(new(work.Task,work.Cancellation));else try{work.Cancellation?.Dispose();}catch{}work.Task=null;work.Cancellation=null;Unclaim(member);}
 static void Scan(GameWorld current){if(world!=current.Pointer){sources.Clear();claims.Clear();delivered.Clear();containers=Array.Empty<LootableContainer>();world=current.Pointer;nextScan=nextContainers=0;}if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+1;sources.Clear();var list=current.LootItems;if(list!=null)for(int i=0;i<list.Count;i++){
   var loot=list.GetByIndex(i)?.TryCast<LootItem>();if(loot?.Item==null)continue;var corpse=loot.TryCast<Corpse>();bool offered=corpse==null&&loot.LastOwner?.ProfileId==current.MainPlayer?.ProfileId;
   if(corpse!=null){if(loot.Item.QuestItem||delivered.Contains(loot.Item.Id)||!CompanionPlans.Corpses.Value)continue;}else if(!CompanionRules.LootCandidate(loot.Item.QuestItem,delivered.Contains(loot.Item.Id),offered,CompanionPlans.LooseLoot.Value,CompanionPlans.AcceptOffers.Value))continue;
   sources.Add(new(loot.Pointer,loot.Item.Id,loot.Item,loot.transform.position,corpse!=null?EBotLootSource.DeadBody:EBotLootSource.WorldItem,loot,null,offered));
  }
  if(CompanionPlans.Containers.Value){if(Time.unscaledTime>=nextContainers){nextContainers=Time.unscaledTime+15;containers=Object.FindObjectsOfType<LootableContainer>();}foreach(var container in containers){if(container==null||container.DoorState==EDoorState.Locked||container.ItemOwner?.RootItem is not {} item)continue;sources.Add(new(container.Pointer,item.Id,item,container.transform.position,EBotLootSource.LootPointContainer,null,container));}}
 }
 static bool Valid(Source source,GameWorld current){if(world!=current.Pointer||delivered.Contains(source.Id)||source.Root.Id!=source.Id)return false;if(source.Container!=null)return source.Container.DoorState!=EDoorState.Locked&&source.Container.ItemOwner?.RootItem?.Pointer==source.Root.Pointer;if(source.Loose==null||source.Loose.Item?.Pointer!=source.Root.Pointer)return false;var list=current.LootItems;if(list==null)return false;for(int i=0;i<list.Count;i++)if(list.GetByIndex(i)?.Pointer==source.Pointer)return true;return false;}
 static bool ClearAccess(Vector3 from,Vector3 point,Source target){
  var start=from+Vector3.up*.8f;var end=point+Vector3.up*.15f;var direction=end-start;
  if(!Physics.Raycast(start,direction.normalized,out var hit,Math.Max(0,direction.magnitude-.15f),LayerMask.GetMask("HighPolyCollider","Terrain","LowPolyCollider"),QueryTriggerInteraction.Ignore))return true;
  // A crate's own collider is the interaction target, not a wall in front of it.
  if(target.Loose!=null&&hit.collider?.GetComponentInParent<LootItem>()?.Pointer==target.Pointer)return true;
  return target.Container!=null&&hit.collider?.GetComponentInParent<LootableContainer>()?.Pointer==target.Pointer;
 }
 static bool FindApproach(Player actor,Source target,Vector3 point,out Vector3 approach){
  approach=default;if(!UnityEngine.AI.NavMesh.SamplePosition(actor.Position,out var origin,2,UnityEngine.AI.NavMesh.AllAreas))return false;
  float best=float.MaxValue;bool found=false;float heading=Mathf.Atan2(actor.Position.x-point.x,actor.Position.z-point.z);
  for(int i=0;i<13;i++){
   float angle=heading+(i-1)*Mathf.PI/4;float radius=i==0?0:i<=8?1.1f:1.6f;var candidate=point+new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*radius;
   if(!UnityEngine.AI.NavMesh.SamplePosition(candidate,out var sample,.8f,UnityEngine.AI.NavMesh.AllAreas)||Math.Abs(sample.position.y-point.y)>2||!ClearAccess(sample.position,point,target))continue;
   var path=new UnityEngine.AI.NavMeshPath();if(!UnityEngine.AI.NavMesh.CalculatePath(origin.position,sample.position,UnityEngine.AI.NavMesh.AllAreas,path)||path.status!=UnityEngine.AI.NavMeshPathStatus.PathComplete)continue;
   var corners=path.corners;float length=0;for(int c=1;c<corners.Length;c++)length+=Vector3.Distance(corners[c-1],corners[c]);
   if(length>=best)continue;best=length;approach=sample.position;found=true;
  }return found;
 }
 internal static bool Pending(CompanionSquad.Member member,GameWorld current,Player actor){var work=member.Work;
  if(work.Draining?.IsCompleted==true){VerifyPickup(member,actor);work.Draining=null;work.Requested.Clear();work.BeforeCounts.Clear();}
  if(work.Task!=null){if(!work.Task.IsCompleted){if(Time.unscaledTime-work.Started>35){try{work.Cancellation?.Cancel();}catch{}work.Draining=work.Task;retired.Add(new(work.Task,work.Cancellation));work.Task=null;work.Cancellation=null;if(work.Claimed.Length>0)work.Tried[work.Claimed]=Time.unscaledTime+60;Unclaim(member);member.Status="拾取未完成，已恢复行动";work.LastPickup="上次拾取超时，等待取消完成后再尝试";return false;}if(Time.unscaledTime-work.Started>30){try{work.Cancellation?.Cancel();}catch{}member.Status="拾取超时，等待原生操作取消";}else member.Status="正在拾取物资";return true;}
   if(work.VerifySince<0)work.VerifySince=Time.unscaledTime;if(Time.unscaledTime-work.VerifySince<.5f){member.Status="正在确认物品入包";return true;}
   bool confirmed=false;try{confirmed=VerifyPickup(member,actor);if(!confirmed&&work.Task.IsFaulted)member.Status="拾取失败："+work.Task.Exception?.GetBaseException()?.Message;FeatureToasts.Show(member.Name+"："+member.Status);}finally{work.Task=null;try{work.Cancellation?.Dispose();}catch{}work.Cancellation=null;if(work.Claimed.Length>0)work.Tried[work.Claimed]=Time.unscaledTime+(confirmed?60:8);Unclaim(member);work.Requested.Clear();work.BeforeCounts.Clear();work.CargoCount=Cargo(member,actor).Count();}
  }
  if(work.DropPending){if(!work.DropDone&&Time.unscaledTime-work.Started<15){member.Status="正在丢下战利品";return true;}
   bool stillOwned=actor.InventoryController.TryFindItem(work.DropId,out var item)&&item?.Pointer==work.DropPointer;
   if(!stillOwned){member.Delivered.UnionWith(work.DropContents);delivered.UnionWith(work.DropContents);work.CargoCount=Cargo(member,actor).Count();member.Status="已丢下战利品";}
   else{member.Status=work.DropDone&&!work.DropSucceeded?"丢物资失败："+work.DropError:"丢物资尚未完成，保留物品并停止本次交付";work.Returning=false;member.Order=CompanionOrder.Follow;}
   work.DropPending=false;work.DropDone=false;work.Callback=null;work.NextDrop=Time.unscaledTime+.5f;
  }
  return false;
 }
 internal static bool TryOffer(CompanionSquad.Member member,GameWorld current,Player actor,Player local){if(!CompanionPlans.AcceptOffers.Value||member.Work.Draining!=null)return false;Scan(current);if(member.Work.Target?.Offered!=true&&!sources.Any(s=>s.Offered&&!delivered.Contains(s.Id)&&(!claims.ContainsKey(s.Id)||claims[s.Id]==member.Actor.Profile)&&member.Work.Tried.GetValueOrDefault(s.Id)<=Time.unscaledTime&&(s.Point-actor.Position).sqrMagnitude<=64))return false;Search(member,current,actor,local,true);return member.Work.Target?.Offered==true||member.Work.Task!=null;}
 internal static void Search(CompanionSquad.Member member,GameWorld current,Player actor,Player local,bool offersOnly=false){var work=member.Work;if(work.Draining!=null){member.Status="等待上次拾取取消完成";return;}work.CargoCount=Cargo(member,actor).Count();if(!offersOnly&&(work.Returning||CompanionPlans.AutoDeliver.Value&&AutoCargo(member,actor).Any())){work.Returning=true;Deliver(member,current,actor,local,true);return;}
  if(work.Target!=null&&!Valid(work.Target,current))Unclaim(member);
  if(work.Target==null){if(Time.unscaledTime<work.NextSearch)return;work.NextSearch=Time.unscaledTime+1;Scan(current);float radius=CompanionPlans.LootRadius.Value;var choices=sources.Where(s=>!delivered.Contains(s.Id)&&!claims.ContainsKey(s.Id)&&work.Tried.GetValueOrDefault(s.Id)<=Time.unscaledTime&&(offersOnly?s.Offered&&(s.Point-actor.Position).sqrMagnitude<=64:(s.Point-local.Position).sqrMagnitude<=radius*radius)).OrderBy(s=>s.Offered?0:1).ThenBy(s=>(s.Point-actor.Position).sqrMagnitude).Take(16);foreach(var candidate in choices){if(!FindApproach(actor,candidate,candidate.Point,out var approach)){work.Tried[candidate.Id]=Time.unscaledTime+10;continue;}if(!Valid(candidate,current))continue;claims[candidate.Id]=member.Actor.Profile;work.Claimed=candidate.Id;work.Target=candidate;work.Approach=approach;work.SourcePosition=candidate.Point;work.Started=Time.unscaledTime;break;}if(work.Target==null){member.Status="范围内暂无可达物资，等待搜索";return;}}
  var target=work.Target;if(Time.unscaledTime-work.Started>45){work.Tried[target.Id]=Time.unscaledTime+60;Unclaim(member);member.Status="物资路线超时，已更换目标";return;}
  var targetPoint=target.Loose?.transform.position??target.Point;if((targetPoint-work.SourcePosition).sqrMagnitude>1){if(!FindApproach(actor,target,targetPoint,out work.Approach)){work.Tried[target.Id]=Time.unscaledTime+10;Unclaim(member);return;}work.SourcePosition=targetPoint;}if(!CompanionSquad.Navigate(member,actor,work.Approach,.7f)){member.Status=(target.Offered?"正在接收玩家丢下的物品 · ":"正在前往物资 · ")+Vector3.Distance(actor.Position,targetPoint).ToString("F0")+" 米";return;}
  if(!ClearAccess(actor.Position,targetPoint,target)){work.Tried[target.Id]=Time.unscaledTime+60;Unclaim(member);member.Status="物资被墙体遮挡，已跳过";return;}
  if(!Valid(target,current)){Unclaim(member);return;}CompanionSquad.StopRequest(member,actor);CompanionSquad.WaitAt(member,actor);work.Cancellation=new();work.Started=Time.unscaledTime;
  try{var looting=actor.AIData?.BotOwner?.Looting??throw new ArgumentException("队友原生拾取控制器尚未就绪。");var items=new Il2CppSystem.Collections.Generic.List<Item>();
   if(target.Kind==EBotLootSource.WorldItem)items.Add(target.Root);
   else if(target.Root.TryCast<InventoryEquipment>() is {} equipment){foreach(var slot in new[]{EquipmentSlot.FirstPrimaryWeapon,EquipmentSlot.SecondPrimaryWeapon,EquipmentSlot.Holster,EquipmentSlot.Headwear,EquipmentSlot.ArmorVest,EquipmentSlot.TacticalVest,EquipmentSlot.Backpack,EquipmentSlot.Pockets}){var item=equipment.GetSlot(slot)?.ContainedItem;if(item!=null&&!item.QuestItem)items.Add(item);}}
   else foreach(var item in LootDisplay.Children(target.Root))if(!item.QuestItem)items.Add(item);
   var weighted=new Il2CppSystem.Collections.Generic.List<BotLootingData.WeightedItem>();looting.ResetLootState();looting.UpdateBotContainersCache(actor.Profile.Inventory.Equipment);BotLootingData.SortItemsByWeight(items.Cast<Il2CppSystem.Collections.Generic.IEnumerable<Item>>(),weighted,false);
   BeforePickup(work,actor,items);work.Task=looting.LootingRemainingItemsAsync(target.Root,weighted,work.Cancellation.Token);member.Status="正在拾取物资";
  }catch{work.Cancellation.Dispose();work.Cancellation=null;work.Tried[target.Id]=Time.unscaledTime+30;Unclaim(member);work.NextSearch=Time.unscaledTime+2;throw;}
 }
 internal static void Deliver(CompanionSquad.Member member,GameWorld current,Player actor,Player local,bool automatic=false){var work=member.Work;var cargo=(automatic?AutoCargo(member,actor):Cargo(member,actor)).ToArray();work.CargoCount=Cargo(member,actor).Count();if(cargo.Length==0){work.Returning=false;Unclaim(member);member.Status="没有可交付的新物资";if(member.Order==CompanionOrder.Deliver)member.Order=CompanionOrder.Follow;return;}if(!CompanionSquad.Navigate(member,actor,local.Position,3)){member.Status="正在把物资送回玩家身边";return;}if(Time.unscaledTime<work.NextDrop)return;
  CompanionSquad.StopRequest(member,actor);CompanionSquad.WaitAt(member,actor);var item=cargo[0];if(actor.HandsController?.Item?.Pointer==item.Pointer||member.Original.Contains(item.Id))throw new ArgumentException("保留队友原有装备与手持物品。");var controller=actor.InventoryController.TryCast<Player.PlayerInventoryController>()??throw new ArgumentException("队友丢物品控制器尚未就绪。");work.DropId=item.Id;work.DropPointer=item.Pointer;work.DropPending=true;work.DropDone=false;work.DropSucceeded=false;work.DropError="";work.Started=Time.unscaledTime;string id=work.DropId;
  work.DropContents=Items(item).Select(i=>(string)i.Id).ToArray();work.Callback=(Action<IResult>)(result=>{if(work.DropId!=id)return;work.DropSucceeded=result.Succeed;work.DropError=result.Error??"";work.DropDone=true;});try{controller.ThrowItem(item,true,work.Callback);}catch{work.DropPending=false;work.Callback=null;work.NextDrop=Time.unscaledTime+5;throw;}
 }
}
