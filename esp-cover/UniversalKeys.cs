using System;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class UniversalKeys {
 internal static ConfigEntry<bool> Keys=null!,Cards=null!;
 static KeycardComponent? wrapper;
 static float nextError;
 sealed class MatchScope {
  readonly WorldInteractiveObject target;
  readonly string original;
  readonly CardReader? reader;
  readonly Il2CppReferenceArray<CardReader.KeyTriggerPair>? map;
  bool restored;
  internal MatchScope(WorldInteractiveObject target){this.target=target;original=target.KeyId;reader=target.TryCast<CardReader>();map=reader?._keyMap;}
  internal void Apply(KeyComponent key){
   target.KeyId=key.Template.KeyId;
   if(reader==null||map==null)return;
   // Preserve the reader's actual success trigger, including its linked door.
   CardReader.KeyTriggerPair? success=null;foreach(var pair in map)if(pair!=null&&pair.Success){success=pair;break;}
   if(success==null)return;
   var replacement=new Il2CppReferenceArray<CardReader.KeyTriggerPair>(map.Length+1);
   replacement[0]=new CardReader.KeyTriggerPair(key.Template.KeyId,success.TriggerId,true);
   for(int i=0;i<map.Length;i++)replacement[i+1]=map[i];reader._keyMap=replacement;
  }
  internal void Restore(){if(restored)return;restored=true;target.KeyId=original;if(reader!=null)reader._keyMap=map;}
 }
 internal static void Initialize(Plugin plugin){
  const string section="56. 修理与耐久";
  Keys=FeatureConfig.Bind(section,"Universal mechanical key","万能钥匙",false,"携带任意一把仍可使用的机械钥匙，即可尝试打开其他钥匙门和门禁卡门；使用原生开锁交互，次数由“不消耗次数”设置控制。");
  Cards=FeatureConfig.Bind(section,"Universal keycard","万能门禁卡",false,"携带任意一张仍可使用的门禁卡，即可尝试打开其他钥匙门和门禁卡门；与万能钥匙独立，使用原生刷卡或开锁交互。");
  plugin.Patches.Patch(AccessTools.Method(typeof(PlayerOwner),"GetKey"),postfix:new HarmonyMethod(typeof(UniversalKeys),nameof(SelectKey)));
  plugin.Patches.Patch(AccessTools.Method(typeof(PlayerOwner),"GetAllKeyCards"),postfix:new HarmonyMethod(typeof(UniversalKeys),nameof(SelectCards)));
  foreach(var type in new[]{typeof(WorldInteractiveObject),typeof(KeycardDoor),typeof(CardReader)})
   plugin.Patches.Patch(AccessTools.DeclaredMethod(type,"UnlockOperation"),prefix:new HarmonyMethod(typeof(UniversalKeys),nameof(BeforeUnlock)),postfix:new HarmonyMethod(typeof(UniversalKeys),nameof(AfterMatch)),finalizer:new HarmonyMethod(typeof(UniversalKeys),nameof(FinishMatch)));
  plugin.Patches.Patch(AccessTools.Method(typeof(CardReader),"ResolveReaction"),prefix:new HarmonyMethod(typeof(UniversalKeys),nameof(BeforeReaction)),postfix:new HarmonyMethod(typeof(UniversalKeys),nameof(AfterMatch)),finalizer:new HarmonyMethod(typeof(UniversalKeys),nameof(FinishMatch)));
  plugin.Log.LogInfo("Universal keys ready: independent carried-key/card options, native unlock operations and scoped lock matching");
 }
 static Player? Local()=>RaidClock.Active?Singleton<GameWorld>.Instance?.MainPlayer:null;
 static bool Available(KeyComponent? key){
  var item=key?.Item;if(item==null)return false;
  return UniversalKeyRules.Allowed(item.GetItemComponent<KeycardComponent>()!=null,Keys.Value,Cards.Value,key!.Template.MaximumNumberOfUsage,key.NumberOfUsages,item.StackObjectsCount);
 }
 static bool Owned(KeyComponent? key){var local=Local();return local?.IsYourPlayer==true&&local.HealthController?.IsAlive==true&&Available(key)&&local.Profile?.Inventory?.Equipment?.TryFindItem(key!.Item.Id,out var found)==true&&found?.Pointer==key.Item.Pointer;}
 static KeyComponent? Find(Player player){
  var equipment=player.Profile?.Inventory?.Equipment;if(equipment==null)return null;
  foreach(var item in ItemExtensions.GetAllItems(equipment)){var key=item.GetItemComponent<KeyComponent>();if(Available(key))return key;}
  return null;
 }
 static void SelectKey(PlayerOwner __instance,ref KeyComponent? __result){try{
  if((!Keys.Value&&!Cards.Value)||__result!=null||__instance.Player?.Pointer!=Local()?.Pointer)return;
  __result=Find(__instance.Player!);
 }catch(Exception error){Warn(error);}}
 static void SelectCards(PlayerOwner __instance,ref Il2CppSystem.Collections.Generic.IEnumerable<KeycardComponent> __result){try{
  if(!Keys.Value||__instance.Player?.Pointer!=Local()?.Pointer)return;
  KeyComponent? mechanical=null;var equipment=__instance.Player!.Profile?.Inventory?.Equipment;if(equipment==null)return;
  foreach(var item in ItemExtensions.GetAllItems(equipment)){var key=item.GetItemComponent<KeyComponent>();if(item.GetItemComponent<KeycardComponent>()==null&&Available(key)){mechanical=key;break;}}
  if(mechanical==null)return;
  // A temporary UI wrapper exposes the real carried key in a reader's item list.
  // It is never added to the item or inventory.
  if(wrapper?.Item?.Pointer!=mechanical.Item.Pointer)wrapper=new KeycardComponent(mechanical.Item,mechanical.Template);
  var choices=new Il2CppSystem.Collections.Generic.List<KeycardComponent>();if(__result!=null)foreach(var card in __result)choices.Add(card);choices.Add(wrapper);
  __result=choices.Cast<Il2CppSystem.Collections.Generic.IEnumerable<KeycardComponent>>();
 }catch(Exception error){Warn(error);}}
 static void BeforeUnlock(WorldInteractiveObject __instance,KeyComponent __0,Player __1,out MatchScope? __state){
  __state=null;if(__1?.Pointer!=Local()?.Pointer)return;BeginMatch(__instance,__0,out __state);
 }
 static void BeforeReaction(CardReader __instance,KeyComponent __0,out MatchScope? __state)=>BeginMatch(__instance,__0,out __state);
 static void BeginMatch(WorldInteractiveObject target,KeyComponent key,out MatchScope? scope){
  scope=null;try{if(!Owned(key))return;scope=new MatchScope(target);scope.Apply(key);}catch(Exception error){scope?.Restore();scope=null;Warn(error);}
 }
 static void AfterMatch(MatchScope? __state){try{__state?.Restore();}catch(Exception error){Warn(error);}}
 static void FinishMatch(MatchScope? __state)=>AfterMatch(__state);
 static void Warn(Exception error){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+10;Plugin.Self.Log.LogWarning("Universal keys: "+error.Message);}
 internal static void Reset(){wrapper=null;}
 internal static object Diagnostics()=>new{mechanical=Keys.Value,keycard=Cards.Value,carriedCandidate=Local() is {} player&&Find(player)!=null};
}
