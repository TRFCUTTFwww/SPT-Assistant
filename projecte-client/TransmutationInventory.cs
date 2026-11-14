using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
namespace ProjectE.Client;
internal sealed record InventoryAccess(Profile Profile,InventoryController Controller,bool Raid) {
 internal static InventoryAccess? Current(){
  var game=Singleton<AbstractGame>.Instance;bool raid=game?.InRaid==true&&game.TryCast<HideoutGame>()==null;
  var player=Singleton<GameWorld>.Instance?.MainPlayer;
  if(raid&&player?.IsYourPlayer==true&&player.HealthController?.IsAlive==true&&player.Profile?.Side!=EPlayerSide.Savage&&player.InventoryController!=null)return new(player.Profile!,player.InventoryController,true);
  if(raid)return null;
  var ui=ItemUiContext.Instance;
  if(ui?._profile?.Side==EPlayerSide.Savage||ui?._inventoryController==null||ui._profile==null)return null;
  return new(ui._profile,ui._inventoryController,false);
 }
 internal IEnumerable<CompoundItem> Roots(){
  var equipment=Profile.Inventory.Equipment;
  foreach(var slot in new[]{EquipmentSlot.Backpack,EquipmentSlot.TacticalVest,EquipmentSlot.Pockets}){
   var root=equipment.GetSlot(slot)?.ContainedItem?.TryCast<CompoundItem>();if(root!=null)yield return root;
  }
  if(!Raid&&Profile.Inventory.Stash!=null)yield return Profile.Inventory.Stash;
 }
 internal static IEnumerable<Item> Children(Item item){
  var c=item.TryCast<CompoundItem>();if(c!=null){
   if(c.Grids!=null)foreach(var grid in c.Grids)if(grid!=null)foreach(var child in grid.Items)if(child!=null)yield return child;
   if(c.Slots!=null)foreach(var slot in c.Slots)if(slot?.ContainedItem!=null)yield return slot.ContainedItem;
  }
  var mag=item.TryCast<Magazine>();if(mag?.Cartridges!=null)foreach(var child in mag.Cartridges.Items)if(child!=null)yield return child;
 }
 internal Item[] Items(){
  var result=new List<Item>();var seen=new HashSet<IntPtr>();
  void Visit(Item item,int depth){if(depth>12||!seen.Add(item.Pointer))return;result.Add(item);foreach(var child in Children(item))Visit(child,depth+1);}
  foreach(var root in Roots())foreach(var child in Children(root))Visit(child,0);return result.ToArray();
 }
 internal GridItemAddress? Destination(Item item){
  foreach(var root in Roots())if(root.Grids!=null)foreach(var grid in root.Grids){if(grid==null)continue;var candidate=grid.FindLocationForItem(item);if(candidate!=null&&ItemManipulator.Add(item,candidate,Controller,true).Succeeded)return candidate;}
  return null;
 }
 internal void Remove(Item item,int count){
  if(item.StackObjectsCount<count||count<1)throw new ArgumentException("物品数量已变化。");
  if(count<item.StackObjectsCount){item.StackObjectsCount-=count;item.RaiseRefreshEvent(false,true);}
  else {var result=ItemManipulator.Remove(item,Controller,false);if(!result.Succeeded)throw new ArgumentException("原生库存拒绝移除物品。");result.Value.RaiseEvents(Controller.Cast<IItemOwner>(),CommandStatus.Succeed);}
  Refresh();
 }
 internal void Add(Item item,ItemAddress destination){
  if(!ItemManipulator.Add(item,destination,Controller,true).Succeeded)throw new ArgumentException("背包空间已变化，无法取出。");
  Controller.AddAndRaiseEvents(item,destination);if(item.CurrentAddress==null)throw new InvalidOperationException("物品添加回读失败。");Refresh();
 }
 internal void Refresh(){Profile.Inventory.Equipment.RaiseRefreshEvent(false,true);if(!Raid)Profile.Inventory.Stash?.RaiseRefreshEvent(false,true);}
 internal static int Quality(Item item){
  float ratio=1;var repair=item.GetItemComponent<RepairableComponent>();if(repair!=null){float maximum=repair.TemplateDurability;if(maximum>0)ratio=Math.Min(ratio,repair.Durability/maximum);}
  var med=item.GetItemComponent<MedKitComponent>();if(med!=null&&med.MaxHpResource>0)ratio=Math.Min(ratio,med.HpResource/med.MaxHpResource);
  var food=item.GetItemComponent<FoodDrinkComponent>();if(food!=null&&food.MaxResource>0)ratio=Math.Min(ratio,food.HpPercent/food.MaxResource);
  var key=item.GetItemComponent<KeyComponent>();if(key!=null&&key.Template.MaximumNumberOfUsage>0)ratio=Math.Min(ratio,1-(float)key.NumberOfUsages/key.Template.MaximumNumberOfUsage);
  return (int)Math.Clamp(float.IsFinite(ratio)?ratio*10000:0,0,10000);
 }
}
