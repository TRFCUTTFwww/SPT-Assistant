using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Comfort.Common;
using EFT;
using EFT.Hideout;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.Screens;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Newtonsoft.Json.Linq;
namespace LocalSpt5Esp;
internal static class CultistStashSync {
 const string RewardGrid="CircleOfCultistsGrid1";
 static readonly HashSet<string> refreshed=new();
 static string diagnostic="尚未同步";
 internal static object Diagnostics(){
  var hideout=HideoutCraftControl.CurrentHideout;var rows=new List<object>();
  void Inspect(string source,InventoryController? controller){if(controller?.Inventory?.HideoutAreaStashes?.TryGetValue(EAreaType.CircleOfCultists,out var stash)!=true||stash==null){rows.Add(new{source,ready=false});return;}rows.Add(new{source,ready=true,controller=controller.Pointer.ToInt64(),stash=stash.Pointer.ToInt64(),grids=stash.Grids.Select(g=>new{id=g.ID,items=g.Items.Select(i=>i.Id.ToString()).ToArray()}).ToArray()});}
  Inspect("hideout",hideout?._inventoryController);Inspect("ui",EFT.UI.ItemUiContext.Instance?._inventoryController);Inspect("player",Singleton<GameWorld>.Instance?.MainPlayer?.InventoryController);
  if(hideout?.AreaDatas!=null)foreach(var area in hideout.AreaDatas)if(area?.Template?.Type==EAreaType.CircleOfCultists)Inspect("display",area.StashController?._inventoryController);
  var screens=EftScreenManager.Instance;
  return new{inHideout=CultistCircleControl.InHideout,diagnostic,rows,
   transfer=CultistCircleControl.TransferDiagnostics(),screen=screens?.CurrentScreenController?.ScreenType.ToString(),
   registered=screens?.TryGetScreen(EEftScreenType.HideoutCircleOfCultists,out _)==true};
 }
 internal static void Initialize(){
  CultistCircleControl.SyncStash=Apply;
  CultistCircleControl.OpenStash=OpenRewards;
  CultistCircleControl.IsStashOpen=()=>EftScreenManager.Instance?.CurrentScreenController?.ScreenType==EEftScreenType.HideoutCircleOfCultists;
 }
 static Il2CppSystem.Threading.Tasks.Task<bool>? OpenRewards(JsonElement data){
  var hideout=HideoutCraftControl.CurrentHideout;
  var controller=hideout?._inventoryController;
  if(hideout?.AreaDatas==null||hideout._session is not {} session||controller?.Inventory?.HideoutAreaStashes==null||
     !controller.Inventory.HideoutAreaStashes.TryGetValue(EAreaType.CircleOfCultists,out var stash)||stash?.Grids==null||
     !data.TryGetProperty("stashId",out var stashProp)||stashProp.ValueKind!=JsonValueKind.String||
     stashProp.GetString() is not {} stashId||stashId!=stash.Id.ToString())return null;
  var grid=stash.Grids.FirstOrDefault(g=>g?.ID==RewardGrid);
  var area=hideout.AreaDatas.FirstOrDefault(area=>area?.Template?.Type==EAreaType.CircleOfCultists);
  if(grid==null||area==null)return null;
  var rewardIds=new HashSet<string>(StringComparer.Ordinal);
  foreach(var entry in data.GetProperty("stashItems").EnumerateArray()){
   if(IsRewardRoot(entry,stashId)&&entry.TryGetProperty("_id",out var id)&&id.ValueKind==JsonValueKind.String&&id.GetString() is {} rewardId)
    rewardIds.Add(rewardId);
  }
  if(!grid.Items.Any(item=>item!=null&&rewardIds.Contains(item.Id.ToString())))return null;
  var screens=EftScreenManager.Instance;
  if(screens?.CurrentScreenController?.ScreenType is not (EEftScreenType.MainMenu or EEftScreenType.Hideout)||
     !screens.TryGetScreen(EEftScreenType.HideoutCircleOfCultists,out _))return null;
  var transfer=new HideoutCircleOfCultistsScreen.HideoutCircleOfCultistsScreenController(area,session,controller);
  return transfer.ShowScreenAsync(EScreenState.Queued,false);
 }
 static bool IsRewardRoot(JsonElement entry,string stashId)=>
  entry.TryGetProperty("parentId",out var parent)&&parent.GetString()==stashId&&
  entry.TryGetProperty("slotId",out var slot)&&slot.GetString()==RewardGrid;
 internal static void Apply(JsonElement data){
  if(!CultistCircleControl.InHideout||ItemSpawner.InRaid())return;
  var hideout=HideoutCraftControl.CurrentHideout;
  var controllers=new[]{hideout?._inventoryController,EFT.UI.ItemUiContext.Instance?._inventoryController,Singleton<GameWorld>.Instance?.MainPlayer?.InventoryController};
  var seen=new HashSet<IntPtr>();
  foreach(var controller in controllers)if(controller!=null&&seen.Add(controller.Pointer))ApplyTo(data,controller);
  if(hideout?.AreaDatas==null)return;
  foreach(var area in hideout.AreaDatas)if(area?.Template?.Type==EAreaType.CircleOfCultists&&area.StashController is {} display&&display._areaStash is {} stash){
   // Entering the hideout creates another inventory/controller. Refresh the
   // controller that actually owns the displayed grid, not only the menu one.
   if(display._inventoryController is {} controller&&seen.Add(controller.Pointer))ApplyTo(data,controller);
   string signature=display.Pointer+":"+stash.Pointer+":"+string.Join(",",stash.GetAllItems().Select(i=>i.Id.ToString()).OrderBy(i=>i));
   if(refreshed.Count>128)refreshed.Clear();
   if(refreshed.Add(signature)){area.DecideStatus(area.CurrentLevel);display.UpdateStash(true);}
  }
 }
 static void ApplyTo(JsonElement data,InventoryController controller){
  if(!data.TryGetProperty("stashId",out var stashProp)||stashProp.ValueKind!=JsonValueKind.String)return;
  string stashId=stashProp.GetString()??"";
  if(stashId.Length==0||!data.TryGetProperty("stashItems",out var itemsProp)||itemsProp.ValueKind!=JsonValueKind.Array||itemsProp.GetArrayLength()==0)return;
  if(controller?.Inventory?.HideoutAreaStashes==null)return;
  if(!controller.Inventory.HideoutAreaStashes.TryGetValue(EAreaType.CircleOfCultists,out var stash)||stash==null||stash.Id.ToString()!=stashId||stash.Grids==null||stash.Grids.Length==0)return;
  var grid=stash.Grids.FirstOrDefault(g=>g?.ID==RewardGrid);
  if(grid==null)return;
  var factory=Singleton<ItemFactory>.Instance;if(factory==null)return;
  var existing=new HashSet<string>(StringComparer.Ordinal);
  foreach(var item in stash.GetAllItems())if(item!=null)existing.Add(item.Id.ToString());
  var all=new List<JsonElement>();
  foreach(var entry in itemsProp.EnumerateArray())all.Add(entry);
  var roots=all.Where(e=>e.TryGetProperty("_id",out var idProp)&&!existing.Contains(idProp.GetString()??"")&&IsRewardRoot(e,stashId)).ToArray();
  diagnostic=$"待同步奖励 {roots.Length}，本地物品 {existing.Count}";
  if(roots.Length==0)return;
  int added=0;
  foreach(var root in roots){
   string rootId=root.GetProperty("_id").GetString()!;
   // A reward moved to the player's inventory while the response was pending
   // must never be recreated in the ritual grid.
   if(controller.TryFindItem(rootId,out var owned)&&owned!=null)continue;
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
   // Server locations are only hints. The hideout grid can have changed size,
   // rotation rules or occupied cells since the ritual response was produced.
   // Prefer the recorded location when it is valid, then retry with the native
   // first-fit search instead of leaving the reward invisible.
   var preferred=ReadLocation(root);
   var address=grid.HasFreeSpace(item,preferred)?grid.CreateItemAddress(preferred):null;
   var result=address==null?default:ItemManipulator.Add(item,address,controller,true);
   if (result is { Succeeded: true }) { controller.AddAndRaiseEvents(item,address!); if(item.CurrentAddress!=null)added++; continue; }
   var fallback=grid.FindLocationForItem(item);
   if(fallback==null)continue;
   result=ItemManipulator.Add(item,fallback,controller,true);
   if(!result.Succeeded)continue;
   controller.AddAndRaiseEvents(item,fallback);
   if(item.CurrentAddress!=null)added++;
  }
  diagnostic=$"已同步奖励 {added}/{roots.Length}";
  if(added>0){
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
