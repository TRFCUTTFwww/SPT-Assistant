using System;
namespace LocalSpt5Esp;
internal static class HideoutCraftSyncRules {
 internal static bool CanApplyLive(string localProfile,string serverProfile,bool inRaid,long requestedItem,long currentItem,long requestedProcess,long currentProcess,double localDuration,long serverStart,int serverDuration,double progress)=>
  !inRaid&&!string.IsNullOrEmpty(localProfile)&&localProfile==serverProfile&&requestedItem!=0&&requestedItem==currentItem&&requestedProcess!=0&&requestedProcess==currentProcess&&double.IsFinite(localDuration)&&localDuration>0&&localDuration==serverDuration&&serverStart>0&&serverStart<=int.MaxValue&&double.IsFinite(progress)&&progress>=serverDuration;
 internal static bool CanApply(string localProfile,string serverProfile,bool inRaid,bool inProgress,bool interrupted,long localStart,long serverStart,int localDuration,int serverDuration,double progress)=>
  !inRaid&&!string.IsNullOrEmpty(localProfile)&&localProfile==serverProfile&&inProgress&&!interrupted&&localStart>0&&localStart==serverStart&&localDuration>0&&localDuration==serverDuration&&double.IsFinite(progress)&&progress>=serverDuration;
}
