using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using LocalGearMail;
using LocalMiscShared;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Ragfair;
using SPTarkov.Server.Core.Models.Eft.Trade;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Services.Ragfair;

internal static class NativeFleaChecks {
 public static int Run(){
  int count=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
  foreach(string role in new[]{"assault","bossBully","bossKilla","bossZryachiy","followerZryachiy","bossKnight","followerBigPipe","followerBirdEye","sectantPriest","sectantWarrior"})
   Check(MiscSettings.Diplomacy(true,true,true,true,true,role=="assault",role),"Scav diplomatic immunity includes "+role);
  foreach(var denied in new[]{(false,true,true,true,true,true,"assault"),(true,false,true,true,true,true,"assault"),(true,true,false,true,true,true,"bossKilla"),(true,true,true,false,true,true,"assault"),(true,true,true,true,false,true,"bossKilla"),(true,true,true,true,true,false,"pmcUSEC"),(true,true,true,true,true,false,"pmcBEAR"),(true,true,true,true,true,true,"shooterBTR")})
   Check(!MiscSettings.Diplomacy(denied.Item1,denied.Item2,denied.Item3,denied.Item4,denied.Item5,denied.Item6,denied.Item7),"immunity excludes disabled/non-raid/PMC/other targets/humans/BTR case "+count);
  var previous=MiscService.Options;var option=typeof(MiscService).GetProperty(nameof(MiscService.Options))!;
  var h=new Harmony("assistant.native-flea-regression");
  try{
   FleaAutomation.Install(h);
   h.Patch(AccessTools.Method(typeof(PaymentService),"PayMoney"),prefix:new HarmonyMethod(typeof(NativeFleaChecks),nameof(Payment)));
   var controller=(RagfairController)RuntimeHelpers.GetUninitializedObject(typeof(RagfairController));
   var tax=(RagfairTaxService)RuntimeHelpers.GetUninitializedObject(typeof(RagfairTaxService));
   typeof(RagfairTaxService).GetField("_playerOfferTaxCache",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(tax,new Dictionary<MongoId,StorePlayerOfferTaxAmountRequestData>());
   var config=(RagfairConfig)RuntimeHelpers.GetUninitializedObject(typeof(RagfairConfig));config.OfferListingTaxMultiplier=1;config.Sell=(Sell)RuntimeHelpers.GetUninitializedObject(typeof(Sell));
   foreach(var f in typeof(RagfairController).GetFields(BindingFlags.Instance|BindingFlags.NonPublic)){
    if(f.FieldType==typeof(RagfairTaxService))f.SetValue(controller,tax);
    if(f.FieldType==typeof(RagfairConfig))f.SetValue(controller,config);
    if(f.FieldType==typeof(PaymentService))f.SetValue(controller,RuntimeHelpers.GetUninitializedObject(typeof(PaymentService)));
    if(f.FieldType==typeof(ISptLogger<RagfairController>))f.SetValue(controller,DispatchProxy.Create<ISptLogger<RagfairController>,FleaQuietLog>());
   }
   var session=new MongoId();var item=new Item{Id=new MongoId(),Template=new MongoId()};var second=new MongoId();
   var pmc=new PmcData{Id=session,Inventory=new(){Items=[]}};var request=new AddOfferRequestData{Items=[item.Id,second],Requirements=[]};
   foreach(var scenario in new[]{(true,1,false,true),(false,0,false,true),(false,1,true,true),(true,1,false,true),(false,1,true,true),(false,1,false,false)}){
    option.SetValue(null,new MiscSettings{FreeFlea=scenario.Item1});config.OfferListingTaxMultiplier=scenario.Item2;config.Sell.Fees=scenario.Item4;
    tax.StoreClientOfferTaxValue(session,new(){Id=item.Id,Count=1,Fee=12345});tax.StoreClientOfferTaxValue(session,new(){Id=second,Count=1,Fee=6789});
    paid=null;var output=new ItemEventRouterResponse{Warnings=[],ProfileChanges=[]};
    var failed=(bool)AccessTools.Method(typeof(RagfairController),"ChargePlayerTaxFee").Invoke(controller,[session,item,pmc,10000d,1,request,output])!;
    Check(!failed&&output.Warnings.Count==0,"native tax boundary returns success with no carried cash");
    Check(scenario.Item3?paid==12345:paid==null,scenario.Item3?"disabling waiver restores native cached fee without bypassing payment":"waiver bypasses cached nonzero client fee and zero cash payment");
    Check(tax.GetStoredClientOfferTaxValueById(item.Id)==null,"consumed client fee cache is cleared");
    if(!scenario.Item3)Check(tax.GetStoredClientOfferTaxValueById(second)==null,"waiver clears every listed-item fee cache");
   }
  }finally{h.UnpatchSelf();option.SetValue(null,previous);}
  return count;
 }
 static double? paid;
 public static bool Payment(ProcessBuyTradeRequestData __1){paid=__1.SchemeItems!.Sum(i=>i.Count??0);return false;}
}
public class FleaQuietLog:DispatchProxy{protected override object? Invoke(MethodInfo? method,object?[]? args)=>method?.ReturnType==typeof(bool)?false:null;}
