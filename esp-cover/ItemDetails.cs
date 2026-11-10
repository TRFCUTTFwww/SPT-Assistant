using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
namespace LocalSpt5Esp;

// Read template data without creating native items or retaining the catalog JSON.
internal static class ItemDetails {
 internal readonly record struct Detail(string Label,string Value,string Unit="");
 internal static Detail[] Read(JsonElement props){
  var rows=new List<Detail>();
  void Number(string key,string label,string unit=""){if(TryNumber(props,key,out double value))rows.Add(new(label,Format(value),unit));}
  if(TryNumber(props,"Width",out double width)&&TryNumber(props,"Height",out double height))rows.Add(new("占用尺寸",Format(width)+" × "+Format(height)));
  Number("Weight","重量","kg");Number("StackMaxSize","最大堆叠");
  if(props.TryGetProperty("PenetrationPower",out _)){
   Number("Damage","单弹丸伤害");Number("PenetrationPower","穿透力");Number("ArmorDamage","护甲伤害","%");Number("InitialSpeed","初速","m/s");
   double count=TryNumber(props,"buckshotBullets",out var pellets)&&pellets>0?pellets:TryNumber(props,"ProjectileCount",out var projectiles)?projectiles:1;
   if(count>1){rows.Add(new("弹丸数量",Format(count)));if(TryNumber(props,"Damage",out var damage))rows.Add(new("全部弹丸理论伤害",Format(damage*count)));}
  }
  if(props.TryGetProperty("Grids",out var grids)&&grids.ValueKind==JsonValueKind.Array){
   double capacity=0;var dimensions=new List<string>();
   foreach(var grid in grids.EnumerateArray())if(grid.TryGetProperty("_props",out var settings)&&TryNumber(settings,"cellsH",out var w)&&TryNumber(settings,"cellsV",out var h)&&w>0&&h>0){capacity+=w*h;dimensions.Add(Format(w)+" × "+Format(h));}
   if(dimensions.Count>0){rows.Add(new("容纳格数",Format(capacity)));rows.Add(new("独立分区",string.Join(" / ",dimensions)));}
  }
  Number("armorClass","防护等级");
  if(props.TryGetProperty("ArmorMaterial",out var material)&&material.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(material.GetString()))rows.Add(new("防护材质",Material(material.GetString()!)));
  Number("MaxDurability","模板最大耐久");Number("MaxHpResource","医疗资源");Number("MaxResource","食物资源");Number("MaximumNumberOfUsage","模板使用次数");
  return rows.ToArray();
 }
 static string Material(string value)=>value switch{"Aluminium"=>"铝合金","Aramid"=>"芳纶","ArmoredSteel"=>"装甲钢","Ceramic"=>"陶瓷","Combined"=>"复合材料","Glass"=>"防弹玻璃","Titan"=>"钛合金","UHMWPE"=>"超高分子量聚乙烯",_=>value};
 static bool TryNumber(JsonElement props,string key,out double value){value=0;return props.TryGetProperty(key,out var raw)&&(raw.ValueKind==JsonValueKind.Number?raw.TryGetDouble(out value):raw.ValueKind==JsonValueKind.String&&double.TryParse(raw.GetString(),NumberStyles.Float,CultureInfo.InvariantCulture,out value))&&double.IsFinite(value);}
 static string Format(double value)=>value.ToString("0.###",CultureInfo.InvariantCulture);
}
