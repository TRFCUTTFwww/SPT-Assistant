using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HarmonyLib;
using LocalGearMail;
using LocalMiscShared;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Ragfair;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Services.Ragfair;
using SPTarkov.Server.Core.Utils;

internal static class CustomFleaChecks {
 internal static void Run(){
  var json=new JsonUtil([new SPTarkov.Server.Core.Utils.Json.SptJsonConverterRegistrator()]);
  var template=new TemplateItem{Id=new MongoId(),Type="Item",Properties=new(){MaxDurability=100}};
  CustomFleaOffers.Draft Validate(object data)=>CustomFleaOffers.Validate(JsonSerializer.SerializeToElement(data),template);
  var draft=Validate(new{playerName="  Test Seller  ",unitPrice=12345,stock=37,remainingMinutes=73,rating=456.75,durability=31.5,maxDurability=82});
  void Check(bool valid,string message){if(!valid)throw new Exception(message);Console.WriteLine("PASS "+message);}
  void Reject(object data){try{Validate(data);}catch(ArgumentException){return;}throw new Exception("Invalid quote accepted: "+JsonSerializer.Serialize(data));}
  Reject(draft with{PlayerName=""});Reject(draft with{PlayerName="a\nb"});Reject(draft with{Durability=null});
  var bound=json.Deserialize<CharacterDataRequest>(JsonSerializer.Serialize(new{action="fleaOfferAdd",template=template.Id.ToString(),fleaOffer=draft}))!;
  Check(CustomFleaOffers.Validate(bound.FleaOffer,template)==draft,"native request serializer binds custom quote fields; malformed data are rejected");
  Check(Validate(draft with{UnitPrice=int.MinValue,Stock=int.MaxValue,RemainingMinutes=int.MaxValue,Rating=-1000001,Durability=200,MaxDurability=-5}).Rating==-1000001,"developer values are accepted beyond former quote caps");
  Reject(new{playerName="Test",unitPrice=2147483648L});
  var root=new Item{Id=new MongoId(),Template=template.Id,ParentId="hideout",SlotId="hideout",Upd=new(){StackObjectsCount=2,BuyRestrictionMax=1}};
  var child=new Item{Id=new MongoId(),Template=new MongoId(),ParentId=root.Id.ToString(),SlotId="mod_magazine",Upd=new(){StackObjectsCount=1}};
  var offer=new RagfairOffer{Id=new MongoId(),InternalId=17,Root=root.Id,Items=[root,child],User=new(){Id=new MongoId()},CreatedBy=OfferCreator.FakePlayer};
  CustomFleaOffers.Apply(offer,draft);
  var roundtrip=json.Deserialize<RagfairOffer>(json.Serialize(offer))!;
  Check(roundtrip.User.Nickname=="Test Seller"&&roundtrip.User.Rating==456.75&&roundtrip.EndTime-roundtrip.StartTime==73*60&&roundtrip.Items![0].Upd!.Repairable!.Durability==31.5&&roundtrip.Items[0].Upd!.Repairable!.MaxDurability==82,"seller, reputation, expiry and condition survive native quote serialization");
  Check(roundtrip.Requirements!.Single().Count==12345&&roundtrip.Quantity==37&&roundtrip.Items![0].Upd!.StackObjectsCount==37&&CustomFleaOffers.IsCustom(roundtrip),"custom stock, unit price and quote marker survive native serialization");
  var permanent=json.Deserialize<RagfairOffer>(json.Serialize(offer))!;
  CustomFleaOffers.Apply(permanent,draft with{RemainingMinutes=int.MinValue});
  Check(CustomFleaOffers.Permanent(permanent)&&permanent.EndTime>DateTimeOffset.UtcNow.ToUnixTimeSeconds(),"any negative expiry creates a permanent native quote");
  var paused=json.Deserialize<RagfairOffer>(json.Serialize(offer))!;CustomFleaOffers.Apply(paused,draft with{Paused=true});
  paused=json.Deserialize<RagfairOffer>(json.Serialize(paused))!;
  Check(CustomFleaOffers.PausedRemaining(paused)==73*60&&!CustomFleaOffers.Permanent(paused)&&paused.EndTime==CustomFleaOffers.PermanentEndTime,"paused temporary quotes retain frozen seconds through native serialization without becoming permanent");
  CustomFleaOffers.Apply(paused,draft);Check(CustomFleaOffers.PausedRemaining(paused)==null&&paused.EndTime-paused.StartTime==73*60,"resuming ordinary quote creation removes the pause marker");
  string baseline=json.Serialize(offer)!;
  var copy=MarketSupply.Copy(offer);
  Check(copy.Quantity==int.MaxValue&&copy.Items![0].Upd!.StackObjectsCount==int.MaxValue&&copy.UnlimitedCount==true&&copy.Items[0].Upd!.BuyRestrictionMax==null,"market oversupply uses Int32.MaxValue and clears purchase limits");
  Check(json.Serialize(offer)==baseline&&copy.Items![1].Upd!.StackObjectsCount==1,"market oversupply preserves original stock and attachment contents");
  foreach(var creator in new[]{OfferCreator.Player,OfferCreator.Trader})Check(!MarketSupply.Eligible(offer with{CreatedBy=creator}),"market stock excludes "+creator);
  var holder=new RagfairOfferHolder(DispatchProxy.Create<ISptLogger<RagfairOfferHolder>,FleaQuietLog>(),null!,null!,null!);
  var service=(RagfairOfferService)RuntimeHelpers.GetUninitializedObject(typeof(RagfairOfferService));
  typeof(RagfairOfferService).GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Single(f=>f.FieldType==typeof(RagfairOfferHolder)).SetValue(service,holder);
  service.AddOffer(offer);
  var h=new Harmony("assistant.custom-flea-tests");var previous=MiscService.Options;var setting=typeof(MiscService).GetProperty(nameof(MiscService.Options))!;
  try{
   MarketSupply.Install(h);
   setting.SetValue(null,new MiscSettings{UnlimitedMarketStock=true});
   Check(service.GetOfferByOfferId(offer.Id)!.Items![0].Upd!.StackObjectsCount==int.MaxValue,"native purchase lookup receives unlimited market stock");
   service.ReduceOfferQuantity(offer.Id,30);
   setting.SetValue(null,new MiscSettings{UnlimitedTraderPurchases=true});
   Check(service.GetOfferByOfferId(offer.Id)!.Items![0].Upd!.StackObjectsCount==37,"purchases preserve stock while enabled; merchant switch alone restores finite market stock");
   service.ReduceOfferQuantity(offer.Id,7);
   Check(service.GetOfferByOfferId(offer.Id)!.Quantity==30,"disabling market oversupply restores native stock consumption");
   service.RemoveOfferById(offer.Id);
   Check(!service.DoesOfferExist(offer.Id),"custom quote removal clears the native offer pool");
  }finally{h.UnpatchSelf();setting.SetValue(null,previous);}
 }
}
