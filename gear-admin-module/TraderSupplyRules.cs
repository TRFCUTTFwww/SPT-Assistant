using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;
public static class TraderSupplyRules {
 // Work on a detached assortment. Turning the feature off reveals untouched
 // stock and purchase counters instead of restoring stale database snapshots.
 public static TraderAssort Copy(TraderAssort source,JsonUtil json,bool unlimited=true,bool clearance=false){
  var copy=json.Deserialize<TraderAssort>(json.Serialize(source))!;
  if(clearance&&copy.BarterScheme!=null)foreach(var schemes in copy.BarterScheme.Values)foreach(var scheme in schemes)foreach(var requirement in scheme)requirement.Count=1;
  foreach(var item in unlimited?copy.Items??[]:[]){
   // Only actual offers get stock overrides; never the weapon's attachments.
   if(copy.BarterScheme?.ContainsKey(item.Id)!=true)continue;
   item.Upd??=new();item.Upd.UnlimitedCount=true;item.Upd.StackObjectsCount=int.MaxValue;
   item.Upd.BuyRestrictionMax=null;item.Upd.BuyRestrictionCurrent=null;
  }
  return copy;
 }
}
