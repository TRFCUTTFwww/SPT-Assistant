using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class AiSceneEquipment {
 sealed class Job {internal LiveActorReference Actor=null!;internal ItemAttachmentNode Tree=null!;internal EquipmentSlot Slot;internal bool Started,Ready,Paused,Committed,DrawDone;internal float Since;internal string Error="";internal Callback<IEmptyHandsController>? Empty;internal Callback<IFirearmHandsController>? Draw;}
 static readonly Queue<Job> queue=new();static Job? active;static ConfigEntry<bool> birth=null!;static ConfigEntry<string> weapon=null!;static string search="",notice="";static Vector2 scroll;static EquipmentSlot slot=EquipmentSlot.FirstPrimaryWeapon;
 internal static void Initialize(){birth=FeatureConfig.Bind("49. AI 动作导演","Birth player weapon","出生使用玩家枪械预设",false);weapon=FeatureConfig.Bind("49. AI 动作导演","Birth weapon tree","出生枪械预设数据","");}
 internal static bool Busy=>active!=null||queue.Count>0;
 internal static ItemAttachmentNode? BirthWeapon=>birth.Value&&weapon.Value.Length>0?JsonSerializer.Deserialize<ItemAttachmentNode>(weapon.Value):null;
 internal static ItemAttachmentNode? ToolWeapon=>weapon.Value.Length>0?JsonSerializer.Deserialize<ItemAttachmentNode>(weapon.Value):null;
 internal static void LoadBirth(ItemAttachmentNode? tree){weapon.Value=tree==null?"":JsonSerializer.Serialize(tree);birth.Value=tree!=null;}
 internal static ItemAttachmentNode Clone(ItemAttachmentNode tree)=>JsonSerializer.Deserialize<ItemAttachmentNode>(JsonSerializer.Serialize(tree))!;
 static Weapon Build(ItemAttachmentNode tree){var factory=Singleton<ItemFactory>.Instance??throw new ArgumentException("物品工厂未就绪。");var item=factory.CreateItem(factory.NextId.ToString(),tree.Template,null)?.TryCast<Weapon>()??throw new ArgumentException("预设不是枪械。");tree.Parameters.Apply(item);ItemAttachments.Apply(item,factory,tree);return item;}
 internal static Dictionary<string,ItemAttachmentNode?> Capture(Player actor){var result=new Dictionary<string,ItemAttachmentNode?>();var slots=actor.Profile?.Inventory?.Equipment?.Slots;if(slots!=null)foreach(var slot in slots)if(slot!=null)result[slot.ID]=slot.ContainedItem is {} item?LoadoutPlans.CaptureItem(item):null;return result;}
 internal static void Prepare(Profile profile,AiBirthPreset preset){
  var equipment=profile.Inventory?.Equipment;var factory=Singleton<ItemFactory>.Instance;if(equipment==null||factory==null)return;
  if(preset.Equipment!=null)foreach(var pair in preset.Equipment){if(!Enum.TryParse<EquipmentSlot>(pair.Key,out var type))continue;var target=equipment.GetSlot(type);if(target==null)continue;var tree=pair.Value;Item? item=null;if(tree!=null){item=factory.CreateItem(factory.NextId.ToString(),tree.Template,null);tree.Parameters.Apply(item);ItemAttachments.Apply(item,factory,tree);}if(target.ContainedItem!=null&&!target.RemoveItemWithoutRestrictions().Succeeded)throw new ArgumentException("无法还原装备槽："+pair.Key);if(item!=null&&!target.AddWithoutRestrictions(item).Succeeded)throw new ArgumentException("装备槽还原失败："+pair.Key);}
  if(preset.Weapon!=null){var gun=Build(preset.Weapon);var target=equipment.GetSlot(EquipmentSlot.FirstPrimaryWeapon);if(target.ContainedItem!=null&&!target.RemoveItemWithoutRestrictions().Succeeded)throw new ArgumentException("无法替换出生主武器。");if(!target.Add(gun,false,true).Succeeded)throw new ArgumentException("出生枪械无法放入主武器槽。");}
  if(preset.Appearance!=null){var custom=new BodyCustomization();foreach(var pair in preset.Appearance)custom[Enum.Parse<EBodyModelPart>(pair.Key)]=new MongoID(pair.Value);profile.Customization=custom;}
  if(!string.IsNullOrWhiteSpace(preset.Name))profile.Info.Nickname=preset.Name;
 }
 internal static void Enqueue(GameWorld current,Player actor,ItemAttachmentNode tree,EquipmentSlot target=EquipmentSlot.FirstPrimaryWeapon){if(AiEquipmentRemoval.Busy)throw new ArgumentException("正在删除 AI 装备，请稍候。");if(queue.Count>32)throw new ArgumentException("装备队列已满。");new LiveActorReference(current,actor).Resolve();queue.Enqueue(new(){Actor=new(current,actor),Tree=Clone(tree),Slot=target});notice="已加入 AI 枪械队列。";}
 static void Finish(){if(active?.Started==true)try{AiTestControl.SetPaused(new[]{active.Actor.Resolve()},active.Paused);}catch{}active=null;}
 internal static void Reset(){Finish();queue.Clear();}
 internal static void Tick(GameWorld? current){if(current==null){Reset();return;}if(active==null&&queue.Count>0)active=queue.Dequeue();var job=active;if(job==null)return;
  try{var actor=job.Actor.Resolve();if(job.Committed){if(job.DrawDone||Time.unscaledTime-job.Since>20)Finish();return;}if(!job.Started){job.Started=true;job.Since=Time.unscaledTime;job.Paused=AiTestControl.IsPaused(actor);AiTestControl.SetPaused(new[]{actor},true);var old=actor.Profile.Inventory.Equipment.GetSlot(job.Slot).ContainedItem;if(old!=null&&actor.HandsController?.Item?.Pointer==old.Pointer){job.Empty=(Action<Result<IEmptyHandsController>>)(result=>{job.Ready=true;job.Error=result.Succeed?"":result.Error??"AI 收起武器失败。";});actor.HandsController.TryCast<Player.FirearmController>()?.SetTriggerPressed(false);actor.Proceed(false,job.Empty,true);}else job.Ready=true;}
   if(Time.unscaledTime-job.Since>20)throw new ArgumentException("AI 收起武器超时。");if(!job.Ready)return;if(job.Error.Length>0)throw new ArgumentException(job.Error);
   var target=actor.Profile.Inventory.Equipment.GetSlot(job.Slot);var oldItem=target.ContainedItem;if(oldItem!=null&&actor.HandsController?.Item?.Pointer==oldItem.Pointer)return;
   var gun=Build(job.Tree);var controller=actor.InventoryController;var address=target.CreateItemAddress();
   if(oldItem!=null){var removed=ItemManipulator.RemoveWithoutRestrictions(oldItem,controller);if(!removed.Succeeded)throw new ArgumentException("旧武器未能移除。");removed.Value.RaiseEvents(controller.Cast<IItemOwner>(),CommandStatus.Succeed);}
   var added=ItemManipulator.Add(gun,address,controller,true);if(!added.Succeeded){if(oldItem!=null){ItemManipulator.Add(oldItem,address,controller,true);controller.AddAndRaiseEvents(oldItem,address);}throw new ArgumentException("预设枪械无法装备，已尝试恢复旧武器。");}controller.AddAndRaiseEvents(gun,address);actor.RecalculateEquipmentParams();actor.Physical?.MarkWeightAsDirty();actor.AIData?.BotOwner?.WeaponManager?.UpdateWeaponsList();actor.AIData?.BotOwner?.WeaponManager?.Selector?.FindItemsInSlots(true);actor.AIData?.BotOwner?.WeaponManager?.Selector?.UpdateWeaponsList();
   job.Committed=true;job.Since=Time.unscaledTime;job.Draw=(Action<Result<IFirearmHandsController>>)(result=>{job.DrawDone=true;if(!result.Succeed)Plugin.Self.Log.LogWarning("AI equip draw: "+result.Error);});actor.Proceed(gun,job.Draw,true);notice="已给 "+actor.Profile.Nickname+" 装备玩家预设枪械。";
  }catch(Exception e){notice=e.Message;Plugin.Self.Log.LogWarning("AI scene equipment: "+e.Message);Finish();}}
 internal static void Draw(Rect rect){scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-20,1000));float x=8,y=8,w=rect.width-36;InterfaceMenu.Label(new Rect(x,y,w,30),"AI 玩家预设枪械",InterfaceMenu.Accent,21);y+=46;InterfaceMenu.DrawEntry(birth,new Rect(x,y,w,80));y+=92;
  if(InterfaceMenu.Button(new Rect(x,y,w,32),"目标槽："+slot)){slot=slot==EquipmentSlot.FirstPrimaryWeapon?EquipmentSlot.SecondPrimaryWeapon:slot==EquipmentSlot.SecondPrimaryWeapon?EquipmentSlot.Holster:EquipmentSlot.FirstPrimaryWeapon;}y+=44;search=InterfaceMenu.Field(new Rect(x,y,w,32),search);y+=44;
  foreach(var entry in PlayerWeaponPresets.SceneEntries().Where(e=>e.Name.Contains(search,StringComparison.OrdinalIgnoreCase))){InterfaceMenu.Label(new Rect(x,y,w,26),entry.Name,null,14);y+=32;float half=(w-12)/2;if(InterfaceMenu.Button(new Rect(x,y,half,32),"用于出生／模板"))try{weapon.Value=JsonSerializer.Serialize(PlayerWeaponPresets.SceneWeapon(entry.Id));birth.Value=true;notice="出生预设："+entry.Name;}catch(Exception e){notice=e.Message;}
   if(InterfaceMenu.Button(new Rect(x+half+12,y,half,32),"装备到所选 AI"))try{var current=Singleton<GameWorld>.Instance!;var targets=AiTestControl.PoseSelection;if(targets.Length==0)throw new ArgumentException("请先选择 AI。");var tree=PlayerWeaponPresets.SceneWeapon(entry.Id);foreach(var actor in targets)Enqueue(current,actor,tree,slot);}catch(Exception e){notice=e.Message;}y+=48;}
  InterfaceMenu.Paragraph(new Rect(x,y,w,72),notice+"\n读取当前登录玩家在改枪界面保存的预设。替换前收起旧枪，保留配件和弹药。",InterfaceMenu.Accent,13);GUI.EndScrollView();}
}
