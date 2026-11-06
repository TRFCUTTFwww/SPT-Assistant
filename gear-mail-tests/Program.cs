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

var pmc=new SPTarkov.Server.Core.Models.Eft.Common.PmcData{Inventory=new(){Items=[new(){Id=new MongoId(),Template=new MongoId(CharacterDataRules.Tarcoin),Upd=new(){StackObjectsCount=100}},new(){Id=new MongoId(),Template=template,Upd=new(){StackObjectsCount=7}}]},TarCoinBalance=50};
if(CharacterDataRules.Deposit(pmc,100,50)!=100||pmc.TarCoinBalance!=150||pmc.Inventory.Items.Count!=1||pmc.Inventory.Items[0].Upd?.StackObjectsCount!=7)throw new Exception("Deposit failed conservation or changed unrelated items");
void Deny(Action action){try{action();}catch(ArgumentException){return;}throw new Exception("Invalid data operation accepted");}
Deny(()=>CharacterDataRules.Deposit(pmc,100,50));Deny(()=>CharacterDataRules.Wallet(pmc,200,50));Deny(()=>CharacterDataRules.Wallet(pmc,-1,150));CharacterDataRules.Wallet(pmc,200,150);
pmc.Inventory.Items.Add(new(){Id=new MongoId(),Template=new MongoId(CharacterDataRules.Tarcoin),Upd=new(){StackObjectsCount=100}});pmc.TarCoinBalance=int.MaxValue-1;Deny(()=>CharacterDataRules.Deposit(pmc,100,int.MaxValue-1));if(CharacterDataRules.PhysicalCoins(pmc)!=100||pmc.TarCoinBalance!=int.MaxValue-1)throw new Exception("Overflow rejection changed inventory");
pmc.Skills=new(){Common=[new(){Id=SPTarkov.Server.Core.Models.Enums.SkillTypes.Endurance,Progress=100}]};CharacterDataRules.Skill(pmc,"Endurance",25.5);if(pmc.Skills.Common.Single().Progress!=2550)throw new Exception("Skill level transport incorrect");Deny(()=>CharacterDataRules.Skill(pmc,"Endurance",double.NaN));Deny(()=>CharacterDataRules.Skill(pmc,"Endurance",52));Deny(()=>CharacterDataRules.Skill(pmc,"Missing",1));
var achievementId=new MongoId();var achievements=new[]{new Achievement{Id=achievementId,Index=0,ImageUrl="",AssetPath="",Rewards=[],Conditions=new(){AvailableForFinish=[]},InstantComplete=false,ShowNotificationsInGame=false,ShowProgress=false,prefab="",Rarity="",Hidden=false,ShowConditions=false,ProgressBarEnabled=false,Side=""}};CharacterDataRules.Achievement(pmc,achievementId.ToString(),true,achievements,123);CharacterDataRules.Achievement(pmc,achievementId.ToString(),true,achievements,456);if(pmc.Achievements![achievementId]!=123)throw new Exception("Duplicate achievement changed timestamp");CharacterDataRules.Achievement(pmc,achievementId.ToString(),false,achievements,0);if(pmc.Achievements.Count!=0)throw new Exception("Achievement removal failed");
Console.WriteLine("PASS wallet conservation, duplicate/stale/overflow rejection, unrelated inventory, skills and achievement toggles");

var gameRoot=Environment.GetEnvironmentVariable("SPT_GAME_ROOT")??"D:/SPT-5.0.0-48558";
var nativeJson=new SPTarkov.Server.Core.Utils.JsonUtil([new SPTarkov.Server.Core.Utils.Json.SptJsonConverterRegistrator()]);
T Read<T>(string name)=>nativeJson.Deserialize<T>(File.ReadAllText(System.IO.Path.Combine(gameRoot,"SPT_Runtime","SPT_Data","database","templates",name)))!;
var tables=(SPTarkov.Server.Core.Models.Spt.Tables.TemplateTable)Activator.CreateInstance(typeof(SPTarkov.Server.Core.Models.Spt.Tables.TemplateTable))!;typeof(SPTarkov.Server.Core.Models.Spt.Tables.TemplateTable).GetProperty("Quests")!.SetValue(tables,Read<Dictionary<MongoId,Quest>>("quests.json"));typeof(SPTarkov.Server.Core.Models.Spt.Tables.TemplateTable).GetProperty("VariableGroups")!.SetValue(tables,Read<List<SPTarkov.Server.Core.Models.Eft.Quests.VariableGroupData>>("variableGroups.json"));typeof(SPTarkov.Server.Core.Models.Spt.Tables.TemplateTable).GetProperty("QuestVariables")!.SetValue(tables,Read<Dictionary<MongoId,SPTarkov.Server.Core.Models.Eft.Quests.QuestVariableGroup>>("questVariables.json"));
var branches=tables.Quests.Keys.Where(id=>StoryRules.Alternatives(id,tables).Count>0).ToArray();if(branches.Length==0)throw new Exception("No native branch relationships found");foreach(var id in branches)foreach(var alternative in StoryRules.Alternatives(id,tables))if(!StoryRules.Alternatives(alternative,tables).Contains(id))throw new Exception("Branch graph asymmetrical");
var testId=branches[0];var otherId=new MongoId();var counter=new MongoId();pmc.Quests=[];pmc.TaskConditionCounters=new(){{counter,new(){SourceId=testId,Value=8}}};StoryRules.Set(pmc,testId,SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Success,200);StoryRules.Set(pmc,otherId,SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Success,100);StoryRules.Set(pmc,testId,SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Started,300);if(pmc.Quests.Single(q=>q.QId==testId).StatusTimers.Count!=1||pmc.TaskConditionCounters.Count!=0||pmc.Quests.Single(q=>q.QId==otherId).StartTime!=100)throw new Exception("Quest reset leaked state or touched unrelated quest");
var questConfig=nativeJson.Deserialize<SPTarkov.Server.Core.Models.Spt.Config.QuestConfig>(File.ReadAllText(System.IO.Path.Combine(gameRoot,"SPT_Runtime","SPT_Data","configs","quest.json")))!;
var delayed=questConfig.HiddenVariableGates.First(p=>p.Value.Delay>0&&p.Value.Quests?.Count>0&&tables.Quests.ContainsKey(p.Key));tables.Quests[delayed.Key]=tables.Quests[delayed.Key] with{IsStoryQuest=true};var predecessor=delayed.Value.Quests![0];StoryRules.Set(pmc,predecessor,SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Success,100000);StoryRules.SkipWaits(pmc,tables,questConfig,100001);var prereq=pmc.Quests.Single(q=>q.QId==predecessor);if(prereq.Status!=SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Success||prereq.StatusTimers[SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Success]+delayed.Value.Delay>=100001)throw new Exception("Hidden wait unchanged");
var variableHelper=new SPTarkov.Server.Core.Helpers.Quest.QuestVariableHelper(tables);StoryRules.SyncVariables(pmc,tables,variableHelper,new HashSet<MongoId>{predecessor});
Console.WriteLine($"PASS native quest templates, {branches.Length} branch nodes, scoped counters, hidden wait backdating and variable rebuilding");

// The host owns fixed routes; module generations have no injected singleton registrations.
var modulePath=typeof(CharacterDataRules).Assembly.Location;var host=new AdminModuleHost(null!,nativeJson,null!,tables,null!,null!,null!,questConfig);
var load=typeof(AdminModuleHost).GetMethod("LoadFile",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
bool LoadModule(string path)=>(bool)load.Invoke(host,[path])!;
if(!LoadModule(modulePath)||!AdminModuleHost.Ready||LoadModule(modulePath))throw new Exception("Host failed initial or unchanged generation handling");
var tempModule=System.IO.Path.Combine(AppContext.BaseDirectory,"admin-test.module");File.WriteAllBytes(tempModule,[1,2,3]);
try{LoadModule(tempModule);throw new Exception("Corrupt module accepted");}catch(System.Reflection.TargetInvocationException){}
if(!AdminModuleHost.Ready||LoadModule(modulePath))throw new Exception("Failed reload replaced healthy module");
File.WriteAllBytes(tempModule,File.ReadAllBytes(modulePath).Concat(new byte[]{0}).ToArray());if(!LoadModule(tempModule))throw new Exception("Changed valid module was not swapped");File.Delete(tempModule);
Console.WriteLine("PASS collectible server module swap, unchanged hash, corrupt candidate preservation and live-generation replacement");

var profilePath=Directory.GetFiles(System.IO.Path.Combine(gameRoot,"SPT_Runtime","user","profiles"),"*.json").First();var diskBefore=File.ReadAllBytes(profilePath);
var clone=nativeJson.Deserialize<SPTarkov.Server.Core.Models.Eft.Profile.SptProfile>(System.Text.Encoding.UTF8.GetString(diskBefore))!;var clonedPmc=clone.CharacterData!.PmcData!;
long physical=CharacterDataRules.PhysicalCoins(clonedPmc);int walletBefore=clonedPmc.TarCoinBalance??0;string questsBefore=nativeJson.Serialize(clonedPmc.Quests)!;
if(physical>0&&physical<=int.MaxValue-(long)walletBefore){CharacterDataRules.Deposit(clonedPmc,physical,walletBefore);if(CharacterDataRules.PhysicalCoins(clonedPmc)!=0||clonedPmc.TarCoinBalance!=walletBefore+physical||nativeJson.Serialize(clonedPmc.Quests)!=questsBefore)throw new Exception("Real profile clone deposit changed quests or lost value");}
var unrelatedVariable=new MongoId();clonedPmc.Variables??=[];clonedPmc.Variables[unrelatedVariable]=73;StoryRules.Set(clonedPmc,testId,SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Started,300);StoryRules.SyncVariables(clonedPmc,tables,variableHelper,new HashSet<MongoId>{testId});if(clonedPmc.Variables[unrelatedVariable]!=73||!File.ReadAllBytes(profilePath).SequenceEqual(diskBefore))throw new Exception("Unrelated variable or source profile changed");
Console.WriteLine("PASS actual profile clone conversion and rewind retain unrelated state; source profile bytes unchanged");

var stashRoot=new MongoId();var stashBag=new MongoId();var stashAmmo=new MongoId();var raidItem=new MongoId();var otherRoot=new MongoId();
List<Item> stashBefore=[new(){Id=stashRoot,Template=template},new(){Id=stashBag,Template=template,ParentId=stashRoot.ToString()},new(){Id=stashAmmo,Template=template,ParentId=stashBag.ToString()},new(){Id=raidItem,Template=template,ParentId=otherRoot.ToString()},new(){Id=otherRoot,Template=template}];
List<Item> stashAfter=[stashBefore[0],new(){Id=raidItem,Template=template,ParentId=stashRoot.ToString()}];
var mergedStash=RaidStashRules.Merge(stashBefore,stashRoot.ToString(),stashAfter);
if(mergedStash.Count!=3||mergedStash.Count(i=>i.Id==raidItem)!=1||mergedStash.Any(i=>i.Id==stashAmmo||i.Id==stashBag)||!mergedStash.Any(i=>i.Id==otherRoot)||stashBefore.Count!=5)throw new Exception("Stash deposits/withdrawals did not preserve identity, removal, unrelated roots or input immutability");
if(RaidStashRules.Merge(mergedStash,stashRoot.ToString(),stashAfter).Count!=3)throw new Exception("Stash repeat duplicated items");
void RejectStash(List<Item> incoming){try{RaidStashRules.Merge(stashBefore,stashRoot.ToString(),incoming);}catch(ArgumentException){return;}throw new Exception("Malformed stash accepted");}
RejectStash([]);RejectStash([stashBefore[0],stashBefore[0]]);RejectStash([stashBefore[0],new(){Id=raidItem,Template=template,ParentId="orphan"}]);RejectStash([stashBefore[0],new(){Id=raidItem,Template=template,ParentId=stashBag.ToString()},new(){Id=stashBag,Template=template,ParentId=raidItem.ToString()}]);RejectStash([new(){Id=stashRoot,Template=new MongoId(),ParentId=null}]);
Console.WriteLine("PASS raid stash deposits/withdrawals, nested removal, identity preservation, idempotency, unrelated roots and malformed/cyclic tree rejection");

// Captured IL2CPP wire shape, including repeated wrappers and later outer updates.
string wrapped="""
[{"_id":"5449016a4bdc2d6f028b456f","_tpl":"544fb45d4bdc2dee738b4568","parentId":"5449016a4bdc2d6f028b456e","slotId":"hideout","location":{"JToken":{"JToken":{"x":5,"y":50,"r":"Horizontal"}}},"upd":{"JToken":{"StackObjectsCount":42,"SpawnedInSession":true,"Repairable":{"Durability":33,"MaxDurability":80}},"StackObjectsCount":40}},
{"_id":"5449016a4bdc2d6f028b4570","_tpl":"544fb45d4bdc2dee738b4568","parentId":"5449016a4bdc2d6f028b456f","slotId":"cartridges","location":{"JToken":3},"upd":{"JToken":{"FoodDrink":{"HpPercent":12.5},"Key":{"NumberOfUsages":4}}}}]
""";
var clean=LocalSpt.Shared.RaidStashJson.Normalize(wrapped,out int fixedLocations,out int fixedUpdates);
var transport=nativeJson.Deserialize<CharacterDataRequest>("{\"stashItems\":"+wrapped+"}")!;
var wire=LocalSpt.Shared.RaidStashJson.Normalize(transport.StashItems!.Value.GetRawText(),out _,out _);
var roundtrip=nativeJson.Deserialize<List<Item>>(wire.ToJsonString())!;
var persisted=System.Text.Json.Nodes.JsonNode.Parse(nativeJson.Serialize(roundtrip)!)!.AsArray();
if(fixedLocations!=2||fixedUpdates!=2||roundtrip[0].Upd?.StackObjectsCount!=40||roundtrip[0].Upd?.SpawnedInSession!=true||roundtrip[0].Upd?.Repairable?.Durability!=33||roundtrip[1].Upd?.FoodDrink?.HpPercent!=12.5||roundtrip[1].Upd?.Key?.NumberOfUsages!=4||persisted[0]!["location"]!["x"]!.GetValue<int>()!=5||persisted[0]!["location"]!["y"]!.GetValue<int>()!=50||persisted[0]!["location"]!["r"]!.GetValue<string>()!="Horizontal"||persisted[1]!["location"]!.GetValue<int>()!=3||roundtrip[1].ParentId!=roundtrip[0].Id.ToString())throw new Exception("Native stash transport lost coordinates, rotation, nested identity or item attributes");
var twice=LocalSpt.Shared.RaidStashJson.Normalize(clean.ToJsonString(),out int againLocations,out int againUpdates);
if(againLocations!=0||againUpdates!=0||!System.Text.Json.Nodes.JsonNode.DeepEquals(clean,twice))throw new Exception("Stash normalization is not idempotent");
try{LocalSpt.Shared.RaidStashJson.Normalize("[{\"upd\":{\"JToken\":4,\"StackObjectsCount\":2}}]",out _,out _);throw new Exception("Ambiguous wrapper accepted");}catch(ArgumentException){}
Console.WriteLine("PASS captured IL2CPP request -> raw normalization -> native Item -> save roundtrip preserves grid, slot index, nested items, stack, durability, FiR and usage; rejects ambiguous data");

using var originalDoc=JsonDocument.Parse(diskBefore);
var rawItems=originalDoc.RootElement.GetProperty("characters").GetProperty("pmc").GetProperty("Inventory").GetProperty("items");
var repair=LocalSpt.Shared.RaidStashJson.Normalize(rawItems.GetRawText(),out int actualLocations,out int actualUpdates);
var repairTyped=nativeJson.Deserialize<List<Item>>(repair.ToJsonString())!;
var liveTyped=nativeJson.Deserialize<SPTarkov.Server.Core.Models.Eft.Profile.SptProfile>(System.Text.Encoding.UTF8.GetString(diskBefore))!;
var liveRaw=LocalSpt.Shared.RaidStashJson.Normalize(nativeJson.Serialize(liveTyped.CharacterData!.PmcData!.Inventory!.Items)!,out int liveLocations,out int liveUpdates);
var liveRepair=nativeJson.Deserialize<List<Item>>(liveRaw.ToJsonString())!;
if(actualLocations!=liveLocations||actualUpdates!=liveUpdates||repairTyped.Count!=rawItems.GetArrayLength()||!repairTyped.Select(i=>i.Id).SequenceEqual(liveRepair.Select(i=>i.Id))||nativeJson.Serialize(repairTyped)!=nativeJson.Serialize(liveRepair))throw new Exception("In-memory repair differs from repair of raw profile fields");
var repairedWire=LocalSpt.Shared.RaidStashJson.Normalize(nativeJson.Serialize(liveRepair)!,out int remainingLocations,out int remainingUpdates);
if(remainingLocations!=0||remainingUpdates!=0||!File.ReadAllBytes(profilePath).SequenceEqual(diskBefore))throw new Exception("Repair left wrappers or modified source profile");
Console.WriteLine($"PASS actual profile repair dry run: {liveRepair.Count} identities retained, {actualLocations} locations/{actualUpdates} attributes normalized, no wrappers remain; source bytes unchanged");
