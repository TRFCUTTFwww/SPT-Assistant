using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
namespace LocalGearMail;
public record LoadoutSnapshot(string EquipmentRoot,List<Item> Items);
public static class ScavLoadoutRules {
 public static readonly HashSet<string> Slots=["FirstPrimaryWeapon","SecondPrimaryWeapon","Holster","Headwear","Earpiece","FaceCover","ArmorVest","TacticalVest","Backpack","Pockets","Eyewear"];
 public static LoadoutSnapshot Capture(BotBaseInventory inventory){
  string root=inventory.Equipment?.ToString()??throw new ArgumentException("角色没有装备根节点。");
  var source=inventory.Items??throw new ArgumentException("角色没有库存数据。");
  var selected=source.Where(item=>item.ParentId==root&&item.SlotId!=null&&Slots.Contains(item.SlotId)).Select(item=>item.Id.ToString()).ToHashSet();
  Expand(source,selected);
  var items=source.Where(item=>selected.Contains(item.Id.ToString())).ToList();
  if(items.Count==0)throw new ArgumentException("没有可复制的出击装备。");
  return new(root,items);
 }
 static void Expand(List<Item> items,HashSet<string> selected){bool changed;do{changed=false;foreach(var item in items)if(item.ParentId!=null&&selected.Contains(item.ParentId)&&selected.Add(item.Id.ToString()))changed=true;}while(changed);}
 public static void Apply(BotBaseInventory destination,LoadoutSnapshot snapshot){
  string root=destination.Equipment?.ToString()??throw new ArgumentException("Scav 没有装备根节点。");
  destination.Items??=[];
  var remove=destination.Items.Where(item=>item.ParentId==root).Select(item=>item.Id.ToString()).ToHashSet();Expand(destination.Items,remove);
  var ids=snapshot.Items.ToDictionary(item=>item.Id.ToString(),_=>new MongoId());
  var copied=snapshot.Items.Select(item=>item with{Id=ids[item.Id.ToString()],ParentId=item.ParentId==snapshot.EquipmentRoot?root:item.ParentId!=null&&ids.TryGetValue(item.ParentId,out var parent)?parent.ToString():throw new ArgumentException("装备树有孤立物品。")}).ToList();
  destination.Items.RemoveAll(item=>remove.Contains(item.Id.ToString()));destination.Items.AddRange(copied);destination.FastPanel=[];
 }
}
