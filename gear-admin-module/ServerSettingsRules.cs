using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace LocalGearMail;
public sealed record ServerSetting(string Id,string Group,string Name,string Help,string File,string Path,double Minimum=0,double Maximum=1000000,bool Integer=false,bool Boolean=false,bool MinusOne=false);
public static class ServerSettingsRules {
 public static readonly ServerSetting[] Definitions=[
  new("insurance.prapor","保险与修理","Prapor 保险返还概率（%）","原生保险返还概率；被拿走的装备仍按保险模拟规则处理。","insurance","returnChancePercent.54cb50c76803fa8b248b4571",0,100),
  new("insurance.therapist","保险与修理","Therapist 保险返还概率（%）","原生保险返还概率。","insurance","returnChancePercent.54cb57776803fa99248b456e",0,100),
  new("insurance.ref","保险与修理","Ref 保险返还概率（%）","原生保险返还概率。","insurance","returnChancePercent.5c0647fdd443bc2504c2d371",0,100),
  new("insurance.return","保险与修理","保险返还等待（秒）","0 使用商人原始时间；正数覆盖返还等待时间。","insurance","returnTimeOverrideSeconds",0,int.MaxValue,true),
  new("insurance.storage","保险与修理","保险邮件领取期限（秒）","0 使用原始期限；影响随后处理的保险邮件。","insurance","storageTimeOverrideSeconds",0,int.MaxValue,true),
  new("insurance.taken","保险与修理","模拟保险物品被拿走","关闭后不模拟其他人拿走已投保物品。","insurance","simulateItemsBeingTaken",Boolean:true),
  new("repair.price","保险与修理","商人修理费用倍率","1 为原始费用；不改变修理包余量。","repair","priceMultiplier",0,1000),
  new("repair.random","保险与修理","随机化最大耐久损失","原生随机损耗；辅助的商人修复无损开关优先。","repair","applyRandomizeDurabilityLoss",Boolean:true),
  new("hideout.craft","藏身处","普通制作时间覆盖（秒）","-1 使用原始时间；只影响随后开始的普通制作。","hideout","overrideCraftTimeSeconds",-1,int.MaxValue,true,MinusOne:true),
  new("hideout.build","藏身处","设施建造时间覆盖（秒）","-1 使用原始时间；不自动完成已在建的设施。","hideout","overrideBuildTimeSeconds",-1,int.MaxValue,true,MinusOne:true),
  new("hideout.xp","藏身处","每次制作技能经验","原生制作技能经验，不是角色总经验倍率。","hideout","craftingExpAmount",0,100000),
  new("hideout.hourXp","藏身处","持续制作技能经验","按原生累计制作时间规则发放的经验。","hideout","craftingExpForHoursOfCrafting",0,100000),
  new("hideout.cultist","藏身处","邪教圈制作时间覆盖（秒）","-1 使用原始时间；影响后续献祭。","hideout","cultistCircle.craftTimeOverride",-1,int.MaxValue,true,MinusOne:true),
  new("trader.price","交易与任务","商人商品价格倍率","1 为原始价格；真实 BTC 收购仍由真实 BTC 功能单独控制。","trader","traderPriceMultiplier",0.001,1000),
  new("trader.fir","交易与任务","商人购买物品带战局标记","只影响此后购买的物品，不改现有库存。","trader","purchasesAreFoundInRaid",Boolean:true),
  new("flea.tax","交易与任务","跳蚤挂牌税费倍率","-1 使用原始税费；0 关闭挂牌税费。","ragfair","offerListingTaxMultiplier",-1,1000,MinusOne:true),
  new("flea.fees","交易与任务","启用跳蚤售卖费用","原生跳蚤售卖费用开关。","ragfair","sell.fees",Boolean:true),
  new("flea.sellChance","交易与任务","跳蚤基础售出概率（%）","物品定价等原生条件仍参与售出计算。","ragfair","sell.chance.base",0,100),
  new("flea.prices","交易与任务","跳蚤基础价格生成倍率","影响重启后重新生成的跳蚤基础价格。","ragfair","dynamic.generateBaseFleaPrices.priceMultiplier",0.001,1000),
  new("quest.mail","交易与任务","任务奖励邮件期限（小时）","普通版本角色的奖励邮件领取期限。","quest","mailRedeemTimeHours.default",1,100000,true),
  new("quest.mailUnheard","交易与任务","Unheard 奖励邮件期限（小时）","Unheard 版本角色的奖励邮件领取期限。","quest","mailRedeemTimeHours.unheard_edition",1,100000,true),
  new("raid.secureFir","出击规则","死亡时保留保险箱物品战局标记","仅控制安全容器内物品的战局发现标记，不保留全部死亡装备。","inraid","keepFiRSecureContainerOnDeath",Boolean:true),
  new("raid.allFir","出击规则","结算时始终保留战局标记","改变战局结算的战局发现标记规则。","inraid","alwaysKeepFoundInRaidOnRaidEnd",Boolean:true),
  new("raid.tarcoin","出击规则","结算时自动存入 TC 币","原生战局结束后 TC 币钱包存入开关。","inraid","depositTarcoinsOnRaidEnd",Boolean:true),
  new("raid.boss","出击规则","准备战局时默认启用 BOSS","战局准备页默认值；单次出击选择仍可覆盖。","inraid","raidMenuSettings.bossEnabled",Boolean:true),
  new("raid.scavWars","出击规则","准备战局时默认 Scav 战争","战局准备页默认值。","inraid","raidMenuSettings.scavWars",Boolean:true),
  new("raid.cursed","出击规则","准备战局时默认标记与诅咒","战局准备页默认值。","inraid","raidMenuSettings.taggedAndCursed",Boolean:true),
  new("karma.car","出击规则","车辆撤离基础 Scav 好感奖励","后续正常车辆撤离的基础增量；重复使用衰减仍按原生规则。","inraid","carExtractBaseStandingGain",0,100),
  new("karma.coop","出击规则","合作撤离基础 Scav 好感奖励","后续合作撤离的基础增量。","inraid","coopExtractBaseStandingGain",0,100),
  new("karma.scavExtract","出击规则","Scav 撤离好感奖励","后续正常 Scav 撤离的好感增量。","inraid","scavExtractStandingGain",0,100)
 ];
 public static JsonNode? Read(JsonNode root,string path){JsonNode? node=root;foreach(string part in path.Split('.'))node=(node as JsonObject)?[part];return node;}
 public static void Apply(JsonObject root,ServerSetting setting,JsonElement value){
  if(Read(root,setting.Path)==null)throw new ArgumentException("当前服务端没有此配置项。");
  JsonNode replacement;
  if(setting.Boolean){if(value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))throw new ArgumentException("开关值必须为 true 或 false。");replacement=JsonValue.Create(value.GetBoolean())!;}
  else{if(value.ValueKind!=JsonValueKind.Number||!value.TryGetDouble(out double number)||!double.IsFinite(number)||number<setting.Minimum||number>setting.Maximum||setting.Integer&&number!=Math.Truncate(number)||setting.MinusOne&&number<0&&number!=-1)throw new ArgumentException("配置数值无效或超出允许范围。");replacement=setting.Integer?JsonValue.Create((long)number)!:JsonValue.Create(number)!;}
  string[] parts=setting.Path.Split('.');JsonObject parent=root;foreach(string part in parts[..^1])parent=parent[part] as JsonObject??throw new ArgumentException("配置结构已改变，请刷新。");parent[parts[^1]]=replacement;
 }
 public static string Revision(IEnumerable<KeyValuePair<string,byte[]>> files)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",files.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>p.Key+":"+Convert.ToHexString(SHA256.HashData(p.Value)))))));
}
