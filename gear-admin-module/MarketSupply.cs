using HarmonyLib;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Ragfair;
using SPTarkov.Server.Core.Services.Ragfair;

namespace LocalGearMail;

internal static class MarketSupply {
 internal static bool Eligible(RagfairOffer offer)=>!offer.IsPlayerOffer()&&!offer.IsTraderOffer();
 internal static void Install(Harmony h){
  h.Patch(AccessTools.Method(typeof(RagfairController),nameof(RagfairController.GetOffers)),postfix:new HarmonyMethod(typeof(MarketSupply),nameof(Offers)));
  h.Patch(AccessTools.Method(typeof(RagfairController),nameof(RagfairController.GetOfferByInternalId)),postfix:new HarmonyMethod(typeof(MarketSupply),nameof(Single)));
  h.Patch(AccessTools.Method(typeof(RagfairOfferService),nameof(RagfairOfferService.GetOfferByOfferId)),postfix:new HarmonyMethod(typeof(MarketSupply),nameof(Single)));
  h.Patch(AccessTools.Method(typeof(RagfairOfferService),nameof(RagfairOfferService.ReduceOfferQuantity)),prefix:new HarmonyMethod(typeof(MarketSupply),nameof(Consume)));
 }
 internal static RagfairOffer Copy(RagfairOffer source){
  if(!Eligible(source))return source;
  var items=source.Items?.Select(i=>i.Id==source.Root?i with{Upd=(i.Upd??new Upd()) with{
   StackObjectsCount=int.MaxValue,OriginalStackObjectsCount=int.MaxValue,UnlimitedCount=true,BuyRestrictionMax=null,BuyRestrictionCurrent=null}}:i).ToList();
  return source with{Items=items,Quantity=int.MaxValue,UnlimitedCount=true,BuyRestrictionMax=null,BuyRestrictionCurrent=null};
 }
 static void Offers(ref GetOffersResult __result){
  if(MiscService.Options.UnlimitedMarketStock&&__result?.Offers!=null)__result=__result with{Offers=__result.Offers.Select(Copy).ToList()};
 }
 static void Single(ref RagfairOffer? __result){if(MiscService.Options.UnlimitedMarketStock&&__result!=null)__result=Copy(__result);}
 static bool Consume(RagfairOfferService __instance,MongoId __0)=>!MiscService.Options.UnlimitedMarketStock||__instance.GetOfferByOfferId(__0) is not {} offer||!Eligible(offer);
}
