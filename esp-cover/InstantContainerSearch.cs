using System;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic.Operations;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class InstantContainerSearch {
 internal static ConfigEntry<bool> Enabled=null!;
 static float nextError;
 internal static void Initialize(Plugin plugin){
  Enabled=FeatureConfig.Bind("56. 修理与耐久","Instant container search","秒搜索容器",false,"本地玩家主动搜索容器、背包或胸挂时使用原生即时搜索流程；不自动打开远处容器，不改变物品或其他玩家的搜索。关闭后新搜索恢复原规则。");
  plugin.Patches.Patch(AccessTools.PropertyGetter(typeof(ActiveSearchContentOperation),nameof(ActiveSearchContentOperation.Instant)),postfix:new HarmonyMethod(typeof(InstantContainerSearch),nameof(AfterInstant)));
 }
 static void AfterInstant(ActiveSearchContentOperation __instance,ref bool __result){try{
  if(__result||!Enabled.Value||!RaidClock.Active||__instance.Terminated)return;
  var local=Singleton<GameWorld>.Instance?.MainPlayer;
  var search=local?.InventoryController?.TryCast<Player.PlayerInventoryController>()?.PlayerSearchController;
  if(local?.IsYourPlayer!=true||local.HealthController?.IsAlive!=true||search==null||__instance._searchController==null||__instance._profile==null)return;
  // Match the actual search controller as well as the profile: observing another
  // player's operation must never grant instant search to that operation.
  if(InstantSearchRules.Allowed(Enabled.Value,RaidClock.Active,local.HealthController.IsAlive,__instance.Terminated,search.Pointer.ToInt64(),__instance._searchController.Pointer.ToInt64(),local.ProfileId,__instance._profile.Id))__result=true;
 }catch(Exception error){if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+10;Plugin.Self.Log.LogWarning("Instant container search: "+error.Message);}}}
}
