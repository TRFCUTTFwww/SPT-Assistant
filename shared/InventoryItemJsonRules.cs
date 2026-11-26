using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LocalSpt.Shared;

public static class InventoryItemJsonRules {
 public const int Limit=131072;
 public const int TreeLimit=1048576;
 static readonly string[] Locked={"_id","_tpl","parentId","slotId","location"};
 static readonly JsonSerializerOptions editorJson=new(){WriteIndented=true,NewLine="\n"};
 public static string Format(string text)=>ParseDocument(text,TreeLimit).ToJsonString(editorJson);
 public static JsonObject Parse(string text)=>ParseDocument(text,Limit) as JsonObject??throw new ArgumentException("物品 JSON 必须是对象。");
 static JsonNode ParseDocument(string text,int limit){
  if(string.IsNullOrWhiteSpace(text)||text.Length>limit)throw new ArgumentException("物品 JSON 为空或超过大小限制。");
  try{
   using var document=JsonDocument.Parse(text,new JsonDocumentOptions{MaxDepth=64});
   void Check(JsonElement value){
    if(value.ValueKind==JsonValueKind.Object){
     var names=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
     foreach(var field in value.EnumerateObject()){
      if(!names.Add(field.Name))throw new ArgumentException("JSON 包含重复字段。");
      Check(field.Value);
     }
    }else if(value.ValueKind==JsonValueKind.Array)foreach(var entry in value.EnumerateArray())Check(entry);
   }
   Check(document.RootElement);
   return JsonNode.Parse(text)??throw new ArgumentException("物品 JSON 不能为空。");
  }catch(JsonException error){throw new ArgumentException($"JSON 语法无效：第 {error.LineNumber.GetValueOrDefault()+1} 行，第 {error.BytePositionInLine.GetValueOrDefault()+1} 列。");}
 }
 public static JsonObject Validate(string baseline,string draft){
  var original=Parse(baseline);var candidate=Parse(draft);
  return Validate(original,candidate);
 }
 static JsonObject Validate(JsonObject original,JsonObject candidate){
  foreach(string key in Locked)if(!JsonNode.DeepEquals(original[key],candidate[key]))
   throw new ArgumentException("物品编号、模板和位置不能在此修改。");
  if(candidate["upd"] is not null and not JsonObject)throw new ArgumentException("upd 必须是对象或 null。");
  if(candidate["upd"] is JsonObject upd){
   if(upd["StackObjectsCount"] is JsonNode count&&(count is not JsonValue scalar||!scalar.TryGetValue<int>(out int number)))
    throw new ArgumentException("堆叠数量必须是 Int32 整数。");
   if(upd["Repairable"] is JsonObject repair){
    double? Read(string key)=>repair[key] is JsonValue value&&value.TryGetValue<double>(out var number)&&double.IsFinite(number)?number:null;
    var maximum=Read("MaxDurability");var current=Read("Durability");
    if(maximum is < -float.MaxValue or >float.MaxValue||current is < -float.MaxValue or >float.MaxValue)
     throw new ArgumentException("耐久超出 Float32 存储范围。");
   }
  }
  return candidate;
 }
 public static JsonArray ParseTree(string text){
  var tree=ParseDocument(text,TreeLimit) as JsonArray??throw new ArgumentException("物品树 JSON 必须是数组。");
  if(tree.Count==0)throw new ArgumentException("物品树不能为空。");
  Index(tree);return tree;
 }
 public static System.Collections.Generic.Dictionary<string,JsonObject> Index(JsonArray tree){
  var items=new System.Collections.Generic.Dictionary<string,JsonObject>(StringComparer.Ordinal);
  foreach(var node in tree){
   if(node is not JsonObject item||item["_id"] is not JsonValue id||!id.TryGetValue<string>(out var key)||string.IsNullOrWhiteSpace(key))
    throw new ArgumentException("每个物品必须包含有效的 _id。");
   if(!items.TryAdd(key,item))throw new ArgumentException("物品树包含重复编号。");
  }
  return items;
 }
 public static JsonArray ValidateTree(string baseline,string draft){
  var original=Index(ParseTree(baseline));var candidate=Index(ParseTree(draft));
  if(original.Count!=candidate.Count||original.Keys.Any(id=>!candidate.ContainsKey(id)))
   throw new ArgumentException("不能在此添加或删除物品。");
  var result=new JsonArray();
  foreach(var pair in original)result.Add(Validate(pair.Value,candidate[pair.Key]).DeepClone());
  return result;
 }
 public static JsonObject Update(JsonObject item)=>item["upd"] is JsonObject upd?(JsonObject)upd.DeepClone():new();
 public static bool RemovesFields(JsonObject original,JsonObject candidate)=>original.Any(pair=>
  !candidate.ContainsKey(pair.Key)||pair.Value is JsonObject before&&(candidate[pair.Key] is not JsonObject after||RemovesFields(before,after)));
 public static bool Matches(JsonObject expected,JsonObject actual)=>expected.All(pair=>
  pair.Key=="StackObjectsCount"&&actual[pair.Key]==null&&pair.Value is JsonValue count&&count.TryGetValue<int>(out int number)&&number==1
  ||MatchesValue(pair.Value,actual[pair.Key]));
 static bool MatchesValue(JsonNode? expected,JsonNode? actual){
  if(JsonNode.DeepEquals(expected,actual))return true;
  if(expected is JsonObject obj)return Matches(obj,actual as JsonObject??new());
  if(actual==null)return expected==null||expected is JsonValue scalar&&(scalar.GetValueKind()==JsonValueKind.False||scalar.TryGetValue<double>(out double number)&&number==0);
  if(expected is JsonValue left&&actual is JsonValue right&&left.TryGetValue<double>(out var a)&&right.TryGetValue<double>(out var b))
   return (float)a==(float)b;
  return false;
 }
}
