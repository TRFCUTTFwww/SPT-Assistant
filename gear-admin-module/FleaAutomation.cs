using System.Reflection;
using HarmonyLib;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Helpers.Ragfair;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Ragfair;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Services.Ragfair;
using SPTarkov.Server.Core.Services.Locales;
namespace LocalGearMail;
public static class FleaAutomation {
 static readonly FieldInfo tax=Field<RagfairTaxService>(),config=Field<RagfairConfig>(),helper=Field<RagfairOfferHelper>();
 static readonly FieldInfo locale=typeof(RagfairOfferHelper).GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Single(f=>f.FieldType==typeof(LocaleService));
 [ThreadStatic]static int assistantSale;
 static FieldInfo Field<T>()=>typeof(RagfairController).GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Single(f=>f.FieldType==typeof(T));
 public static void Install(Harmony h){
  h.Patch(AccessTools.Method(typeof(RagfairController),"ChargePlayerTaxFee"),prefix:new HarmonyMethod(typeof(FleaAutomation),nameof(Charge)));
  h.Patch(AccessTools.Method(typeof(RagfairController),"AddPlayerOffer"),prefix:new HarmonyMethod(typeof(FleaAutomation),nameof(BeforeAdd)),postfix:new HarmonyMethod(typeof(FleaAutomation),nameof(AfterAdd)));
  h.Patch(AccessTools.Method(typeof(RagfairOfferHelper),"GetLocalisedOfferSoldMessage"),prefix:new HarmonyMethod(typeof(FleaAutomation),nameof(SaleMessage)));
 }
 static bool Charge(RagfairController __instance,AddOfferRequestData __5,ref bool __result){
  var settings=(RagfairConfig)config.GetValue(__instance)!;
  if(!MiscService.Options.FreeFlea&&settings.OfferListingTaxMultiplier!=0&&settings.Sell?.Fees!=false)return true;
  // EFT 48558 normally submits its own cached fee; this boundary also covers
  // that path. A zero payment needs no cash stack or inventory mutation.
  var service=(RagfairTaxService)tax.GetValue(__instance)!;
  foreach(var id in __5.Items??[])service.ClearStoredOfferTaxById(id);
  __result=false; // Native true means an error; false permits offer creation.
  return false;
 }
 static void BeforeAdd(PmcData __0,out HashSet<MongoId>? __state){__state=MiscService.Options.InstantFleaSales?(__0.RagfairInfo?.Offers??[]).Select(o=>o.Id).ToHashSet():null;}
 static void AfterAdd(RagfairController __instance,PmcData __0,MongoId __2,ItemEventRouterResponse __result,HashSet<MongoId>? __state){
  if(__state==null||(__result.Warnings?.Count??0)>0)return;
  var offers=(__0.RagfairInfo?.Offers??[]).Where(o=>!__state.Contains(o.Id)).ToArray();if(offers.Length==0)return;
  var native=(RagfairOfferHelper)helper.GetValue(__instance)!;
  assistantSale++;
  try{foreach(var offer in offers){int amount=offer.SellInOnePiece==true?1:offer.Quantity;if(amount<=0)continue;
   native.CompleteOffer(__2,offer,amount);
   if(__result.ProfileChanges?.TryGetValue(__2,out var change)==true)change.RagFairOffers?.RemoveAll(o=>o.Id==offer.Id);
  }}finally{assistantSale--;}
 }
 static bool SaleMessage(RagfairOfferHelper __instance,MongoId __0,int __1,ref string __result){
  if(assistantSale<=0)return true;
  var dictionary=((LocaleService)locale.GetValue(__instance)!).GetLocaleDb(null);
  string template=dictionary.GetValueOrDefault("5bdabfb886f7743e152e867e 0")??"{buyerNickname} purchased {itemCount} × {soldItem}.";
  string name=dictionary.GetValueOrDefault(__0+" Name")??__0.ToString();
  __result=template.Replace("{soldItem}",name).Replace("{itemCount}",__1.ToString(System.Globalization.CultureInfo.InvariantCulture)).Replace("{buyerNickname}","Assistant");return false;
 }
}
