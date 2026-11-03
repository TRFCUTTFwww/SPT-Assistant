using System.Text.Json;
using LocalGearMail;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Common;
using var props=JsonDocument.Parse("{\"MaxHpResource\":400,\"MaxResource\":60,\"MaximumNumberOfUsage\":40}");
var upd=new Upd{Repairable=new UpdRepairable{Durability=100,MaxDurability=100},MedKit=new UpdMedKit{HpResource=400},FoodDrink=new UpdFoodDrink{HpPercent=60}};
GearAttributeRules.Apply(upd,new GearAttributes{Durability=50,MaxDurability=75,MedicalResource=200,FoodResource=30,KeyUses=4,FoundInRaid=true},props.RootElement);
if(upd.Repairable.Durability!=50||upd.Repairable.MaxDurability!=75||upd.MedKit.HpResource!=200||upd.FoodDrink.HpPercent!=30||upd.Key?.NumberOfUsages!=4||upd.SpawnedInSession!=true)throw new Exception("Attribute values not applied");
void Reject(GearAttributes attrs){try{GearAttributeRules.Apply(upd,attrs,props.RootElement);}catch(ArgumentException){return;}throw new Exception("Invalid attributes accepted");}
Reject(new(){Durability=100,MaxDurability=50});Reject(new(){Durability=double.NaN});Reject(new(){MedicalResource=double.PositiveInfinity});Reject(new(){FoodResource=-1});Reject(new(){KeyUses=41});
GearAttributeRules.Apply(upd,new GearAttributes{MedicalResource=4000,FoodResource=600,KeyRemaining=1000},props.RootElement);
if(upd.MedKit.HpResource!=4000||upd.FoodDrink.HpPercent!=600||upd.Key?.NumberOfUsages!=-960)throw new Exception("Over-template values or remaining key uses were not applied");
var extra=new Upd{SideEffect=new UpdSideEffect(),Resource=new UpdResource(),RepairKit=new UpdRepairKit(),Togglable=new UpdTogglable()};
GearAttributeRules.Apply(extra,new GearAttributes{PoisonUses=100000,Resource=50000,RepairKitResource=50000,ToggledOn=true},props.RootElement);
if(extra.SideEffect.Value!=100000||extra.Resource.Value!=50000||extra.RepairKit.Resource!=50000||extra.Togglable.On!=true)throw new Exception("Extended component values not applied");
using var resourceProps=JsonDocument.Parse("{\"Resource\":100,\"MaxRepairResource\":1200}");var generated=new Upd();GearAttributeRules.Apply(generated,new GearAttributes{Resource=50000,RepairKitResource=50000},resourceProps.RootElement);
if(generated.Resource?.Value!=50000||generated.Resource.UnitsConsumed!=0||generated.RepairKit?.Resource!=50000)throw new Exception("Missing default server resource fields were not generated");
try{GearAttributeRules.Apply(new Upd(),new GearAttributes{Resource=1},props.RootElement);throw new Exception("Resource accepted on unsupported template");}catch(ArgumentException){}
try{GearAttributeRules.Apply(new Upd(),new GearAttributes{MedicalResource=1},props.RootElement);throw new Exception("Unsupported attributes accepted");}catch(ArgumentException){}
var legacy=JsonSerializer.Serialize(new GearSpec{Ref="test",Template="544fb45d4bdc2dee738b4568"});
using var serialized=JsonDocument.Parse(legacy);if(serialized.RootElement.TryGetProperty("attributes",out _)||serialized.RootElement.TryGetProperty("location",out _))throw new Exception("Legacy request digest format changed");
GearAttributeRules.Apply(upd,new GearAttributes{Durability=1e20,MaxDurability=1e20,MedicalResource=1e20,KeyRemaining=int.MaxValue},props.RootElement);if(upd.Repairable.Durability!=1e20||upd.Key?.NumberOfUsages!=40-int.MaxValue)throw new Exception("Field transport boundary still has an arbitrary Int32-like float cap");Reject(new(){Durability=(double)float.MaxValue*2,MaxDurability=(double)float.MaxValue*2});
Console.WriteLine("PASS mail property application, input bounds, unsupported fields and legacy request format");
var sourceRoot=new MongoId();var bag=new MongoId();var content=new MongoId();var secure=new MongoId();var secureContent=new MongoId();var template=new MongoId("544fb45d4bdc2dee738b4568");
var source=new BotBaseInventory{Equipment=sourceRoot,Items=[new(){Id=sourceRoot,Template=template},new(){Id=bag,Template=template,ParentId=sourceRoot.ToString(),SlotId="Backpack"},new(){Id=content,Template=template,ParentId=bag.ToString(),SlotId="main"},new(){Id=secure,Template=template,ParentId=sourceRoot.ToString(),SlotId="SecuredContainer"},new(){Id=secureContent,Template=template,ParentId=secure.ToString(),SlotId="main"}]};
var snapshot=ScavLoadoutRules.Capture(source);
if(snapshot.Items.Count!=2)throw new Exception("Snapshot omitted contents or copied secure container");
var targetRoot=new MongoId();var previous=new MongoId();var unrelated=new MongoId();
var target=new BotBaseInventory{Equipment=targetRoot,Items=[new(){Id=targetRoot,Template=template},new(){Id=previous,Template=template,ParentId=targetRoot.ToString(),SlotId="Backpack"},new(){Id=unrelated,Template=template}]};
ScavLoadoutRules.Apply(target,snapshot);
if(source.Items.Count!=5||target.Items.Count!=4||target.Items.Any(item=>item.Id==previous||item.Id==bag||item.Id==content))throw new Exception("Inventory identities leaked or source was altered");
var copiedBag=target.Items.Single(item=>item.SlotId=="Backpack");if(copiedBag.ParentId!=targetRoot.ToString()||!target.Items.Any(item=>item.ParentId==copiedBag.Id.ToString()))throw new Exception("Copied attachment hierarchy invalid");
ScavLoadoutRules.Apply(target,snapshot);if(target.Items.Count!=4)throw new Exception("Repeated preset application duplicated equipment");
int before=target.Items.Count;try{ScavLoadoutRules.Apply(target,new LoadoutSnapshot("missing",[new(){Id=new MongoId(),Template=template,ParentId="orphan"}]));throw new Exception("Orphaned snapshot accepted");}catch(ArgumentException){}if(target.Items.Count!=before)throw new Exception("Invalid snapshot altered inventory");
Console.WriteLine("PASS Scav copy preserves PMC, remaps IDs and contents, excludes secure container and replaces equipment without duplicates");
