using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;
using EFT.Trading;
using EFT.UI.Ragfair;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class FleaFees {
 static bool configFree;static bool? previous;static float next;
 internal static bool Free=>MiscPanel.Options.FreeFlea||configFree;
 internal static void Initialize(Plugin plugin){
  plugin.Patches.Patch(AccessTools.Method(typeof(PriceCalculator),"CalculateTaxPrice"),prefix:new HarmonyMethod(typeof(FleaFees),nameof(Quote)));
  plugin.Patches.Patch(AccessTools.Method(typeof(AddOfferWindow),"EnsureTaxCorrect"),prefix:new HarmonyMethod(typeof(FleaFees),nameof(Correct)));
  plugin.Patches.Patch(AccessTools.Method(typeof(AddOfferWindow),"SetPrices"),postfix:new HarmonyMethod(typeof(FleaFees),nameof(RefreshFreeOffer)));
 }
 static void RefreshFreeOffer(AddOfferWindow __instance){
  if(!Free||__instance._addOfferButton==null||__instance._selectedItem==null||__instance._inventoryController==null||__instance._ragfair==null)return;
  // EFT 48558 SetPrices requires tax > 0 in addition to affordability.
  // Zero tax is affordable without cash. Retain its other native gates:
  // every RequirementView must produce a requirement, and the item tree must
  // pass the game's recursive category/item-level check. AddOffer itself
  // still performs restrictions and price-warning confirmation unchanged.
  var views=__instance._dynamicRequirementViews;
  bool ready=views!=null&&views.All(view=>view!=null&&view.Requirement!=null)
   &&__instance.OfferItemCount>0&&double.IsFinite(__instance._requirementsCost)&&__instance._requirementsCost>0
   &&!__instance._ragfair.IsItemOrChildLockedByLevelRecursive(__instance._selectedItem,out _)
   &&(__instance._loader==null||!__instance._loader.activeInHierarchy);
  __instance._addOfferButton.Interactable=ready;
 }
 static bool Quote(ref double __result){if(!Free)return true;__result=0;return false;}
 static bool Correct()=>!Free;
 internal static void Tick(){
  if(Time.unscaledTime>=next){next=Time.unscaledTime+2;try{using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(BepInEx.Paths.GameRootPath,"SPT_Runtime","SPT_Data","configs","ragfair.json")));var root=json.RootElement;configFree=root.GetProperty("offerListingTaxMultiplier").GetInt32()==0||root.TryGetProperty("sell",out var sell)&&sell.TryGetProperty("fees",out var fees)&&!fees.GetBoolean();}catch{configFree=false;}}
  bool current=Free;if(previous==current)return;previous=current;
  foreach(var window in UnityEngine.Object.FindObjectsOfType<AddOfferWindow>())if(window!=null&&window.gameObject.activeInHierarchy&&window._inventoryController!=null&&window._selectedItem!=null){window.SetPrices();window.UpdateMoneyCount();}
 }
 internal static void Reset(){previous=null;next=0;configFree=false;}
}
