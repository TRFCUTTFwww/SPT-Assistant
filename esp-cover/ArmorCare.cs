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
 static ConfigEntry<bool> noWear=null!,merchant=null!,nesting=null!,keys=null!;static int? synced;static float nextSync,nextError;static bool keyHook,entryHook=false;static Vector2 scroll;
 sealed record Wear(RepairableComponent Repairable,float Current,float Maximum);
 internal static void Initialize(Plugin plugin){const string section="56. 修理与耐久";
  noWear=FeatureConfig.Bind(section,"No equipped armor durability loss","头盔与防弹衣不掉耐久",true,"仅保护本地玩家当前穿戴的头盔、面罩附件、防弹衣和装甲胸挂；不修改 AI 或背包里的装备，也不改变命中与伤害计算。");
  merchant=FeatureConfig.Bind(section,"Trader repair without degradation","商人修复不损失最大耐久",true,"保留修复前的最大耐久，费用照常；不恢复历史最大耐久损耗，不影响修理包。");
  nesting=FeatureConfig.Bind(section,"Allow nested storage cases","允许物品箱嵌套",true,"允许物品箱放进其他物品箱，保持真实格数及防循环嵌套检查；修改后需重启服务端并重新登录游戏加载模板。");
  keys=FeatureConfig.Bind(section,"No key usage loss","钥匙与门禁卡不消耗次数",true,"保留已有钥匙的剩余次数，并保护进入实验室所需的门禁卡；仍须携带有效钥匙或门卡，关闭后恢复原本的消耗规则。");
  UniversalKeys.Initialize(plugin);
  InfiniteInjectors.Initialize(plugin);
  InstantContainerSearch.Initialize(plugin);
  foreach(string name in new[]{"ApplyDurabilityDamage","ApplyExplosionDurabilityDamage"})plugin.Patches.Patch(AccessTools.Method(typeof(ArmorComponent),name),prefix:new HarmonyMethod(typeof(ArmorCare),nameof(BeforeWear)),postfix:new HarmonyMethod(typeof(ArmorCare),nameof(AfterWear)));
  plugin.Patches.Patch(AccessTools.PropertyGetter(typeof(RepairableComponent),"RepairDegradation"),postfix:new HarmonyMethod(typeof(ArmorCare),nameof(RepairPreview)));
  var perkType=AccessTools.TypeByName("EFT.PerkManager");var usage=perkType==null?null:AccessTools.Method(perkType,"GetKeyUsageConsumptionChance",new[]{typeof(KeyComponent)});
  if(usage!=null){plugin.Patches.Patch(usage,postfix:new HarmonyMethod(typeof(ArmorCare),nameof(KeyChance)));keyHook=true;}
#if EFT_48558
  plugin.Patches.Patch(AccessTools.Method(typeof(LocationAccessRequirementsUtility),"TryConsumeAccessRequirement"),prefix:new HarmonyMethod(typeof(ArmorCare),nameof(EntryKey)));
  entryHook=true;
#endif
  plugin.Log.LogInfo("Armor/key care ready: equipped local armor and native key consumption; key hook="+keyHook+", entry hook="+entryHook);
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
#if EFT_48558
 // Validate through the native selector first: a missing, exhausted or ineligible card must still fail.
 static bool EntryKey(Il2CppSystem.Collections.Generic.IEnumerable<Item> __0,Il2CppSystem.Collections.Generic.IReadOnlyDictionary<string,int> __1,Il2CppSystem.Func<Item,bool> __2,ref bool __result){
  var decision=EntryDecision(keys.Value,__0,__1,__2);if(decision==null)return true;__result=decision.Value;return false;
 }
 static bool? EntryDecision(bool protect,Il2CppSystem.Collections.Generic.IEnumerable<Item> items,Il2CppSystem.Collections.Generic.IReadOnlyDictionary<string,int> requirements,Il2CppSystem.Func<Item,bool> eligible){
  if(!protect)return null;
  if(!LocationAccessRequirementsUtility.TryFindSatisfiedRequirement(items,requirements,eligible,out var item,out _,out _))return false;
  return item!=null&&(item.StringTemplateId=="5c94bbff86f7747ee735c08f"||item.GetItemComponent<KeyComponent>()!=null)?true:null;
 }
 internal static object EntryDiagnostics(TarkovApplication app){
  const string template="5c94bbff86f7747ee735c08f";
  var profile=app.GetProfileForLocalGame()??throw new InvalidOperationException("Profile unavailable");
  var cards=new Il2CppSystem.Collections.Generic.List<Item>();foreach(var item in profile.Inventory.GetAllItemByTemplate(template))cards.Add(item);
  var requirements=new Il2CppSystem.Collections.Generic.Dictionary<string,int>();requirements.Add(template,1);
  var request=requirements.Cast<Il2CppSystem.Collections.Generic.IReadOnlyDictionary<string,int>>();
  var eligible=Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Func<Item,bool>>(new Func<Item,bool>(_=>true))??throw new InvalidOperationException("Entry validation predicate unavailable");
  var available=cards.Cast<Il2CppSystem.Collections.Generic.IEnumerable<Item>>();var empty=new Il2CppSystem.Collections.Generic.List<Item>().Cast<Il2CppSystem.Collections.Generic.IEnumerable<Item>>();
  string Snapshot(){var rows=new System.Collections.Generic.List<string>();foreach(var card in cards)rows.Add(card.Id+":"+card.StackObjectsCount+":"+card.GetItemComponent<KeyComponent>()?.NumberOfUsages);return string.Join("|",rows);}
  string before=Snapshot();bool native=LocationAccessRequirementsUtility.HasAnyValidAccessRequirement(available,request,eligible);
  bool? protectedCard=EntryDecision(true,available,request,eligible),missing=EntryDecision(true,empty,request,eligible),disabled=EntryDecision(false,available,request,eligible);
  return new{enabled=keys.Value,entryHook,cards=cards.Count,nativeRequirementSatisfied=native,protectedCard,missingCard=missing,disabledUsesNative=disabled==null,inventoryUnchanged=before==Snapshot()};
 }

#endif
 internal static void Tick(){int state=(merchant.Value?1:0)|(nesting.Value?2:0);if(Time.unscaledTime<nextSync||synced==state)return;nextSync=Time.unscaledTime+3;try{var settings=NextRaidLoot.Read();if(settings.TraderRepairNoLoss!=merchant.Value||settings.AllowContainerNesting!=nesting.Value){settings.TraderRepairNoLoss=merchant.Value;settings.AllowContainerNesting=nesting.Value;NextRaidLoot.Save(settings);}synced=state;}catch(Exception error){Warn(error);}}
 static void Warn(Exception error){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+10;Plugin.Self.Error(error);}
 internal static void Draw(Rect rect){scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-20,1006));DrawBody(new Rect(4,4,rect.width-32,990));GUI.EndScrollView();}
 static void DrawBody(Rect rect){float y=rect.y;InterfaceMenu.Label(new(rect.x,y,rect.width,32),"修理、耐久与箱子",InterfaceMenu.Accent,20);y+=46;
  foreach(var entry in new ConfigEntryBase[]{noWear,merchant,nesting,keys,UniversalKeys.Keys,UniversalKeys.Cards,InfiniteInjectors.Enabled,InstantContainerSearch.Enabled}){InterfaceMenu.DrawEntry(entry,new(rect.x,y,rect.width,84));y+=98;}
  bool ready=false;try{using var data=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(NextRaidLoot.Home,"server-status.json")));ready=data.RootElement.GetProperty("version").GetString()=="1.5.0";}catch{}
  InterfaceMenu.Paragraph(new(rect.x,y,rect.width,110),(ready?"商人修复服务端已加载。":"商人修复需要服务端模块 1.5.0；更新后重启服务端生效。")+"\n商人按原规则收费，修复前最大耐久保留；修理包保持原规则。头甲保护只作用于你当前穿戴的护甲及其护甲附件。"+(keyHook?"":"\n当前客户端没有可用的钥匙消耗接口，此版本无法启用钥匙保护。"),null,13);
 }
}
