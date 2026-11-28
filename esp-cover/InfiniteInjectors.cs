using System;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace LocalSpt5Esp;

internal static class InfiniteInjectors {
 internal static ConfigEntry<bool> Enabled=null!;
 static float nextError;
 [ThreadStatic] static IntPtr protectingItem;

 sealed record MedState(Item Item,MedKitComponent? MedKit,float OriginalHp);

 internal static void Initialize(Plugin plugin){
  Enabled=FeatureConfig.Bind("56. 修理与耐久","Infinite injectors","无限注射器",false,"本地玩家使用注射器时不消耗剩余次数。");
  try{
   if(AccessTools.TypeByName("EFT.PerkManager") is {} perkType
    &&AccessTools.Method(perkType,"GetItemResourceUsageMultiplier",new[]{typeof(Item)}) is {} multiplierMethod){
    plugin.Patches.Patch(multiplierMethod,postfix:new HarmonyMethod(typeof(InfiniteInjectors),nameof(AfterResourceMultiplier)));
   }
   var medEffectType=typeof(ActiveHealthController.MedEffect);
   if(AccessTools.Method(medEffectType,"RegularUpdate") is {} regularUpdate)
    plugin.Patches.Patch(regularUpdate,prefix:new HarmonyMethod(typeof(InfiniteInjectors),nameof(BeforeRegularUpdate)));
   if(AccessTools.Method(medEffectType,"Residue") is {} residue)
    plugin.Patches.Patch(residue,
     prefix:new HarmonyMethod(typeof(InfiniteInjectors),nameof(BeforeResidue)),
     postfix:new HarmonyMethod(typeof(InfiniteInjectors),nameof(AfterResidue)),
     finalizer:new HarmonyMethod(typeof(InfiniteInjectors),nameof(FinishResidue)));
   if(AccessTools.TypeByName("EFT.HealthSystem.MedEffectHelper") is {} helperType
    &&AccessTools.Method(helperType,"RemoveItem",new[]{typeof(Item)}) is {} removeMethod){
    plugin.Patches.Patch(removeMethod,prefix:new HarmonyMethod(typeof(InfiniteInjectors),nameof(BeforeRemoveMedItem)));
   }
   plugin.Patches.Patch(AccessTools.PropertySetter(typeof(MedKitComponent),nameof(MedKitComponent.HpResource)),
    prefix:new HarmonyMethod(typeof(InfiniteInjectors),nameof(BeforeResourceSetter)));
   plugin.Log.LogInfo("Infinite injectors ready: native MedEffect residue/multiplier and MedEffectHelper.RemoveItem protection");
  }catch(Exception error){
   plugin.Log.LogError("Infinite injectors patch failed: "+error);
  }
 }

 static bool IsInjectorItem(Item? item){
  if(item==null)return false;
  if(item.TryCast<Stimulator>()!=null)return true;
  return ItemCatalog.IsChildOf(item.StringTemplateId,"5448f3a64bdc2d60728b456a");
 }

 static bool ShouldProtect(Item? item){
  if(!Enabled.Value||!RaidClock.Active||!IsInjectorItem(item))return false;
  var local=Singleton<GameWorld>.Instance?.MainPlayer;
  if(local?.IsYourPlayer!=true||local.HealthController?.IsAlive!=true)return false;
  var equipment=local.Profile?.Inventory?.Equipment;
  if(equipment!=null&&equipment.TryFindItem(item!.Id,out var owned)&&owned?.Pointer==item.Pointer)return true;
  return local.HandsController?.Item?.Pointer==item!.Pointer;
 }

 static void AfterResourceMultiplier(Item __0,ref float __result){
  try{if(ShouldProtect(__0))__result=0f;}catch(Exception error){Warn(error);}
 }

 static void BeforeRegularUpdate(ActiveHealthController.MedEffect __instance){
  try{if(ShouldProtect(__instance?.MedItem))__instance!._itemResourceUsageMultiplier=0f;}catch(Exception error){Warn(error);}
 }

 static void BeforeResidue(ActiveHealthController.MedEffect __instance,out MedState? __state){
  __state=null;
  try{
   var item=__instance?.MedItem;
   if(item==null||!ShouldProtect(item))return;
   __instance!._itemResourceUsageMultiplier=0f;
   protectingItem=item.Pointer;
   var medKit=__instance._medKit;
   float hp=medKit!=null?Math.Max(1f,medKit.HpResource):0f;
   __state=new(item,medKit,hp);
  }catch(Exception error){Warn(error);}
 }

 static void AfterResidue(MedState? __state){
  protectingItem=IntPtr.Zero;
  if(__state==null)return;
  try{
   if(__state.MedKit!=null&&__state.OriginalHp>0f&&__state.MedKit.HpResource<__state.OriginalHp)
    __state.MedKit.HpResource=__state.OriginalHp;
   __state.Item.RaiseRefreshEvent(false,true);
  }catch(Exception error){Warn(error);}
 }

 static void FinishResidue()=>protectingItem=IntPtr.Zero;

 static bool BeforeRemoveMedItem(Item __0,ref bool __result){
  try{
   if((protectingItem!=IntPtr.Zero&&__0?.Pointer==protectingItem)||ShouldProtect(__0)){
    __result=false;
    return false;
   }
  }catch(Exception error){Warn(error);}
  return true;
 }

 static void BeforeResourceSetter(MedKitComponent __instance,ref float __0){
  try{
   if(!Enabled.Value||!RaidClock.Active)return;
   float current=__instance.HpResource;
   if(!float.IsFinite(current)||current<=0||!float.IsFinite(__0)||__0>=current)return;
   if(ShouldProtect(__instance.Item))__0=current;
  }catch(Exception error){Warn(error);}
 }

 static void Warn(Exception error){
  if(Time.unscaledTime<nextError)return;
  nextError=Time.unscaledTime+10;
  Plugin.Self.Log.LogWarning("Infinite injectors: "+error.Message);
 }
}
