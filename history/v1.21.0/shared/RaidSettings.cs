using System;
using System.Collections.Generic;
namespace LocalRaidShared;
public sealed class RaidSettings {
 public int Schema {get;set;}=1;
 public bool KeepOnDeath {get;set;}
 public bool NoAi {get;set;}
 public bool NoLoot {get;set;}
 public bool AiEnabled {get;set;}
 public System.Collections.Generic.Dictionary<string,double> FactionMultipliers {get;set;}=new();
 public double SpawnMultiplier(string? role)=>NoAi?0:AiEnabled?FactionMultipliers.GetValueOrDefault(RaidSpawnRules.Faction(role),1):1;
 public void RestoreAiDefaults(){NoAi=false;NoLoot=false;AiEnabled=false;FactionMultipliers.Clear();}
 public void RestoreLootDefaults(){LootEnabled=false;LooseMultiplier=ContainerMultiplier=HighValueMultiplier=1;ValueThreshold=100000;}
 public bool LootEnabled {get;set;}
 public double LooseMultiplier {get;set;}=1;
 public double ContainerMultiplier {get;set;}=1;
 public double HighValueMultiplier {get;set;}=1;
 public double ValueThreshold {get;set;}=100000;
 public bool AirdropEnabled {get;set;}
 public string AirdropType {get;set;}="random";
 public int AirdropQuality {get;set;}
 public void Validate(){if(Schema!=1)throw new ArgumentException("设置版本不受支持。");void Nonnegative(double value,string name){if(!double.IsFinite(value)||value<0)throw new ArgumentException(name+"须为非负有限数字。");}
  if(FactionMultipliers==null)throw new ArgumentException("阵营设置无效。");foreach(var pair in FactionMultipliers){if(Array.IndexOf(RaidSpawnRules.Factions,pair.Key)<0)throw new ArgumentException("未知阵营："+pair.Key);Nonnegative(pair.Value,"阵营刷新倍率");}
  Nonnegative(LooseMultiplier,"散落物倍率");Nonnegative(ContainerMultiplier,"容器物资倍率");Nonnegative(HighValueMultiplier,"高价值权重倍率");if(HighValueMultiplier==0)throw new ArgumentException("高价值权重倍率须大于 0，候选概率池必须保留正权重。");Nonnegative(ValueThreshold,"高价值门槛");if(Array.IndexOf(new[]{"random","mixed","weaponArmor","foodMedical","barter"},AirdropType)<0||AirdropQuality<0||AirdropQuality>2)throw new ArgumentException("空投类型或品质无效。");}
 public double Weight(double original,double price)=>price>=ValueThreshold?original*HighValueMultiplier:original;
 // Preserve normal weights exactly; normalize the whole pool together before float/sum overflow.
 public double[] Weights((double Weight,double Price)[] candidates){var result=new double[candidates.Length];bool normalize=false;double ceiling=(double)float.MaxValue/Math.Max(1,candidates.Length);
  for(int i=0;i<result.Length;i++){var candidate=candidates[i];if(!double.IsFinite(candidate.Weight)||candidate.Weight<0)throw new ArgumentException("候选权重无效。");result[i]=Weight(candidate.Weight,candidate.Price);if(!double.IsFinite(result[i])||result[i]>ceiling||candidate.Weight>0&&candidate.Price>=ValueThreshold&&HighValueMultiplier>0&&(float)result[i]==0)normalize=true;}
  if(!normalize)return result;double peak=double.NegativeInfinity;for(int i=0;i<result.Length;i++){var c=candidates[i];double log=c.Weight==0||c.Price>=ValueThreshold&&HighValueMultiplier==0?double.NegativeInfinity:Math.Log(c.Weight)+(c.Price>=ValueThreshold?Math.Log(HighValueMultiplier):0);result[i]=log;peak=Math.Max(peak,log);}for(int i=0;i<result.Length;i++)result[i]=double.IsNegativeInfinity(peak)?0:Math.Exp(result[i]-peak);return result;
 }
 public int AirdropWeight=>AirdropQuality==0?1:AirdropQuality==1?3:8;
}
