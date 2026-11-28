using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LocalSpt.Shared;

public static class InventoryItemJsonRules {
 public const int Limit=int.MaxValue;
 public const int TreeLimit=int.MaxValue;
 static readonly JsonSerializerOptions editorJson=new(){WriteIndented=true,NewLine="\n",Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping,MaxDepth=1024};
 static readonly JsonDocumentOptions parseOptions=new(){AllowTrailingCommas=true,CommentHandling=JsonCommentHandling.Skip,MaxDepth=1024};

 public static string NewMongoId()=>Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();

 public static string Format(string text)=>ParseDocument(text).ToJsonString(editorJson);
 public static JsonObject Parse(string text)=>ParseDocument(text) as JsonObject??throw new ArgumentException("物品 JSON 必须是对象。");

 public static JsonNode ParseDocument(string text){
  string unwrapped=UnwrapBase64IfNeeded(text);
  if(string.IsNullOrWhiteSpace(unwrapped))throw new ArgumentException("物品 JSON 不能为空。");
  try{
   return JsonNode.Parse(unwrapped,documentOptions:parseOptions)??throw new ArgumentException("物品 JSON 不能为空。");
  }catch(JsonException error){throw new ArgumentException($"JSON 语法无效：第 {error.LineNumber.GetValueOrDefault()+1} 行，第 {error.BytePositionInLine.GetValueOrDefault()+1} 列。");}
 }

 static string UnwrapBase64IfNeeded(string? raw){
  if(string.IsNullOrWhiteSpace(raw))return "";
  string trimmed=raw.Trim();
  if(trimmed[0] is '{' or '[')return trimmed;
  try{
   byte[] decoded=Convert.FromBase64String(trimmed);
   string text=Encoding.UTF8.GetString(decoded).TrimStart('\uFEFF').Trim();
   if(text.Length>0&&text[0] is '{' or '[')return text;
  }catch{}
  return trimmed;
 }

 public static JsonObject Validate(string baseline,string draft){
  var original=Parse(baseline);var candidate=Parse(draft);
  return ValidateItemObject(original,candidate);
 }

 static JsonObject ValidateItemObject(JsonObject? original,JsonObject candidate){
  var clone=(JsonObject)candidate.DeepClone();
  if((clone["_id"] is not JsonValue idVal||!idVal.TryGetValue<string>(out var id)||string.IsNullOrWhiteSpace(id))&&original?["_id"]!=null)
   clone["_id"]=original["_id"]!.DeepClone();
  if(clone["_id"] is not JsonValue finalId||!finalId.TryGetValue<string>(out var key)||string.IsNullOrWhiteSpace(key))
   clone["_id"]=NewMongoId();
  if((clone["_tpl"] is not JsonValue tplVal||!tplVal.TryGetValue<string>(out var tpl)||string.IsNullOrWhiteSpace(tpl))&&original?["_tpl"]!=null)
   clone["_tpl"]=original["_tpl"]!.DeepClone();
  if(clone["_tpl"] is not JsonValue finalTpl||!finalTpl.TryGetValue<string>(out var tplKey)||string.IsNullOrWhiteSpace(tplKey))
   throw new ArgumentException("每个物品必须包含有效的 _tpl。");
  if(clone["upd"] is not null and not JsonObject)throw new ArgumentException("upd 必须是对象或 null。"); if(clone["upd"] is JsonObject updObj&&updObj["StackObjectsCount"] is JsonNode sc&&sc.GetValueKind()!=JsonValueKind.Number)throw new ArgumentException("StackObjectsCount 必须是数字。");
  return clone;
 }

 static JsonArray CoerceTreeArray(JsonNode parsed){
  if(parsed is JsonArray array)return (JsonArray)array.DeepClone();
  if(parsed is JsonObject obj){
   if((obj["Items"]??obj["items"]) is JsonArray presetItems)return (JsonArray)presetItems.DeepClone();
   return new JsonArray(obj.DeepClone());
  }
  throw new ArgumentException("物品树 JSON 必须是数组或物品对象。");
 }

 public static JsonArray ParseTree(string text){
  var tree=CoerceTreeArray(ParseDocument(text));
  if(tree.Count==0)throw new ArgumentException("物品树不能为空。");
  for(int i=0;i<tree.Count;i++){
   if(tree[i] is not JsonObject item)throw new ArgumentException("物品树元素必须是对象。");
   if(item["_id"] is not JsonValue idVal||!idVal.TryGetValue<string>(out var id)||string.IsNullOrWhiteSpace(id))
    item["_id"]=NewMongoId();
  }
  Index(tree);return tree;
 }

 public static Dictionary<string,JsonObject> Index(JsonArray tree){
  var items=new Dictionary<string,JsonObject>(StringComparer.Ordinal);
  foreach(var node in tree){
   if(node is not JsonObject item||item["_id"] is not JsonValue id||!id.TryGetValue<string>(out var key)||string.IsNullOrWhiteSpace(key))
    throw new ArgumentException("每个物品必须包含有效的 _id。");
   if(!items.TryAdd(key,item))throw new ArgumentException("物品树包含重复编号。");
  }
  return items;
 }

 public static JsonObject FindRoot(JsonArray tree){
  var byId=Index(tree);
  var roots=byId.Values.Where(item=>{
   string parent=item["parentId"] is JsonValue pv&&pv.TryGetValue<string>(out var p)?p:"";
   return string.IsNullOrWhiteSpace(parent)||!byId.ContainsKey(parent);
  }).ToList();
  if(roots.Count!=1)throw new ArgumentException("物品树必须有且仅有一个根物品。");
  string rootId=roots[0]["_id"]!.GetValue<string>();
  foreach(var pair in byId){
   var seen=new HashSet<string>(StringComparer.Ordinal);
   string cursor=pair.Key;
   while(cursor!=rootId){
    if(!seen.Add(cursor)||!byId.TryGetValue(cursor,out var cur))throw new ArgumentException("物品树包含重复编号或循环引用。");
    cursor=cur["parentId"] is JsonValue pv&&pv.TryGetValue<string>(out var p)?p:"";
   }
  }
  return roots[0];
 }

 public static JsonArray ValidateTree(string baseline,string draft){
  var baselineTree=ParseTree(baseline);
  var baselineById=Index(baselineTree);
  var baselineRoot=FindRoot(baselineTree);
  var rawCandidate=CoerceTreeArray(ParseDocument(draft));
  if(rawCandidate.Count==0)throw new ArgumentException("物品树不能为空。");

  var normalized=new JsonArray();
  foreach(var entry in rawCandidate){
   if(entry is not JsonObject obj)throw new ArgumentException("物品树元素必须是对象。");
   string existingId=obj["_id"] is JsonValue iv&&iv.TryGetValue<string>(out var s)?s:"";
   baselineById.TryGetValue(existingId,out var orig);
   normalized.Add(ValidateItemObject(orig,obj));
  }

  var candidateRoot=FindRoot(normalized);
  string candParent=candidateRoot["parentId"] is JsonValue cp&&cp.TryGetValue<string>(out var cpStr)?cpStr:"";
  if(string.IsNullOrWhiteSpace(candParent)){
   if(baselineRoot["parentId"]!=null)candidateRoot["parentId"]=baselineRoot["parentId"]!.DeepClone();
   if(candidateRoot["slotId"]==null&&baselineRoot["slotId"]!=null)candidateRoot["slotId"]=baselineRoot["slotId"]!.DeepClone();
   if(candidateRoot["location"]==null&&baselineRoot["location"]!=null)candidateRoot["location"]=baselineRoot["location"]!.DeepClone();
  }
  return normalized;
 }

 public static (List<string> RootIds,JsonArray Items) NormalizeForGridAdd(string rawText,Func<string>? allocateId=null){
  allocateId??=NewMongoId;
  var parsed=ParseDocument(rawText);
  var array=CoerceTreeArray(parsed);
  if(array.Count==0)throw new ArgumentException("物品 JSON 不能为空。");

  var rawItems=new List<JsonObject>(array.Count);
  var sourceIds=new List<string>(array.Count);
  var sourceSet=new HashSet<string>(StringComparer.Ordinal);
  for(int i=0;i<array.Count;i++){
   if(array[i] is not JsonObject obj)throw new ArgumentException("物品 JSON 元素必须是对象。");
   var clone=(JsonObject)obj.DeepClone();
   if(clone["_tpl"] is not JsonValue tplVal||!tplVal.TryGetValue<string>(out var tpl)||string.IsNullOrWhiteSpace(tpl))
    throw new ArgumentException("每个物品必须包含有效的 _tpl。");
   if(clone["upd"] is not null and not JsonObject)throw new ArgumentException("upd 必须是对象或 null。"); if(clone["upd"] is JsonObject updObj&&updObj["StackObjectsCount"] is JsonNode sc&&sc.GetValueKind()!=JsonValueKind.Number)throw new ArgumentException("StackObjectsCount 必须是数字。");
   string srcId=clone["_id"] is JsonValue idVal&&idVal.TryGetValue<string>(out var s)&&!string.IsNullOrWhiteSpace(s)?s:$"__auto_{i}";
   if(!sourceSet.Add(srcId))throw new ArgumentException("物品 JSON 包含重复的 _id："+srcId);
   sourceIds.Add(srcId);
   rawItems.Add(clone);
  }

  var idMap=new Dictionary<string,string>(StringComparer.Ordinal);
  var usedNewIds=new HashSet<string>(StringComparer.Ordinal);
  foreach(string srcId in sourceIds){
   string fresh;
   do{fresh=allocateId();}while(string.IsNullOrWhiteSpace(fresh)||!usedNewIds.Add(fresh));
   idMap[srcId]=fresh;
  }

  var rootIds=new List<string>();
  var result=new JsonArray();
  for(int i=0;i<rawItems.Count;i++){
   var item=rawItems[i];
   string srcId=sourceIds[i];
   string freshId=idMap[srcId];
   item["_id"]=freshId;
   string parent=item["parentId"] is JsonValue pv&&pv.TryGetValue<string>(out var p)?p:"";
   if(string.IsNullOrWhiteSpace(parent)||!idMap.TryGetValue(parent,out var mappedParent)){
    item.Remove("parentId");
    item.Remove("slotId");
    item.Remove("location");
    rootIds.Add(freshId);
   }else{
    item["parentId"]=mappedParent;
   }
   result.Add(item);
  }

  if(rootIds.Count==0)throw new ArgumentException("物品 JSON 包含循环父子引用，未找到根物品。");
  var byNewId=Index(result);
  var rootSet=rootIds.ToHashSet(StringComparer.Ordinal);
  foreach(var pair in byNewId){
   var seen=new HashSet<string>(StringComparer.Ordinal);
   string cursor=pair.Key;
   while(!rootSet.Contains(cursor)){
    if(!seen.Add(cursor)||!byNewId.TryGetValue(cursor,out var cur))throw new ArgumentException("物品树包含循环引用。");
    cursor=cur["parentId"] is JsonValue pv&&pv.TryGetValue<string>(out var p)?p:"";
   }
  }
  return (rootIds,result);
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
