using System;
using System.Linq;
using System.Text.Json.Nodes;
namespace LocalRaidShared;
public static class ContainerNestingRules {
 public const string CaseClass="5795f317245977243854e041";
 public static bool Enable(JsonObject item,Func<string,bool> isCase){
  if(!isCase(item["_id"]?.ToString()??"")||item["_props"]?["Grids"] is not JsonArray grids)return false;
  bool changed=false;
  foreach(var grid in grids.OfType<JsonObject>())if(grid["_props"]?["filters"] is JsonArray filters)foreach(var filter in filters.OfType<JsonObject>()){
   if(filter["Filter"] is JsonArray allowed&&allowed.Count>0&&!allowed.Any(v=>v?.ToString()==CaseClass)){allowed.Add(CaseClass);changed=true;}
   if(filter["ExcludedFilter"] is JsonArray excluded)for(int i=excluded.Count-1;i>=0;i--)if(isCase(excluded[i]?.ToString()??"")){excluded.RemoveAt(i);changed=true;}
  }
  return changed;
 }
}
