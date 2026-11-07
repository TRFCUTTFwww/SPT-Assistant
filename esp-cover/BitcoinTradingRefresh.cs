using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.Trading;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using LocalRaidShared;
using UnityEngine;
namespace LocalSpt5Esp;

// Native item-price getters calculate correctly, but existing trading views and
// the prepared selling total cache their previous values until a UI refresh.
internal static class BitcoinTradingRefresh {
 sealed record Cache(SupplyData Supply,double Original);
 static readonly Dictionary<IntPtr,Cache> caches=new();
 static TraderDealScreen? screen;static IntPtr sessionPointer;static double? displayed;
 static bool dirty=true;static float nextError;static int changedCaches,changedViews,refreshes;
 static DateTime lastRefresh;
 internal static bool Busy=>screen!=null&&screen.gameObject.activeInHierarchy&&screen._trader?.CurrentAssortment?.TransactionInProgress==true;
 internal static void Initialize(Plugin plugin){
  screen=UnityEngine.Object.FindObjectOfType<TraderDealScreen>(true);
  plugin.Patches.Patch(AccessTools.Method(typeof(TraderDealScreen),"Show"),postfix:new HarmonyMethod(typeof(BitcoinTradingRefresh),nameof(Opened)));
  plugin.Patches.Patch(AccessTools.Method(typeof(TraderDealScreen),"Close"),prefix:new HarmonyMethod(typeof(BitcoinTradingRefresh),nameof(Closed)));
 }
 static void Opened(TraderDealScreen __instance){screen=__instance;dirty=true;BitcoinControl.RequestRefresh();}
 static void Closed(TraderDealScreen __instance){if(screen!=null&&screen.Pointer==__instance.Pointer){screen=null;dirty=true;}}
 internal static void Tick(double? target,double? original){try{
  if(Busy)return;
  var app=Singleton<TarkovApplication>.Instance??Singleton<AbstractApplication>.Instance?.TryCast<TarkovApplication>()??UnityEngine.Object.FindObjectOfType<TarkovApplication>();
  var session=app?.GetClientBackEndSession()?.TryCast<ITradingSession>();if(session==null)return;
  if(sessionPointer!=session.Pointer){Restore();sessionPointer=session.Pointer;dirty=true;}
  bool changed=false;changedCaches=0;
  foreach(var trader in session.Traders){
   var supply=trader?._supplyData;var prices=supply?.MarketPrices;
   if(supply==null||prices==null||!prices.ContainsKey(BitcoinRules.Template))continue;
   if(target is not double value){if(caches.Remove(supply.Pointer,out var saved)){prices[BitcoinRules.Template]=saved.Original;changed=true;changedCaches++;}continue;}
   if(!caches.ContainsKey(supply.Pointer))caches[supply.Pointer]=new(supply,original??prices[BitcoinRules.Template]);
   if(prices[BitcoinRules.Template]==value)continue;
   prices[BitcoinRules.Template]=value;changed=true;changedCaches++;
  }
  if(displayed!=target||changed)dirty=true;
  if(!dirty||screen==null||!screen.gameObject.activeInHierarchy)return;
  var current=screen._trader;var assortment=current?.CurrentAssortment;
  if(current==null||!current.PricesLoaded||assortment==null||assortment.TransactionInProgress)return;
  // This is a price/UI update, not an inventory operation. Keep the sell table,
  // selection, assortment and all non-BTC items intact.
  changedViews=0;
  foreach(var view in screen.GetComponentsInChildren<TradingPlayerItemView>(true)){
   if(view==null||view.Item==null||view.Item.TemplateId!=BitcoinRules.Template||view.Trader==null)continue;
   view.UpdateScheme();changedViews++;
  }
  bool hasBitcoin=false;var grid=assortment.SellingTableGrid;
  if(grid?.ContainedItems!=null)foreach(var entry in grid.ContainedItems)if(entry.Key?.TemplateId==BitcoinRules.Template){hasBitcoin=true;break;}
  if(hasBitcoin){
   var total=current.GetAssortmentPrice(assortment.SellingStash);
   if(!total.HasValue)throw new InvalidOperationException("BTC selling total could not be recalculated");
   assortment.PreparedSum=total.Value;assortment.PreparedSumChanged.Invoke();screen.OnPriceChanged();
  }
  dirty=false;displayed=target;refreshes++;lastRefresh=DateTime.UtcNow;
  Plugin.Self.Log.LogInfo($"BTC trading prices refreshed: base={target}, trader={current.Id}, views={changedViews}, preparedBitcoin={hasBitcoin}, total={assortment.PreparedSum.Amount}");
 }catch(Exception e){dirty=true;if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+15;Plugin.Self.Log.LogWarning("BTC trading refresh: "+e);}}}
 internal static object Diagnostics()=>new{trackedCaches=caches.Count,changedCaches,changedViews,refreshes,lastRefresh,screenActive=screen!=null&&screen.gameObject.activeInHierarchy,trader=screen?._trader?.Id,displayedBase=displayed};
 static void Restore(){foreach(var saved in caches.Values)try{var prices=saved.Supply?.MarketPrices;if(prices!=null&&prices.ContainsKey(BitcoinRules.Template))prices[BitcoinRules.Template]=saved.Original;}catch{}caches.Clear();}
 internal static void Reset(){Restore();screen=null;sessionPointer=IntPtr.Zero;displayed=null;dirty=true;}
}
