using Path = System.IO.Path;
using System.Text.Json;
using LocalGearMail;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
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
foreach(string presetMode in new[]{"once","always"}){
 var savedPreset=new ScavPreset(presetMode,nativeJson.Deserialize<LoadoutSnapshot>(nativeJson.Serialize(snapshot))!);
 var envelope=new SPTarkov.Server.Core.Models.Eft.Profile.SptProfile{ExtensionData=new(){["localEspScavPreset"]=savedPreset}};
 var readEnvelope=nativeJson.Deserialize<SPTarkov.Server.Core.Models.Eft.Profile.SptProfile>(nativeJson.Serialize(envelope))!;
 var readPreset=nativeJson.Deserialize<ScavPreset>(nativeJson.Serialize(readEnvelope.ExtensionData!["localEspScavPreset"]))!;
 var beforeSource=nativeJson.Serialize(source)!;var detachedTarget=nativeJson.Deserialize<BotBaseInventory>(nativeJson.Serialize(target))!;
 ScavLoadoutRules.Apply(detachedTarget,readPreset.Loadout);
 if(readPreset.Mode!=presetMode||detachedTarget.Items!.Single(i=>i.SlotId=="Backpack").Template!=template||!detachedTarget.Items.Any(i=>i.ParentId==detachedTarget.Items.Single(j=>j.SlotId=="Backpack").Id.ToString())||nativeJson.Serialize(source)!=beforeSource)throw new Exception("Persisted Scav preset did not survive native profile serialization and application");
}
Console.WriteLine("PASS both Scav preset modes survive native save extension serialization, nested contents and detached inventory application");
T Read<T>(string name)=>nativeJson.Deserialize<T>(File.ReadAllText(System.IO.Path.Combine(gameRoot,"SPT_Runtime","SPT_Data","database","templates",name)))!;
var tables=(SPTarkov.Server.Core.Models.Spt.Tables.TemplateTable)Activator.CreateInstance(typeof(SPTarkov.Server.Core.Models.Spt.Tables.TemplateTable))!;typeof(SPTarkov.Server.Core.Models.Spt.Tables.TemplateTable).GetProperty("Quests")!.SetValue(tables,Read<Dictionary<MongoId,Quest>>("quests.json"));typeof(SPTarkov.Server.Core.Models.Spt.Tables.TemplateTable).GetProperty("VariableGroups")!.SetValue(tables,Read<List<SPTarkov.Server.Core.Models.Eft.Quests.VariableGroupData>>("variableGroups.json"));typeof(SPTarkov.Server.Core.Models.Spt.Tables.TemplateTable).GetProperty("QuestVariables")!.SetValue(tables,Read<Dictionary<MongoId,SPTarkov.Server.Core.Models.Eft.Quests.QuestVariableGroup>>("questVariables.json"));
// Run the installed native reward lookup and generator on detached dependencies.
typeof(TemplateTable).GetProperty("Items")!.SetValue(tables,Read<Dictionary<MongoId,TemplateItem>>("items.json"));
var containerConfig=nativeJson.Deserialize<SPTarkov.Server.Core.Models.Spt.Config.InventoryConfig>(File.ReadAllText(System.IO.Path.Combine(gameRoot,"SPT_Runtime","SPT_Data","configs","inventory.json")))!;
string configBefore=nativeJson.Serialize(containerConfig)!;
var inventoryHelper=(SPTarkov.Server.Core.Helpers.Profile.InventoryHelper)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(SPTarkov.Server.Core.Helpers.Profile.InventoryHelper));
typeof(SPTarkov.Server.Core.Helpers.Profile.InventoryHelper).GetField("<inventoryConfig>P",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(inventoryHelper,containerConfig);
var unrelatedReward=containerConfig.RandomLootContainers.First();
if(!ArmoredCaseRewards.Install(tables))throw new Exception("Native story-case templates missing");
var caseRewards=inventoryHelper.GetRandomLootContainerRewardDetails(new MongoId(ArmoredCaseRewards.Case));
if(caseRewards==null||caseRewards.RewardCount!=0||caseRewards.GuaranteedItems?.Count!=2||inventoryHelper.GetRandomLootContainerRewardDetails(unrelatedReward.Key)!=unrelatedReward.Value||inventoryHelper.GetRandomLootContainerRewardDetails(new MongoId())!=null||nativeJson.Serialize(containerConfig)!=configBefore)throw new Exception("Case fallback changed configured/unrelated rewards or database state");
var originalRewards=new SPTarkov.Server.Core.Models.Spt.Config.RewardDetails{RewardCount=3};var preserved=originalRewards;ArmoredCaseRewards.UseMissingRewards(new MongoId(ArmoredCaseRewards.Case),ref preserved);if(!ReferenceEquals(originalRewards,preserved))throw new Exception("Existing upstream case rewards were replaced");
var baseClasses=new SPTarkov.Server.Core.Services.Items.ItemBaseClassService(null!,tables,null!);
var rewardHelper=new SPTarkov.Server.Core.Helpers.Items.ItemHelper(null!,tables,null!,null!,baseClasses,null!,null!,null!,null!);
var lootGenerator=new SPTarkov.Server.Core.Generators.Loot.LootGenerator(null!,tables,new SPTarkov.Server.Core.Utils.RandomUtil(null!,null!),rewardHelper,new SPTarkov.Server.Core.Helpers.Items.PresetHelper(null!,rewardHelper,null!),null!,null!,null!,null!,null!,null!);
var rewardIds=new HashSet<MongoId>();
for(int roll=0;roll<20;roll++){
 var rewards=lootGenerator.GetRandomLootContainerLoot(caseRewards);var roots=rewards.Select(group=>group.Single()).ToArray();
 if(roots.Length!=2||roots.Count(i=>i.Template.ToString()==ArmoredCaseRewards.Keycard)!=1||roots.Count(i=>i.Template.ToString()==ArmoredCaseRewards.Instructions)!=1||roots.Any(i=>i.Upd?.StackObjectsCount!=1||!rewardIds.Add(i.Id)))throw new Exception("Native case generation did not yield exactly one card and one manual with fresh IDs");
}
ArmoredCaseRewards.Install(tables);if(inventoryHelper.GetRandomLootContainerRewardDetails(new MongoId(ArmoredCaseRewards.Case))==null)throw new Exception("Hot reload lost case compatibility");
Console.WriteLine("PASS native armored-case lookup and 20 reward generations: one Kruglov card/one manual, unique IDs, hot-reload replacement, upstream/unrelated rewards preserved; no live inventory mutation");
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

var craft=new Production{RecipeId=new MongoId(),InProgress=true,Progress=15,ProductionTime=3600,StartTimestamp=1234,GivenItemsInStart=[new(){Id=new MongoId(),Template=template}],SptRequiredTools=[new(){Id=new MongoId(),Template=template}],Products=[]};
var completedCraft=craft with{Progress=3600,AvailableForFinish=true};var stoppedCraft=craft with{InProgress=false};var continuousCraft=craft with{SptIsContinuous=true};var scavCraft=craft with{SptIsScavCase=true};var cultistCraft=craft with{SptIsCultistCircle=true};var missingTime=craft with{ProductionTime=null};
var untouched=new[]{completedCraft,stoppedCraft,continuousCraft,scavCraft,cultistCraft,missingTime};string untouchedBefore=nativeJson.Serialize(untouched)!;
if(HideoutCraftRules.Finish(new Production?[]{craft,null}.Concat(untouched))!=1||craft.Progress!=3600||nativeJson.Serialize(untouched)!=untouchedBefore)throw new Exception("Instant craft changed excluded production or failed to complete ordinary craft");
using(var craftJson=JsonDocument.Parse(nativeJson.Serialize(craft)!)){var progressName=craftJson.RootElement.EnumerateObject().Single(p=>p.Name.Equals("progress",StringComparison.OrdinalIgnoreCase)).Name;var original=System.Text.Json.Nodes.JsonNode.Parse(nativeJson.Serialize(craft with{Progress=15,AvailableForFinish=null})!)!;original[progressName]=3600;original["AvailableForFinish"]=true;if(!System.Text.Json.Nodes.JsonNode.DeepEquals(original,System.Text.Json.Nodes.JsonNode.Parse(nativeJson.Serialize(craft)!)))throw new Exception("Completion changed material/tool/product/timestamp state");}
if(HideoutCraftRules.Finish([craft])!=0||!File.ReadAllBytes(profilePath).SequenceEqual(diskBefore))throw new Exception("Repeated craft completion changed profile or produced duplicates");
Console.WriteLine("PASS instant craft finishes ordinary in-progress work once, retains materials/tools/products/timestamps and excludes continuous/Scav/cultist/stopped/invalid work; live profile untouched");
var stranded=craft with{Progress=3600,AvailableForFinish=null};
if(HideoutCraftRules.Finish([stranded])!=1||stranded.AvailableForFinish!=true||stranded.Progress!=3600||HideoutCraftRules.Finish([stranded])!=0)throw new Exception("Already-full progress without a native ready flag was not repaired idempotently");
Console.WriteLine("PASS native AvailableForFinish repair for already-full progress, repeated completion creates no additional products");
var productionTable=nativeJson.Deserialize<SPTarkov.Server.Core.Models.Eft.Hideout.HideoutProductionData>(File.ReadAllText(System.IO.Path.Combine(gameRoot,"SPT_Runtime","SPT_Data","database","hideout","production.json")))!;
var ordinaryRecipes=HideoutCraftRules.OrdinaryRecipes(productionTable.Recipes!);
if(ordinaryRecipes.Count==0||productionTable.Recipes!.Any(r=>r.Continuous&&ordinaryRecipes.Contains(r.Id)))throw new Exception("Continuous native recipes were not excluded when profile flag is absent");
Console.WriteLine($"PASS native hideout recipe whitelist: {ordinaryRecipes.Count} ordinary crafts, continuous recipes excluded independently of optional profile flags");
var contactId=new MongoId("67bc646c6e34475fea09d2a5");var contactPrior=new MongoId("68d82d29d34befa7d30b8771");
var contactPmc=new SPTarkov.Server.Core.Models.Eft.Common.PmcData{Quests=[new(){StartTime=0,QId=contactId,Status=SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.AvailableAfter,AvailableAfter=20000,StatusTimers=[]},new(){StartTime=0,QId=contactPrior,Status=SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Success,StatusTimers=new(){{SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Success,10000}}},new(){StartTime=0,QId=otherId,Status=SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.AvailableAfter,AvailableAfter=50000,StatusTimers=[]}]};
StoryRules.SkipWait(contactPmc,tables,contactId,11000);
if(contactPmc.Quests[0].AvailableAfter!=0||contactPmc.Quests[0].Status!=SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.AvailableForStart||contactPmc.Quests[1].StatusTimers[SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Success]+8100>=11000||contactPmc.Quests[2].AvailableAfter!=50000)throw new Exception("Scoped contact wait changed unrelated wait or failed acceptance unlock/prerequisite backdating");
contactPmc.Quests[0].Status=QuestStatusEnum.AvailableAfter;contactPmc.Quests[0].AvailableAfter=20000;contactPmc.Quests[1].Status=SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Started;Deny(()=>StoryRules.SkipWait(contactPmc,tables,contactId,11000));
Console.WriteLine("PASS scoped Kerman contact wait expiration preserves unrelated quests and choices; unmet predecessor rejected");

// Exercise native delays on detached profiles; never write a live save.
var sideTables=(TemplateTable)Activator.CreateInstance(typeof(TemplateTable))!;
typeof(TemplateTable).GetProperty("Quests")!.SetValue(sideTables,Read<Dictionary<MongoId,Quest>>("quests.json"));
var mainIds=StoryRules.StoryTasks(sideTables);var mainId=mainIds.First();
const long sideNow=2000000000;int ordinaryWaitCases=0;
string nativeQuestsBefore=nativeJson.Serialize(sideTables.Quests)!,nativeConfigBefore=nativeJson.Serialize(questConfig)!;
foreach(var entry in sideTables.Quests.Where(p=>!mainIds.Contains(p.Key)))
foreach(var condition in entry.Value.Conditions?.AvailableForStart??[]){
 if(condition.ConditionType!="Quest"||condition.AvailableAfter is not >0||condition.Status?.Count is not >0)continue;
 foreach(var priorIdText in StoryRules.Targets(condition)){
  var priorId=new MongoId(priorIdText);if(priorId==entry.Key||priorId==mainId)continue;
  var currentState=condition.Status.First();
  var waiting=new QuestStatus{QId=entry.Key,StartTime=37,Status=QuestStatusEnum.AvailableAfter,AvailableAfter=sideNow+86400,StatusTimers=[],CompletedConditions=[new MongoId()]};
  var priorQuest=new QuestStatus{QId=priorId,StartTime=23,Status=currentState,StatusTimers=new(){{currentState,sideNow-1}}};
  var mainWait=new QuestStatus{QId=mainId,StartTime=0,Status=QuestStatusEnum.AvailableAfter,AvailableAfter=sideNow+43200,StatusTimers=[]};
  var unknownWait=new QuestStatus{QId=new MongoId(),StartTime=0,Status=QuestStatusEnum.AvailableAfter,AvailableAfter=sideNow+12345,StatusTimers=[]};
  var sidePmc=new PmcData{Quests=[waiting,priorQuest,mainWait,unknownWait],Variables=new(){{new MongoId(),73}},TarCoinBalance=17};
  string beforeSide=nativeJson.Serialize(sidePmc)!,mainBefore=nativeJson.Serialize(mainWait)!,unknownBefore=nativeJson.Serialize(unknownWait)!;
  StoryRules.SkipWaits(sidePmc,sideTables,questConfig,sideNow,storyQuests:false);
  if(waiting.AvailableAfter>=sideNow||priorQuest.StatusTimers[currentState]+condition.AvailableAfter>=sideNow||nativeJson.Serialize(mainWait)!=mainBefore||nativeJson.Serialize(unknownWait)!=unknownBefore)throw new Exception("Side wait failed to expire or altered main/unknown quest");
  var originalSide=nativeJson.Deserialize<PmcData>(beforeSide)!;var strippedSide=nativeJson.Deserialize<PmcData>(nativeJson.Serialize(sidePmc))!;
  foreach(var savedQuest in strippedSide.Quests!){var originalQuest=originalSide.Quests!.Single(q=>q.QId==savedQuest.QId);savedQuest.AvailableAfter=originalQuest.AvailableAfter;savedQuest.StatusTimers=originalQuest.StatusTimers;}
  if(nativeJson.Serialize(strippedSide)!=beforeSide)throw new Exception("Side skip changed data beyond wait timestamps");
  string once=nativeJson.Serialize(sidePmc)!;if(StoryRules.SkipWaits(sidePmc,sideTables,questConfig,sideNow,storyQuests:false)!=0||nativeJson.Serialize(sidePmc)!=once)throw new Exception("Repeated side skip is not idempotent");
  ordinaryWaitCases++;
 }
}
if(ordinaryWaitCases==0)throw new Exception("No native side quest wait cases tested");
Console.WriteLine($"PASS {ordinaryWaitCases} native side quest delays: explicit and prerequisite timer expiration, main/unknown quest isolation, other profile fields preserved and repeat idempotency");

int hiddenWaitCases=0;
foreach(var entry in questConfig.HiddenVariableGates.Where(p=>!mainIds.Contains(p.Key)&&p.Value.Delay>0&&p.Value.Quests?.Count>0&&sideTables.Quests.ContainsKey(p.Key))){
 var hiddenPmc=new PmcData{Quests=[]};
 foreach(var priorId in entry.Value.Quests!){StoryRules.Set(hiddenPmc,priorId,QuestStatusEnum.Success,sideNow-1);}
 StoryRules.SkipWaits(hiddenPmc,sideTables,questConfig,sideNow,storyQuests:false);
 foreach(var priorQuest in hiddenPmc.Quests)if(priorQuest.Status!=QuestStatusEnum.Success||priorQuest.StatusTimers[QuestStatusEnum.Success]+entry.Value.Delay>=sideNow)throw new Exception("Hidden side delay unchanged or prerequisite state altered");
 string once=nativeJson.Serialize(hiddenPmc)!;if(StoryRules.SkipWaits(hiddenPmc,sideTables,questConfig,sideNow,storyQuests:false)!=0||nativeJson.Serialize(hiddenPmc)!=once)throw new Exception("Hidden side wait repeated mutation");
 foreach(var priorQuest in hiddenPmc.Quests){priorQuest.Status=QuestStatusEnum.Locked;priorQuest.StatusTimers[QuestStatusEnum.Success]=sideNow-1;}
 string unmetBefore=nativeJson.Serialize(hiddenPmc)!;StoryRules.SkipWaits(hiddenPmc,sideTables,questConfig,sideNow,storyQuests:false);
 if(nativeJson.Serialize(hiddenPmc)!=unmetBefore)throw new Exception("Hidden wait changed an unmet prerequisite or historical timer");
 hiddenWaitCases++;
}
if(hiddenWaitCases!=7)throw new Exception("Expected all seven native hidden side delays");
Console.WriteLine($"PASS all {hiddenWaitCases} native hidden side waits including Gunsmith/Lightkeeper; completion prerequisites retained and repeated skips unchanged");

var unmetCondition=sideTables.Quests.Where(p=>!mainIds.Contains(p.Key)).SelectMany(p=>p.Value.Conditions?.AvailableForStart??[]).First(c=>c.ConditionType=="Quest"&&c.AvailableAfter>0&&c.Status?.Contains(QuestStatusEnum.Success)==true&&StoryRules.Targets(c).Any());
var unmetId=new MongoId(StoryRules.Targets(unmetCondition).First());var unmetPmc=new PmcData{Quests=[new(){QId=unmetId,StartTime=11,Status=QuestStatusEnum.Locked,StatusTimers=new(){{QuestStatusEnum.Success,sideNow-1}}}]};
string lockedBefore=nativeJson.Serialize(unmetPmc)!;StoryRules.SkipWaits(unmetPmc,sideTables,questConfig,sideNow,storyQuests:false);
if(nativeJson.Serialize(unmetPmc)!=lockedBefore)throw new Exception("Unmet side prerequisite's old success timer was altered");
var emptyPmc=new PmcData{Quests=[]};if(StoryRules.SkipWaits(emptyPmc,sideTables,questConfig,sideNow,storyQuests:false)!=0||emptyPmc.Quests.Count!=0)throw new Exception("Skip created missing quests");
var sideId=sideTables.Quests.Keys.First(id=>!mainIds.Contains(id));var scopedPmc=new PmcData{Quests=[new(){QId=sideId,StartTime=0,Status=QuestStatusEnum.AvailableAfter,AvailableAfter=sideNow+100,StatusTimers=[]},new(){QId=mainId,StartTime=0,Status=QuestStatusEnum.AvailableAfter,AvailableAfter=sideNow+100,StatusTimers=[]}]};
StoryRules.SkipWaits(scopedPmc,sideTables,questConfig,sideNow);
if(scopedPmc.Quests[0].AvailableAfter!=sideNow+100||scopedPmc.Quests[1].AvailableAfter>=sideNow)throw new Exception("Default main story action changed side wait scope");
if(nativeJson.Serialize(sideTables.Quests)!=nativeQuestsBefore||nativeJson.Serialize(questConfig)!=nativeConfigBefore||!File.ReadAllBytes(profilePath).SequenceEqual(diskBefore))throw new Exception("Wait skip altered native templates, configuration or live profile bytes");
Console.WriteLine("PASS unmet/missing prerequisite isolation, original main story scope, native template/configuration immutability and live save bytes unchanged");

var testSettingsRoot=Path.Combine(Path.GetTempPath(),"spt-server-settings-"+Guid.NewGuid().ToString("N"));
try{
 string configRoot=Path.Combine(testSettingsRoot,"configs"),backupRoot=Path.Combine(testSettingsRoot,"state");Directory.CreateDirectory(configRoot);
 foreach(string file in ServerSettingsRules.Definitions.Select(d=>d.File).Distinct())File.Copy(Path.Combine(gameRoot,"SPT_Runtime/SPT_Data/configs",file+".json"),Path.Combine(configRoot,file+".json"));
 var store=new ServerSettingsService.ServerSettingsStore(configRoot,backupRoot);var original=store.Read();
 if(original.Rows.Length!=ServerSettingsRules.Definitions.Length)throw new Exception("A listed server option has no native configuration field");
 var bytesBefore=Directory.GetFiles(configRoot).ToDictionary(p=>Path.GetFileName(p)!,p=>File.ReadAllBytes(p));
 var changes=new Dictionary<string,JsonElement>{{"insurance.prapor",JsonSerializer.SerializeToElement(42)},{"repair.price",JsonSerializer.SerializeToElement(2.5)}};
 store.Save(changes,original.Revision);var saved=store.Read();if(saved.Revision==original.Revision||!store.RestartPending(saved.Revision))throw new Exception("Native config changes or restart marker missing");
 if(saved.Rows.Single(r=>r.Id=="insurance.prapor").Value.GetDouble()!=42||saved.Rows.Single(r=>r.Id=="repair.price").Value.GetDouble()!=2.5)throw new Exception("Requested settings not applied");
 foreach(var file in bytesBefore){var oldTree=System.Text.Json.Nodes.JsonNode.Parse(file.Value)!;var newTree=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllBytes(Path.Combine(configRoot,file.Key!)))!;foreach(var setting in ServerSettingsRules.Definitions.Where(d=>d.File+".json"==file.Key&&changes.ContainsKey(d.Id)))ServerSettingsRules.Apply(oldTree.AsObject(),setting,changes[setting.Id]);if(!System.Text.Json.Nodes.JsonNode.DeepEquals(oldTree,newTree))throw new Exception("Unrelated native configuration changed");}
 var after=Directory.GetFiles(configRoot).ToDictionary(p=>Path.GetFileName(p)!,p=>File.ReadAllBytes(p));
 void DenySetting(Dictionary<string,JsonElement> values,string revision){try{store.Save(values,revision);throw new Exception("Invalid settings accepted");}catch(ArgumentException){}foreach(var file in after)if(!File.ReadAllBytes(Path.Combine(configRoot,file.Key!)).SequenceEqual(file.Value))throw new Exception("Rejected change modified a file");}
 DenySetting(changes,original.Revision);DenySetting(new(){{"../../core",JsonSerializer.SerializeToElement(1)}},saved.Revision);DenySetting(new(){{"insurance.prapor",JsonSerializer.SerializeToElement(101)}},saved.Revision);DenySetting(new(){{"insurance.taken",JsonSerializer.SerializeToElement(1)}},saved.Revision);DenySetting(new(){{"hideout.craft",JsonSerializer.SerializeToElement(-.5)}},saved.Revision);
 if(Directory.GetFiles(Path.Combine(backupRoot,"backups"),"*.json",SearchOption.AllDirectories).Length!=2)throw new Exception("Unexpected files backed up or invalid write entered the commit phase");
 var karmaProfile=new PmcData{KarmaValue=.5,TradersInfo=new(){{new MongoId(KarmaRules.Fence),new(){Standing=1.25,LoyaltyLevel=1}},{new MongoId("54cb50c76803fa8b248b4571"),new(){Standing=2,LoyaltyLevel=4}}}};
 KarmaRules.Apply(karmaProfile,"pmc",.8,.5,-7,15);KarmaRules.Apply(karmaProfile,"scav",6,1.25,-7,15);
 if(karmaProfile.KarmaValue!=.8||KarmaRules.Scav(karmaProfile)!=6||karmaProfile.TradersInfo[new MongoId("54cb50c76803fa8b248b4571")].Standing!=2)throw new Exception("Karma edits crossed into unrelated profile values");
 string beforeKarma=nativeJson.Serialize(karmaProfile)!;foreach(var input in new[]{("pmc",double.NaN,.8),("pmc",1.1,.8),("pmc",.1,.5),("scav",16d,6d),("unknown",1d,0d)}){try{KarmaRules.Apply(karmaProfile,input.Item1,input.Item2,input.Item3,-7,15);throw new Exception("Invalid karma accepted");}catch(ArgumentException){}}if(nativeJson.Serialize(karmaProfile)!=beforeKarma)throw new Exception("Rejected karma mutation changed the profile");
 Console.WriteLine("PASS native server settings, multi-file backups, unrelated-field preservation, stale/type/range/path rejection and isolated PMC/Scav karma edits");
 karmaProfile.RagfairInfo=new(){Rating=12.34,IsRatingGrowing=false,SellSum=123456,NotSellSum=98,Offers=[]};
 var expectedFlea=nativeJson.Deserialize<PmcData>(nativeJson.Serialize(karmaProfile))!;expectedFlea.RagfairInfo!.Rating=42.125;
 KarmaRules.ApplyFlea(karmaProfile,42.125,12.34);
 if(nativeJson.Serialize(karmaProfile)!=nativeJson.Serialize(expectedFlea))throw new Exception("Flea edit changed unrelated profile, turnover, direction or offers");
 string beforeFlea=nativeJson.Serialize(karmaProfile)!;
 foreach(var input in new[]{(double.NaN,(double?)42.125),(double.PositiveInfinity,(double?)42.125),(-1d,(double?)42.125),(1000001d,(double?)42.125),(3d,(double?)12.34),(3d,(double?)null)}){try{KarmaRules.ApplyFlea(karmaProfile,input.Item1,input.Item2);throw new Exception("Invalid or stale flea rating accepted");}catch(ArgumentException){}}
 if(nativeJson.Serialize(karmaProfile)!=beforeFlea)throw new Exception("Rejected flea edit mutated profile");
 KarmaRules.ApplyFlea(karmaProfile,0,42.125);KarmaRules.ApplyFlea(karmaProfile,KarmaRules.MaxFleaRating,0);
 var absentFlea=new PmcData();try{KarmaRules.ApplyFlea(absentFlea,5,0);throw new Exception("Missing flea data silently initialized");}catch(ArgumentException){}
 Console.WriteLine("PASS fractional flea reputation, exact profile-field isolation, stale/nonfinite/range rejection and missing-data protection");
}finally{if(Directory.Exists(testSettingsRoot))Directory.Delete(testSettingsRoot,true);}

// Reproduce the installed Mechanic story wait on a detached profile.
var mechanicId=new MongoId("67bc9eda1d99d6ebe5014b65");
var mechanicQuest=sideTables.Quests[mechanicId];
var mechanicCondition=mechanicQuest.Conditions.AvailableForStart.Single();
var mechanicPrior=new MongoId(StoryRules.Targets(mechanicCondition).Single());
var mechanicWait=new QuestStatus{QId=mechanicId,StartTime=0,Status=QuestStatusEnum.AvailableAfter,AvailableAfter=sideNow+86400,StatusTimers=[],CompletedConditions=[new MongoId()]};
var mechanicSaved=new PmcData{Quests=[mechanicWait,new(){QId=mechanicPrior,StartTime=41,Status=QuestStatusEnum.Success,StatusTimers=new(){{QuestStatusEnum.Success,sideNow-60}}}]};
string completedBefore=nativeJson.Serialize(mechanicWait.CompletedConditions)!;
StoryRules.SkipWaits(mechanicSaved,sideTables,questConfig,sideNow);
if(mechanicWait.Status!=QuestStatusEnum.AvailableForStart||mechanicWait.AvailableAfter!=0||mechanicWait.StartTime!=0||nativeJson.Serialize(mechanicWait.CompletedConditions)!=completedBefore||mechanicSaved.Quests[1].Status!=QuestStatusEnum.Success)throw new Exception("Mechanic expired wait did not unlock safely");
string mechanicOnce=nativeJson.Serialize(mechanicSaved)!;if(StoryRules.SkipWaits(mechanicSaved,sideTables,questConfig,sideNow)!=0||nativeJson.Serialize(mechanicSaved)!=mechanicOnce)throw new Exception("Mechanic unlock is not idempotent");
mechanicWait.Status=QuestStatusEnum.AvailableAfter;mechanicWait.AvailableAfter=sideNow-1;mechanicSaved.Quests[1].Status=QuestStatusEnum.Started;
if(StoryRules.PromoteExpiredWaits(mechanicSaved,sideTables,[mechanicId],questConfig,sideNow)!=0||mechanicWait.Status!=QuestStatusEnum.AvailableAfter)throw new Exception("Unmet Mechanic predecessor bypassed");
mechanicSaved.Quests[1].Status=QuestStatusEnum.Success;
sideTables.Quests[mechanicId]=mechanicQuest with{Conditions=new(){AvailableForFinish=mechanicQuest.Conditions.AvailableForFinish,Fail=mechanicQuest.Conditions.Fail,AvailableForStart=[..mechanicQuest.Conditions.AvailableForStart,new(){Id=new MongoId(),DynamicLocale=false,ConditionType="UnknownNativeGate"}]}};
if(StoryRules.PromoteExpiredWaits(mechanicSaved,sideTables,[mechanicId],questConfig,sideNow)!=0)throw new Exception("Unknown native gate bypassed");
sideTables.Quests[mechanicId]=mechanicQuest;
Console.WriteLine("PASS native Mechanic wait: unlocks acceptance only, preserves progress, rejects unmet/unknown gates, repeated skip is idempotent");
