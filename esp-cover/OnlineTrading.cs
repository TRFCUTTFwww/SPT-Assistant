using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.HandBook;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.Ragfair;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace LocalSpt5Esp;

internal static class OnlineTrading {
 sealed record NativeSession(Item InitialItem,ItemController Controller,CompoundItem GridContainer,EFT.InventoryLogic.Grid Grid,AddOfferWindow Window,IntPtr Raid){
  internal bool IsCurrent(){
   if(!ItemSpawner.InRaid()||ChatFikaSession.Role!=0)return false;
   var game=Singleton<AbstractGame>.Instance;
   if(game==null||game.Pointer!=Raid)return false;
   return Window!=null&&Window.gameObject.activeInHierarchy;
  }

  internal Item? FindItem(string id){
   if(InitialItem!=null&&InitialItem.Pointer!=IntPtr.Zero&&InitialItem.Id==id&&InitialItem.CurrentAddress!=null)return InitialItem;
   if(GridContainer!=null&&GridContainer.Pointer!=IntPtr.Zero){
    foreach(var candidate in GridContainer.GetAllItems()){
     if(candidate!=null&&candidate.Id==id&&candidate.CurrentAddress!=null)return candidate;
    }
   }
   return null;
  }
 }

 static NativeSession? activeSession;
 static Task<string>? submitting;
 static Item? pendingItem;
 static ItemController? pendingController;
 static Il2CppSystem.Action? pendingCallback;
 static IntPtr pendingRaid;

 internal static bool Open=>activeSession!=null&&activeSession.Window!=null&&activeSession.Window.gameObject.activeInHierarchy;
 internal static bool Saving=>submitting!=null;

 static IntPtr CurrentRaid(){
  var game=Singleton<AbstractGame>.Instance;
  return game!=null&&game.InRaid&&game.TryCast<HideoutGame>()==null?game.Pointer:IntPtr.Zero;
 }

 internal static void Initialize(Plugin plugin){
  plugin.Patches.Patch(AccessTools.Method(typeof(RagFair),nameof(RagFair.AddOffer)),prefix:new HarmonyMethod(typeof(OnlineTrading),nameof(InterceptAddOffer)));
  plugin.Patches.Patch(AccessTools.Method(typeof(AddOfferWindow),nameof(AddOfferWindow.SetPrices)),postfix:new HarmonyMethod(typeof(OnlineTrading),nameof(RefreshNativeButton)));
  plugin.Patches.Patch(AccessTools.Method(typeof(AddOfferWindow),nameof(AddOfferWindow.SetupWeapon)),prefix:new HarmonyMethod(typeof(OnlineTrading),nameof(GuardWeaponPreview)));
  plugin.Patches.Patch(AccessTools.Method(typeof(AddOfferWindow),nameof(AddOfferWindow.Close)),postfix:new HarmonyMethod(typeof(OnlineTrading),nameof(OnWindowClosed)));
 }

 static bool GuardWeaponPreview(AddOfferWindow __instance)=>__instance._weaponPreviewPool!=null;

 internal static void OpenNative(Item item,ItemUiContext uiContext,ItemController? fallbackController){
  if(Saving)return;
  if(!MiscPanel.Options.OnlineTrading)throw new ArgumentException("请先在杂项设置中开启「线上交易」。");
  if(!ItemSpawner.InRaid()||ChatFikaSession.Role!=0)throw new ArgumentException("仅支持本地单人战局中使用线上交易。");
  if(item.CurrentAddress==null)throw new ArgumentException("物品已离开当前容器，请重新打开右键菜单。");

  var itemGrid=item.CurrentAddress.Container?.TryCast<EFT.InventoryLogic.Grid>()
   ??throw new ArgumentException("请将物品放入背包或容器网格后再新增报价。");
  var gridOwner=itemGrid.ParentItem
   ??item.CurrentAddress.Container.ParentItem?.TryCast<CompoundItem>()
   ??throw new ArgumentException("未找到该物品所在的容器网格。");

  var window=uiContext._addOfferWindow??throw new ArgumentException("游戏原生报价窗口尚未就绪。");
  var app=Singleton<AbstractApplication>.Instance?.TryCast<TarkovApplication>();
  var ragfair=uiContext._ragfair
   ??uiContext.Session?.RagFair
   ??uiContext.ClientSession?.TryCast<IEftSession>()?.RagFair
   ??app?.Session?.RagFair
   ??throw new ArgumentException("跳蚤市场会话尚未就绪。");
  var handbook=uiContext.Handbook??Singleton<Handbook>.Instance??throw new ArgumentException("图鉴数据尚未就绪。");
  var player=Singleton<GameWorld>.Instance?.MainPlayer;
  var invController=player?.InventoryController??uiContext._inventoryController??throw new ArgumentException("玩家库存控制器尚未就绪。");
  var itemController=item.Owner?.TryCast<ItemController>()??fallbackController??invController.Cast<ItemController>();

  var originalGrids=gridOwner.Grids;
  bool swapGrids=originalGrids==null||originalGrids.Length==0||originalGrids[0]?.Pointer!=itemGrid.Pointer;
  try{
   if(swapGrids)gridOwner.Grids=new Il2CppReferenceArray<EFT.InventoryLogic.Grid>(new[]{itemGrid});
   var lootItems=new Il2CppReferenceArray<CompoundItem>(new[]{gridOwner});
   activeSession=new NativeSession(item,itemController,gridOwner,itemGrid,window,CurrentRaid());
   window.Show(invController,lootItems,ragfair,handbook,uiContext,uiContext.WeaponPreviewPool,item);
  }finally{
   if(swapGrids&&originalGrids!=null)gridOwner.Grids=originalGrids;
  }
 }

 static bool InterceptAddOffer(bool __0,Il2CppStringArray __1,Il2CppReferenceArray<BarterTemplate> __2,Il2CppSystem.Action __3){
  if(activeSession is not NativeSession session||!session.IsCurrent())return true;
  try{
   if(Saving)return false;
   if(__1==null||__1.Length==0)throw new ArgumentException("请选择要上架的物品。");
   if(__1.Length>1)throw new ArgumentException("战局内线上交易每次请只选择一件或一组物品。");
   if(__2==null||__2.Length==0||__2[0]==null)throw new ArgumentException("请先设置报价币种与金额。");

   string itemId=__1[0];
   var item=session.FindItem(itemId)??throw new ArgumentException("物品已离开当前容器，请重新选择。");
   var controller=item.Owner?.TryCast<ItemController>()??session.Controller;
   var factory=Singleton<ItemFactory>.Instance??throw new ArgumentException("物品工厂尚未就绪。");
   var flat=factory.TreeToFlatItems(new Item[]{item});
   string jsonTree=LocalSpt.Shared.RaidStashJson.Normalize(Newtonsoft.Json.JsonConvert.SerializeObject(new Il2CppSystem.Object(flat.Pointer)),out _,out _).ToJsonString();

   var req=__2[0];
   string currency=req._tpl??"5449016a4bdc2d6f028b456f";
   double price=Math.Truncate(req.count);
   int count=Math.Max(1,item.StackObjectsCount);

   pendingItem=item;
   pendingController=controller;
   pendingCallback=__3;
   pendingRaid=session.Raid;
   submitting=CharacterDataPanel.Call("/local/gear/data","characterData",new(){
    {"action","onlineAddOffer"},
    {"stashItems",JsonSerializer.Deserialize<JsonElement>(jsonTree)},
    {"currency",currency},
    {"value",price},
    {"count",count},
    {"sellInOnePiece",__0}
   });
   FeatureToasts.Show(I18n.Text("正在上架到跳蚤市场…"));
  }catch(Exception error){
   FeatureToasts.Show(error.Message);
  }
  return false;
 }

 static void RefreshNativeButton(AddOfferWindow __instance){
  if(activeSession==null||__instance.Pointer!=activeSession.Window.Pointer)return;
  if(__instance._addOfferButton==null||__instance._selectedItem==null)return;
  var views=__instance._dynamicRequirementViews;
  bool ready=views!=null&&views.All(view=>view!=null&&view.Requirement!=null)
   &&__instance.OfferItemCount>0&&double.IsFinite(__instance._requirementsCost)&&__instance._requirementsCost>0
   &&(__instance._loader==null||!__instance._loader.activeInHierarchy);
  __instance._addOfferButton.Interactable=ready;
 }

 static void OnWindowClosed(AddOfferWindow __instance){
  if(activeSession!=null&&__instance.Pointer==activeSession.Window.Pointer&&!Saving){
   activeSession=null;
  }
 }

 static void RemoveFromRaid(Item item,ItemController controller){
  if(item==null||item.Pointer==IntPtr.Zero||item.CurrentAddress==null||controller==null||controller.Pointer==IntPtr.Zero)return;
  var result=ItemManipulator.Remove(item,controller,false);
  if(!result.Succeeded)result=ItemManipulator.RemoveWithoutRestrictions(item,controller);
  if(result.Succeeded)result.Value.RaiseEvents(controller.Cast<IItemOwner>(),CommandStatus.Succeed);
 }

 internal static void Tick(){
  if(submitting?.IsCompleted!=true)return;
  bool listed=false;
  try{
   using var doc=JsonDocument.Parse(submitting.GetAwaiter().GetResult());
   var response=doc.RootElement;
   if(response.TryGetProperty("err",out var err)&&err.GetInt32()!=0)
    throw new ArgumentException(response.TryGetProperty("errmsg",out var msg)?msg.GetString():"上架失败。");
   var data=CharacterDataPanel.Data(response);
   listed=data.TryGetProperty("ok",out var ok)&&ok.ValueKind==JsonValueKind.True;
   if(!listed)throw new ArgumentException("上架结果无法确认。");
   bool instantSold=data.TryGetProperty("instantSold",out var sold)&&sold.GetBoolean();
   if(CurrentRaid()==pendingRaid&&pendingItem!=null&&pendingController!=null)
    RemoveFromRaid(pendingItem,pendingController);
   try{pendingCallback?.Invoke();}catch{}
   try{if(activeSession?.Window!=null&&activeSession.Window.gameObject.activeInHierarchy)activeSession.Window.Close();}catch{}
   activeSession=null;
   FeatureToasts.Show(I18n.Text(instantSold?"报价已上架并立即售出（请查收 Ragman 邮件）。":"报价已上架跳蚤市场。"));
  }catch(Exception error){
   if(listed){
    try{pendingCallback?.Invoke();}catch{}
    activeSession=null;
    FeatureToasts.Show(I18n.Text("报价已上架跳蚤市场。"));
   }else{
    FeatureToasts.Show(error.Message);
   }
  }finally{
   submitting=null;
   pendingItem=null;
   pendingController=null;
   pendingCallback=null;
   pendingRaid=IntPtr.Zero;
  }
 }

 internal static void Reset(){
  activeSession=null;
  submitting=null;
  pendingItem=null;
  pendingController=null;
  pendingCallback=null;
  pendingRaid=IntPtr.Zero;
 }
}
