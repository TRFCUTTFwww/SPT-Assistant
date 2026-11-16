using System.Text.Json.Nodes;
namespace LocalGearMail;
public static class FunQuarantineRules {
 public const string Key="localAssistantFunQuarantine";
 public static string Strip(string source,out int count){var root=JsonNode.Parse(source)!.AsObject();var archived=root[Key] as JsonArray??new JsonArray();root.Remove(Key);int removed=0;
  void Visit(JsonNode? node){if(node is JsonObject obj){foreach(var value in obj.Select(p=>p.Value).ToArray())Visit(value);}else if(node is JsonArray array){var rows=array.OfType<JsonObject>().Where(i=>i["_id"] is JsonValue&&i["_tpl"] is JsonValue).ToArray();if(rows.Length>0){var ids=rows.Where(i=>FunItemPolicy.IsFunTemplate(i["_tpl"]!.GetValue<string>())).Select(i=>i["_id"]!.GetValue<string>()).ToHashSet();bool changed;do{changed=false;foreach(var row in rows)if(row["parentId"] is JsonValue parent&&ids.Contains(parent.GetValue<string>()))changed|=ids.Add(row["_id"]!.GetValue<string>());}while(changed);if(ids.Count>0){var group=new JsonArray();foreach(var row in rows.Where(i=>ids.Contains(i["_id"]!.GetValue<string>()))){var copy=row.DeepClone().AsObject();if(copy["parentId"] is not JsonValue parent||!ids.Contains(parent.GetValue<string>())){copy.Remove("parentId");copy.Remove("slotId");copy.Remove("location");}group.Add(copy);array.Remove(row);removed++;}archived.Add(group);}}foreach(var child in array.ToArray())Visit(child);}}
  Visit(root);if(archived.Count>0)root[Key]=archived;count=removed;return removed==0?source:root.ToJsonString();}
}
