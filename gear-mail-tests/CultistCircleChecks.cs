using LocalGearMail;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
internal static class CultistCircleChecks {
 internal static void Run(){
  void Check(bool value,string message){if(!value)throw new Exception(message);}
  var stash=new MongoId();var reward=new Item{Id=new MongoId(),Template=new MongoId(),ParentId=stash.ToString(),SlotId="CircleOfCultistsGrid1",Upd=new(){StackObjectsCount=3}};
  var child=new Item{Id=new MongoId(),Template=new MongoId(),ParentId=reward.Id.ToString(),SlotId="mod_scope"};
  var other=new Item{Id=new MongoId(),Template=new MongoId(),ParentId=stash.ToString(),SlotId="hideout"};
  var pmc=new PmcData{Id=new MongoId(),Inventory=new(){HideoutAreaStashes=new(){{"27",stash}},Items=[reward,child,other]}};
  var rewards=CultistCircleRules.Rewards(pmc);Check(rewards.Length==1&&ReferenceEquals(rewards[0],reward),"Preview must return only existing circle reward roots without cloning");
  var recipe=new MongoId();var production=new Production{SptIsCultistCircle=true,Progress=12,StartTimestamp=1234,ProductionTime=14400,InProgress=true,AvailableForFinish=false};
  string revision=CultistCircleRules.Revision(pmc.Id!.Value,recipe,production,rewards);
  CultistCircleRules.RequireCurrent(recipe.ToString(),revision,recipe,revision);
  Check(CultistCircleRules.Finish(production),"Cultist ritual did not complete");
  Check(production.Progress==0&&production.InProgress==false&&production.AvailableForFinish==true&&production.StartTimestamp==1234&&production.ProductionTime==14400,"Completion differs from native cultist flags or changed timing identity");
  Check(CultistCircleRules.Revision(pmc.Id.Value,recipe,production,rewards)==revision,"Natural progress/completion invalidated ritual identity");
  Check(!CultistCircleRules.Finish(production)&&pmc.Inventory.Items.Count==3&&reward.Upd.StackObjectsCount==3&&ReferenceEquals(pmc.Inventory.Items[0],reward),"Repeated completion duplicated or changed rewards");
  production.StartTimestamp++;Check(CultistCircleRules.Revision(pmc.Id.Value,recipe,production,rewards)!=revision,"New sacrifice reused stale identity");
  try{CultistCircleRules.RequireCurrent(recipe.ToString(),revision,recipe,"changed");throw new Exception("Stale request accepted");}catch(ArgumentException){}
  foreach(var invalid in new[]{new Production{InProgress=true,ProductionTime=20,StartTimestamp=1},new Production{SptIsCultistCircle=true,SptIsScavCase=true,InProgress=true,ProductionTime=20,StartTimestamp=1},new Production{SptIsCultistCircle=true,SptIsContinuous=true,InProgress=true,ProductionTime=20,StartTimestamp=1},new Production{SptIsCultistCircle=true,Interrupted=true,InProgress=true,ProductionTime=20,StartTimestamp=1}}){Check(!CultistCircleRules.Eligible(invalid),"Invalid ritual accepted");try{CultistCircleRules.Finish(invalid);throw new Exception("Invalid completion accepted");}catch(ArgumentException){}}
  var tree=CultistCircleRules.StashTree(pmc,out string stashId);
  Check(stashId==stash.ToString()&&tree.Length==2&&ReferenceEquals(tree[0],reward)&&ReferenceEquals(tree[1],child),"StashTree did not collect exact reward subtree and exclude unrelated stash items");
  pmc.Inventory.HideoutAreaStashes.Clear();Check(CultistCircleRules.Rewards(pmc).Length==0&&CultistCircleRules.StashTree(pmc,out stashId).Length==0&&stashId=="","Missing stash exposed unrelated inventory");
  Console.WriteLine("PASS Cultist Circle native completion, idempotence, reward identity/isolation and stale sacrifice rejection");
 }
}
