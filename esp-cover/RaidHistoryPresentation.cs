using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
namespace LocalSpt5Esp;
internal static class RaidHistoryPresentation {
 internal sealed record Equipment(JsonElement Item,JsonElement[] Attachments,bool ContextOnly);
 internal static string Text(JsonElement e,string key)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??"":"";
 static bool Attachment(string slot)=>slot.StartsWith("mod_",StringComparison.OrdinalIgnoreCase)||slot.StartsWith("soft_armor",StringComparison.OrdinalIgnoreCase)||slot.EndsWith("_plate",StringComparison.OrdinalIgnoreCase)||slot is "cartridges" or "patron_in_weapon" or "chambers";
 internal static Equipment[] Group(JsonElement[] items,JsonElement[] context){
  var all=context.Concat(items).Where(i=>Text(i,"Id").Length>0).GroupBy(i=>Text(i,"Id"),StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.Last(),StringComparer.Ordinal);
  var selected=items.Select(i=>Text(i,"Id")).ToHashSet(StringComparer.Ordinal);var order=new List<string>();var roots=new Dictionary<string,JsonElement>(StringComparer.Ordinal);var children=new Dictionary<string,List<JsonElement>>(StringComparer.Ordinal);
  foreach(var item in items){string id=Text(item,"Id");var root=item;var visited=new HashSet<string>(StringComparer.Ordinal){id};bool cycle=false;
   while(Attachment(Text(root,"Slot"))&&all.TryGetValue(Text(root,"Parent"),out var parent)){if(!visited.Add(Text(parent,"Id"))){cycle=true;break;}root=parent;}
   if(cycle)root=item;string rootId=Text(root,"Id");if(!roots.ContainsKey(rootId)){order.Add(rootId);roots[rootId]=root;children[rootId]=new();}if(rootId!=id)children[rootId].Add(item);
  }
  return order.Select(id=>new Equipment(roots[id],children[id].ToArray(),!selected.Contains(id))).ToArray();
 }
 internal static string[] Route(JsonElement detail){if(!detail.TryGetProperty("Route",out var route)||route.ValueKind!=JsonValueKind.Array)return Array.Empty<string>();return route.EnumerateArray().Where(e=>e.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(e.GetString())).Select(e=>e.GetString()!).ToArray();}
}
