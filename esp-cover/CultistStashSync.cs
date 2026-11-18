using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Comfort.Common;
using EFT;
using EFT.Hideout;
using EFT.InventoryLogic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Newtonsoft.Json.Linq;
namespace LocalSpt5Esp;
internal static class CultistStashSync {
 internal static void Initialize()=>CultistCircleControl.SyncStash=Apply;
 internal static void Apply(JsonElement data){
  if(!data.TryGetProperty("stashId",out var stashProp)||stashProp.ValueKind!=JsonValueKind.String)return;
  string stashId=stashProp.GetString()??"";
  if(stashId.Length==0||!data.TryGetProperty("stashItems",out var itemsProp)||itemsProp.ValueKind!=JsonValueKind.Array||itemsProp.GetArrayLength()==0)return;
  var hideout=HideoutCraftControl.CurrentHideout;
  var controller=hideout?._inventoryController??EFT.UI.ItemUiContext.Instance?._inventoryController;
  if(controller?.Inventory?.HideoutAreaStashes==null)return;
  if(!controller.Inventory.HideoutAreaStashes.TryGetValue(EAreaType.CircleOfCultists,out var stash)||stash==null||stash.Id.ToString()!=stashId||stash.Grids==null||stash.Grids.Length==0)return;
  var grid=stash.Grids.FirstOrDefault(g=>g?.ID=="CircleOfCultistsGrid1")??stash.Grids[0];
  if(grid==null)return;
  var factory=Singleton<ItemFactory>.Instance;if(factory==null)return;
  var existing=new HashSet<string>(StringComparer.Ordinal);
  foreach(var item in stash.GetAllItems())if(item!=null)existing.Add(item.Id.ToString());
  var all=new List<JsonElement>();
  foreach(var entry in itemsProp.EnumerateArray())all.Add(entry);
  var roots=all.Where(e=>e.TryGetProperty("_id",out var idProp)&&!existing.Contains(idProp.GetString()??"")&&e.TryGetProperty("parentId",out var pProp)&&pProp.GetString()==stashId&&e.TryGetProperty("slotId",out var sProp)&&sProp.GetString()=="CircleOfCultistsGrid1").ToArray();
  if(roots.Length==0)return;
  int added=0;
  foreach(var root in roots){
   string rootId=root.GetProperty("_id").GetString()!;
   var subtree=CollectSubtree(all,rootId);
   var flat=new Il2CppReferenceArray<JsonType.FlatItem>(subtree.Count);
   for(int i=0;i<subtree.Count;i++){
    var entry=subtree[i];
    string id=entry.GetProperty("_id").GetString()!;
    var native=new JsonType.FlatItem{_id=new MongoID(id),_tpl=new MongoID(entry.GetProperty("_tpl").GetString()!)};
    if(id!=rootId){
     if(entry.TryGetProperty("parentId",out var parent)&&parent.ValueKind==JsonValueKind.String)native.parentId=new Il2CppSystem.Nullable<MongoID>(new MongoID(parent.GetString()!));
     if(entry.TryGetProperty("slotId",out var slot)&&slot.ValueKind==JsonValueKind.String)native.slotId=slot.GetString();
     if(entry.TryGetProperty("location",out var location)&&location.ValueKind!=JsonValueKind.Null)native.location=new UnparsedData{JToken=JToken.Parse(location.GetRawText())};
    }
    if(entry.TryGetProperty("upd",out var upd)&&upd.ValueKind!=JsonValueKind.Null)native.upd=new UnparsedData{JToken=JToken.Parse(upd.GetRawText())};
    flat[i]=native;
   }
   var tree=factory.FlatItemsToTree(flat,false,null);
   if(tree?.Items==null||!tree.Items.ContainsKey(rootId))continue;
   var item=tree.Items[rootId];
   var loc=ReadLocation(root);
   var address=grid.HasFreeSpace(item,loc)?grid.CreateItemAddress(loc):grid.FindLocationForItem(item);
   if(address==null||!ItemManipulator.Add(item,address,controller,true).Succeeded)continue;
   controller.AddAndRaiseEvents(item,address);
   if(item.CurrentAddress!=null)added++;
  }
  if(added>0&&hideout?.AreaDatas!=null){
   foreach(var ad in hideout.AreaDatas)if(ad?.Template?.Type==EAreaType.CircleOfCultists){ad.DecideStatus(ad.CurrentLevel);ad.StashController?.UpdateStash(false);}
   Plugin.Self.Log.LogInfo($"Cultist stash sync: added {added} reward item(s) to CircleOfCultistsGrid1 in hideout");
  }
 }
 static List<JsonElement> CollectSubtree(List<JsonElement> all,string rootId){
  var ids=new HashSet<string>(StringComparer.Ordinal){rootId};
  var result=new List<JsonElement>();
  foreach(var entry in all)if(entry.TryGetProperty("_id",out var id)&&id.GetString()==rootId)result.Add(entry);
  bool more=true;
  while(more){
   more=false;
   foreach(var entry in all){
    string? id=entry.TryGetProperty("_id",out var idProp)?idProp.GetString():null;
    string? parent=entry.TryGetProperty("parentId",out var pProp)&&pProp.ValueKind==JsonValueKind.String?pProp.GetString():null;
    if(id!=null&&parent!=null&&ids.Contains(parent)&&ids.Add(id)){result.Add(entry);more=true;}
   }
  }
  return result;
 }
 static LocationInGrid ReadLocation(JsonElement entry){
  int x=0,y=0;ItemRotation rot=ItemRotation.Horizontal;
  if(entry.TryGetProperty("location",out var loc)&&loc.ValueKind==JsonValueKind.Object){
   if(loc.TryGetProperty("x",out var xp)&&xp.TryGetInt32(out int xv))x=xv;
   if(loc.TryGetProperty("y",out var yp)&&yp.TryGetInt32(out int yv))y=yv;
   if(loc.TryGetProperty("r",out var rp)){
    if(rp.ValueKind==JsonValueKind.String&&string.Equals(rp.GetString(),"Vertical",StringComparison.OrdinalIgnoreCase))rot=ItemRotation.Vertical;
    else if(rp.ValueKind==JsonValueKind.Number&&rp.TryGetInt32(out int rv)&&rv!=0)rot=ItemRotation.Vertical;
   }
  }
  return new LocationInGrid(x,y,rot);
 }
}
