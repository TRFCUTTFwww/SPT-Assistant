using LocalGearMail;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Ragfair;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Utils;
internal static class ClearanceChecks {
 public static int Run(){
  int count=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
  var json=new JsonUtil([new SPTarkov.Server.Core.Utils.Json.SptJsonConverterRegistrator()]);
  var rub=new MongoId("5449016a4bdc2d6f028b456f");var usd=new MongoId("5696686a4bdc2da3298b456a");var eur=new MongoId("569668774bdc2da2298b4568");var barter=new MongoId("544fb25a4bdc2dfb738b4567");
  var ids=new[]{new MongoId(),new MongoId(),new MongoId(),new MongoId()};var currencies=new[]{rub,usd,eur,barter};
  var source=new TraderAssort{Items=ids.Select(id=>new Item{Id=id,Template=barter,ParentId="hideout",SlotId="hideout",Upd=new(){StackObjectsCount=5,BuyRestrictionMax=2,BuyRestrictionCurrent=1}}).ToList(),BarterScheme=ids.Select((id,i)=>(id,i)).ToDictionary(p=>p.id,p=>new List<List<BarterScheme>>{new(){new(){Template=currencies[p.i],Count=100+p.i}}}),LoyalLevelItems=ids.ToDictionary(id=>id,_=>3)};
  string original=json.Serialize(source)!;
  foreach(bool unlimited in new[]{false,true}){
   var copy=TraderSupplyRules.Copy(source,json,unlimited,true);
   for(int i=0;i<ids.Length;i++)Check(copy.BarterScheme![ids[i]][0][0].Template==currencies[i]&&copy.BarterScheme[ids[i]][0][0].Count==1,"trader clearance preserves currency/barter template "+currencies[i]);
   Check(json.Serialize(source)==original,"clearance never changes original trader prices or stock");
   Check(copy.LoyalLevelItems!.All(p=>p.Value==3),"clearance retains original loyalty requirements");
   Check(unlimited?copy.Items!.All(i=>i.Upd?.UnlimitedCount==true):copy.Items!.All(i=>i.Upd?.StackObjectsCount==5&&i.Upd.BuyRestrictionMax==2&&i.Upd.BuyRestrictionCurrent==1),"price and unlimited-stock switches remain independent");
  }
  Check(json.Serialize(TraderSupplyRules.Copy(source,json,false,false))==original,"turning clearance off exposes original prices exactly");
  var item=new Item{Id=new MongoId(),Template=barter,Upd=new(){StackObjectsCount=7,SpawnedInSession=false}};
  var offer=new RagfairOffer{Id=new MongoId(),Root=item.Id,Items=[item],User=new(){Id=new MongoId(),Nickname="Test seller"},CreatedBy=OfferCreator.FakePlayer,Requirements=currencies.Select(c=>new OfferRequirement{TemplateId=c,Count=500,OnlyFunctional=true,Level=10}).ToArray(),RequirementsCost=99999,SummaryCost=99999,ItemsCost=777,BuyRestrictionMax=4,BuyRestrictionCurrent=2,SellInOnePiece=true};
  string before=json.Serialize(offer)!;var cheap=ClearanceSales.Copy(offer,c=>c==rub?1:c==usd?100:c==eur?110:250);
  Check(cheap.Requirements!.Select(r=>r.TemplateId).SequenceEqual(currencies)&&cheap.Requirements.All(r=>r.Count==1&&r.OnlyFunctional==true&&r.Level==10),"market clearance retains all currencies, barter and requirement restrictions");
  Check(cheap.RequirementsCost==461&&cheap.SummaryCost==461,"market display keeps correct ruble conversion for original currency types");
  Check(cheap.Quantity==offer.Quantity&&cheap.Root==offer.Root&&cheap.ItemsCost==777&&cheap.SellInOnePiece==true&&cheap.BuyRestrictionMax==4&&cheap.BuyRestrictionCurrent==2,"market quote retains item tree, quantity, pack and purchase limits");
  Check(json.Serialize(offer)==before,"market price quote does not mutate global original offer");
  Check(!ClearanceSales.Eligible(offer with{CreatedBy=OfferCreator.Player},true,true),"saved human/player offers never get their sale price overwritten");
  Check(ClearanceSales.Eligible(offer,false,true)&&!ClearanceSales.Eligible(offer,true,false),"market-only seller follows only market toggle");
  Check(ClearanceSales.Eligible(offer with{CreatedBy=OfferCreator.Trader},true,false),"trader offers reflect trader clearance consistently");
  var other=new Item{Id=new MongoId(),Template=barter,Upd=new(){StackObjectsCount=3,SpawnedInSession=false}};var bag=new BotBaseInventory{Items=[item,other]};
  ItemFirRules.Apply(bag,item.Id.ToString(),true,0);Check(item.Upd!.SpawnedInSession==true&&other.Upd!.SpawnedInSession==false&&item.Upd.StackObjectsCount==7,"FIR toggle changes only selected inventory item flag");
  string marked=json.Serialize(bag)!;
  foreach(var invalid in new[]{(item.Id.ToString(),(double?)0),(new MongoId().ToString(),(double?)0),(item.Id.ToString(),(double?)null),(item.Id.ToString(),(double?)2)}){bool denied=false;try{ItemFirRules.Apply(bag,invalid.Item1,false,invalid.Item2);}catch(ArgumentException){denied=true;}Check(denied&&json.Serialize(bag)==marked,"stale/missing/invalid FIR requests preserve inventory");}
  ItemFirRules.Apply(bag,item.Id.ToString(),false,1);Check(item.Upd!.SpawnedInSession==false,"FIR removal restores the native flag");
  return count;
 }
}
