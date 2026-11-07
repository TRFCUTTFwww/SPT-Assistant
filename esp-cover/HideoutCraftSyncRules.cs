using System;
namespace LocalSpt5Esp;
internal static class HideoutCraftSyncRules {
 internal static bool CanApply(string localProfile,string serverProfile,bool inRaid,bool inProgress,bool interrupted,long localStart,long serverStart,int localDuration,int serverDuration,double progress)=>
  !inRaid&&!string.IsNullOrEmpty(localProfile)&&localProfile==serverProfile&&inProgress&&!interrupted&&localStart>0&&localStart==serverStart&&localDuration>0&&localDuration==serverDuration&&double.IsFinite(progress)&&progress>=serverDuration;
}
