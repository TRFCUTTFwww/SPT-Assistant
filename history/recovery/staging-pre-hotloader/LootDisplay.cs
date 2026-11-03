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
internal static class LootDisplay {
 internal static ConfigEntry<bool> Prices=null!,Contents=null!;
 internal static ConfigEntry<int> ContentLines=null!,ContainerLimit=null!;
 internal static ConfigEntry<float> ContentRange=null!;
 internal static ConfigEntry<int> HighValueThreshold=null!;
 internal static ConfigEntry<int> Opacity=null!;
 internal static readonly Color HighValueColor=new(1f,0.48f,0.05f);
 static readonly Color ContainerColor=new(0.3f,0.9f,1f);
 static Dictionary<string,double> prices=new(StringComparer.Ordinal);
 static Dictionary<string,string> names=new(StringComparer.Ordinal);
 static readonly Dictionary<IntPtr,Snapshot> snapshots=new();
 record Row(string Name,int Count,double Value);
 record Snapshot(string Name,double Value,List<Row> Lines,int Count,bool Expanded,float Expires);
 internal static void Initialize(Plugin p){
  Opacity=CnConfig.Bind(p.Config,"6. Loot details","Opacity percent",50,new ConfigDescription("Opacity of loot/container text, shadows, panels and position lines. 0 = invisible; 50 = semi-transparent; 100 = full opacity.",new AcceptableValueRange<int>(0,100)));
  Prices=CnConfig.Bind(p.Config,"6. Loot details","Show item values",true,"Estimated ruble value from the server price snapshot, including stack quantity and attachments. Not a live sale quote.");
  Contents=CnConfig.Bind(p.Config,"6. Loot details","Show container contents",true,"Show container name, contents and total estimated value.");
  ContentRange=CnConfig.Bind(p.Config,"6. Loot details","Contents distance",35f,new ConfigDescription("Detailed contents within this many meters",new AcceptableValueRange<float>(5,150)));
  ContentLines=CnConfig.Bind(p.Config,"6. Loot details","Maximum contents lines",8,new ConfigDescription("Most valuable item types first; total includes hidden lines",new AcceptableValueRange<int>(1,30)));
  ContainerLimit=CnConfig.Bind(p.Config,"6. Loot details","Maximum containers",32,new ConfigDescription("Closest containers to evaluate per scan",new AcceptableValueRange<int>(1,100)));
  HighValueThreshold=CnConfig.Bind(p.Config,"6. Loot details","High value threshold",100000,new ConfigDescription("Items worth at least this many rubles are orange, including stacks and attachments; also applies to individual container rows",new AcceptableValueRange<int>(1,100000000)));
  string dir=Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP");
  try{prices=JsonSerializer.Deserialize<Dictionary<string,double>>(File.ReadAllText(Path.Combine(dir,"esp5-prices.json")))??new();names=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(dir,"esp5-names.json")))??new();p.Log.LogInfo($"Loot price snapshot loaded: {prices.Count} prices, {names.Count} names");}
  catch(Exception e){p.Log.LogWarning("Loot price/name snapshot unavailable: "+e.Message);}
 }
 internal static void Reset()=>snapshots.Clear();
 internal static string ItemName(Item item,bool shortName=true){
  var id=item.StringTemplateId;
  if(shortName&&names.TryGetValue(id+" ShortName",out var shortText))return shortText;
  if(names.TryGetValue(id,out var full))return full;
  var raw=shortName?item.ShortName:item.Name;
  string text=LocalizationExtensions.Localized(raw,EStringCase.None);
  if(string.IsNullOrWhiteSpace(text))text=raw;
  names[id]=text;return text;
 }
 internal static string Money(double value)=>value>0?"₽"+Math.Round(value).ToString("N0",CultureInfo.InvariantCulture):"价格未知";
 internal static IEnumerable<Item> Children(Item item){
  var compound=item.TryCast<CompoundItem>();if(compound==null)yield break;
  if(compound.Grids!=null)for(int i=0;i<compound.Grids.Length;i++){var grid=compound.Grids[i];if(grid==null)continue;foreach(var child in grid.Items)if(child!=null)yield return child;}
  if(compound.Slots!=null)for(int i=0;i<compound.Slots.Length;i++){var child=compound.Slots[i]?.ContainedItem;if(child!=null)yield return child;}
 }
 internal static double Value(Item root)=>Value(root,new HashSet<IntPtr>(),0);
 static double Value(Item item,HashSet<IntPtr> seen,int depth){
  if(item==null||depth>12||!seen.Add(item.Pointer))return 0;
  double unit=prices.TryGetValue(item.StringTemplateId,out var price)?price:0;
  double total=unit*Math.Max(1,item.StackObjectsCount);
  foreach(var child in Children(item))total+=Value(child,seen,depth+1);
  return total;
 }
 internal static string LooseLabel(Item item,float distance,out Color color){
  double value=Value(item);color=value>=HighValueThreshold.Value?HighValueColor:new Color(1f,0.85f,0.2f);
  string count=item.StackObjectsCount>1?" ×"+item.StackObjectsCount:"";
  return ItemName(item)+count+$" [{distance:F0}m]"+(Prices.Value?"  估值 "+Money(value):"");
 }
 internal static string ContainerLabel(LootableContainer container,float distance,out Color[] colors){
  bool expanded=Contents.Value&&distance<=ContentRange.Value;
  if(!snapshots.TryGetValue(container.Pointer,out var snap)||Time.unscaledTime>=snap.Expires){
   var root=container.ItemOwner?.RootItem;
   string name="容器";
   if(!string.IsNullOrWhiteSpace(container.Template)&&names.TryGetValue(container.Template,out var templateName))name=templateName;
   else if(root!=null&&names.TryGetValue(root.StringTemplateId,out var rootName))name=rootName;
   else if(!string.IsNullOrWhiteSpace(container.ItemOwner?.ContainerName))name=LocalizationExtensions.Localized(container.ItemOwner.ContainerName,EStringCase.None);
   else if(!string.IsNullOrWhiteSpace(container.TypeKey))name=LocalizationExtensions.Localized(container.TypeKey,EStringCase.None);
   var rows=new Dictionary<string,(int Count,double Value)>();double total=0;
   if(root!=null)foreach(var item in Children(root)){
    string itemName=ItemName(item);double value=Value(item);int count=Math.Max(1,item.StackObjectsCount);total+=value;
    rows.TryGetValue(itemName,out var prior);rows[itemName]=(prior.Count+count,prior.Value+value);
   }
   var lines=rows.OrderByDescending(x=>x.Value.Value).Select(x=>new Row(x.Key,x.Value.Count,x.Value.Value)).ToList();
   snap=new Snapshot(name,total,lines,rows.Count,expanded,Time.unscaledTime+3f);snapshots[container.Pointer]=snap;
  }
  var sb=new StringBuilder(snap.Name+$" [{distance:F0}m]");
  var lineColors=new List<Color>{snap.Lines.Any(x=>x.Value>=HighValueThreshold.Value)?HighValueColor:ContainerColor};
  if(Prices.Value)sb.Append("  估值 ").Append(snap.Count==0?"₽0":Money(snap.Value));
  if(snap.Count==0)sb.Append(" [空]");
  else if(expanded){int limit=Math.Min(ContentLines.Value,snap.Lines.Count);for(int i=0;i<limit;i++){var row=snap.Lines[i];sb.Append('\n').Append(row.Name).Append(" ×").Append(row.Count);if(Prices.Value)sb.Append("  ").Append(Money(row.Value));lineColors.Add(row.Value>=HighValueThreshold.Value?HighValueColor:ContainerColor);}if(snap.Lines.Count>limit){sb.Append("\n…另 ").Append(snap.Lines.Count-limit).Append(" 类（总估值已包含）");lineColors.Add(ContainerColor);}}
  colors=lineColors.ToArray();
  return sb.ToString();
 }
 internal static string CorpseLabel(string name,Item? root,float distance,bool expanded,out Color[] colors){
  var rows=new Dictionary<string,(int Count,double Value)>();
  var seen=new HashSet<IntPtr>();double total=0;
  void Visit(Item item,int depth){
   if(depth>12||!seen.Add(item.Pointer))return;
   int count=Math.Max(1,item.StackObjectsCount);
   double value=(prices.TryGetValue(item.StringTemplateId,out var unit)?unit:0)*count;
   total+=value;string label=ItemName(item);
   rows.TryGetValue(label,out var old);rows[label]=(old.Count+count,old.Value+value);
   foreach(var child in Children(item))Visit(child,depth+1);
  }
  // Equipment itself is a virtual root; count actual gear and its nested contents once.
  var secured=root?.TryCast<InventoryEquipment>()?.GetSlot(EquipmentSlot.SecuredContainer)?.ContainedItem;
  if(root!=null)foreach(var child in Children(root))if(secured==null||child.Pointer!=secured.Pointer)Visit(child,0);
  var sorted=rows.OrderByDescending(r=>r.Value.Value).ThenBy(r=>r.Key,StringComparer.Ordinal).ToArray();
  var color=new Color(0.75f,0.75f,0.85f);
  var lineColors=new List<Color>{sorted.Any(r=>r.Value.Value>=HighValueThreshold.Value)?HighValueColor:color};
  var sb=new StringBuilder(name+$" [{distance:F0}米]");
  if(Prices.Value)sb.Append(" 估值 ").Append(total>0?Money(total):"₽0");
  if(root==null)sb.Append(" [物品未加载]");
  else if(sorted.Length==0)sb.Append(" [空]");
  else if(expanded){
   int limit=Math.Min(ContentLines.Value,sorted.Length);
   for(int i=0;i<limit;i++){var r=sorted[i];sb.Append('\n').Append(r.Key).Append(" ×").Append(r.Value.Count);if(Prices.Value)sb.Append(" ").Append(Money(r.Value.Value));lineColors.Add(r.Value.Value>=HighValueThreshold.Value?HighValueColor:color);}
   if(sorted.Length>limit){sb.Append("\n…另 ").Append(sorted.Length-limit).Append(" 类物品（总估值已包含）");lineColors.Add(color);}
  }
  colors=lineColors.ToArray();return sb.ToString();
 }
}
