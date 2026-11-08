using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
namespace LocalGearMail;
public static class ScavSecureRules {
 public const string Alpha="544a11ac4bdc2d470e8b456a";
 public static HashSet<string> Protected(BotBaseInventory inventory){
  var selected=(inventory.Items??[]).Where(i=>i.ParentId==inventory.Equipment?.ToString()&&i.SlotId=="SecuredContainer").Select(i=>i.Id.ToString()).ToHashSet();
  bool added;do{added=false;foreach(var item in inventory.Items??[])if(item.ParentId!=null&&selected.Contains(item.ParentId)&&selected.Add(item.Id.ToString()))added=true;}while(added);return selected;
 }
 public static List<Item> Capture(BotBaseInventory inventory){var ids=Protected(inventory);return (inventory.Items??[]).Where(i=>ids.Contains(i.Id.ToString())).ToList();}
 public static List<Item> Select(List<Item> stored,string template){
  if(stored.Count==0)return [new(){Id=new MongoId(),Template=new MongoId(template),SlotId="SecuredContainer"}];
  var root=stored.Single(i=>i.SlotId=="SecuredContainer");
  if(root.Template.ToString()==template)return stored;
  if(stored.Count>1)throw new ArgumentException("请先清空 Scav 安全箱，再更换箱型；原箱和内容物已保留。");
  return [root with{Template=new MongoId(template)}];
 }
 public static void Attach(BotBaseInventory inventory,List<Item> stored,string template=Alpha){
  if(inventory.Equipment==null)throw new ArgumentException("Scav equipment root missing");
  stored=Select(stored,template);
  if(stored.Count(i=>i.SlotId=="SecuredContainer")!=1)throw new ArgumentException("Invalid Scav secure container tree");
  var ids=stored.ToDictionary(i=>i.Id.ToString(),_=>new MongoId());
  var copied=stored.Select(i=>i with{Id=ids[i.Id.ToString()],ParentId=i.SlotId=="SecuredContainer"?inventory.Equipment.ToString():i.ParentId!=null&&ids.TryGetValue(i.ParentId,out var p)?p.ToString():throw new ArgumentException("Orphaned Scav secure item")}).ToList();
  inventory.Items??=[];var old=Protected(inventory);inventory.Items.RemoveAll(i=>old.Contains(i.Id.ToString()));inventory.Items.AddRange(copied);
 }
 public static IEnumerable<Item> Sellable(BotBaseInventory inventory,IEnumerable<Item> candidates){var ids=Protected(inventory);return candidates.Where(i=>!ids.Contains(i.Id.ToString()));}
}
