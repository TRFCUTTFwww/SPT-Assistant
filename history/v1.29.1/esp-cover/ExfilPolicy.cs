namespace LocalSpt5Esp;
[System.Flags]
internal enum ExfilFaction { Unknown=0, Pmc=1, Scav=2, Both=Pmc|Scav }
internal static class ExfilPolicy {
 internal static ExfilFaction Faction(bool scav,bool shared)=>shared?ExfilFaction.Both:scav?ExfilFaction.Scav:ExfilFaction.Pmc;
 internal static ExfilFaction MergeFactions(System.Collections.Generic.IEnumerable<ExfilFaction> factions){var result=ExfilFaction.Unknown;foreach(var faction in factions)result|=faction;return result;}
 internal static string FactionLabel(ExfilFaction faction)=>faction switch{ExfilFaction.Pmc=>"PMC",ExfilFaction.Scav=>"Scav",ExfilFaction.Both=>"PMC／Scav",_=>"阵营未知"};
 internal static bool SameFaction(bool playerScav,bool scavExit)=>playerScav==scavExit;
 internal readonly record struct State(bool Available,string Text);
 internal static bool CanIgnoreStatus(string status,bool eligible,bool banned)=>eligible&&!banned&&status is ("UncompleteRequirements" or "AwaitsManualActivation" or "Pending");
 internal static State Evaluate(string status,bool eligible,bool requirementsMet,bool banned,bool car,bool queued){
  if(banned)return new(false,"本局禁止撤离");
  if(!eligible)return new(false,"未分配给你");
  if(status=="NotPresent")return new(false,"未出现／已关闭");
  if(status=="Hidden")return new(false,"尚未发现");
  if(status=="AwaitsManualActivation")return new(false,"等待手动开启");
  if(status=="Pending")return new(false,"等待启动");
  if(!requirementsMet)return new(false,"条件未满足");
  if(status=="UncompleteRequirements")return new(false,"等待条件生效");
  if(status=="Countdown")return car&&!queued?new(false,"车辆倒计时／未登记"):new(true,"撤离倒计时");
  return status=="RegularMode"?new(true,"可用"):new(false,"状态未知");
 }
 internal static string Kind(string name,bool money,bool lever){
  if(money)return "黑车";
  if(lever)return "拉闸";
  string lower=name.ToLowerInvariant();
  if(lower.Contains("old gas")||lower.Contains("zb-016")||lower.Contains("zb-014")||lower.Contains("zb-012")||lower.Contains("zb-1012")||lower.Contains("rock passage")||name.Contains("老加油站")||name.Contains("岩石通道"))return "绿烟";
  return "普通";
 }
}
