using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
namespace LocalGearMail;
public static class ProfileRestoreRules {
 public static readonly string[] Categories=["stash","skills","progress","health","quests","traders","all"];
 public static string Revision(string json)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
 static JsonObject Object(JsonNode? node,string name)=>node as JsonObject??throw new ArgumentException(name+"数据缺失或格式不兼容。");
 static JsonObject Pmc(JsonObject profile)=>Object(Object(profile["characters"],"角色")["pmc"],"PMC");
 public static string Nickname(string raw){var root=Object(JsonNode.Parse(raw),"备份");return Pmc(root)["Info"]?["Nickname"]?.GetValue<string>()??"未命名角色";}
 static string Id(JsonNode item)=>item["_id"]?.GetValue<string>()??throw new ArgumentException("物品 ID 缺失。");
 static string NewId()=>Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
 static void Copy(JsonObject source,JsonObject target,params string[] names){foreach(string name in names)if(source.TryGetPropertyValue(name,out var value))target[name]=value?.DeepClone();}
 static void ReplaceStrings(JsonNode? node,IReadOnlyDictionary<string,string> map){
  if(node is JsonObject obj){foreach(string key in obj.Select(p=>p.Key).ToArray()){var child=obj[key];if(child is JsonValue v&&v.TryGetValue<string>(out string? s)&&map.TryGetValue(s,out var replacement))obj[key]=replacement;else ReplaceStrings(child,map);if(map.TryGetValue(key,out var mappedKey)&&mappedKey!=key){if(obj.ContainsKey(mappedKey))throw new ArgumentException("还原引用键冲突。");var held=obj[key];obj.Remove(key);obj[mappedKey]=held;}}}
  else if(node is JsonArray array)for(int i=0;i<array.Count;i++){if(array[i] is JsonValue v&&v.TryGetValue<string>(out string? s)&&map.TryGetValue(s,out var replacement))array[i]=replacement;else ReplaceStrings(array[i],map);}
 }
 static IEnumerable<string> ItemIds(JsonNode? node){if(node is JsonObject obj){if(obj["_id"] is JsonValue id&&id.TryGetValue<string>(out var value)&&obj["_tpl"] is JsonValue)yield return value;foreach(var pair in obj)foreach(string child in ItemIds(pair.Value))yield return child;}else if(node is JsonArray array)foreach(var item in array)foreach(string child in ItemIds(item))yield return child;}
 static HashSet<string> Tree(JsonArray items,string root){var byParent=items.Where(i=>i!=null&&i["parentId"] is JsonValue).GroupBy(i=>i!["parentId"]!.GetValue<string>()).ToDictionary(g=>g.Key,g=>g.Select(i=>Id(i!)).ToArray());var result=new HashSet<string>();var queue=new Queue<string>();queue.Enqueue(root);while(queue.TryDequeue(out string? current)){if(!result.Add(current))throw new ArgumentException("物品容器存在循环引用。");if(byParent.TryGetValue(current,out var children))foreach(string child in children)queue.Enqueue(child);}return result;}
 static JsonArray Items(JsonObject inventory)=>inventory["items"] as JsonArray??throw new ArgumentException("物品列表缺失。");
 static Dictionary<string,string> InventoryTree(JsonObject source,JsonObject target,string field){
  var from=Object(source["Inventory"],"源库存");var to=Object(target["Inventory"],"目标库存");var sourceItems=Items(from);var targetItems=Items(to);
  string? sourceRoot=from[field]?.GetValue<string>(),targetRoot=to[field]?.GetValue<string>();
  if(string.IsNullOrEmpty(sourceRoot)&&string.IsNullOrEmpty(targetRoot))return new();
  if(string.IsNullOrEmpty(sourceRoot)||string.IsNullOrEmpty(targetRoot))throw new ArgumentException("两个存档的 "+field+" 容器结构不兼容，请选择全部还原。");
  var incoming=Tree(sourceItems,sourceRoot);var outgoing=Tree(targetItems,targetRoot);var kept=targetItems.Where(i=>i!=null&&!outgoing.Contains(Id(i))).Select(i=>i!.DeepClone()).ToArray();
  var map=incoming.ToDictionary(id=>id,id=>id==sourceRoot?targetRoot:NewId());var used=kept.Select(Id).ToHashSet();foreach(string id in map.Values)if(!used.Add(id))throw new ArgumentException("生成的物品 ID 重复，请重试。");
  var result=new JsonArray();foreach(var item in kept)result.Add(item);foreach(var item in sourceItems.Where(i=>i!=null&&incoming.Contains(Id(i)))){var copy=item!.DeepClone();ReplaceStrings(copy,map);result.Add(copy);}
  to["items"]=result;
  // Clear only references to replaced objects. Equipment and its quick slots
  // remain exactly as the target character had them.
  if(to["fastPanel"] is JsonObject panel)foreach(string key in panel.Select(p=>p.Key).ToArray())if(panel[key] is JsonValue v&&v.TryGetValue<string>(out var id)&&outgoing.Contains(id))panel.Remove(key);
  if(target["InsuredItems"] is JsonArray insured)for(int i=insured.Count-1;i>=0;i--)if(insured[i]?["itemId"] is JsonValue v&&v.TryGetValue<string>(out var id)&&outgoing.Contains(id))insured.RemoveAt(i);
  return map;
 }
 public static string Build(string sourceJson,string targetJson,string[] categories,Func<string,bool>? installed=null){
  if(categories.Length==0||categories.Distinct().Count()!=categories.Length||categories.Any(c=>!Categories.Contains(c))||categories.Contains("all")&&categories.Length!=1)throw new ArgumentException("请选择有效且不重复的还原类别。");
  var source=Object(JsonNode.Parse(sourceJson),"备份");var target=Object(JsonNode.Parse(targetJson),"目标存档");var from=Pmc(source);var to=Pmc(target);
  if(categories.Contains("all")){
   var result=(JsonObject)source.DeepClone();
   // Keep the new account's login, session and character identities. Exact
   // identity references are translated throughout the copied snapshot.
   var identities=new Dictionary<string,string>();
   foreach(string side in new[]{"pmc","scav"}){
    var src=source["characters"]?[side] as JsonObject;var dst=target["characters"]?[side] as JsonObject;if(src==null||dst==null)throw new ArgumentException("两个存档都须已创建 PMC 和 Scav 后，才能还原全部数据。");
    foreach(string key in new[]{"_id","aid","savage","sessionId"})if(src[key] is JsonValue a&&dst[key] is JsonValue b&&a.TryGetValue<string>(out var old)&&b.TryGetValue<string>(out var next)&&old.Length>0)identities[old]=next;
   }
   if(from["_id"]?.ToString()!=to["_id"]?.ToString())foreach(string id in ItemIds(result).Distinct()){if(identities.ContainsKey(id))throw new ArgumentException("物品与角色身份 ID 冲突。");identities[id]=NewId();}
   ReplaceStrings(result,identities);Copy(target,result,"_id","info","spt","aki");
   foreach(string side in new[]{"pmc","scav"}){var dst=Object(target["characters"]![side],side);var copy=Object(result["characters"]![side],side);Copy(dst,copy,"_id","aid","savage","sessionId");}
   Copy(target,result,"inraid");target=result;to=Pmc(target);
  }else foreach(string category in categories)switch(category){
   case "stash":InventoryTree(from,to,"stash");break;
   case "skills":Copy(from,to,"Skills");break;
   case "progress":Copy(from,to,"Stats","Achievements","Encyclopedia","TarCoinBalance","Bonuses");if(from["Info"] is JsonObject a&&to["Info"] is JsonObject b)Copy(a,b,"Level","Experience","SurvivorClass");break;
   case "health":Copy(from,to,"Health");break;
   case "quests":{
    string[] fields=["Quests","TaskConditionCounters","BackendCounters","RepeatableQuests"];
    Copy(from,to,fields);foreach(string root in new[]{"questRaidItems","questStashItems"}){var map=InventoryTree(from,to,root);foreach(string field in fields)ReplaceStrings(to[field],map);}break;
   }
   case "traders":Copy(from,to,"TradersInfo");break;
  }
  Validate(target,installed);return target.ToJsonString();
 }
 public static void Validate(JsonObject profile,Func<string,bool>? installed){
  foreach(string side in new[]{"pmc","scav"}){
   if(profile["characters"]?[side] is not JsonObject character)continue;
   var inventory=Object(character["Inventory"],side+"库存");var items=Items(inventory);var ids=new HashSet<string>();
   foreach(var item in items){if(item==null||!ids.Add(Id(item)))throw new ArgumentException("库存包含空物品或重复 ID，未还原。");string template=item["_tpl"]?.GetValue<string>()??throw new ArgumentException("物品模板缺失。");if(installed!=null&&!installed(template))throw new ArgumentException("缺少物品模板 "+template+"；请安装对应 MOD 后还原。");}
   foreach(var item in items){string? parent=item?["parentId"]?.GetValue<string>();if(!string.IsNullOrEmpty(parent)&&parent!="hideout"&&!ids.Contains(parent))throw new ArgumentException("物品容器引用缺失，未还原。");}
   foreach(string root in new[]{"equipment","stash","sortingTable","questRaidItems","questStashItems"})if(inventory[root] is JsonValue v&&v.TryGetValue<string>(out var id)&&id.Length>0&&!ids.Contains(id))throw new ArgumentException("库存根容器缺失："+root);
   // Walk every parent chain to reject cycles, including disconnected cycles.
   var parents=items.Where(i=>i?["parentId"]!=null).ToDictionary(i=>Id(i!),i=>i!["parentId"]!.GetValue<string>());foreach(string id in ids){var seen=new HashSet<string>();string current=id;while(parents.TryGetValue(current,out var p)&&ids.Contains(p)){if(!seen.Add(current))throw new ArgumentException("库存存在容器循环，未还原。");current=p;}}
  }
 }
}
