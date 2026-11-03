using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class AiEquipmentRemoval {
 sealed class Request {internal LiveActorReference Actor=null!;internal EquipmentSlot Slot;internal Item Item=null!;internal string Id="",Name="";internal float Started;internal bool Begun,Ready,Committed;internal string Error="";internal Callback<IEmptyHandsController>? Callback;}
 static readonly Queue<Request> queue=new();static Request? pending;static bool batch;static int actors,total,removed,failed,empty;static readonly List<string> errors=new();
 internal static bool Busy=>pending!=null||queue.Count>0;internal static string Status{get;private set;}="";
 internal static bool Blocks(Player? actor)=>pending is {} request&&actor!=null&&actor.Pointer==request.Actor.Pointer&&actor.ProfileId==request.Actor.Profile;
 internal static void Reset(){pending=null;queue.Clear();batch=false;errors.Clear();Status="";}
 static Request Capture(LiveActorReference actor,EquipmentSlot slot,Item item)=>new(){Actor=actor,Slot=slot,Item=item,Id=item.Id,Name=LootDisplay.ItemName(item,false)};
 static Player Resolve(Request request){var actor=request.Actor.Resolve();if(actor.IsYourPlayer||!(actor.IsAI||actor.AIData?.BotOwner!=null)||!AiEquipmentRemovalRules.Allowed(request.Slot.ToString()))throw new ArgumentException("请选择 AI 的头甲或武器槽。");var current=actor.Profile?.Inventory?.Equipment?.GetSlot(request.Slot)?.ContainedItem;if(current==null||!AiEquipmentRemovalRules.Matches(request.Id,request.Item.Pointer.ToInt64(),current.Id,current.Pointer.ToInt64()))throw new ArgumentException("该装备已被更换或移动，未删除新的装备。");return actor;}
 internal static void Start(LiveActorReference actor,EquipmentSlot slot,Item item){if(Busy||AiSceneEquipment.Busy)throw new ArgumentException("正在处理 AI 装备队列，请稍候。");var request=Capture(actor,slot,item);Resolve(request);batch=false;pending=request;Tick();}
 internal static void RemoveWeapons(GameWorld current,IEnumerable<Player> selected){
  if(Busy||AiSceneEquipment.Busy)throw new ArgumentException("正在处理 AI 装备队列，请稍候。");var targets=selected.Where(p=>!ReferenceEquals(p,null)).GroupBy(p=>p.Pointer).Select(g=>g.First()).ToArray();if(targets.Length==0)throw new ArgumentException("请先勾选 AI。");if(targets.Length>256)throw new ArgumentException("单次最多处理 256 名 AI。");
  actors=targets.Length;total=removed=failed=empty=0;errors.Clear();batch=true;
  foreach(var actor in targets)try{var reference=new LiveActorReference(current,actor);reference.Resolve();if(reference.Local||!(actor.IsAI||actor.AIData?.BotOwner!=null))throw new ArgumentException("只能删除 AI 武器。");int count=0;foreach(string name in AiEquipmentRemovalRules.WeaponSlots){var slot=Enum.Parse<EquipmentSlot>(name);var item=actor.Profile?.Inventory?.Equipment?.GetSlot(slot)?.ContainedItem;if(item==null)continue;queue.Enqueue(Capture(reference,slot,item));count++;total++;}if(count==0)empty++;}catch(Exception e){failed++;errors.Add("AI #"+actor.Pointer+"："+e.Message);}
  Progress();FeatureToasts.Show(Status);if(queue.Count==0)batch=false;
 }
 static void Progress(){Status=$"所选 {actors} 名 AI：武器 {total} 件，已删除 {removed} 件，待处理 {queue.Count+(pending==null?0:1)} 件，未装备武器 {empty} 名，失败 {failed}。";if(errors.Count>0)Status+=" "+string.Join("；",errors.Take(2));}
 static void Done(Request request,Exception? error){pending=null;if(batch){if(request.Committed)removed++;if(error!=null){failed++;errors.Add(request.Actor.Name+" / "+request.Name+"："+error.Message);}Progress();if(queue.Count==0){batch=false;FeatureToasts.Show(Status);Plugin.Self.Log.LogInfo("AI weapon removal batch: "+Status);}}else{Status=error==null?"已删除："+request.Name+"（含配件）。":(request.Committed?"装备已删除，刷新提示：":"删除未完成：")+error.Message;FeatureToasts.Show(Status);}if(error!=null)Plugin.Self.Log.LogWarning("AI equipment removal: "+request.Actor.Name+" / "+request.Name+": "+error.Message);AiTestControl.EquipmentNotice(Status);AiActorControl.EquipmentNotice(Status);try{AiEquipmentEditor.RemovalFinished(request.Actor);}catch{}}
 internal static void Tick(){
  if(pending==null&&queue.Count>0)pending=queue.Dequeue();var request=pending;if(request==null)return;
  try{
   var target=Resolve(request);
   if(!request.Begun){request.Begun=true;request.Started=Time.unscaledTime;if(!batch)Status="正在删除 "+request.Actor.Name+" 的 "+request.Name;if(target.HandsController?.Item?.Pointer==request.Item.Pointer){target.HandsController.TryCast<Player.FirearmController>()?.SetTriggerPressed(false);target.AIData?.BotOwner?.StopMove();request.Callback=(Action<Result<IEmptyHandsController>>)(result=>{if(!ReferenceEquals(pending,request))return;request.Error=result.Succeed?"":result.Error??"AI 未能收起当前武器。";request.Ready=true;});target.Proceed(false,request.Callback,true);}else request.Ready=true;}
   if(Time.unscaledTime-request.Started>20)throw new ArgumentException("AI 收起武器超时，装备未删除，请稍后重试。");if(!request.Ready)return;if(request.Error.Length>0)throw new ArgumentException(request.Error);if(target.HandsController?.Item?.Pointer==request.Item.Pointer)return;
   var controller=target.InventoryController??throw new ArgumentException("AI 库存控制器暂不可用。");var result=ItemManipulator.RemoveWithoutRestrictions(request.Item,controller);if(!result.Succeeded)throw new ArgumentException("游戏未能移除这件装备。");request.Committed=true;result.Value.RaiseEvents(controller.Cast<IItemOwner>(),CommandStatus.Succeed);
   var equipment=target.Profile?.Inventory?.Equipment;if(equipment?.GetSlot(request.Slot)?.ContainedItem?.Pointer==request.Item.Pointer||equipment?.TryFindItem(request.Id,out var stillPresent)==true&&stillPresent?.Pointer==request.Item.Pointer){request.Committed=false;throw new InvalidOperationException("装备移除后仍存在，请刷新检查。");}
   AiEquipmentEditor.ForgetRemoved(request.Item);WeaponEditor.ForgetRemoved(request.Item);target.RecalculateEquipmentParams();target.Physical?.MarkWeightAsDirty();
   if(request.Item.TryCast<Weapon>()!=null||request.Slot==EquipmentSlot.Scabbard){var manager=target.AIData?.BotOwner?.WeaponManager;manager?.UpdateWeaponsList();manager?.Selector?.FindItemsInSlots(true);manager?.Selector?.UpdateWeaponsList();if(request.Slot==EquipmentSlot.Scabbard&&manager?.Melee is {} melee){melee.HaveMelee=false;melee.MeleeWeaponEquipped=false;melee.KnifeController=null!;}}
   Done(request,null);
  }catch(Exception e){Done(request,e);}
 }
}