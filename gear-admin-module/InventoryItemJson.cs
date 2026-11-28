using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using LocalSpt.Shared;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Utils;

namespace LocalGearMail;

public static class InventoryItemJson {
 public static List<Item> Tree(BotBaseInventory inventory,string id,string? template=null){
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
  if(!string.IsNullOrEmpty(revision)&&TreeRevision(original,json)!=revision)throw new ArgumentException("物品数据已有更新，请刷新后重试。");
  var candidate=InventoryItemJsonRules.ValidateTree(json.Serialize(original)!,draft);
  var candidateRootObj=InventoryItemJsonRules.FindRoot(candidate);
  string newRootId=candidateRootObj["_id"]!.GetValue<string>();

  var oldIds=original.Select(item=>item.Id.ToString()).ToHashSet(StringComparer.Ordinal);
  var outsideIds=inventory.Items!.Where(item=>!oldIds.Contains(item.Id.ToString())).Select(item=>item.Id.ToString()).ToHashSet(StringComparer.Ordinal);

  var updatedList=candidate.OfType<JsonObject>().Select(item=>Decode(item,json)).ToList();
  foreach(var item in updatedList)
   if(outsideIds.Contains(item.Id.ToString()))
    throw new ArgumentException("物品 _id 与库存中其他已有物品冲突："+item.Id);

  int insertAt=inventory.Items!.FindIndex(item=>item.Id.ToString()==id);
  var nextItems=inventory.Items!.Where(item=>!oldIds.Contains(item.Id.ToString())).ToList();
  if(insertAt>=0&&insertAt<=nextItems.Count)nextItems.InsertRange(insertAt,updatedList);
  else nextItems.AddRange(updatedList);
  inventory.Items=nextItems;

  return Tree(inventory,newRootId);
 }

 public static List<Item> AddItemsToGrid(BotBaseInventory inventory,string rawItemsJson,JsonUtil json){
  var array=InventoryItemJsonRules.ParseTree(rawItemsJson);
  var existingIds=(inventory.Items??throw new ArgumentException("角色库存尚未就绪。")).Select(i=>i.Id.ToString()).ToHashSet(StringComparer.Ordinal);
  var added=array.OfType<JsonObject>().Select(obj=>Decode(obj,json)).ToList();
  if(added.Count==0)throw new ArgumentException("没有可添加的物品。");
  foreach(var item in added){
   if(!MongoId.IsValidMongoId(item.Id.ToString()))throw new ArgumentException("物品 _id 必须是 24 位十六进制 MongoId："+item.Id);
   if(!existingIds.Add(item.Id.ToString()))throw new ArgumentException("物品 _id 与库存中已有物品冲突："+item.Id);
  }
  foreach(var item in added){
   if(!string.IsNullOrEmpty(item.ParentId)&&!existingIds.Contains(item.ParentId))
    throw new ArgumentException("目标父容器不存在："+item.ParentId);
  }
  var next=inventory.Items.ToList();
  next.AddRange(added);
  inventory.Items=next;
  return added;
 }

 public static Item Find(BotBaseInventory inventory,string id,string? template=null)=>
  inventory.Items?.SingleOrDefault(item=>item.Id.ToString()==id&&(string.IsNullOrEmpty(template)||item.Template.ToString()==template))
   ??throw new ArgumentException("该物品已不在当前角色库存中。");

 public static string Revision(Item item,JsonUtil json)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json.Serialize(item)!)));

 public static Item Apply(BotBaseInventory inventory,string id,string template,string draft,string revision,JsonUtil json){
  var original=Find(inventory,id,template);
  if(!string.IsNullOrEmpty(revision)&&Revision(original,json)!=revision)throw new ArgumentException("物品数据已有更新，请刷新后重试。");
  var candidate=InventoryItemJsonRules.Validate(json.Serialize(original)!,draft);
  var updated=Decode(candidate,json);
  if(updated.Id!=original.Id&&inventory.Items!.Any(item=>item.Id==updated.Id))
   throw new ArgumentException("物品 _id 与库存中其他已有物品冲突："+updated.Id);
  var items=inventory.Items!.ToList();
  int index=items.IndexOf(original);
  items[index]=updated;
  if(updated.Id!=original.Id){
   string oldId=original.Id.ToString(),newId=updated.Id.ToString();
   for(int i=0;i<items.Count;i++)if(i!=index&&items[i].ParentId==oldId)items[i]=items[i] with {ParentId=newId};
  }
  inventory.Items=items;
  return updated;
 }

 static Item Decode(JsonObject candidate,JsonUtil json){
  try{
   return json.Deserialize<Item>(candidate.ToJsonString())??throw new ArgumentException("物品 JSON 无法读取。");
  }catch(Exception ex) when(ex is System.Text.Json.JsonException or FormatException or OverflowException){
   throw new ArgumentException("物品 JSON 字段类型无效。");
  }
 }
}
