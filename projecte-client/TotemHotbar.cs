using Comfort.Common;
using EFT;
using EFT.InputSystem;
using EFT.InventoryLogic;
using HarmonyLib;
namespace ProjectE.Client;
public sealed partial class ProjectModule {
 void InitializeTotemHotbar(Harmony harmony){
  harmony.Patch(AccessTools.Method(typeof(InventoryController),nameof(InventoryController.IsAtBindablePlace)),postfix:new HarmonyMethod(typeof(ProjectModule),nameof(TotemBindable)));
  harmony.Patch(AccessTools.Method(typeof(GamePlayerOwner),nameof(GamePlayerOwner.TranslateCommand)),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(TotemSlotCommand)){priority=Priority.First});
 }
 static void TotemBindable(InventoryController __instance,Item __0,ref bool __result){
  if(active?.context?.IsActive!=true||__0?.StringTemplateId!=Gadgets.Totem)return;
  var access=InventoryAccess.Current();if(access==null||access.Controller.Pointer!=__instance.Pointer)return;
  bool reachable=false;var equipment=access.Profile.Inventory.Equipment;
  foreach(var slot in new[]{EquipmentSlot.Pockets,EquipmentSlot.TacticalVest}){
   var root=equipment.GetSlot(slot)?.ContainedItem?.TryCast<CompoundItem>();
   if(root?.Grids!=null)foreach(var grid in root.Grids)if(grid!=null&&grid.Items.Any(i=>i.Pointer==__0.Pointer)){reachable=true;break;}
  }
  __result=TotemRescueGate.Bindable(true,true,reachable);
 }
 static bool TotemSlotCommand(GamePlayerOwner __instance,ECommand __0,ref InputNode.ETranslateResult __result){
  if(active?.context?.IsActive!=true||__instance.Player?.IsYourPlayer!=true)return true;
  EBoundItem? slot=__0 switch {
   ECommand.SelectFastSlot4 or ECommand.PressSlot4=>EBoundItem.Item4,
   ECommand.SelectFastSlot5 or ECommand.PressSlot5=>EBoundItem.Item5,
   ECommand.SelectFastSlot6 or ECommand.PressSlot6=>EBoundItem.Item6,
   ECommand.SelectFastSlot7 or ECommand.PressSlot7=>EBoundItem.Item7,
   ECommand.SelectFastSlot8 or ECommand.PressSlot8=>EBoundItem.Item8,
   ECommand.SelectFastSlot9 or ECommand.PressSlot9=>EBoundItem.Item9,
   ECommand.SelectFastSlot0 or ECommand.PressSlot0=>EBoundItem.Item10,_=>null
  };
  if(slot==null||__instance.Player.InventoryController?.FastAccess?.GetBoundItem(slot.Value)?.StringTemplateId!=Gadgets.Totem)return true;
  // The totem is passive: binding it must not send a generic barter item into
  // the native medicine/weapon use path or drop the player's current gun.
  __result=InputNode.ETranslateResult.BlockAll;return false;
 }
}
