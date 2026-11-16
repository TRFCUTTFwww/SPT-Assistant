using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
namespace LocalSpt5Esp;
internal static class ItemFirContext {
 static Task<string>? request;static Item? pending;static Profile? profile;static bool desired;static bool fromCommand;
 internal static bool CanReload=>request==null;
 internal static void Initialize(Plugin plugin)=>plugin.Patches.Patch(AccessTools.Method(typeof(ItemUiContext),"ShowContextMenu"),postfix:new HarmonyMethod(typeof(ItemFirContext),nameof(Menu)));
 static bool Owned(Profile? owner,Item? item)=>owner?.Inventory?.AllRealPlayerItems?.Any(i=>i!=null&&i.Id==item?.Id)==true;
 static void Menu(ItemUiContext __instance,ItemContext __0){
  var item=__0?.Item;var owner=__instance._profile;var container=__instance.ContextMenu?._interactionButtonsContainer;
  if(item==null||!Owned(owner,item)||container==null||request!=null)return;
  string caption=item.SpawnedInSession?"移除战局内发现标记":"添加战局内发现标记";
  container.CreateContextButton("assistant.found-in-raid",I18n.Text(caption),container._buttonTemplate,container._buttonsContainer,null,(Action)(()=>{try{Change(owner,item,!item.SpawnedInSession);}catch(Exception e){FeatureToasts.Show(e.Message);}}),(Action)(()=>{}),false,true);
 }
 internal static IEnumerable<(string Id,string Name)> Items(){var owner=ItemUiContext.Instance?._profile;return owner?.Inventory?.AllRealPlayerItems?.Where(i=>i!=null).Select(i=>(i.Id,LootDisplay.ItemName(i,false))).ToArray()??Array.Empty<(string,string)>();}
 internal static void Command(string id,bool value){var owner=ItemUiContext.Instance?._profile??throw new ArgumentException("当前角色库存尚未就绪。");var item=owner.Inventory.AllRealPlayerItems.SingleOrDefault(i=>i.Id==id)??throw new ArgumentException("找不到该物品实例 ID；请用 Tab 选择当前库存物品。");Change(owner,item,value,true);}
 static void Change(Profile owner,Item item,bool value,bool command=false){
  if(request!=null)throw new ArgumentException("正在保存另一件物品的标记。");
  if(!Owned(owner,item))throw new ArgumentException("该物品已不在当前角色库存中。");
  if(ItemSpawner.InRaid()){
   if(ChatFikaSession.Role!=0)throw new ArgumentException("FIKA 战局中的库存标记写入尚未接入同步。");
   Apply(owner,item,value);return;
  }
  pending=item;profile=owner;desired=value;fromCommand=command;
  request=CharacterDataPanel.Call("/local/gear/data","characterData",new Dictionary<string,object?>{{"action","itemFoundInRaid"},{"id",item.Id},{"unlocked",value},{"expectedValue",item.SpawnedInSession?1:0},{"categories",new[]{owner.Side==EPlayerSide.Savage?"scav":"pmc"}}});
 }
 static void Apply(Profile owner,Item item,bool value){item.SpawnedInSession=value;owner.Inventory.Equipment?.RaiseRefreshEvent(false,true);owner.Inventory.Stash?.RaiseRefreshEvent(false,true);FeatureToasts.Show(value?"已添加战局内发现标记":"已移除战局内发现标记");}
 internal static void Tick(){if(request?.IsCompleted!=true)return;string message;try{using var doc=JsonDocument.Parse(request.GetAwaiter().GetResult());var root=doc.RootElement;if(root.TryGetProperty("err",out var err)&&err.GetInt32()!=0)throw new ArgumentException(root.TryGetProperty("errmsg",out var msg)?msg.GetString():"保存失败。");if(profile!=null&&pending!=null&&Owned(profile,pending))Apply(profile,pending,desired);message=desired?"已添加战局内发现标记并保存。":"已移除战局内发现标记并保存。";}catch(Exception e){message=e.Message;FeatureToasts.Show(message);}if(fromCommand)RaidChat.Output(message);request=null;pending=null;profile=null;fromCommand=false;}
 internal static void Reset(){ItemUiContext.Instance?.ContextMenu?.Close();request=null;pending=null;profile=null;fromCommand=false;}
}
