using EFT;
using EFT.InventoryLogic;
namespace ProjectE.Client;
internal static class CarriedInventory {
 internal static Item[] Items(Player player){
  var items=new List<Item>();var seen=new HashSet<IntPtr>();
  void Visit(Item item,int depth){if(depth>20||!seen.Add(item.Pointer))return;items.Add(item);foreach(var child in InventoryAccess.Children(item))Visit(child,depth+1);}
  var root=player.Profile?.Inventory?.Equipment;if(root!=null)Visit(root,0);
  return items.ToArray();
 }
}
