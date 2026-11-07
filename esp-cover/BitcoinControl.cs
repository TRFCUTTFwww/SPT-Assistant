using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using EFT;
using EFT.HandBook;
using EFT.Trading;
using EFT.InventoryLogic;
using HarmonyLib;
using LocalRaidShared;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class BitcoinControl {
 static BitcoinStatus? status;static float nextRead;static string notice="";static double? price,baseline;[ThreadStatic]static bool compareOriginal;
 internal static double? Price=>price;
 internal static void RequestRefresh()=>nextRead=0;
 internal static void Reset(){BitcoinTradingRefresh.Reset();price=baseline=null;status=null;}
 internal static void Initialize(Plugin plugin){BitcoinTradingRefresh.Initialize(plugin);plugin.Patches.Patch(AccessTools.Method(typeof(Trader),"GetUserItemPrice",new[]{typeof(Item)}),postfix:new HarmonyMethod(typeof(BitcoinControl),nameof(TraderPrice)));plugin.Patches.Patch(AccessTools.Method(typeof(Handbook),"GetBasePrice"),prefix:new HarmonyMethod(typeof(BitcoinControl),nameof(HandbookPrice)));plugin.Patches.Patch(AccessTools.Method(typeof(SupplyData),"GetBasePrice"),prefix:new HarmonyMethod(typeof(BitcoinControl),nameof(SupplyPrice)));}
 // Preserve native eligibility and currency; only replace the BTC amount after pricing succeeds.
 static void TraderPrice(Trader __instance,Item __0,ref Il2CppSystem.Nullable<Trader.ItemPrice> __result){
  if(compareOriginal||price is not double live||__0?.TemplateId!=BitcoinRules.Template||!__result.HasValue)return;
  var native=__result.Value;var currency=native.CurrencyId;
  if(!currency.HasValue)return;
  double rate=currency.Value.ToString()=="5449016a4bdc2d6f028b456f"?1:__instance._supplyData.GetBasePrice(currency.Value);
  if(!double.IsFinite(rate)||rate<=0)return;
  if(BitcoinRules.TraderOffer(live,__0.StackObjectsCount,rate) is not int amount)return;
  native.Amount=amount;__result=new Il2CppSystem.Nullable<Trader.ItemPrice>(native);
 }
 static bool SupplyPrice(MongoID __0,ref double __result){if(compareOriginal||__0.ToString()!=BitcoinRules.Template||(price??baseline) is not double live)return true;__result=live;return false;}
 internal static object Diagnostics(TarkovApplication app){
  nextRead=0;Tick();var session=app.GetClientBackEndSession().TryCast<EFT.Trading.ITradingSession>()??throw new InvalidOperationException("Trading session unavailable");var profile=app.GetProfileForLocalGame();EFT.InventoryLogic.Item? coin=null;
  if(profile?.Inventory!=null)foreach(var item in profile.Inventory.GetAllItemByTemplate(BitcoinRules.Template)){coin=item;break;}
  var rows=new System.Collections.Generic.List<object>();foreach(var trader in session.Traders){var supply=trader._supplyData;double? cached=supply?.MarketPrices!=null&&supply.MarketPrices.ContainsKey(BitcoinRules.Template)?supply.MarketPrices[BitcoinRules.Template]:null;int? before=null,after=null,disabledOffer=null;double? native=null,resolved=null,disabledBase=null;
   try{compareOriginal=true;if(supply!=null)native=supply.GetBasePrice(new MongoID(BitcoinRules.Template));if(coin!=null&&trader.PricesLoaded){var value=trader.GetUserItemPrice(coin);if(value.HasValue)before=value.Value.Amount;}}finally{compareOriginal=false;}
   if(supply!=null)resolved=supply.GetBasePrice(new MongoID(BitcoinRules.Template));if(coin!=null&&trader.PricesLoaded){var value=trader.GetUserItemPrice(coin);if(value.HasValue)after=value.Value.Amount;}
   var savedPrice=price;try{price=null;if(supply!=null)disabledBase=supply.GetBasePrice(new MongoID(BitcoinRules.Template));if(coin!=null&&trader.PricesLoaded){var value=trader.GetUserItemPrice(coin);if(value.HasValue)disabledOffer=value.Value.Amount;}}finally{price=savedPrice;}
   rows.Add(new{id=trader.Id,loaded=trader.PricesLoaded,cachedBase=cached,originalSupplyBase=native,liveSupplyBase=resolved,originalOffer=before,liveOffer=after,disabledBase,disabledOffer});
  }return new{status,appliedBase=price,stackQuantity=coin?.StackObjectsCount,tradingRefresh=BitcoinTradingRefresh.Diagnostics(),hasInventoryBitcoin=coin!=null,traders=rows};
 }
 static bool HandbookPrice(MongoID __0,ref double __result){if(compareOriginal||__0.ToString()!=BitcoinRules.Template||(price??baseline) is not double live)return true;__result=live;return false;}
 internal static void Tick(){if(Time.unscaledTime<nextRead)return;nextRead=Time.unscaledTime+2;if(BitcoinTradingRefresh.Busy)return;try{status=JsonSerializer.Deserialize<BitcoinStatus>(File.ReadAllText(Path.Combine(NextRaidLoot.Home,"bitcoin-status.json")));bool running=status!=null&&Process.GetProcessesByName("SPT.Server").Any(p=>p.Id==status.ServerPid);baseline=running&&status!.OriginalGameRub is >0?status.OriginalGameRub:null;price=running&&status!.Enabled&&BitcoinRules.Valid(status.Quote)?status.Quote!.GameRub:null;}catch{price=baseline=null;status=null;}BitcoinTradingRefresh.Tick(price??baseline,baseline);}
 static void Action(Action action){try{action();}catch(Exception e){notice=e.Message;FeatureToasts.Show(notice);}}
 internal static void Draw(Rect rect){float x=rect.x,y=rect.y,w=rect.width;InterfaceMenu.Label(new Rect(x,y,w,32),"真实 BTC 行情",InterfaceMenu.Accent,21);y+=48;bool enabled=status?.Enabled??NextRaidLoot.Read().LiveBitcoinEnabled;if(InterfaceMenu.Button(new Rect(x,y,300,36),enabled?"BTC 联网定价 · 开启":"BTC 联网定价 · 关闭",enabled))Action(()=>{var settings=NextRaidLoot.Read();settings.LiveBitcoinEnabled=!enabled;NextRaidLoot.Save(settings);notice="设置已保存，正在等待服务端同步。";FeatureToasts.Show("BTC 联网定价",settings.LiveBitcoinEnabled);nextRead=0;});y+=54;InterfaceMenu.Paragraph(new Rect(x,y,w,70),"1 BTC 的真实卢布报价 × 0.2 = 每枚游戏 BTC 的商人收购价。不再额外乘商人折扣；美元、欧元按游戏原汇率换算，商人的收购资格保持原规则。",null,14);y+=90;if(status?.Quote is {} quote){InterfaceMenu.Paragraph(new Rect(x,y,w,120),"1 BTC = ₽"+quote.Rub.ToString("N2")+"\n美元参考：$"+quote.Usd.ToString("N2")+"\n每枚 BTC 收购价：₽"+quote.GameRub.ToString("N0")+"\n获取时间："+quote.FetchedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")+" · "+quote.Source,InterfaceMenu.Accent,15);y+=140;}else{InterfaceMenu.Label(new Rect(x,y,w,38),"尚未取得行情，继续使用原游戏价格。",null,14);y+=54;}if(InterfaceMenu.Button(new Rect(x,y,230,36),"立即联网更新行情"))Action(()=>{Directory.CreateDirectory(NextRaidLoot.Home);File.WriteAllText(Path.Combine(NextRaidLoot.Home,"bitcoin-refresh.request"),DateTime.UtcNow.ToString("O"));notice="已请求更新行情。";FeatureToasts.Show(notice);});y+=56;InterfaceMenu.Paragraph(new Rect(x,y,w,100),notice+"\n"+(status?.Error??"")+"\n每 5 分钟更新；断网时保留最后一次成功行情。商人收购价随行情自动更新。",null,12);}
}
