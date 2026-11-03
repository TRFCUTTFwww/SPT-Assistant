using System;
namespace LocalSpt5Esp;
internal enum RaidEndMode { Survived,Runner,MissingInAction,Killed }
internal static class RaidEndRules {
 internal static string Name(RaidEndMode mode)=>mode switch{RaidEndMode.Survived=>"幸存",RaidEndMode.Runner=>"匆匆撤离",RaidEndMode.MissingInAction=>"迷失",RaidEndMode.Killed=>"阵亡",_=>throw new ArgumentOutOfRangeException(nameof(mode))};
 internal static string StatusName(RaidEndMode mode){_ = Name(mode);return mode.ToString();}
 internal static string Consequence(RaidEndMode mode)=>mode switch{RaidEndMode.Survived=>"按幸存结束并带出装备与物资。",RaidEndMode.Runner=>"按匆匆撤离结束，装备与经验按游戏原有规则结算。",RaidEndMode.MissingInAction=>"按迷失结束，装备与物资可能按原版规则损失。",RaidEndMode.Killed=>"按阵亡结束，装备、保险与经验按死亡规则结算。",_=>throw new ArgumentOutOfRangeException(nameof(mode))};
}
internal sealed record RaidEndRequest(long World,long Game,string Profile,RaidEndMode Mode){
 internal bool Matches(long world,long game,string profile)=>World!=0&&Game!=0&&World==world&&Game==game&&Profile==profile;
}
