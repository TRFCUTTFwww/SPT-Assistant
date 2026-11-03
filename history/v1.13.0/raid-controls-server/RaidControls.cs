using System.Reflection;
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
 public SemanticVersioning.Version Version{get;init;}=new("1.0.0");public SemanticVersioning.Range SptVersion{get;init;}=new("~5.0.0");public bool HasPrepatcher{get;init;}public List<string>? Incompatibilities{get;init;}public Dictionary<string,SemanticVersioning.Range>? ModDependencies{get;init;}public string? Url{get;init;}public string License{get;init;}="MIT";
}
[Injectable(TypePriority=OnLoadOrder.PostLoad+100)]
public class Startup(ISptLogger<Startup> logger):IOnLoad {
 public Task OnLoadAsync(CancellationToken cancellationToken){Patches.Log=logger;Patches.Start();return Task.CompletedTask;}
}
public static class Patches {
 internal static ISptLogger<Startup> Log=null!;
 static readonly string Home=Path.Combine(AppContext.BaseDirectory,"user","mods","LocalRaidControls");
 static readonly Dictionary<string,double> prices=new();
 [ThreadStatic]static RaidSettings? raid;
 [ThreadStatic]static RaidSettings? airdrop;
 static readonly JsonSerializerOptions json=new(){WriteIndented=true};
 public sealed class Scope {internal RaidSettings? Previous;}
 static RaidSettings Read(){try{var file=Path.Combine(Home,"settings.json");var value=File.Exists(file)?JsonSerializer.Deserialize<RaidSettings>(File.ReadAllText(file))??new():new RaidSettings();value.Validate();return value;}catch(Exception e){Log.Warning("Raid loot settings ignored: "+e.Message);return new();}}
 static double Price(string tpl)=>prices.GetValueOrDefault(tpl);
 static void Status(string? map=null,RaidSettings? settings=null){try{Directory.CreateDirectory(Home);string file=Path.Combine(Home,"server-status.json");File.WriteAllText(file+".tmp",JsonSerializer.Serialize(new{version="1.0.0",serverPid=Environment.ProcessId,ready=true,map,settings,utc=DateTime.UtcNow},json));File.Move(file+".tmp",file,true);}catch(Exception e){Log.Warning("Raid status file: "+e.Message);}}
 internal static void Start(){Directory.CreateDirectory(Home);string file=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","BepInEx","plugins","LocalSpt5ESP","esp5-prices.json"));if(File.Exists(file))foreach(var pair in JsonSerializer.Deserialize<Dictionary<string,double>>(File.ReadAllText(file))??[])prices[pair.Key]=pair.Value;
  var h=new Harmony("local.spt5.raid-controls");void Patch(Type type,string method,string? before=null,string? after=null,string? final=null){h.Patch(AccessTools.Method(type,method),prefix:before==null?null:new HarmonyMethod(typeof(Patches),before),postfix:after==null?null:new HarmonyMethod(typeof(Patches),after),finalizer:final==null?null:new HarmonyMethod(typeof(Patches),final));}
  Patch(typeof(LocationLootGenerator),"GenerateLocationLoot",nameof(BeginRaid),final:nameof(EndRaid));Patch(typeof(LocationLootGenerator),"GetLooseLootMultiplierForLocation",after:nameof(LooseMultiplier));Patch(typeof(LocationLootGenerator),"GetStaticLootMultiplierForLocation",after:nameof(ContainerMultiplier));Patch(typeof(LocationLootGenerator),"GenerateDynamicLoot",nameof(DynamicWeights));Patch(typeof(LocationLootGenerator),"GetPossibleLootItemsForContainer",nameof(StaticWeights));
  Patch(typeof(AirdropService),"GenerateAirdropLoot",nameof(BeginAirdrop),final:nameof(EndAirdrop));Patch(typeof(LootGenerator),"FindAndAddRandomItemToLoot",nameof(AirdropItems));Patch(typeof(LootGenerator),"FindAndAddRandomPresetToLoot",nameof(AirdropPresets));Status();Log.Success("Local raid loot controls ready: next-generation scope; stock tables unchanged");
 }
 static void BeginRaid(string __0,out Scope __state){__state=new(){Previous=raid};raid=Read();Status(__0,raid);Log.Info($"Next raid loot {__0}: enabled={raid.LootEnabled}, loose={raid.LooseMultiplier}, containers={raid.ContainerMultiplier}, valuableWeight={raid.HighValueMultiplier}");}
 static void EndRaid(Scope? __state){if(__state!=null)raid=__state.Previous;}
 static void LooseMultiplier(ref double __result){if(raid?.LootEnabled==true)__result*=raid.LooseMultiplier;}
 static void ContainerMultiplier(ref double __result){if(raid?.LootEnabled==true)__result*=raid.ContainerMultiplier;}
 public static LooseLoot AdjustDynamic(LooseLoot source,RaidSettings options,Func<string,double> price){return new LooseLoot{SpawnpointCount=source.SpawnpointCount,SpawnpointsForced=source.SpawnpointsForced,ExtensionData=source.ExtensionData,Spawnpoints=(source.Spawnpoints??[]).Select(point=>{if(point.Template?.IsAlwaysSpawn==true)return point;
   var items=point.Template?.Items?.ToDictionary(i=>i.ComposedKey??i.Id.ToString(),i=>i.Template.ToString())??[];double highest=0;
   var distribution=point.ItemDistribution?.Select(item=>{string key=item.ComposedKey?.Key??"";var tpl=items.GetValueOrDefault(key)??"";double value=price(tpl);highest=Math.Max(highest,value);return new LooseLootItemDistribution{ComposedKey=item.ComposedKey,RelativeProbability=options.Weight(item.RelativeProbability??1,value),ExtensionData=item.ExtensionData};}).ToArray();
   return new Spawnpoint{LocationId=point.LocationId,Template=point.Template,Probability=options.Weight(point.Probability??1,highest),ItemDistribution=distribution,ExtensionData=point.ExtensionData};}).ToArray()};}
 static void DynamicWeights(ref LooseLoot __0){if(raid?.LootEnabled==true&&raid.HighValueMultiplier!=1)__0=AdjustDynamic(__0,raid,Price);}
 public static StaticLootDetails AdjustStatic(StaticLootDetails source,RaidSettings options,Func<string,double> price)=>new(){ItemCountDistribution=source.ItemCountDistribution,ExtensionData=source.ExtensionData,ItemDistribution=source.ItemDistribution.Select(i=>new ItemDistribution{Tpl=i.Tpl,RelativeProbability=(float)options.Weight(i.RelativeProbability??1,price(i.Tpl.ToString())),ExtensionData=i.ExtensionData}).ToArray()};
 static void StaticWeights(MongoId __0,ref Dictionary<MongoId,StaticLootDetails> __1){if(raid?.LootEnabled!=true||raid.HighValueMultiplier==1||!__1.TryGetValue(__0,out var source))return;__1=new(__1){[__0]=AdjustStatic(source,raid,Price)};}
 static void BeginAirdrop(ref SptAirdropTypeEnum? __0,out Scope __state){__state=new(){Previous=airdrop};airdrop=Read();if(__0 is SptAirdropTypeEnum.radar or SptAirdropTypeEnum.toiletPaper)airdrop.AirdropEnabled=false;if(airdrop.AirdropEnabled&&airdrop.AirdropType!="random")__0=Enum.Parse<SptAirdropTypeEnum>(airdrop.AirdropType);Log.Info($"Airdrop controls: enabled={airdrop.AirdropEnabled}, type={__0}, quality={airdrop.AirdropQuality}");}
 static void EndAirdrop(Scope? __state){if(__state!=null)airdrop=__state.Previous;}
 static void AirdropItems(ref IEnumerable<TemplateItem> __0){if(airdrop?.AirdropEnabled!=true||airdrop.AirdropWeight==1)return;var all=__0.ToArray();var rare=all.Where(i=>Price(i.Id.ToString())>=airdrop.ValueThreshold).ToArray();if(rare.Length==0)return;__0=all.Concat(Enumerable.Range(1,airdrop.AirdropWeight-1).SelectMany(_=>rare)).ToArray();}
 static void AirdropPresets(ref IEnumerable<Preset> __0){if(airdrop?.AirdropEnabled!=true||airdrop.AirdropWeight==1)return;var all=__0.ToArray();var rare=all.Where(p=>(p.Items??[]).Sum(i=>Price(i.Template.ToString()))>=airdrop.ValueThreshold).ToArray();if(rare.Length==0)return;__0=all.Concat(Enumerable.Range(1,airdrop.AirdropWeight-1).SelectMany(_=>rare)).ToArray();}
}
