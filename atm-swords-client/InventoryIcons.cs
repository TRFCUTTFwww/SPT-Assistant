using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine;
using Local.ModHost;
namespace AllTheModium.Client;
public sealed partial class SwordModule {
 readonly Dictionary<IntPtr,ItemView> iconViews=[];
 readonly Dictionary<IntPtr,DraggedItemView> draggedIconViews=[];
 float nextIconScan;
 bool discoverExistingIcons=true;
 void InitializeInventoryIcons(Harmony harmony){
  harmony.Patch(AccessTools.Method(typeof(ItemView),nameof(ItemView.IconChangedHandler)),prefix:new HarmonyMethod(typeof(SwordModule),nameof(BeforeInventoryIcon)),finalizer:new HarmonyMethod(typeof(SwordModule),nameof(AfterInventoryIcon)));
  harmony.Patch(AccessTools.Method(typeof(DraggedItemView),nameof(DraggedItemView.OnIconChanged)),prefix:new HarmonyMethod(typeof(SwordModule),nameof(BeforeDraggedIcon)),finalizer:new HarmonyMethod(typeof(SwordModule),nameof(AfterDraggedIcon)));
 }
 // A temporary private ItemIcon lets native layout, rotation, tint and slot
 // scaling run normally. Never rewrite the shared or on-disk icon cache.
 static void BeforeInventoryIcon(ItemView __instance,out ItemIcon? __state){
  __state=null;var a=active;string? id=__instance.Item?.StringTemplateId;
  if(a?.context?.IsActive!=true||id==null||!a.assets.TryGetValue(id,out var asset)||__instance._itemIcon==null)return;
  __state=__instance._itemIcon;__instance._itemIcon=asset.InventoryIcon;a.iconViews[__instance.Pointer]=__instance;
 }
 static void AfterInventoryIcon(ItemView __instance,ItemIcon? __state){if(__state==null)return;__instance._itemIcon=__state;
  if(__instance.GetIl2CppType().Name==nameof(GridItemView)&&__instance.Item!=null){var size=__instance.Item.CalculateRotatedSize(__instance.ItemRotation);var rect=__instance.RectTransform;if(rect!=null){rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,InventoryIconRules.Pixels(size.X));rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,InventoryIconRules.Pixels(size.Y));}}
 }
 static void BeforeDraggedIcon(DraggedItemView __instance,out ItemIcon? __state){
  __state=null;var a=active;string? id=__instance._item?.StringTemplateId;
  if(a?.context?.IsActive!=true||id==null||!a.assets.TryGetValue(id,out var asset)||__instance._itemIcon==null)return;
  __state=__instance._itemIcon;__instance._itemIcon=asset.InventoryIcon;a.draggedIconViews[__instance.Pointer]=__instance;
 }
 static void AfterDraggedIcon(DraggedItemView __instance,ItemIcon? __state){if(__state==null)return;__instance._itemIcon=__state;
  if(__instance._item!=null&&__instance.ItemContext!=null){var size=__instance._item.CalculateRotatedSize(__instance.ItemContext.ItemRotation);var rect=__instance.RectTransform;if(rect!=null){rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,InventoryIconRules.Pixels(size.X));rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,InventoryIconRules.Pixels(size.Y));}}
 }
 void RefreshInventoryIcons(){
  if(Time.unscaledTime<nextIconScan)return;nextIconScan=Time.unscaledTime+.5f;
  foreach(var key in iconViews.Where(p=>p.Value==null||p.Value.IsKilled).Select(p=>p.Key).ToArray())iconViews.Remove(key);
  foreach(var key in draggedIconViews.Where(p=>p.Value==null).Select(p=>p.Key).ToArray())draggedIconViews.Remove(key);
  // One discovery pass covers hot reload with inventory already open. New
  // views register at the native icon callback; no recurring global scan.
  if(discoverExistingIcons){discoverExistingIcons=false;foreach(var view in Resources.FindObjectsOfTypeAll<ItemView>()){
   string? id=view?.Item?.StringTemplateId;if(view==null||view.IsKilled||!view.gameObject.activeInHierarchy||id==null||!assets.TryGetValue(id,out var asset))continue;
   iconViews[view.Pointer]=view;
   if(view.MainImage?.sprite?.Pointer!=asset.InventorySprite.Pointer)view.IconChangedHandler();
  }}
  foreach(var view in iconViews.Values){
   string? id=view.Item?.StringTemplateId;if(view.IsKilled||!view.gameObject.activeInHierarchy||id==null||!assets.TryGetValue(id,out var asset))continue;
   if(view.MainImage?.sprite?.Pointer!=asset.InventorySprite.Pointer)view.IconChangedHandler();
  }
 }
 void RestoreInventoryIcons(){
  foreach(var view in iconViews.Values)try{if(view!=null&&!view.IsKilled&&view.gameObject.activeInHierarchy&&view.Item!=null&&view._itemIcon!=null)view.IconChangedHandler();}catch(Exception e){context?.Log(LogLevel.Warning,"Restore inventory icon: "+e.Message);}
  foreach(var view in draggedIconViews.Values)try{if(view!=null&&view.gameObject.activeInHierarchy&&view._item!=null&&view.ItemContext!=null&&view._itemIcon!=null)view.OnIconChanged();}catch{}
  iconViews.Clear();draggedIconViews.Clear();
 }
}
