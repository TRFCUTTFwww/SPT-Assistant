using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace LocalGearMail;

// EFT's multi/pack offers store the first selected tree with the combined root
// stack count. Attachments and container contents are not additional sale units.
internal static class OnlineOfferItems {
 internal sealed record Selection(List<Item> Roots,List<Item> Representative,int Count);

 internal static Selection Read(List<Item> items){
  if(items.Count==0)throw new ArgumentException("物品数据为空。");
  var byId=new Dictionary<string,Item>(StringComparer.Ordinal);
  foreach(var item in items)
   if(!byId.TryAdd(item.Id.ToString(),item))throw new ArgumentException("上架物品数据含重复 ID。");
  var roots=items.Where(item=>string.IsNullOrEmpty(item.ParentId)||!byId.ContainsKey(item.ParentId)).ToList();
  if(roots.Count==0)throw new ArgumentException("上架物品树没有根物品。");
  if(roots.Any(item=>item.Template!=roots[0].Template))throw new ArgumentException("同一报价请选择同类物品。");
  var children=items.Where(item=>!string.IsNullOrEmpty(item.ParentId)).ToLookup(item=>item.ParentId!,StringComparer.Ordinal);
  var seen=new HashSet<string>(StringComparer.Ordinal);
  var representative=new List<Item>();
  long count=0;
  foreach(var root in roots){
   double stack=root.Upd?.StackObjectsCount??1;
   if(!double.IsFinite(stack)||stack<1||stack!=Math.Truncate(stack)||stack>int.MaxValue)
    throw new ArgumentException("上架物品数量无效。");
   count+=(long)stack;
   if(count>int.MaxValue)throw new ArgumentException("上架总数量超出游戏支持范围。");
   var queue=new Queue<Item>();queue.Enqueue(root);
   while(queue.Count>0){
    var item=queue.Dequeue();
    if(!seen.Add(item.Id.ToString()))throw new ArgumentException("上架物品树包含循环或重复引用。");
    if(root==roots[0])representative.Add(item);
    foreach(var child in children[item.Id.ToString()])queue.Enqueue(child);
   }
  }
  if(seen.Count!=items.Count)throw new ArgumentException("上架物品树包含断开的循环。");
  return new(roots,representative,(int)count);
 }
}
