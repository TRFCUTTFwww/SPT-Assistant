using System.Reflection;
using System.Reflection.Emit;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.InRaid;
using EndLocalRaidRequestData=SPTarkov.Server.Core.Models.Eft.Match.EndLocalRaidRequestData;
using Path=System.IO.Path;
using System.Text.Json;
using HarmonyLib;
using LocalRaidShared;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Generators.Loot;
using SPTarkov.Server.Core.Services.InRaid;
using SPTarkov.Common.Models.Logging;
namespace LocalRaidControls;
public record Metadata:IModMetadata {
 public string ModGuid{get;init;}="local.spt5.raid-controls";public string Name{get;init;}="Local Next Raid Loot Controls";public string Author{get;init;}="Local";public List<string>? Contributors{get;init;}
 public SemanticVersioning.Version Version{get;init;}=new("1.4.0");public SemanticVersioning.Range SptVersion{get;init;}=new("~5.0.0");public bool HasPrepatcher{get;init;}public List<string>? Incompatibilities{get;init;}public Dictionary<string,SemanticVersioning.Range>? ModDependencies{get;init;}public string? Url{get;init;}public string License{get;init;}="MIT";
}
[Injectable(TypePriority=OnLoadOrder.PostLoad+100)]
public class Startup(ISptLogger<Startup> logger):IOnLoad {
 public Task OnLoadAsync(CancellationToken cancellationToken){Patches.Log=logger;Patches.Start();return Task.CompletedTask;}
}
public static partial class Patches {
 internal static ISptLogger<Startup> Log=null!;
 static readonly string Home=Path.Combine(AppContext.BaseDirectory,"user","mods","LocalRaidControls");
 static readonly Dictionary<string,double> prices=new();
 [ThreadStatic]static RaidSettings? raid;
 [ThreadStatic]static RaidSettings? generation;
 [ThreadStatic]static RaidSettings? airdrop;
 static readonly JsonSerializerOptions json=new(){WriteIndented=true};
 public sealed class Scope {internal RaidSettings? Previous;}
 internal static RaidSettings Read(){try{var file=Path.Combine(Home,"settings.json");var value=File.Exists(file)?JsonSerializer.Deserialize<RaidSettings>(File.ReadAllText(file))??new():new RaidSettings();value.Validate();return value;}catch(Exception e){Log.Warning("Raid loot settings ignored: "+e.Message);return new();}}
 static double Price(string tpl)=>tpl==BitcoinRules.Template&&LiveBitcoin.Effective is double live?live:prices.GetValueOrDefault(tpl);
 static void Status(string? map=null,RaidSettings? settings=null){try{Directory.CreateDirectory(Home);string file=Path.Combine(Home,"server-status.json");File.WriteAllText(file+".tmp",JsonSerializer.Serialize(new{version="1.4.0",serverPid=Environment.ProcessId,ready=true,map,settings,utc=DateTime.UtcNow},json));File.Move(file+".tmp",file,true);}catch(Exception e){Log.Warning("Raid status file: "+e.Message);}}
 internal static void Start(){Directory.CreateDirectory(Home);string file=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","BepInEx","plugins","LocalSpt5ESP","esp5-prices.json"));if(File.Exists(file))foreach(var pair in JsonSerializer.Deserialize<Dictionary<string,double>>(File.ReadAllText(file))??[])prices[pair.Key]=pair.Value;
  var h=new Harmony("local.spt5.raid-controls");void Patch(Type type,string method,string? before=null,string? after=null,string? final=null){h.Patch(AccessTools.Method(type,method),prefix:before==null?null:new HarmonyMethod(typeof(Patches),before),postfix:after==null?null:new HarmonyMethod(typeof(Patches),after),finalizer:final==null?null:new HarmonyMethod(typeof(Patches),final));}
  Patch(typeof(LocationLifecycleService),"HandleInsuredItemLostEvent",nameof(FilterDeathInsurance),final:nameof(RestoreDeathInsurance));
  Patch(typeof(InRaidHelper),"DeleteInventory",nameof(AllowDeathLoss));Patch(typeof(InRaidHelper),"GetInventoryItemsLostOnDeath",nameof(KeepDeathItems));
  Patch(typeof(LocationLifecycleService),"GenerateLocationAndLoot",nameof(BeginLocation),nameof(FinishLocation),nameof(EndLocation));
  Patch(typeof(LocationLootGenerator),"GenerateLocationLoot",nameof(BeginRaid),nameof(FinishLoot),nameof(EndRaid));Patch(typeof(LocationLootGenerator),"GenerateDynamicLoot",nameof(AllowDynamic));Patch(typeof(LocationLootGenerator),"AddLootToContainer",nameof(EmptyStatic));Patch(typeof(LocationLootGenerator),"GetLooseLootMultiplierForLocation",after:nameof(LooseMultiplier));Patch(typeof(LocationLootGenerator),"GetStaticLootMultiplierForLocation",after:nameof(ContainerMultiplier));Patch(typeof(LocationLootGenerator),"GenerateDynamicLoot",nameof(DynamicWeights));Patch(typeof(LocationLootGenerator),"GetPossibleLootItemsForContainer",nameof(StaticWeights));
  h.Patch(AccessTools.Method(typeof(LocationLootGenerator),"GenerateDynamicLoot"),transpiler:new HarmonyMethod(typeof(Patches),nameof(DynamicCountBoundary)));h.Patch(AccessTools.Method(typeof(LocationLootGenerator),"GetWeightedCountOfContainerItems"),transpiler:new HarmonyMethod(typeof(Patches),nameof(ContainerCountBoundary)));
  Patch(typeof(AirdropService),"GenerateAirdropLoot",nameof(BeginAirdrop),final:nameof(EndAirdrop));Patch(typeof(LootGenerator),"FindAndAddRandomItemToLoot",nameof(AirdropItems),nameof(FinishWeightedAir));Patch(typeof(LootGenerator),"FindAndAddRandomPresetToLoot",nameof(AirdropPresets),nameof(FinishWeightedAir));AirWeightPatches(h);LiveBitcoin.Start(h);Status();Log.Success("Local raid loot controls ready: next-generation scope; stock tables unchanged");
 }
 static void BeginRaid(string __0,out Scope __state){__state=new(){Previous=raid};raid=generation??Read();Status(__0,raid);Log.Info($"Next raid loot {__0}: enabled={raid.LootEnabled}, loose={raid.LooseMultiplier}, containers={raid.ContainerMultiplier}, valuableWeight={raid.HighValueMultiplier}");}
 public static bool KeepsDeathInventory(RaidSettings settings,PmcData profile)=>settings.KeepOnDeath&&(string.Equals(profile.Info?.Side,"Usec",StringComparison.OrdinalIgnoreCase)||string.Equals(profile.Info?.Side,"Bear",StringComparison.OrdinalIgnoreCase));
 public sealed class InsuranceScope {internal EndLocalRaidRequestData Request=null!;internal IEnumerable<Item>? Original;}
 public static IEnumerable<Item> LostInsuranceOnly(PmcData profile,IEnumerable<Item> lost){var retained=(profile.Inventory?.Items??[]).Select(i=>i.Id).ToHashSet();return lost.Where(i=>!retained.Contains(i.Id)).ToArray();}
 static void FilterDeathInsurance(PmcData __1,EndLocalRaidRequestData __2,out InsuranceScope? __state){__state=null;if(!KeepsDeathInventory(Read(),__1)||__2.LostInsuredItems==null)return;__state=new(){Request=__2,Original=__2.LostInsuredItems};__2.LostInsuredItems=LostInsuranceOnly(__1,__2.LostInsuredItems);}
 static void RestoreDeathInsurance(InsuranceScope? __state){if(__state!=null)__state.Request.LostInsuredItems=__state.Original;}
 static bool AllowDeathLoss(PmcData __0)=>!KeepsDeathInventory(Read(),__0);
 static bool KeepDeathItems(PmcData __0,ref IEnumerable<Item> __result){if(!KeepsDeathInventory(Read(),__0))return true;__result=Array.Empty<Item>();return false;}
 static void BeginLocation(string __1,out Scope __state){__state=new(){Previous=generation};generation=Read();if(!__1.Equals("hideout",StringComparison.OrdinalIgnoreCase))Status(__1,generation);}
 static void FinishLocation(string __1,ref LocationBase __result){if(generation==null||__1.Equals("hideout",StringComparison.OrdinalIgnoreCase))return;__result=RaidGenerationRules.Apply(__result,generation);Log.Info($"Raid advanced {__1}: noAI={generation.NoAi}, noLoot={generation.NoLoot}, factions={generation.AiEnabled}, waves={__result.Waves?.Count}, squads={__result.BossLocationSpawn?.Count}");}
 static void EndLocation(Scope? __state){if(__state!=null)generation=__state.Previous;}
 static void FinishLoot(ref List<SpawnpointTemplate> __result){if(raid?.NoLoot==true)__result=RaidGenerationRules.EmptyLoot(__result);}
 static bool AllowDynamic(ref List<SpawnpointTemplate> __result){if(raid?.NoLoot!=true)return true;__result=[];return false;}
 static bool EmptyStatic(StaticContainerData __0,ref StaticContainerData __result){if(raid?.NoLoot!=true)return true;__result=__0 with {Template=RaidGenerationRules.EmptyContainer(__0.Template??throw new InvalidDataException("Static container has no template"))};return false;}
 static void EndRaid(Scope? __state){if(__state!=null)raid=__state.Previous;}
 static void LooseMultiplier(ref double __result){if(raid?.LootEnabled==true)__result=Product(__result,raid.LooseMultiplier);}
 static void ContainerMultiplier(ref double __result){if(raid?.LootEnabled==true)__result=Product(__result,raid.ContainerMultiplier);}
 public static double Product(double original,double multiplier){if(original==0||multiplier==0)return 0;double result=original*multiplier;return double.IsPositiveInfinity(result)?double.MaxValue:result;}
 public static int DynamicCount(double requested,LooseLoot source)=>BoundCount(requested,source.Spawnpoints?.Count()??0);
 public static int BoundCount(double requested,long capacity)=>double.IsNaN(requested)||requested<=0?0:(int)Math.Min(Math.Min(int.MaxValue,Math.Max(0,capacity)),requested);
 public static int GridCapacity(TemplateItem template)=>BoundCount(double.MaxValue,template.Properties?.Grids?.Sum(grid=>(long)Math.Max(0,grid.Properties?.CellsH??0)*Math.Max(0,grid.Properties?.CellsV??0))??0);
 static int ContainerCount(double requested,MongoId template,ItemHelper helper){if(raid?.LootEnabled!=true)return unchecked((int)requested);var item=helper.GetItem(template);if(!item.Key||item.Value==null)throw new InvalidOperationException("Container template missing: "+template);return BoundCount(requested,GridCapacity(item.Value));}
 static IEnumerable<CodeInstruction> DynamicCountBoundary(IEnumerable<CodeInstruction> input){int count=0;foreach(var code in input){if(code.opcode==OpCodes.Conv_I4){count++;code.opcode=OpCodes.Ldarg_1;code.operand=null;yield return code;yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(Patches),nameof(DynamicCount)));}else yield return code;}if(count!=1)throw new InvalidOperationException("Dynamic loot conversion layout changed");}
 static IEnumerable<CodeInstruction> ContainerCountBoundary(IEnumerable<CodeInstruction> input){int count=0;var helper=typeof(LocationLootGenerator).GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Single(field=>field.FieldType==typeof(ItemHelper));foreach(var code in input){if(code.opcode==OpCodes.Conv_I4){count++;code.opcode=OpCodes.Ldarg_1;code.operand=null;yield return code;yield return new CodeInstruction(OpCodes.Ldarg_0);yield return new CodeInstruction(OpCodes.Ldfld,helper);yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(Patches),nameof(ContainerCount)));}else yield return code;}if(count!=1)throw new InvalidOperationException("Container loot conversion layout changed");}
 public static LooseLoot AdjustDynamic(LooseLoot source,RaidSettings options,Func<string,double> price){var original=(source.Spawnpoints??[]).ToArray();var copied=new List<Spawnpoint>();var pointValues=new List<(double Weight,double Factor)>();var untouched=new List<bool>();foreach(var point in original){if(point.Template?.IsAlwaysSpawn==true){copied.Add(point);pointValues.Add((point.Probability??1,1));untouched.Add(true);continue;}var items=point.Template?.Items?.ToDictionary(i=>i.ComposedKey??i.Id.ToString(),i=>i.Template.ToString())??[];var distribution=(point.ItemDistribution??[]).ToArray();var templates=distribution.Select(item=>items.GetValueOrDefault(item.ComposedKey?.Key??"")??"").ToArray();var values=distribution.Select((item,index)=>(Weight:item.RelativeProbability??1,Factor:options.ItemFactor(templates[index],price(templates[index])))).ToArray();var weights=LootWeightMath.Scale(values);var updated=distribution.Select((item,index)=>new LooseLootItemDistribution{ComposedKey=item.ComposedKey,RelativeProbability=weights[index],ExtensionData=item.ExtensionData}).Where(item=>item.RelativeProbability>0).ToArray();if(updated.Length==0&&distribution.Length>0)continue;double highest=templates.Length==0?0:templates.Max(price);bool overridden=options.HasItemWeights&&templates.Any(options.ItemWeights.ContainsKey);double factor=overridden?LootWeightMath.AverageFactor(values):options.LootEnabled&&highest>=options.ValueThreshold?options.HighValueMultiplier:1;copied.Add(new Spawnpoint{LocationId=point.LocationId,Template=point.Template,Probability=point.Probability,ItemDistribution=updated,ExtensionData=point.ExtensionData});pointValues.Add((point.Probability??1,factor));untouched.Add(false);}var pointWeights=LootWeightMath.Scale(pointValues.ToArray());for(int i=0;i<copied.Count;i++)if(!untouched[i])copied[i].Probability=pointWeights[i];return new LooseLoot{SpawnpointCount=source.SpawnpointCount,SpawnpointsForced=source.SpawnpointsForced,ExtensionData=source.ExtensionData,Spawnpoints=copied};}
 static bool WeightsActive(RaidSettings? options)=>options!=null&&(options.HasItemWeights||options.LootEnabled&&options.HighValueMultiplier!=1);
 static void DynamicWeights(ref LooseLoot __0){if(WeightsActive(raid))__0=AdjustDynamic(__0,raid!,Price);}
 public static StaticLootDetails AdjustStatic(StaticLootDetails source,RaidSettings options,Func<string,double> price){var items=source.ItemDistribution.ToArray();var weights=LootWeightMath.Scale(items.Select(item=>(Weight:(double)(item.RelativeProbability??1),Factor:options.ItemFactor(item.Tpl.ToString(),price(item.Tpl.ToString())))).ToArray());return new(){ItemCountDistribution=source.ItemCountDistribution,ExtensionData=source.ExtensionData,ItemDistribution=items.Select((item,index)=>new ItemDistribution{Tpl=item.Tpl,RelativeProbability=(float)weights[index],ExtensionData=item.ExtensionData}).Where(item=>item.RelativeProbability>0).ToArray()};}
 static void StaticWeights(MongoId __0,ref Dictionary<MongoId,StaticLootDetails> __1){if(!WeightsActive(raid)||!__1.TryGetValue(__0,out var source))return;__1=new(__1){[__0]=AdjustStatic(source,raid!,Price)};}
 static void BeginAirdrop(ref SptAirdropTypeEnum? __0,out Scope __state){__state=new(){Previous=airdrop};airdrop=Read();if(__0 is SptAirdropTypeEnum.radar or SptAirdropTypeEnum.toiletPaper){airdrop.AirdropEnabled=false;airdrop.ItemWeightsEnabled=false;}if(airdrop.AirdropEnabled&&airdrop.AirdropType!="random")__0=Enum.Parse<SptAirdropTypeEnum>(airdrop.AirdropType);Log.Info($"Airdrop controls: enabled={airdrop.AirdropEnabled}, type={__0}, quality={airdrop.AirdropQuality}");}
 static void EndAirdrop(Scope? __state){if(__state!=null)airdrop=__state.Previous;}
 static bool AirdropItems(ref IEnumerable<TemplateItem> __0,ref bool __result){if(airdrop?.HasItemWeights==true){var eligible=__0.Where(i=>AirFactor(i.Id.ToString(),Price(i.Id.ToString()))>0).ToArray();__0=eligible;if(eligible.Length==0){__result=true;return false;}return true;}if(airdrop?.AirdropEnabled!=true||airdrop.AirdropWeight==1)return true;var all=__0.ToArray();var rare=all.Where(i=>Price(i.Id.ToString())>=airdrop.ValueThreshold).ToArray();if(rare.Length==0)return true;__0=all.Concat(Enumerable.Range(1,airdrop.AirdropWeight-1).SelectMany(_=>rare)).ToArray();return true;}
 static bool AirdropPresets(ref IEnumerable<Preset> __0,ref bool __result){if(airdrop?.HasItemWeights==true){var eligible=__0.Where(p=>AirFactor(p.Encyclopedia?.ToString()??"",(p.Items??[]).Sum(i=>Price(i.Template.ToString())))>0).ToArray();__0=eligible;if(eligible.Length==0){__result=true;return false;}return true;}if(airdrop?.AirdropEnabled!=true||airdrop.AirdropWeight==1)return true;var all=__0.ToArray();var rare=all.Where(p=>(p.Items??[]).Sum(i=>Price(i.Template.ToString()))>=airdrop.ValueThreshold).ToArray();if(rare.Length==0)return true;__0=all.Concat(Enumerable.Range(1,airdrop.AirdropWeight-1).SelectMany(_=>rare)).ToArray();return true;}
}
