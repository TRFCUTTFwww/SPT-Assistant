using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;
public static class TraderSupplyRules {
 // Work on a detached assortment. Turning the feature off reveals untouched
 // stock and purchase counters instead of restoring stale database snapshots.
 public static TraderAssort Copy(TraderAssort source,JsonUtil json){
  var copy=json.Deserialize<TraderAssort>(json.Serialize(source))!;
  foreach(var item in copy.Items??[]){
   // Only actual offers get stock overrides; never the weapon's attachments.
   if(copy.BarterScheme?.ContainsKey(item.Id)!=true)continue;
   item.Upd??=new();item.Upd.UnlimitedCount=true;item.Upd.StackObjectsCount=int.MaxValue;
   item.Upd.BuyRestrictionMax=null;item.Upd.BuyRestrictionCurrent=null;
  }
  return copy;
 }
}
