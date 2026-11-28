using LocalGearMail;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

internal static class OnlineOfferItemsChecks {
 internal static void Run(){
  var template=new MongoId();
  Item Root(double count)=>new(){Id=new MongoId(),Template=template,ParentId=new MongoId().ToString(),Upd=new(){StackObjectsCount=count}};
  void Assert(bool value,string message){if(!value)throw new Exception(message);}
  void Reject(List<Item> items){try{OnlineOfferItems.Read(items);}catch(ArgumentException){return;}throw new Exception("Invalid offer selection accepted");}
  var ammoA=Root(30);var ammoB=Root(60);
  var stacks=OnlineOfferItems.Read([ammoA,ammoB]);
  Assert(stacks.Count==90&&stacks.Roots.Count==2&&stacks.Representative.Count==1,"Multi-stack quantity was not combined");
  Assert(ammoA.Upd!.StackObjectsCount==30&&ammoB.Upd!.StackObjectsCount==60,"Selection mutated source counts");
  var gunA=Root(1);var gunB=Root(1);
  var magA=Root(1);magA.ParentId=gunA.Id.ToString();
  var ammo=Root(30);ammo.ParentId=magA.Id.ToString();
  var magB=Root(1);magB.ParentId=gunB.Id.ToString();
  var guns=OnlineOfferItems.Read([ammo,gunA,magA,gunB,magB]);
  Assert(guns.Count==2&&guns.Representative.SequenceEqual(new[]{gunA,magA,ammo}),"Weapon contents counted as sale units or tree reordered incorrectly");
  var single=OnlineOfferItems.Read([gunA,magA,ammo]);
  Assert(single.Count==1&&single.Representative.Count==3,"Single weapon tree changed");
  var different=Root(1);different.Template=new MongoId();
  Reject([ammoA,different]);Reject([ammoA,ammoA]);Reject([]);
  Reject([Root(0)]);Reject([Root(-1)]);Reject([Root(double.NaN)]);Reject([Root(double.PositiveInfinity)]);
  Assert(OnlineOfferItems.Read([Root(.5)]).Count==1,"SPT's native rounding changed");
  Reject([Root(int.MaxValue),Root(1)]);
  var loopA=Root(1);var loopB=Root(1);loopA.ParentId=loopB.Id.ToString();loopB.ParentId=loopA.Id.ToString();
  Reject([loopA,loopB]);Reject([ammoA,loopA,loopB]);
  Console.WriteLine("PASS online offers: multi-stack quantities, weapon trees, source preservation, duplicate IDs, mixed templates, invalid counts, overflow and cycles");
 }
}
