using System;
using System.Collections.Generic;
namespace LocalSpt5Esp;
internal static class BtrRouteRules {
 internal static string[] Plan(string start,string target,IEnumerable<(string From,string To)> connections){
  if(string.IsNullOrWhiteSpace(start)||string.IsNullOrWhiteSpace(target))throw new ArgumentException("BTR 站点尚未就绪。");
  if(start==target)return Array.Empty<string>();
  var graph=new Dictionary<string,List<string>>(StringComparer.Ordinal);foreach(var edge in connections){if(edge.From==edge.To)continue;if(!graph.TryGetValue(edge.From,out var next))graph[edge.From]=next=new();next.Add(edge.To);}
  var previous=new Dictionary<string,string>(StringComparer.Ordinal){{start,""}};var queue=new Queue<string>();queue.Enqueue(start);
  while(queue.Count>0){string current=queue.Dequeue();if(!graph.TryGetValue(current,out var next))continue;foreach(string point in next){if(previous.ContainsKey(point))continue;previous[point]=current;if(point==target){var path=new List<string>();for(string step=target;step!=start;step=previous[step])path.Add(step);path.Reverse();return path.ToArray();}queue.Enqueue(point);}}
  throw new ArgumentException("所选站点没有可用的 BTR 行车路线。");
 }
 internal readonly record struct Next(string Id,string Pending,string Source);
 internal static Next Resolve(string current,string liveTarget,string requested,bool stopped,bool depot,bool liveVehicle){
  current=current?.Trim()??"";liveTarget=liveTarget?.Trim()??"";requested=requested?.Trim()??"";
  if(depot)return new("","暂未安排（车库）","depot");
  if(liveTarget.Length>0&&!liveTarget.Equals(current,StringComparison.OrdinalIgnoreCase))return new(liveTarget,"","vehicle");
  if(requested.Length>0&&!requested.Equals(current,StringComparison.OrdinalIgnoreCase))return new(requested,"","request");
  return new("",stopped?"发车后确定":liveVehicle?"路线切换中":"车辆路线数据不可用","pending");
 }
}
