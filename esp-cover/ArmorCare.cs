using System;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class ArmorCare {
 static ConfigEntry<bool> noWear=null!,merchant=null!,nesting=null!,keys=null!;static int? synced;static float nextSync,nextError;static bool keyHook;static Vector2 scroll;
 sealed record Wear(RepairableComponent Repairable,float Current,float Maximum);
 internal static void Initialize(Plugin plugin){const string section="56. 修理与耐久";
  noWear=FeatureConfig.Bind(section,"No equipped armor durability loss","头盔与防弹衣不掉耐久",true,"仅保护本地玩家当前穿戴的头盔、面罩附件、防弹衣和装甲胸挂；不修改 AI 或背包里的装备，也不改变命中与伤害计算。");
  merchant=FeatureConfig.Bind(section,"Trader repair without degradation","商人修复不损失最大耐久",true,"保留修复前的最大耐久，费用照常；不恢复历史最大耐久损耗，不影响修理包。");
  nesting=FeatureConfig.Bind(section,"Allow nested storage cases","允许物品箱嵌套",true,"允许物品箱放进其他物品箱，保持真实格数及防循环嵌套检查；修改后需重启服务端并重新登录游戏加载模板。");
  keys=FeatureConfig.Bind(section,"No key usage loss","钥匙与门禁卡不消耗次数",true,"使用已有钥匙时保留当前剩余次数，继续显示原始数值；按钥匙原本适配的锁具正常使用。");
  foreach(string name in new[]{"ApplyDurabilityDamage","ApplyExplosionDurabilityDamage"})plugin.Patches.Patch(AccessTools.Method(typeof(ArmorComponent),name),prefix:new HarmonyMethod(typeof(ArmorCare),nameof(BeforeWear)),postfix:new HarmonyMethod(typeof(ArmorCare),nameof(AfterWear)));
  plugin.Patches.Patch(AccessTools.PropertyGetter(typeof(RepairableComponent),"RepairDegradation"),postfix:new HarmonyMethod(typeof(ArmorCare),nameof(RepairPreview)));
  var perkType=AccessTools.TypeByName("EFT.PerkManager");var usage=perkType==null?null:AccessTools.Method(perkType,"GetKeyUsageConsumptionChance",new[]{typeof(KeyComponent)});
  if(usage!=null){plugin.Patches.Patch(usage,postfix:new HarmonyMethod(typeof(ArmorCare),nameof(KeyChance)));keyHook=true;}
  plugin.Log.LogInfo("Armor/key care ready: equipped local armor and native key consumption; key hook="+keyHook);
 }
 static bool Worn(ArmorComponent armor){
  if(!noWear.Value||!RaidClock.Active)return false;
  var local=Singleton<GameWorld>.Instance?.MainPlayer;var equipment=local?.Profile?.Inventory?.Equipment;var item=armor.Item;
  if(local?.IsYourPlayer!=true||local.HealthController?.IsAlive!=true||equipment==null||item==null)return false;
  foreach(var slot in new[]{EquipmentSlot.Headwear,EquipmentSlot.FaceCover,EquipmentSlot.ArmorVest,EquipmentSlot.TacticalVest}){
   var root=equipment.GetSlot(slot)?.ContainedItem;if(root==null)continue;
   if(root.Pointer==item.Pointer)return true;
   if(root.TryCast<ContainerCollection>() is {} container&&container.TryFindItem(item.Id,out var found)&&found?.Pointer==item.Pointer)return true;
  }
  return false;
 }
 static void BeforeWear(ArmorComponent __instance,out Wear? __state){__state=Worn(__instance)&&__instance.Repairable is {} repair?new(repair,repair.Durability,repair.MaxDurability):null;}
 static void AfterWear(ArmorComponent __instance,Wear? __state){if(__state==null)return;try{
  __state.Repairable.MaxDurability=Math.Max(__state.Repairable.MaxDurability,__state.Maximum);
  __state.Repairable.Durability=Math.Max(__state.Repairable.Durability,__state.Current);
  __instance.Item?.RaiseRefreshEvent(false,true);
 }catch(Exception error){Warn(error);}}
 static void RepairPreview(ref Vector2 __result){if(merchant.Value)__result=Vector2.zero;}
 static void KeyChance(KeyComponent __0,ref float __result){
  if(!keys.Value||!RaidClock.Active||__0?.Item==null)return;
  var local=Singleton<GameWorld>.Instance?.MainPlayer;var equipment=local?.Profile?.Inventory?.Equipment;
  if(local?.IsYourPlayer==true&&equipment!=null&&equipment.TryFindItem(__0.Item.Id,out var item)&&item?.Pointer==__0.Item.Pointer)__result=0;
 }
 internal static void Tick(){int state=(merchant.Value?1:0)|(nesting.Value?2:0);if(Time.unscaledTime<nextSync||synced==state)return;nextSync=Time.unscaledTime+3;try{var settings=NextRaidLoot.Read();if(settings.TraderRepairNoLoss!=merchant.Value||settings.AllowContainerNesting!=nesting.Value){settings.TraderRepairNoLoss=merchant.Value;settings.AllowContainerNesting=nesting.Value;NextRaidLoot.Save(settings);}synced=state;}catch(Exception error){Warn(error);}}
 static void Warn(Exception error){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+10;Plugin.Self.Error(error);}
 internal static void Draw(Rect rect){scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-20,610));DrawBody(new Rect(4,4,rect.width-32,594));GUI.EndScrollView();}
 static void DrawBody(Rect rect){float y=rect.y;InterfaceMenu.Label(new(rect.x,y,rect.width,32),"修理、耐久与箱子",InterfaceMenu.Accent,20);y+=46;
  foreach(var entry in new ConfigEntryBase[]{noWear,merchant,nesting,keys}){InterfaceMenu.DrawEntry(entry,new(rect.x,y,rect.width,84));y+=98;}
  bool ready=false;try{using var data=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(NextRaidLoot.Home,"server-status.json")));ready=data.RootElement.GetProperty("version").GetString()=="1.5.0";}catch{}
  InterfaceMenu.Paragraph(new(rect.x,y,rect.width,110),(ready?"商人修复服务端已加载。":"商人修复需要服务端模块 1.5.0；更新后重启服务端生效。")+"\n商人按原规则收费，修复前最大耐久保留；修理包保持原规则。头甲保护只作用于你当前穿戴的护甲及其护甲附件。"+(keyHook?"":"\n当前客户端没有可用的钥匙消耗接口，此版本无法启用钥匙保护。"),null,13);
 }
}
