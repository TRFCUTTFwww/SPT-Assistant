using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using Il2CppInterop.Runtime.Runtime;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class ItemFirContext {
 static float nextWarning;static Task<string>? request;static Item? pending;static Profile? profile;static bool desired;static bool fromCommand;
 const string EditButtonId="assistant.edit-inventory-item";
 const string FirButtonId="assistant.found-in-raid";
 const string OfferButtonId="assistant.online-add-offer";
 internal static bool CanReload=>request==null&&!ItemInventoryEditor.Saving&&!OnlineTrading.Saving&&!GridCellItemCreator.Saving;
 internal static void Initialize(Plugin plugin){
  InventoryContextSettings.Initialize();
  var method=AccessTools.Method(typeof(ItemUiContext),nameof(ItemUiContext.ShowContextMenu));
  if(Harmony.GetPatchInfo(method) is {} patches)
   foreach(var patch in patches.Postfixes)
    if(patch.owner==plugin.Patches.Id&&patch.PatchMethod.DeclaringType?.FullName==typeof(ItemFirContext).FullName)
     plugin.Patches.Unpatch(method,patch.PatchMethod);
  plugin.Patches.Patch(method,postfix:new HarmonyMethod(typeof(ItemFirContext),nameof(Menu)));
  ClearLoadedMenus();
 }
 static bool Owned(Profile? owner,Item? item)=>owner?.Inventory?.AllRealPlayerItems?.Any(i=>i!=null&&i.Id==item?.Id)==true;
 internal static bool IsAssistantButton(SimpleContextMenuButton button,string? owner=null){
  // The native menu wraps the supplied click action. Compare the managed
  // callback's owner by name because each hot reload loads a new assembly.
  var target=button._onClicked?.m_target;
  var wrapped=target?.TryCast<InteractionButtonsContainer.__c__DisplayClass27_0>();
  if(wrapped!=null)target=wrapped.onButtonClicked?.m_target;
  if(target==null||target.GetIl2CppType().Name!="Il2CppToMonoDelegateReference")return false;
  var reference=ClassInjectorBase.GetMonoObjectFromIl2CppPointer(target.Pointer);
  var callback=reference?.GetType().GetField("ReferencedDelegate",BindingFlags.Instance|BindingFlags.Public)?.GetValue(reference) as Delegate;
  var declaringType=callback?.Method.DeclaringType;
  return declaringType!=null&&declaringType.DeclaringType?.FullName==(owner??typeof(ItemFirContext).FullName)&&
   declaringType.Assembly.GetName().Name==typeof(ItemFirContext).Assembly.GetName().Name;
 }
 static void Warn(Exception error){
  if(UnityEngine.Time.unscaledTime<nextWarning)return;
  nextWarning=UnityEngine.Time.unscaledTime+15;
  Plugin.Self?.Log.LogWarning("Inventory menu update failed: "+error.Message);
 }
 static void ClearLoadedMenus(){
  try{
   foreach(var container in UnityEngine.Object.FindObjectsOfType<InteractionButtonsContainer>(true))
    ClearButtons(container);
  }catch(Exception error){Warn(error);}
 }
 internal static void ClearButtons(InteractionButtonsContainer? container){
  if(container?._buttonsContainer==null)return;
  foreach(var button in container._buttonsContainer.GetComponentsInChildren<SimpleContextMenuButton>(true)){
   try{
    if(button==null||button.Pointer==container._buttonTemplate?.Pointer||!IsAssistantButton(button))continue;
    container._redrawRequired=true;
    var instances=container._buttonInstancePools;
    if(instances?.TryGetValue(button,out var pool)==true){
     instances.Remove(button);
     if(pool._poolInUse?.Contains(button)==true){
      pool.Release(button);
      continue;
     }
    }
    if(button.gameObject.activeSelf){
     button.Close();
     button.gameObject.SetActive(false);
    }
   }catch(Exception error){Warn(error);}
  }
 }
 static void AddButton(InteractionButtonsContainer container,string id,string caption,Action clicked){
  var button=container.CreateContextButton(id,caption,container._buttonTemplate,container._buttonsContainer,null,clicked,(Action)(()=>{}),false,true);
  button.SetButtonInteraction(Comfort.Common.SuccessfulResult.New);
 }
 static void Menu(ItemUiContext __instance,ItemContext __0){try{
  GridCellItemCreator.NativeItemMenuOpened();
  var item=__0?.Item;var owner=__instance._profile;var container=__instance.ContextMenu?._interactionButtonsContainer;
  ClearButtons(container);
  if(item==null||container?._buttonTemplate==null||container._buttonsContainer==null)return;
  RaidItemPoolGuard.Warm(item);
  if(MiscPanel.Options.OnlineTrading&&ItemSpawner.InRaid()&&ChatFikaSession.Role==0&&!OnlineTrading.Open&&!OnlineTrading.Saving&&!ItemInventoryEditor.Open&&!ItemInventoryEditor.Saving){
   AddButton(container,OfferButtonId,I18n.Text("新增报价"),()=>{try{OnlineTrading.OpenNative(item,__instance,__instance._itemController??__instance._inventoryController);}catch(Exception error){FeatureToasts.Show(error.Message);}});
  }
  if(owner==null||!Owned(owner,item))return;
  if(InventoryContextSettings.Edit&&!ItemInventoryEditor.Open&&!ItemInventoryEditor.Saving){
   AddButton(container,EditButtonId,I18n.Text("编辑"),()=>{try{if(InventoryContextSettings.Edit)ItemInventoryEditor.Show(owner,item);}catch(Exception error){FeatureToasts.Show(error.Message);}});
  }
  if(!InventoryContextSettings.Fir||request!=null||ItemInventoryEditor.Saving)return;
  string caption=item.SpawnedInSession?"移除战局内发现标记":"添加战局内发现标记";
  bool original=item.SpawnedInSession;
  AddButton(container,FirButtonId,I18n.Text(caption),()=>{try{if(!InventoryContextSettings.Fir)return;if(item.SpawnedInSession!=original)throw new ArgumentException("物品标记已经改变，请重新打开右键菜单。");Change(owner,item,!original);}catch(Exception e){FeatureToasts.Show(e.Message);}});
 }catch(Exception error){Warn(error);}
 }
 internal static IEnumerable<(string Id,string Name)> Items(){var owner=ItemUiContext.Instance?._profile;return owner?.Inventory?.AllRealPlayerItems?.Where(i=>i!=null).Select(i=>(i.Id,LootDisplay.ItemName(i,false))).ToArray()??Array.Empty<(string,string)>();}
 internal static void Command(string id,bool value){var owner=ItemUiContext.Instance?._profile??throw new ArgumentException("当前角色库存尚未就绪。");var item=owner.Inventory.AllRealPlayerItems.SingleOrDefault(i=>i.Id==id)??throw new ArgumentException("找不到该物品实例 ID；请用 Tab 选择当前库存物品。");Change(owner,item,value,true);}
 static void Change(Profile owner,Item item,bool value,bool command=false){
  if(request!=null||ItemInventoryEditor.Saving)throw new ArgumentException("正在保存另一件物品的标记。");
  if(!Owned(owner,item))throw new ArgumentException("该物品已不在当前角色库存中。");
  if(ItemSpawner.InRaid()){
   if(ChatFikaSession.Role!=0)throw new ArgumentException("FIKA 战局中的库存标记写入尚未接入同步。");
   Apply(owner,item,value);return;
  }
  pending=item;profile=owner;desired=value;fromCommand=command;
  request=CharacterDataPanel.Call("/local/gear/data","characterData",new Dictionary<string,object?>{{"action","itemFoundInRaid"},{"id",item.Id},{"unlocked",value},{"expectedValue",item.SpawnedInSession?1:0},{"categories",new[]{owner.Side==EPlayerSide.Savage?"scav":"pmc"}}});
 }
 static void Apply(Profile owner,Item item,bool value){
  item.SpawnedInSession=value;
  Refresh(owner,item);
  FeatureToasts.Show(value?"已添加战局内发现标记":"已移除战局内发现标记");
 }
 internal static void Refresh(Profile owner,Item item){
  item.RaiseRefreshEvent(true,true);
  owner.Inventory.Equipment?.RaiseRefreshEvent(false,true);owner.Inventory.Stash?.RaiseRefreshEvent(false,true);
  // Refresh the already-visible native item views in place. The inventory
  // events update data, but some grid views defer the quest/FIR sprite until
  // the next full redraw, which made the toggle appear broken.
  foreach(var view in UnityEngine.Object.FindObjectsOfType<ItemView>(true))try{if(view?.Item?.Id==item.Id){view.RefreshIcon();view.SetQuestItemViewPanel();}}catch{}
 }
 internal static void Tick(){if(request?.IsCompleted!=true)return;string message;try{using var doc=JsonDocument.Parse(request.GetAwaiter().GetResult());var root=doc.RootElement;if(root.TryGetProperty("err",out var err)&&err.GetInt32()!=0)throw new ArgumentException(root.TryGetProperty("errmsg",out var msg)?msg.GetString():"保存失败。");if(profile!=null&&pending!=null&&Owned(profile,pending))Apply(profile,pending,desired);message=desired?"已添加战局内发现标记并保存。":"已移除战局内发现标记并保存。";}catch(Exception e){message=e.Message;FeatureToasts.Show(message);}if(fromCommand)RaidChat.Output(message);request=null;pending=null;profile=null;fromCommand=false;}
 internal static void Reset(){
  ItemInventoryEditor.Reset();
  GridCellItemCreator.Reset();
  ClearLoadedMenus();
  ItemUiContext.Instance?.ContextMenu?.Close();
  request=null;pending=null;profile=null;fromCommand=false;
 }
}
