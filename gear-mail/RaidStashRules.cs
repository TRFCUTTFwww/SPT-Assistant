using SPTarkov.Server.Core.Models.Eft.Common.Tables;
namespace LocalGearMail;
public static class RaidStashRules {
 public static List<Item> Tree(List<Item> items,string root){var selected=new HashSet<string>{root};bool changed;do{changed=false;foreach(var item in items)if(item.ParentId!=null&&selected.Contains(item.ParentId))changed|=selected.Add(item.Id.ToString());}while(changed);return items.Where(item=>selected.Contains(item.Id.ToString())).ToList();}
 public static List<Item> Merge(List<Item> current,string root,List<Item> incoming){
  if(incoming.Count==0||incoming.Select(i=>i.Id).Distinct().Count()!=incoming.Count)throw new ArgumentException("仓库物品数据不完整。");
  var original=current.Single(i=>i.Id.ToString()==root);var replacement=incoming.SingleOrDefault(i=>i.Id.ToString()==root);
  if(replacement==null||replacement.Template!=original.Template||replacement.ParentId!=original.ParentId||Tree(incoming,root).Count!=incoming.Count)throw new ArgumentException("仓库物品层级无效。");
  var ids=incoming.Select(i=>i.Id.ToString()).ToHashSet();foreach(var item in incoming.Where(i=>i.Id.ToString()!=root)){var visited=new HashSet<string>{item.Id.ToString()};var parent=item.ParentId;while(parent!=root){if(parent==null||!visited.Add(parent))throw new ArgumentException("仓库物品层级无效。");parent=incoming.Single(i=>i.Id.ToString()==parent).ParentId;}}
  var old=Tree(current,root).Select(i=>i.Id.ToString()).ToHashSet();return current.Where(i=>!old.Contains(i.Id.ToString())&&!ids.Contains(i.Id.ToString())).Concat(incoming).ToList();
 }
}
