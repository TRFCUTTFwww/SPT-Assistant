using System;
using System.Globalization;
namespace LocalSpt5Esp;
internal sealed record BuffOption(string Type,string Name,string Description,bool WholeBody=true);
internal static class BuffRules {
 internal static readonly BuffOption[] Common={
  new("PainKiller","止痛","抑制疼痛感受。"),
  new("PainSuppressed","疼痛抑制","暂时抑制指定部位的疼痛。",false),
  new("LightBleedingSuppressed","轻出血抑制","暂时抑制指定部位的轻出血。",false),
  new("HeavyBleedingSuppressed","重出血抑制","暂时抑制指定部位的重出血。",false),
  new("FractureSuppressed","骨折抑制","暂时抑制指定部位的骨折影响。",false),
  new("HealthBoost","生命恢复","按游戏规则提供生命恢复。"),
  new("Pain","疼痛","施加疼痛状态。",false),
  new("Tremor","手部震颤","施加震颤状态。"),
  new("LightBleeding","轻出血","施加轻出血，会持续损血。",false),
  new("HeavyBleeding","重出血","施加重出血，会持续损血。",false),
  new("Fracture","骨折","施加四肢骨折状态。",false),
  new("Contusion","挫伤／耳鸣","施加挫伤与感知干扰。"),
  new("Intoxication","未知毒素","施加未知毒素，会按当前毒素规则持续损血。")
 };
 internal static float Number(string raw,float minimum,float maximum,string label){if(!float.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out float value)||!float.IsFinite(value)||value<minimum||value>maximum)throw new ArgumentException(label+"需为 "+minimum.ToString(CultureInfo.InvariantCulture)+"～"+maximum.ToString(CultureInfo.InvariantCulture)+"。");return value;}
 internal static string Time(float seconds){if(float.IsNaN(seconds))return "未知";if(float.IsInfinity(seconds)||seconds<0||seconds>=float.MaxValue*.5f)return "无固定时限";if(seconds<=0)return "即将结束";if(seconds>long.MaxValue)return seconds.ToString("G5",CultureInfo.InvariantCulture)+" 秒";var s=(long)Math.Ceiling(seconds);return s>=3600?$"{s/3600}:{s/60%60:00}:{s%60:00}":$"{s/60:00}:{s%60:00}";}
 internal static float SubRemaining(float elapsed,float endTime)=>Math.Max(0,endTime-elapsed);
 internal static string SubState(bool active,bool processed,float elapsed,float delay,float endTime)=>active?"生效中":processed?"已结束":elapsed<delay?"待生效":elapsed>=endTime?"已结束":"未触发";
 internal static bool Matches(string value,string query)=>value.Contains(query.Trim(),StringComparison.OrdinalIgnoreCase);
 internal static bool CanRemove(string type)=>type!="Existence";
 internal static string TrackingKey(long world,long health,string profile)=>world.ToString("X")+":"+health.ToString("X")+":"+profile;
 internal static bool SameTarget(long expectedWorld,long expectedHealth,string expectedProfile,long world,long health,string profile)=>expectedWorld!=0&&expectedWorld==world&&expectedHealth!=0&&expectedHealth==health&&expectedProfile==profile;
 internal static string Name(string type)=>type switch{
  "Existence"=>"基础生理进程", "Stimulator"=>"针剂／食品效果", "Regeneration"=>"生命再生", "MedEffect"=>"药物治疗", "DamageModifier"=>"伤害倍率", "Endurance"=>"耐力增益", "Berserk"=>"狂暴", "Dehydration"=>"脱水", "Exhaustion"=>"精疲力竭", "StaminaZero"=>"体力耗尽", "ChronicStaminaFatigue"=>"慢性疲劳", "Encumbered"=>"超重", "OverEncumbered"=>"严重超重", "LowEdgeHealth"=>"生命危急", "Disorientation"=>"迷失方向", "Intoxication"=>"未知毒素", "LethalIntoxication"=>"致命中毒", "ZombieInfection"=>"感染", "Frostbite"=>"冻伤", "BodyTemperature"=>"体温变化", "MusclePain"=>"肌肉疼痛", "Wound"=>"伤口", "Stun"=>"眩晕", "Flash"=>"闪光失明", "TunnelVision"=>"隧道视野", "RadExposure"=>"辐射暴露", "TearGas" or "TearGasWeak" or "TearGasStrong"=>"催泪瓦斯", "PanicEffect"=>"恐慌", "MisfireEffect"=>"故障影响", "ScavRegeneration"=>"Scav 生命再生", "FullHealthRegenerationEffect"=>"完整生命再生", "ImmunityPreventedNegativeEffect"=>"免疫抑制负面效果", "HalloweenBuff"=>"节日效果", "SandingScreen"=>"视野颗粒效果", _=>FindName(type)
 };
 static string FindName(string type){foreach(var option in Common)if(option.Type==type)return option.Name;return type;}
}
