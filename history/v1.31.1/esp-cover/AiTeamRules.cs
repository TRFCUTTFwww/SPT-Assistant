using System;
using System.Collections.Generic;
namespace LocalSpt5Esp;
internal static class AiTeamRules {
 internal static bool Same(long world,long from,long to,ISet<long> managed)=>world!=0&&from!=0&&from==to&&managed.Contains(from);
 internal static bool MatchesDestination(long expectedWorld,long expectedGroup,long currentWorld,long currentGroup)=>expectedWorld!=0&&expectedGroup!=0&&expectedWorld==currentWorld&&expectedGroup==currentGroup;
}
