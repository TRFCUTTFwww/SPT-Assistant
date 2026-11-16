using System;
using System.IO;
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
