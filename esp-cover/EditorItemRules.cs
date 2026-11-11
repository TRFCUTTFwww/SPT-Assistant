using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
namespace LocalSpt5Esp;
// Pure validation/remapping keeps a copied inventory independent of its source.
internal static class EditorItemRules {
 internal static JsonArray Remap(string snapshot,string root,Func<string> next,out string newRoot){
  var data=JsonNode.Parse(snapshot) as JsonArray??throw new ArgumentException("Invalid item snapshot.");
  if(data.Count is <1 or >4096)throw new ArgumentException("Invalid item count.");
  var entries=new Dictionary<string,JsonObject>(StringComparer.Ordinal);
  foreach(var node in data){var entry=node as JsonObject??throw new ArgumentException("Invalid item.");string id=entry["_id"]?.GetValue<string>()??"",tpl=entry["_tpl"]?.GetValue<string>()??"";if(id.Length!=24||tpl.Length!=24||!id.All(Uri.IsHexDigit)||!tpl.All(Uri.IsHexDigit)||!entries.TryAdd(id,entry))throw new ArgumentException("Invalid or duplicate item ID.");}
  if(!entries.ContainsKey(root))throw new ArgumentException("Missing root item.");
  foreach(string id in entries.Keys){var seen=new HashSet<string>();string cursor=id;while(cursor!=root){if(!seen.Add(cursor)||!entries.TryGetValue(cursor,out var entry))throw new ArgumentException("Disconnected or cyclic item tree.");cursor=entry["parentId"]?.GetValue<string>()??"";}}
  var mapping=new Dictionary<string,string>();var allocated=new HashSet<string>(entries.Keys);
  foreach(string id in entries.Keys){string fresh=next();if(fresh.Length!=24||!fresh.All(Uri.IsHexDigit)||!allocated.Add(fresh))throw new ArgumentException("Item ID allocation failed.");mapping[id]=fresh;}
  foreach(var pair in entries){pair.Value["_id"]=mapping[pair.Key];if(pair.Key==root){pair.Value.Remove("parentId");pair.Value.Remove("slotId");pair.Value.Remove("location");}else pair.Value["parentId"]=mapping[pair.Value["parentId"]!.GetValue<string>()];}
  newRoot=mapping[root];return data;
 }
 internal static float Distance(float value,float wheel){if(!float.IsFinite(value)||!float.IsFinite(wheel))throw new ArgumentException("Invalid holding distance.");return Math.Clamp(value+wheel*Math.Max(1,value*.15f),1,100);}
}
