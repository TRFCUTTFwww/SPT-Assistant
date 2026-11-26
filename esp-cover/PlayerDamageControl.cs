using BepInEx.Configuration;
namespace LocalSpt5Esp;
internal static class PlayerDamageControl {
 static ConfigEntry<bool> enabled=null!;static ConfigEntry<float> multiplier=null!;
 internal static void Initialize(){enabled=FeatureConfig.Bind("39. 玩家伤害","Enabled","启用玩家伤害倍率",false,"调整玩家对其他角色造成的生命伤害。");multiplier=FeatureConfig.Bind("39. 玩家伤害","Outgoing damage multiplier","玩家伤害倍率",2f,"1 为正常伤害。",new AcceptableValueRange<float>(0,10));}
 internal static double Factor=>enabled!=null&&enabled.Value?multiplier.Value:1d;
}
