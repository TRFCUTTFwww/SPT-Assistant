using System;
namespace LocalSpt5Esp;
internal static class QuestWaitSyncRules {
 internal static bool SameQuest(string localProfile,string serverProfile,bool inRaid,int localState,int serverState,long localStart,long serverStart)=>
  !inRaid&&!string.IsNullOrEmpty(localProfile)&&localProfile==serverProfile&&localState==serverState&&localStart==serverStart;
 internal static bool Shortens(double current,double proposed)=>double.IsFinite(current)&&double.IsFinite(proposed)&&proposed>0&&proposed<current;
}
