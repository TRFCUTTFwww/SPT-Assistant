using System.Reflection;
using HarmonyLib;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Ragfair;
using SPTarkov.Server.Core.Services.Ragfair;
namespace LocalGearMail;
public static class ClearanceSales {
 [ThreadStatic]static bool marketPurchase;
 internal static bool InMarketPurchase=>marketPurchase;
 static readonly FieldInfo price=typeof(RagfairController).GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Single(f=>f.FieldType==typeof(RagfairPriceService));
 public static bool CheapTrader=>MiscService.Options.ClearanceTraders||marketPurchase&&MiscService.Options.ClearanceMarket;
 public static void Install(Harmony h){
  h.Patch(AccessTools.Method(typeof(RagfairController),"GetOffers"),postfix:new HarmonyMethod(typeof(ClearanceSales),nameof(Offers)));
  h.Patch(AccessTools.Method(typeof(RagfairController),"GetOfferByInternalId"),postfix:new HarmonyMethod(typeof(ClearanceSales),nameof(Single)));
  h.Patch(AccessTools.Method(typeof(TradeController),"ConfirmRagfairTrading"),prefix:new HarmonyMethod(typeof(ClearanceSales),nameof(BeginBuy)),finalizer:new HarmonyMethod(typeof(ClearanceSales),nameof(EndBuy)));
 }
 public static bool Eligible(RagfairOffer offer,bool traders,bool market)=>!offer.IsPlayerOffer()&&(offer.IsTraderOffer()?traders||market:market);
 // Only the quote is detached. Keep the offer identity, stock, item tree,
 // availability and expiry intact; never overwrite a saved player listing.
 public static RagfairOffer Copy(RagfairOffer source,Func<MongoId,double> value){
  var requirements=(source.Requirements??[]).Select(r=>r with{Count=1}).ToArray();
  double converted=requirements.Sum(r=>value(r.TemplateId));
  return source with{Requirements=requirements,RequirementsCost=converted,SummaryCost=converted};
 }
 static RagfairOffer Quote(RagfairController controller,RagfairOffer offer){
  if(!Eligible(offer,MiscService.Options.ClearanceTraders,MiscService.Options.ClearanceMarket))return offer;
  var native=(RagfairPriceService)price.GetValue(controller)!;
  return Copy(offer,id=>native.GetStaticPriceForItem(id)??native.GetFleaPriceForItem(id));
 }
 static void Offers(RagfairController __instance,ref GetOffersResult __result){if(__result?.Offers==null||!MiscService.Options.ClearanceTraders&&!MiscService.Options.ClearanceMarket)return;__result=__result with{Offers=__result.Offers.Select(o=>Quote(__instance,o)).ToList()};}
 static void Single(RagfairController __instance,ref RagfairOffer __result){if(__result!=null)__result=Quote(__instance,__result);}
 static void BeginBuy(out bool __state){__state=marketPurchase;marketPurchase=true;}
 static void EndBuy(bool __state){marketPurchase=__state;}
}
