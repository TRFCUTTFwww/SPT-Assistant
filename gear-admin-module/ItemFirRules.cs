using SPTarkov.Server.Core.Models.Eft.Common.Tables;
namespace LocalGearMail;
public static class ItemFirRules {
 public static void Apply(BotBaseInventory inventory,string id,bool value,double? expected){
  if(expected is not (0 or 1))throw new ArgumentException("缺少原战局发现标记，请重新打开右键菜单。");
  var item=inventory.Items?.SingleOrDefault(i=>i.Id.ToString()==id)??throw new ArgumentException("该物品已不在当前角色库存中。");
  if((item.Upd?.SpawnedInSession==true)!=(expected==1))throw new ArgumentException("物品标记已改变，请刷新后重试。");
  item.Upd??=new();item.Upd.SpawnedInSession=value;
 }
}
