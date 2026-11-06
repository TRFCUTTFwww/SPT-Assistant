using System;
using System.Collections.Generic;
namespace LocalRaidShared;
public sealed class RaidSettings {
 public int Schema {get;set;}=1;
 public bool LiveBitcoinEnabled {get;set;}
 public bool ItemWeightsEnabled {get;set;}
 public Dictionary<string,double> ItemWeights {get;set;}=new();
 public bool KeepOnDeath {get;set;}
 public bool TraderRepairNoLoss {get;set;}=true;
 public bool AllowContainerNesting {get;set;}=true;
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
  if(ItemWeights==null)throw new ArgumentException("单物品权重设置无效。");foreach(var pair in ItemWeights){if(pair.Key.Length!=24)throw new ArgumentException("物品编号无效。");foreach(char c in pair.Key)if(!Uri.IsHexDigit(c))throw new ArgumentException("物品编号无效。");Nonnegative(pair.Value,"单物品权重");}
  Nonnegative(LooseMultiplier,"散落物倍率");Nonnegative(ContainerMultiplier,"容器物资倍率");Nonnegative(HighValueMultiplier,"高价值权重倍率");if(HighValueMultiplier==0)throw new ArgumentException("高价值权重倍率须大于 0，候选概率池必须保留正权重。");Nonnegative(ValueThreshold,"高价值门槛");if(Array.IndexOf(new[]{"random","mixed","weaponArmor","foodMedical","barter"},AirdropType)<0||AirdropQuality<0||AirdropQuality>2)throw new ArgumentException("空投类型或品质无效。");}
 public double Weight(double original,double price)=>price>=ValueThreshold?original*HighValueMultiplier:original;
 // Preserve normal weights exactly; normalize the whole pool together before float/sum overflow.
 public double[] Weights((double Weight,double Price)[] candidates){var result=new double[candidates.Length];bool normalize=false;double ceiling=(double)float.MaxValue/Math.Max(1,candidates.Length);
  for(int i=0;i<result.Length;i++){var candidate=candidates[i];if(!double.IsFinite(candidate.Weight)||candidate.Weight<0)throw new ArgumentException("候选权重无效。");result[i]=Weight(candidate.Weight,candidate.Price);if(!double.IsFinite(result[i])||result[i]>ceiling||candidate.Weight>0&&candidate.Price>=ValueThreshold&&HighValueMultiplier>0&&(float)result[i]==0)normalize=true;}
  if(!normalize)return result;double peak=double.NegativeInfinity;for(int i=0;i<result.Length;i++){var c=candidates[i];double log=c.Weight==0||c.Price>=ValueThreshold&&HighValueMultiplier==0?double.NegativeInfinity:Math.Log(c.Weight)+(c.Price>=ValueThreshold?Math.Log(HighValueMultiplier):0);result[i]=log;peak=Math.Max(peak,log);}for(int i=0;i<result.Length;i++)result[i]=double.IsNegativeInfinity(peak)?0:Math.Exp(result[i]-peak);return result;
 }
 public bool HasItemWeights=>ItemWeightsEnabled&&ItemWeights.Count>0;
 public double ItemFactor(string template,double price)=>HasItemWeights&&ItemWeights.TryGetValue(template,out var factor)?factor:LootEnabled&&price>=ValueThreshold?HighValueMultiplier:1;
 public int AirdropWeight=>AirdropQuality==0?1:AirdropQuality==1?3:8;
}

public static class LootWeightMath {
 public static double[] Scale((double Weight,double Factor)[] values){var raw=new double[values.Length];bool normalize=false;double max=(double)float.MaxValue/Math.Max(1,values.Length);for(int i=0;i<values.Length;i++){var v=values[i];if(!double.IsFinite(v.Weight)||!double.IsFinite(v.Factor)||v.Weight<0||v.Factor<0)throw new ArgumentException("候选权重无效。");raw[i]=v.Weight==0||v.Factor==0?0:v.Weight*v.Factor;if(!double.IsFinite(raw[i])||raw[i]>max||v.Weight>0&&v.Factor>0&&(float)raw[i]==0)normalize=true;}if(!normalize)return raw;var logs=new double[values.Length];for(int i=0;i<values.Length;i++)logs[i]=Log(values[i].Weight)+Log(values[i].Factor);double peak=double.NegativeInfinity;foreach(double v in logs)peak=Math.Max(peak,v);for(int i=0;i<logs.Length;i++)raw[i]=double.IsNegativeInfinity(peak)?0:Math.Exp(logs[i]-peak);return raw;}
 public static double Log(double value)=>value<=0?double.NegativeInfinity:Math.Log(value);
 public static double SumLog(double[] logs){double peak=double.NegativeInfinity;foreach(double value in logs)peak=Math.Max(peak,value);if(double.IsNegativeInfinity(peak))return peak;double sum=0;foreach(double value in logs)sum+=Math.Exp(value-peak);return peak+Math.Log(sum);}
 public static double AverageFactor((double Weight,double Factor)[] values){var before=new double[values.Length];var after=new double[values.Length];for(int i=0;i<values.Length;i++){before[i]=Log(values[i].Weight);after[i]=before[i]+Log(values[i].Factor);}double a=SumLog(after),b=SumLog(before);return double.IsNegativeInfinity(a)||double.IsNegativeInfinity(b)?0:Math.Exp(Math.Min(Math.Log(double.MaxValue),a-b));}
 public static int Pick(double[] weights,double unit){if(!double.IsFinite(unit)||unit<0||unit>=1)throw new ArgumentException("随机数无效。");double sum=0;foreach(double weight in weights)sum+=weight;if(sum<=0)return -1;double choice=unit*sum;int last=-1;for(int i=0;i<weights.Length;i++)if(weights[i]>0){last=i;choice-=weights[i];if(choice<0)return i;}return last;}
}

public sealed record BitcoinQuote(double Rub,double Usd,double GameRub,DateTime FetchedUtc,string Source);
public sealed class BitcoinStatus {public bool Enabled{get;set;}public bool Available{get;set;}public double? OriginalGameRub{get;set;}public BitcoinQuote? Quote{get;set;}public string Error{get;set;}="";public int ServerPid{get;set;}public DateTime UpdatedUtc{get;set;}}
public static class BitcoinRules {
 public const string Template="59faff1d86f7746c51718c9c";
 public static double GamePrice(double rub){if(!double.IsFinite(rub)||rub<=0||rub*.2>float.MaxValue)throw new ArgumentException("BTC 卢布价格无效。");return (double)(float)Math.Max(1,Math.Round(rub*.2,MidpointRounding.AwayFromZero));}
 public static BitcoinQuote Parse(string response,DateTime fetchedUtc){using var data=System.Text.Json.JsonDocument.Parse(response);var quote=data.RootElement.GetProperty("data");if(quote.GetProperty("currency").GetString()!="BTC")throw new ArgumentException("行情基准币种不是 BTC。");double Rate(string currency){var raw=quote.GetProperty("rates").GetProperty(currency);string value=raw.ValueKind==System.Text.Json.JsonValueKind.String?raw.GetString()??"":raw.GetRawText();if(!double.TryParse(value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double result)||!double.IsFinite(result)||result<=0)throw new ArgumentException("行情汇率无效："+currency);return result;}double rub=Rate("RUB"),usd=Rate("USD");return new(rub,usd,GamePrice(rub),fetchedUtc,"Coinbase");}
 public static bool Valid(BitcoinQuote? quote)=>quote!=null&&double.IsFinite(quote.Usd)&&quote.Usd>0&&double.IsFinite(quote.Rub)&&quote.Rub>0&&quote.GameRub==GamePrice(quote.Rub)&&quote.FetchedUtc!=default;
}
