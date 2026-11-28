using BepInEx.Configuration;

namespace LocalSpt5Esp;

internal static class InventoryContextSettings {
 static ConfigEntry<bool> add=null!,edit=null!,fir=null!;
 internal static bool Add=>add?.Value==true;
 internal static bool Edit=>edit?.Value==true;
 internal static bool Fir=>fir?.Value==true;
 internal static void Initialize(){
  const string section="76. 库存右键菜单";
  add=FeatureConfig.Bind(section,"Enable grid context add","启用右键添加物品",true,"在空格位的游戏原生右键菜单中显示添加物品入口。");
  edit=FeatureConfig.Bind(section,"Enable item context edit","启用右键编辑物品",true,"在物品的游戏原生右键菜单中显示编辑入口。");
  fir=FeatureConfig.Bind(section,"Enable item context FIR","启用右键设置 FIR",true,"在物品的游戏原生右键菜单中显示添加或移除战局内发现标记。");
 }
}
