using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using BepInEx.Configuration;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class LootDisplay {
 internal static ConfigEntry<bool> Prices=null!,Contents=null!,AmmoDetails=null!;
 internal static ConfigEntry<int> ContentLines=null!,ContainerLimit=null!;
 internal static ConfigEntry<float> ContentRange=null!;
 internal static ConfigEntry<int> HighValueThreshold=null!;
 internal static ConfigEntry<int> Opacity=null!;
 internal static readonly Color HighValueColor=new(1f,0.48f,0.05f);
 static readonly Color ContainerColor=new(0.3f,0.9f,1f);
 static Dictionary<string,double> prices=new(StringComparer.Ordinal);
 static Dictionary<string,string> names=new(StringComparer.Ordinal);
 static readonly Dictionary<IntPtr,Snapshot> snapshots=new();
 readonly record struct RowKey(string Name,ItemCatalog.EspStyle Style);
 record Row(RowKey Key,int Count,double Value);
 internal readonly record struct AmmoStack(string Id,int Count);
 internal const int AmmoDetailLineCount=7;
 record Snapshot(string Name,double Value,List<Row> Lines,AmmoStack[] Ammo,float Expires);
 internal static void Initialize(Plugin p){
  Opacity=CnConfig.Bind(p.Config,"6. Loot details","Opacity percent",50,new ConfigDescription("Opacity of loot/container text, shadows, panels and position lines. 0 = invisible; 50 = semi-transparent; 100 = full opacity.",new AcceptableValueRange<int>(0,100)));
  Prices=CnConfig.Bind(p.Config,"6. Loot details","Show item values",true,"Estimated ruble value from the server price snapshot, including stack quantity and attachments. Not a live sale quote.");
  Contents=CnConfig.Bind(p.Config,"6. Loot details","Show container contents",true,"Show container name, contents and total estimated value.");
  AmmoDetails=FeatureConfig.Bind("06. 物品与容器详情","Show ammo details","显示弹药详情",true,
   "准星指向地面弹药时显示参数与相邻等级；查看容器时可用详情热键翻到弹药页。");
  ContentRange=CnConfig.Bind(p.Config,"6. Loot details","Contents distance",35f,new ConfigDescription("Detailed contents within this many meters",new AcceptableValueRange<float>(5,150)));
  ContentLines=CnConfig.Bind(p.Config,"6. Loot details","Maximum contents lines",8,new ConfigDescription("Most valuable item types first; total includes hidden lines",new AcceptableValueRange<int>(1,30)));
  ContainerLimit=CnConfig.Bind(p.Config,"6. Loot details","Maximum containers",32,new ConfigDescription("Closest containers to evaluate per scan",new AcceptableValueRange<int>(1,100)));
  HighValueThreshold=CnConfig.Bind(p.Config,"6. Loot details","High value threshold",100000,new ConfigDescription("",new AcceptableValueRange<int>(1,100000000)));
  LootPalette.Initialize();
  string dir=Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP");
  try{prices=JsonSerializer.Deserialize<Dictionary<string,double>>(File.ReadAllText(Path.Combine(dir,"esp5-prices.json")))??new();names=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(dir,"esp5-names.json")))??new();p.Log.LogInfo($"Loot price snapshot loaded: {prices.Count} prices, {names.Count} names");}
  catch(Exception e){p.Log.LogWarning("Loot price/name snapshot unavailable: "+e.Message);}
  try{string fallback=Path.Combine(dir,"item-name-fallbacks.json");if(File.Exists(fallback))foreach(var pair in JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(fallback))??new()){
   if(pair.Key.EndsWith(" ShortName",StringComparison.Ordinal)&&names.TryGetValue(pair.Key[..^10],out var fullName)&&ReadableName(fullName))continue;
   if(!names.TryGetValue(pair.Key,out var existing)||!ReadableName(existing))names[pair.Key]=pair.Value;
  }}
  catch(Exception e){p.Log.LogWarning("Item name supplement unavailable: "+e.Message);}
  foreach(var clue in new[]{("68616c6c6f7765656e6b6579","停车场车钥匙"),("68616c6c6f7765656e6d6170","收集者地图")}){names[clue.Item1]=clue.Item2;names[clue.Item1+" ShortName"]=clue.Item2;}
 }
 internal static void Reset()=>snapshots.Clear();
 internal static void Shutdown(){snapshots.Clear();prices=new(StringComparer.Ordinal);names=new(StringComparer.Ordinal);}
 [System.Text.RegularExpressions.GeneratedRegex("^[a-fA-F0-9]{24}( Name| ShortName| Description)?$")]
 private static partial System.Text.RegularExpressions.Regex UnlocalizedName();
 static bool ReadableName(string? text)=>!string.IsNullOrWhiteSpace(text)&&!UnlocalizedName().IsMatch(text);
 internal static string TemplateName(string id){
  foreach(var key in new[]{id+" ShortName",id,id+" Name"})if(names.TryGetValue(key,out var name)&&ReadableName(name))return name;
  foreach(var suffix in new[]{" ShortName"," Name"}){string text=LocalizationExtensions.Localized(id+suffix,EStringCase.None);if(ReadableName(text)){names[id+suffix]=text;return text;}}
  return id;
 }
 internal static string ItemName(Item item,bool shortName=true){
  var id=item.StringTemplateId;
  if(shortName&&names.TryGetValue(id+" ShortName",out var shortText)&&ReadableName(shortText))return shortText;
  if(names.TryGetValue(id,out var full)&&ReadableName(full))return full;
  if(names.TryGetValue(id+" Name",out full)&&ReadableName(full))return full;
  var raw=shortName?item.ShortName:item.Name;
  string text=LocalizationExtensions.Localized(raw,EStringCase.None);
  if(!ReadableName(text))text=LocalizationExtensions.Localized(id+" Name",EStringCase.None);
  if(!ReadableName(text))text=LocalizationExtensions.Localized(item.Name,EStringCase.None);
  if(!ReadableName(text))return "未命名物品 · "+id;
  names[shortName?id+" ShortName":id]=text;return text;
 }
 internal static string Money(double value)=>value>0?"₽"+Math.Round(value).ToString("N0",CultureInfo.InvariantCulture):"价格未知";
 internal static IEnumerable<Item> Children(Item item){
  return item.TryCast<CompoundItem>() is {} compound?Children(compound):Array.Empty<Item>();
 }
 static IEnumerable<Item> Children(CompoundItem compound){
  if(compound.Grids!=null)for(int i=0;i<compound.Grids.Length;i++){var grid=compound.Grids[i];if(grid==null)continue;foreach(var child in grid.Items)if(child!=null)yield return child;}
  if(compound.Slots!=null)for(int i=0;i<compound.Slots.Length;i++){var child=compound.Slots[i]?.ContainedItem;if(child!=null)yield return child;}
  var magazine=compound.TryCast<Magazine>();if(magazine?.Cartridges!=null)foreach(var round in magazine.Cartridges.Items)if(round!=null)yield return round;
 }
 internal static double TemplatePrice(string id)=>id==LocalRaidShared.BitcoinRules.Template&&BitcoinControl.Price is double live?live:prices.TryGetValue(id,out var price)?price:0;
 internal static double Value(Item root)=>root==null?0:ValueTree(root,root.StringTemplateId,Math.Max(1,root.StackObjectsCount));
 static double ValueTree(Item item,string id,int count,bool forEsp=false,HashSet<IntPtr>? seen=null,int depth=0,
  Dictionary<string,int>? rounds=null){
  if(depth>12||seen?.Add(item.Pointer)==false)return 0;
  if(rounds!=null&&ItemCatalog.Ammo(id)!=null)rounds[id]=rounds.GetValueOrDefault(id)+count;
  if(rounds!=null&&ItemCatalog.IsAmmoBox(id)){
   ReadAmmoBox(item,rounds);
   rounds=null;
  }
  double total=(!forEsp||ItemCatalog.ForEsp(id).HasEstimate)?TemplatePrice(id)*count:0;
  if(item.TryCast<CompoundItem>() is not {} compound)return total;
  seen??=new HashSet<IntPtr>{item.Pointer};
  foreach(var child in Children(compound))
   total+=ValueTree(child,child.StringTemplateId,Math.Max(1,child.StackObjectsCount),forEsp,seen,depth+1,rounds);
  return total;
 }
 internal static string LooseLabel(Item item,float distance,out Color color,AmmoStack[]? ammo){
  string id=item.StringTemplateId;
  var style=ItemCatalog.ForEsp(id);
  color=LootPalette.For(AmmoBoxStyle(style,ammo));
  int count=item.StackObjectsCount;
  string quantity=count>1?" ×"+count:"";
  string estimate=Prices.Value&&style.HasEstimate?I18n.Text("  估值 ")+Money(ValueTree(item,id,Math.Max(1,count),forEsp:true)):"";
  return ItemName(item)+quantity+$" [{distance:F0}m]"+estimate+TaskNeeds.Hint(id,item.SpawnedInSession);
 }
 internal static AmmoStack[]? LooseAmmo(Item item){
  string id=item.StringTemplateId;
  if(ItemCatalog.Ammo(id)!=null)return [new(id,Math.Max(1,item.StackObjectsCount))];
  if(!ItemCatalog.IsAmmoBox(id))return null;
  var rounds=new Dictionary<string,int>(StringComparer.Ordinal);
  ReadAmmoBox(item,rounds);
  return SortedAmmo(rounds);
 }
 static void ReadAmmoBox(Item item,Dictionary<string,int> rounds){
  var seen=new HashSet<IntPtr>();
  var contents=item.TryCast<AmmoBox>()?.Cartridges?.Items;
  if(contents==null)return;
  foreach(var child in contents){
   if(child==null)continue;
   var id=child.StringTemplateId;
   if(ItemCatalog.Ammo(id)!=null&&seen.Add(child.Pointer))
    rounds[id]=rounds.GetValueOrDefault(id)+Math.Max(1,child.StackObjectsCount);
  }
 }
 static AmmoStack[] SortedAmmo(Dictionary<string,int> rounds)=>rounds
  .Select(pair=>new AmmoStack(pair.Key,pair.Value))
  .OrderBy(stack=>TemplateName(stack.Id),StringComparer.Ordinal).ThenBy(stack=>stack.Id,StringComparer.Ordinal).ToArray();
 static ItemCatalog.EspStyle AmmoBoxStyle(ItemCatalog.EspStyle style,AmmoStack[]? ammo){
  if(ammo is not {Length:>0})return style;
  float strongest=0;
  foreach(var stack in ammo)if(ItemCatalog.Ammo(stack.Id) is {} round)
   strongest=Math.Max(strongest,ItemCatalog.Strength(round));
  return style with {AmmoStrength=strongest};
 }
 internal static (string Text,Color Color)[] AmmoLines(AmmoStack stack){
  var round=ItemCatalog.Ammo(stack.Id)!;
  var pair=ItemCatalog.Neighbors(round);
  string Label(string key)=>I18n.Text(key);
  string Format(double? value,string unit="")=>value is double number?number.ToString("0.##",CultureInfo.InvariantCulture)+unit:"—";
  Color Tint(ItemCatalog.AmmoStats ammo)=>LootPalette.For(new(LootCategory.弹药,ItemCatalog.Strength(ammo)));
  (string,Color) Neighbor(ItemCatalog.AmmoStats? neighbor,string label,string boundary)=>neighbor==null?
   (Label(label)+" · "+Label(boundary),Color.white):
   (Label(label)+" · "+TemplateName(neighbor.Id)+"   "+Label("穿透力")+" "+Format(neighbor.Penetration)+
    " / "+Label("伤害")+" "+Format(neighbor.Damage),Tint(neighbor));
  string caliber=LocalizationExtensions.Localized(round.Caliber,EStringCase.None);
  if(string.IsNullOrWhiteSpace(caliber)||caliber==round.Caliber)
   caliber=ItemCatalog.FormatCaliber(round.Caliber);
  string damage=round.Projectiles>1?Format(round.Damage)+" × "+Format(round.Projectiles):Format(round.Damage);
  return [
   (TemplateName(round.Id)+" ×"+stack.Count,Tint(round)),
   (Label("口径")+" · "+caliber,Color.white),
   (Label("伤害")+" "+damage+"   ·   "+Label("穿透力")+" "+Format(round.Penetration),Color.white),
   (Label("护甲伤害")+" "+Format(round.ArmorDamage,"%")+"   ·   "+Label("初速")+" "+Format(round.Speed," m/s"),Color.white),
   (Label("后坐修正")+" "+(round.Recoil>0?"+":"")+Format(round.Recoil,"%"),Color.white),
   Neighbor(pair.Higher,"上一级弹药","已是最高级"),
   Neighbor(pair.Lower,"下一级弹药","已是最低级")
  ];
 }
 static void AddRow(Dictionary<RowKey,(int Count,double Value)> rows,RowKey key,int count,double value){
  rows.TryGetValue(key,out var prior);
  rows[key]=(prior.Count+count,prior.Value+value);
 }
 static List<Row> SortedRows(Dictionary<RowKey,(int Count,double Value)> rows)=>
  rows.OrderByDescending(x=>x.Value.Value).ThenBy(x=>x.Key.Name,StringComparer.Ordinal)
   .Select(x=>new Row(x.Key,x.Value.Count,x.Value.Value)).ToList();
 static void AppendRows(StringBuilder label,List<Color> colors,List<Row> rows,Color overflowColor){
  int limit=Math.Min(ContentLines.Value,rows.Count);
  for(int i=0;i<limit;i++){
   var row=rows[i];
   label.Append('\n').Append(row.Key.Name).Append(" ×").Append(row.Count);
   if(Prices.Value&&row.Key.Style.HasEstimate)label.Append("  ").Append(Money(row.Value));
   colors.Add(LootPalette.For(row.Key.Style));
  }
  if(rows.Count<=limit)return;
  label.Append(I18n.Text("\n…另 ")).Append(rows.Count-limit).Append(I18n.Text(" 类物品"));
  colors.Add(overflowColor);
 }
 internal static string ContainerLabel(LootableContainer container,float distance,out Color[] colors,out AmmoStack[] ammo){
  bool expanded=Contents.Value&&distance<=ContentRange.Value;
  if(!snapshots.TryGetValue(container.Pointer,out var snap)||Time.unscaledTime>=snap.Expires){
   var root=container.ItemOwner?.RootItem;
   string name="容器";
   if(!string.IsNullOrWhiteSpace(container.Template)&&names.TryGetValue(container.Template,out var templateName))name=templateName;
   else if(root!=null&&names.TryGetValue(root.StringTemplateId,out var rootName))name=rootName;
   else if(!string.IsNullOrWhiteSpace(container.ItemOwner?.ContainerName))name=LocalizationExtensions.Localized(container.ItemOwner.ContainerName,EStringCase.None);
   else if(!string.IsNullOrWhiteSpace(container.TypeKey))name=LocalizationExtensions.Localized(container.TypeKey,EStringCase.None);
   var rows=new Dictionary<RowKey,(int Count,double Value)>();double total=0;
   var rounds=new Dictionary<string,int>(StringComparer.Ordinal);
   if(root!=null)foreach(var item in Children(root)){
    string id=item.StringTemplateId;
    var style=ItemCatalog.ForEsp(id);
    if(ItemCatalog.IsAmmoBox(id))style=AmmoBoxStyle(style,LooseAmmo(item));
    int count=Math.Max(1,item.StackObjectsCount);
    double value=ValueTree(item,id,count,forEsp:true,rounds:rounds);
    total+=value;
    AddRow(rows,new(ItemName(item),style),count,value);
   }
   snap=new Snapshot(name,total,SortedRows(rows),SortedAmmo(rounds),Time.unscaledTime+3f);snapshots[container.Pointer]=snap;
  }
  var sb=new StringBuilder(snap.Name+$" [{distance:F0}m]");
  var lineColors=new List<Color>{snap.Lines.Count>0&&snap.Lines[0].Value>=HighValueThreshold.Value?HighValueColor:ContainerColor};
  if(Prices.Value&&(snap.Lines.Count==0||snap.Lines.Any(row=>row.Key.Style.HasEstimate)))
   sb.Append(I18n.Text("  估值 ")).Append(snap.Lines.Count==0?"₽0":Money(snap.Value));
  if(snap.Lines.Count==0)sb.Append(I18n.Text(" [空]"));
  else if(expanded)AppendRows(sb,lineColors,snap.Lines,ContainerColor);
  colors=lineColors.ToArray();
  ammo=expanded?snap.Ammo:Array.Empty<AmmoStack>();
  return sb.ToString();
 }
 internal static string CorpseLabel(string name,Item? root,float distance,bool expanded,out Color[] colors){
  var rows=new Dictionary<RowKey,(int Count,double Value)>();
  var seen=new HashSet<IntPtr>();double total=0;
  void Visit(Item item,int depth){
   if(depth>12||!seen.Add(item.Pointer))return;
   int count=Math.Max(1,item.StackObjectsCount);
   string id=item.StringTemplateId;
   var style=ItemCatalog.ForEsp(id);
   double value=style.HasEstimate?TemplatePrice(id)*count:0;
   total+=value;
   AddRow(rows,new(ItemName(item),style),count,value);
   foreach(var child in Children(item))Visit(child,depth+1);
  }
  // Equipment itself is a virtual root; count actual gear and its nested contents once.
  var secured=root?.TryCast<InventoryEquipment>()?.GetSlot(EquipmentSlot.SecuredContainer)?.ContainedItem;
  if(root!=null)foreach(var child in Children(root))if(secured==null||child.Pointer!=secured.Pointer)Visit(child,0);
  var sorted=SortedRows(rows);
  var color=new Color(0.75f,0.75f,0.85f);
  var lineColors=new List<Color>{sorted.Count>0&&sorted[0].Value>=HighValueThreshold.Value?HighValueColor:color};
  var sb=new StringBuilder(name+$" [{distance:F0}米]");
  if(Prices.Value&&(sorted.Count==0||sorted.Any(row=>row.Key.Style.HasEstimate)))
   sb.Append(I18n.Text(" 估值 ")).Append(total>0?Money(total):"₽0");
  if(root==null)sb.Append(I18n.Text(" [物品未加载]"));
  else if(sorted.Count==0)sb.Append(I18n.Text(" [空]"));
  else if(expanded)AppendRows(sb,lineColors,sorted,color);
  colors=lineColors.ToArray();return sb.ToString();
 }
}
