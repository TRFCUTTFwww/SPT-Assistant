using System.Globalization;
using LocalRaidShared;
using SPTarkov.Server.Core.Models.Eft.Common;
namespace LocalRaidControls;
public static class RaidGenerationRules {
 public static string EscortAmount(string? amount,double multiplier){if(multiplier==1)return amount??"0";return string.Join(",",(amount??"0").Split(',').Select(raw=>int.TryParse(raw.Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out int count)&&count>=0?RaidSpawnRules.Count(count,multiplier).ToString(CultureInfo.InvariantCulture):throw new InvalidDataException("Invalid escort amount: "+raw)));}
 public static SpawnpointTemplate EmptyContainer(SpawnpointTemplate source){var items=(source.Items??[]).ToArray();var root=items.FirstOrDefault(item=>item.Id.ToString()==source.Root)??items.FirstOrDefault(item=>item.ParentId==null);if(root==null)throw new InvalidDataException("Container has no root: "+source.Id);var keep=new List<SptLootItem>{root};var known=new HashSet<string>{root.Id.ToString()};bool changed;do{changed=false;foreach(var item in items)if(!known.Contains(item.Id.ToString())&&item.ParentId is string parent&&known.Contains(parent)&&item.SlotId?.StartsWith("mod_",StringComparison.Ordinal)==true){keep.Add(item);known.Add(item.Id.ToString());changed=true;}}while(changed);return source with {Items=keep};}
 public static List<SpawnpointTemplate> EmptyLoot(IEnumerable<SpawnpointTemplate> source)=>source.Where(point=>point.IsContainer).Select(EmptyContainer).ToList();
 public static LocationBase Apply(LocationBase source,RaidSettings settings){
  var result=source;
  if(settings.NoAi){result=result with {BossLocationSpawn=[],Waves=[],MinMaxBots=[],BotSpawnCountStep=0,NewSpawn=false,OldSpawn=false,OfflineNewSpawn=false,OfflineOldSpawn=false,NonWaveGroupScenario=source.NonWaveGroupScenario is {} scenario?scenario with {IsEnabled=false}:new(){IsEnabled=false,Chance=0,MaximumToBeGrouped=0,MinimumToBeGrouped=0}};}
  else if(settings.AiEnabled&&settings.FactionMultipliers.Values.Any(value=>value!=1)){
   var waves=(source.Waves??[]).Select(wave=>{double factor=settings.SpawnMultiplier(wave.WildSpawnType.ToString());return factor==1?wave:wave with {SlotsMin=RaidSpawnRules.Count(wave.SlotsMin,factor),SlotsMax=RaidSpawnRules.Count(wave.SlotsMax,factor)};}).Where(wave=>wave.SlotsMax>0).ToList();
   var bosses=new List<BossLocationSpawn>();foreach(var boss in source.BossLocationSpawn??[]){double factor=settings.SpawnMultiplier(boss.BossName);if(factor==0)continue;string leader=RaidSpawnRules.Faction(boss.BossName),escort=RaidSpawnRules.Faction(boss.BossEscortType);double escortFactor=leader==escort?1:settings.SpawnMultiplier(boss.BossEscortType);var copy=boss with {BossChance=factor==1?boss.BossChance:RaidSpawnRules.Chance(boss.BossChance??0,factor),BossEscortAmount=EscortAmount(boss.BossEscortAmount,escortFactor),Supports=(boss.Supports??[]).Select(support=>support with {BossEscortAmount=EscortAmount(support.BossEscortAmount,RaidSpawnRules.Faction(support.BossEscortType)==leader?1:settings.SpawnMultiplier(support.BossEscortType))}).ToArray()};bosses.Add(copy);}
   var limits=(source.MinMaxBots??[]).Where(limit=>settings.SpawnMultiplier(limit.WildSpawnType)>0).Select(limit=>limit with {Min=RaidSpawnRules.Count(limit.Min,settings.SpawnMultiplier(limit.WildSpawnType)),Max=RaidSpawnRules.Count(limit.Max,settings.SpawnMultiplier(limit.WildSpawnType))}).ToList();
   bool scavOff=settings.SpawnMultiplier("assault")==0;result=result with {Waves=waves,BossLocationSpawn=bosses,MinMaxBots=limits,NewSpawn=scavOff?false:source.NewSpawn,OfflineNewSpawn=scavOff?false:source.OfflineNewSpawn};
  }
  if(settings.NoLoot)result=result with {Loot=EmptyLoot(result.Loot??[]),AirdropParameters=[]};
  return result;
 }
}
