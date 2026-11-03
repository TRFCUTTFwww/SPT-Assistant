using System;
namespace LocalSpt5Esp;
internal static class BtrRouteRules {
 internal readonly record struct Next(string Id,string Pending,string Source);
 internal static Next Resolve(string current,string liveTarget,string requested,bool stopped,bool depot,bool liveVehicle){
  current=current?.Trim()??"";liveTarget=liveTarget?.Trim()??"";requested=requested?.Trim()??"";
  if(depot)return new("","暂未安排（车库）","depot");
  if(liveTarget.Length>0&&!liveTarget.Equals(current,StringComparison.OrdinalIgnoreCase))return new(liveTarget,"","vehicle");
  if(requested.Length>0&&!requested.Equals(current,StringComparison.OrdinalIgnoreCase))return new(requested,"","request");
  return new("",stopped?"发车后确定":liveVehicle?"路线切换中":"车辆路线数据不可用","pending");
 }
}
