using System.Security.Cryptography;
using System.Text;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums.Hideout;
namespace LocalGearMail;
public static class CultistCircleRules {
 public static bool Eligible(Production? p)=>p?.SptIsCultistCircle==true&&p.SptIsContinuous!=true&&p.SptIsScavCase!=true&&p.Interrupted!=true&&(p.InProgress==true||p.AvailableForFinish==true)&&p.ProductionTime is >0&&p.StartTimestamp is >0&&double.IsFinite(p.Progress??0);
 // Native SPT creates these exact rewards at sacrifice start. Never generate,
 // move, clone or claim items when previewing them.
 public static Item[] Rewards(PmcData pmc){
  if(pmc.Inventory?.HideoutAreaStashes?.TryGetValue(((int)HideoutAreas.CircleOfCultists).ToString(),out var stash)!=true)return [];
  return (pmc.Inventory.Items??[]).Where(item=>item.ParentId==stash.ToString()&&item.SlotId=="CircleOfCultistsGrid1").OrderBy(item=>item.Id.ToString(),StringComparer.Ordinal).ToArray();
 }
 public static Item[] StashTree(PmcData pmc,out string stashId){
  stashId="";
  if(pmc.Inventory?.HideoutAreaStashes?.TryGetValue(((int)HideoutAreas.CircleOfCultists).ToString(),out var stash)!=true)return [];
  string rootParent=stash.ToString();stashId=rootParent;
  var all=pmc.Inventory.Items??[];
  var roots=all.Where(item=>item.ParentId==rootParent&&item.SlotId=="CircleOfCultistsGrid1").OrderBy(item=>item.Id.ToString(),StringComparer.Ordinal).ToList();
  if(roots.Count==0)return [];
  var ids=roots.Select(i=>i.Id.ToString()).ToHashSet(StringComparer.Ordinal);
  var result=new List<Item>(roots);
  bool added=true;
  while(added){
   added=false;
   foreach(var item in all){
    string id=item.Id.ToString();
    if(item.ParentId!=null&&ids.Contains(item.ParentId)&&ids.Add(id)){result.Add(item);added=true;}
   }
  }
  return result.ToArray();
 }
 public static string Revision(MongoId profile,MongoId recipe,Production production,IEnumerable<Item> rewards)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|",profile,recipe,production.StartTimestamp,production.ProductionTime,string.Join(",",rewards.Select(item=>$"{item.Id}:{item.Template}:{item.Upd?.StackObjectsCount??1}").Order(StringComparer.Ordinal))))));
 public static void RequireCurrent(string expectedId,string expectedRevision,MongoId recipe,string revision){if(expectedId!=recipe.ToString()||expectedRevision.Length==0||expectedRevision!=revision)throw new ArgumentException("仪式已经改变，请刷新预览后重试。");}
 public static bool Finish(Production production){
  if(!Eligible(production))throw new ArgumentException("当前没有可完成的仪式。");
  if(production.AvailableForFinish==true&&production.InProgress!=true)return false;
  // Cultist completion differs from ordinary crafting: native SPT clears
  // progress and InProgress, and sets AvailableForFinish. Preserve the rest.
  production.FlagCultistCircleCraftAsComplete();return true;
 }
}
