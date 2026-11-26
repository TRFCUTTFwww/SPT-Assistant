using LocalGearMail;
using LocalSpt.Shared;
using System.Text.Json;
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

  using var props=System.Text.Json.JsonDocument.Parse("""{"Durability":90,"MaxDurability":100,"MaxHpResource":400}""");
  InventoryItemEdit.Apply(item,new GearAttributes{MaxDurability=65,MedicalResource=120,FoundInRaid=true},props.RootElement,json);
  Check(item.Upd!.Repairable?.Durability==90&&item.Upd.Repairable.MaxDurability==65
   &&item.Upd.MedKit?.HpResource==120&&item.Upd.SpawnedInSession==true
   &&item.Upd.StackObjectsCount==7&&other.Upd!.SpawnedInSession==false,
   "inventory editor saves selected properties with template defaults and preserves the other item");
  string edited=json.Serialize(bag)!;
  bool rejected=false;
  try{InventoryItemEdit.Apply(item,new GearAttributes{Durability=45,ToggledOn=true},props.RootElement,json);}
  catch(ArgumentException){rejected=true;}
  Check(rejected&&json.Serialize(bag)==edited,"unsupported inventory edit leaves earlier fields untouched");
  InventoryItemEdit.Apply(item,new GearAttributes{ToggledOn=true},props.RootElement,json,supportsToggle:true);
  Check(item.Upd.Togglable?.On==true,"a supported toggle can be saved before the first native toggle");
  edited=json.Serialize(bag)!;
  foreach(string wrongTemplate in new[]{new MongoId().ToString(),""}){
   rejected=false;
   try{InventoryItemEdit.Apply(bag,item.Id.ToString(),wrongTemplate,new GearAttributes{FoundInRaid=false},null!,json);}
   catch(ArgumentException){rejected=true;}
   Check(rejected&&json.Serialize(bag)==edited,"inventory editor rejects the wrong template for an existing ID");
  }

  using var dogtagProps=JsonDocument.Parse("""{"DogTagQualities":true}""");
  var dogtagId=new MongoId("59f32c3b86f77472a31742f0");
  var tagItem=new Item{Id=new MongoId(),Template=dogtagId,Upd=new(){SpawnedInSession=true,Dogtag=new(){
   Nickname="Original",Time="2026-11-24T10:30:00Z",Side=DogtagSide.NotApplicable,Level=0,Status="",
   AccountId="known-owner",ProfileId="known-profile",KillerName="Known killer",WeaponName="Known weapon",
   ExtensionData=new(){["extraDogtagProperty"]=true}
  }}};
  var requestedTag=new DogtagEdit("Updated name","2026-11-24T18:30:26.125+08:00","BEAR",42,"Killed by");
  string wire=JsonSerializer.Serialize(new{action="editInventoryDogtag",dogtag=requestedTag});
  var request=json.Deserialize<CharacterDataRequest>(wire)!;
  Check(request.Action=="editInventoryDogtag"&&request.Dogtag==requestedTag,"dogtag fields bind from the client JSON request");
  InventoryItemEdit.Apply(tagItem,null,dogtagProps.RootElement,json,dogtag:request.Dogtag);
  var savedTag=json.Deserialize<Item>(json.Serialize(tagItem)!)!.Upd!.Dogtag!;
  Check(savedTag.Nickname=="Updated name"&&savedTag.Time=="2026-11-24T10:30:26.1250000Z"
   &&savedTag.Side==DogtagSide.Bear&&savedTag.Level==42&&savedTag.Status=="Killed by",
   "all five dogtag fields survive the native save round trip");
  Check(tagItem.Template==dogtagId&&tagItem.Upd!.SpawnedInSession==true&&savedTag.AccountId=="known-owner"
   &&savedTag.ProfileId=="known-profile"&&savedTag.KillerName=="Known killer"&&savedTag.WeaponName=="Known weapon"
   &&savedTag.ExtensionData!.ContainsKey("extraDogtagProperty"),"dogtag editing preserves ownership, killer metadata, template and FIR");
  InventoryItemEdit.Apply(tagItem,null,dogtagProps.RootElement,json,dogtag:new(Status:""));
  Check(tagItem.Upd!.Dogtag!.Status==""&&tagItem.Upd.Dogtag.Nickname=="Updated name"&&tagItem.Upd.Dogtag.Level==42,
   "a status-only edit can clear the status without altering other dogtag fields");
  const string weaponKey="5447a9cd4bdc2dbd208b4567 ShortName";
  var weaponRequest=new DogtagEdit(WeaponName:weaponKey);
  string weaponWire=JsonSerializer.Serialize(new{action="editInventoryDogtag",dogtag=weaponRequest});
  var weaponEdit=json.Deserialize<CharacterDataRequest>(weaponWire)!.Dogtag;
  Check(weaponEdit==weaponRequest,"a weapon-only update binds from the client JSON request");
  InventoryItemEdit.Apply(tagItem,null,dogtagProps.RootElement,json,dogtag:weaponEdit);
  savedTag=json.Deserialize<Item>(json.Serialize(tagItem)!)!.Upd!.Dogtag!;
  Check(savedTag.WeaponName==weaponKey&&savedTag.Nickname=="Updated name"&&savedTag.Time=="2026-11-24T10:30:26.1250000Z"
   &&savedTag.Side==DogtagSide.Bear&&savedTag.Level==42&&savedTag.Status==""&&savedTag.KillerName=="Known killer",
   "a weapon-only update persists the native localization key and preserves the other dogtag fields");
  InventoryItemEdit.Apply(tagItem,null,dogtagProps.RootElement,json,dogtag:new(WeaponName:"Custom weapon"));
  Check(tagItem.Upd!.Dogtag!.WeaponName=="Custom weapon","the dogtag weapon accepts a plain display name");
  InventoryItemEdit.Apply(tagItem,null,dogtagProps.RootElement,json,dogtag:new(WeaponName:""));
  Check(tagItem.Upd!.Dogtag!.WeaponName=="","the dogtag weapon can be cleared");
  Check(new DogtagEdit(Time:"2026-11-24 10:30:26").Validate().Time=="2026-11-24T10:30:26.0000000Z"
   &&new DogtagEdit(Time:"2026-11-24T10:30:26Z").Validate().Time=="2026-11-24T10:30:26.0000000Z",
   "UTC time entry and ISO time without a fractional part are accepted");
  foreach(int level in new[]{int.MinValue,-1,0,10001,1000000,int.MaxValue}){
   string levelWire=JsonSerializer.Serialize(new{action="editInventoryDogtag",dogtag=new DogtagEdit(Level:level)});
   var levelRequest=json.Deserialize<CharacterDataRequest>(levelWire)!.Dogtag;
   InventoryItemEdit.Apply(tagItem,null,dogtagProps.RootElement,json,dogtag:levelRequest);
   var roundTrip=json.Deserialize<Item>(json.Serialize(tagItem)!)!.Upd!.Dogtag!;
   Check(roundTrip.Level==level&&roundTrip.Nickname=="Updated name"&&roundTrip.AccountId=="known-owner",
    "dogtag levels beyond the former cap survive request binding and native save without changing other fields");
  }
  string tagBefore=json.Serialize(tagItem)!;
  foreach(var invalid in new DogtagEdit[]{new(),new(Nickname:""),new(Time:"2026-02-31 12:00:00"),new(Side:"Savage"),new(Status:"multi\nline"),
   new(WeaponName:"multi\nline"),new(WeaponName:new string('x',129))}){
   rejected=false;
   try{InventoryItemEdit.Apply(tagItem,new GearAttributes{FoundInRaid=false},dogtagProps.RootElement,json,dogtag:invalid);}
   catch(ArgumentException){rejected=true;}
   Check(rejected&&json.Serialize(tagItem)==tagBefore,"invalid dogtag parameters leave the item untouched");
  }
  rejected=false;
  try{InventoryItemEdit.Apply(item,null,props.RootElement,json,dogtag:new(Level:50));}
  catch(ArgumentException){rejected=true;}
  Check(rejected&&json.Serialize(bag)==edited,"ordinary inventory items reject dogtag fields");
  return count;
 }
}
