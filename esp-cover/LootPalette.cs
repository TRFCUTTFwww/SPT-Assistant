using System;
using BepInEx.Configuration;
using UnityEngine;

namespace LocalSpt5Esp;

internal static class LootPalette {
 const string Section="06. 物品与容器详情",RgbHelp="填写 #RRGGBB 十六进制颜色。";
 static readonly (LootCategory Category,string Key,string Label,string Hex)[] defaults={
  (LootCategory.武器,"Weapon color","武器颜色","#FFAE70"),
  (LootCategory.弹药,"Ammo color","弹药颜色","#FFD46A"),
  (LootCategory.配件,"Mod color","配件颜色","#D3B3FF"),
  (LootCategory.防具装备,"Equipment color","防具和装备颜色","#8FDBD0"),
  (LootCategory.医疗,"Medical color","医疗用品颜色","#92E6A0"),
  (LootCategory.食物饮料,"Food color","食物和饮料颜色","#69B9FF"),
  (LootCategory.钥匙,"Key color","钥匙和门卡颜色","#E9ABFF"),
  (LootCategory.交易材料,"Barter color","交易材料颜色","#FFDA69"),
  (LootCategory.其他,"Other color","其他物品颜色","#FFF0A5"),
  (LootCategory.任务物品,"Quest color","任务物品颜色","#FF9ED3"),
 };
 static readonly Swatch[] swatches=new Swatch[Enum.GetValues<LootCategory>().Length];
 static Swatch strongAmmo=null!,cash=null!,documents=null!;

 internal static void Initialize(){
  foreach(var entry in defaults)swatches[(int)entry.Category]=new(entry.Key,entry.Label,entry.Hex);
  strongAmmo=new("Strong ammo color","高伤害和高穿透弹药颜色","#FF4C53","弹药按伤害和穿透从弹药颜色渐变到此颜色。");
  cash=new("Cash color","现金颜色","#8FF294");
  documents=new("Season document color","赛季文件颜色","#D5AAFF");
 }

 internal static Color For(ItemCatalog.EspStyle style){
  if(style.Cash)return cash.Value;
  if(style.SeasonDocument)return documents.Value;
  var color=swatches[(int)style.Category].Value;
  return style.AmmoStrength is float strength?Color.Lerp(color,strongAmmo.Value,strength):color;
 }

 sealed class Swatch(string key,string name,string fallback,string help=RgbHelp) {
  readonly ConfigEntry<string> entry=FeatureConfig.Bind(Section,key,name,fallback,help);
  string? last;
  Color color;

  internal Color Value {
   get {
    string code=entry.Value??fallback;
    if(code==last)return color;
    last=code;
    return color=code.Length==7&&code[0]=='#'&&ColorUtility.TryParseHtmlString(code,out var parsed)
     ?parsed:ColorUtility.TryParseHtmlString(fallback,out var original)?original:Color.white;
   }
  }
 }
}
