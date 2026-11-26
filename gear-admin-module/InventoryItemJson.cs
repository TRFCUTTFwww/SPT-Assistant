using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using LocalSpt.Shared;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Utils;

namespace LocalGearMail;

public static class InventoryItemJson {
 public static List<Item> Tree(BotBaseInventory inventory,string id,string template){
  var root=Find(inventory,id,template);
  var children=inventory.Items!.Where(item=>!string.IsNullOrEmpty(item.ParentId)).ToLookup(item=>item.ParentId!,StringComparer.Ordinal);
  var result=new List<Item>{root};var seen=new HashSet<string>(StringComparer.Ordinal){id};
  for(int i=0;i<result.Count;i++)foreach(var child in children[result[i].Id.ToString()]){
   if(!seen.Add(child.Id.ToString()))throw new ArgumentException("物品树包含重复编号或循环引用。");
   result.Add(child);
  }
  if(inventory.Items!.Count(item=>seen.Contains(item.Id.ToString()))!=result.Count)throw new ArgumentException("库存包含重复物品编号。");
  return result;
 }
 public static string TreeRevision(IEnumerable<Item> items,JsonUtil json)=>
  Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json.Serialize(items.OrderBy(item=>item.Id.ToString(),StringComparer.Ordinal).ToList())!)));
 public static List<Item> ApplyTree(BotBaseInventory inventory,string id,string template,string draft,string revision,JsonUtil json){
  var original=Tree(inventory,id,template);
  if(TreeRevision(original,json)!=revision)throw new ArgumentException("物品数据已有更新，请刷新后重试。");
  var candidate=InventoryItemJsonRules.ValidateTree(json.Serialize(original)!,draft);
  var updated=candidate.OfType<JsonObject>().Select(item=>Decode(item,json)).ToDictionary(item=>item.Id);
  var items=inventory.Items!.Select(item=>updated.TryGetValue(item.Id,out var replacement)?replacement:item).ToList();
  inventory.Items=items;
  return original.Select(item=>updated[item.Id]).ToList();
 }
 public static Item Find(BotBaseInventory inventory,string id,string template)=>
  inventory.Items?.SingleOrDefault(item=>item.Id.ToString()==id&&item.Template.ToString()==template)
   ??throw new ArgumentException("该物品已不在当前角色库存中。");
 public static string Revision(Item item,JsonUtil json)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json.Serialize(item)!)));
 public static Item Apply(BotBaseInventory inventory,string id,string template,string draft,string revision,JsonUtil json){
  var original=Find(inventory,id,template);
  if(Revision(original,json)!=revision)throw new ArgumentException("物品数据已有更新，请刷新后重试。");
  var candidate=InventoryItemJsonRules.Validate(json.Serialize(original)!,draft);
  var updated=Decode(candidate,json);
  var items=inventory.Items!.ToList();items[items.IndexOf(original)]=updated;inventory.Items=items;
  return updated;
 }
 static Item Decode(JsonObject candidate,JsonUtil json){
  Item updated;
  try{updated=json.Deserialize<Item>(candidate.ToJsonString())??throw new ArgumentException("物品 JSON 无法读取。");}
  catch(System.Text.Json.JsonException){throw new ArgumentException("物品 JSON 字段类型无效。");}
  if(!JsonNode.DeepEquals(candidate,JsonNode.Parse(json.Serialize(updated)!)))
   throw new ArgumentException("部分 JSON 字段无法按原值保存，请检查字段类型。");
  return updated;
 }
}
