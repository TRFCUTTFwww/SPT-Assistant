using System;
using System.Linq;
using System.Text.Json.Nodes;
namespace LocalSpt.Shared;

// IL2CPP UnparsedData exposes its JSON through JToken; that wrapper is not wire data.
public static class RaidStashJson {
 public static JsonArray Normalize(string json,out int locations,out int updates){
  var items=JsonNode.Parse(json) as JsonArray??throw new ArgumentException("仓库物品格式无效。");locations=updates=0;
  foreach(var node in items){
   var item=node as JsonObject??throw new ArgumentException("仓库物品格式无效。");
   foreach(string key in new[]{"location","upd"}){
    var value=item[key];bool changed=false;int depth=0;
    while(value is JsonObject wrapper&&wrapper.ContainsKey("JToken")){
     if(++depth>64)throw new ArgumentException("仓库物品字段嵌套过深。");
     var inner=wrapper["JToken"]?.DeepClone();
     if(wrapper.Count>1){
      if(inner is not JsonObject fields)throw new ArgumentException("仓库物品字段格式无效。");
      // A later native operation may already have updated a real outer property.
      foreach(var field in wrapper.Where(p=>p.Key!="JToken"))fields[field.Key]=field.Value?.DeepClone();
     }
     value=inner;changed=true;
    }
    if(changed){item[key]=value;if(key=="location")locations++;else updates++;}
   }
  }
  return items;
 }
}
