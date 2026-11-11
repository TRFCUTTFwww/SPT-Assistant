using System;
using System.Text.Json;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.Interactive;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class WorldEditor {
 sealed record ItemCopy(string Json,string Root);
 sealed class LootReference {
  readonly IntPtr world,pointer,item;readonly string id;
  internal LootReference(GameWorld current,LootItem loot){world=current.Pointer;pointer=loot.Pointer;item=loot.Item.Pointer;id=loot.Item.Id.ToString();}
  internal LootItem Resolve(){var current=Singleton<GameWorld>.Instance;if(current==null||current.Pointer!=world)throw new ArgumentException("战局已改变，请重新选择物品。");var all=current.LootItems;for(int i=0;i<all.Count;i++){var loot=all.GetByIndex(i);if(loot?.Pointer==pointer&&loot.Item?.Pointer==item&&loot.Item.Id.ToString()==id)return loot;}throw new ArgumentException("物品已被拾取、移除或替换。");}
 }
 static ItemCopy CaptureItem(Item root){var factory=Singleton<ItemFactory>.Instance;var flat=factory.TreeToFlatItems(new Item[]{root});var json=LocalSpt.Shared.RaidStashJson.Normalize(Newtonsoft.Json.JsonConvert.SerializeObject(new Il2CppSystem.Object(flat.Pointer)),out _,out _);return new(json.ToJsonString(),root.Id.ToString());}
 static Item CloneItem(ItemCopy copy){
  var factory=Singleton<ItemFactory>.Instance;var data=EditorItemRules.Remap(copy.Json,copy.Root,()=>factory.NextId.ToString(),out string root);using var doc=JsonDocument.Parse(data.ToJsonString());var flat=new Il2CppReferenceArray<JsonType.FlatItem>(data.Count);int i=0;
  foreach(var entry in doc.RootElement.EnumerateArray()){
   var native=new JsonType.FlatItem{_id=new MongoID(entry.GetProperty("_id").GetString()!),_tpl=new MongoID(entry.GetProperty("_tpl").GetString()!)};
   if(entry.TryGetProperty("parentId",out var parent))native.parentId=new Il2CppSystem.Nullable<MongoID>(new MongoID(parent.GetString()!));
   if(entry.TryGetProperty("slotId",out var slot))native.slotId=slot.GetString();
   if(entry.TryGetProperty("location",out var location)&&location.ValueKind!=JsonValueKind.Null)native.location=new UnparsedData{JToken=JToken.Parse(location.GetRawText())};
   if(entry.TryGetProperty("upd",out var upd)&&upd.ValueKind!=JsonValueKind.Null)native.upd=new UnparsedData{JToken=JToken.Parse(upd.GetRawText())};flat[i++]=native;
  }
  var tree=factory.FlatItemsToTree(flat,false,null);if(tree.DeserializationErrors?.Count>0)throw new ArgumentException("物品副本无法完整读取。");return tree.Items[root];
 }
 static void PlaceItem(Item item){var current=Current();current.ThrowItem(item,current.MainPlayer.Cast<IPlayer>(),point+Vector3.up*.15f,Quaternion.identity,Vector3.zero,Vector3.zero,true,true,0);}
}
