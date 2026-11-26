using System;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class InfiniteInjectors {
 internal static ConfigEntry<bool> Enabled=null!;
 static float nextError;
 internal static void Initialize(Plugin plugin){
  Enabled=FeatureConfig.Bind("56. 修理与耐久","Infinite injectors","无限注射器",false,"本地玩家使用注射器时不消耗剩余次数。");
  plugin.Patches.Patch(AccessTools.PropertySetter(typeof(MedKitComponent),nameof(MedKitComponent.HpResource)),prefix:new HarmonyMethod(typeof(InfiniteInjectors),nameof(BeforeResource)));
 }
 static void BeforeResource(MedKitComponent __instance,ref float __0){try{
  if(!Enabled.Value||!RaidClock.Active)return;
  float current=__instance.HpResource;if(!float.IsFinite(current)||current<=0||!float.IsFinite(__0)||__0>=current)return;
  var item=__instance.Item;var local=Singleton<GameWorld>.Instance?.MainPlayer;
  if(item==null||local?.IsYourPlayer!=true||local.HealthController?.IsAlive!=true)return;
  // The actual template ancestry excludes drinks, painkillers and medical kits.
  if(!ItemCatalog.IsChildOf(item.StringTemplateId,"5448f3a64bdc2d60728b456a"))return;
  if(local.Profile?.Inventory?.Equipment?.TryFindItem(item.Id,out var owned)==true&&owned?.Pointer==item.Pointer)__0=current;
 }catch(Exception error){if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+10;Plugin.Self.Log.LogWarning("Infinite injectors: "+error.Message);}}}
}
