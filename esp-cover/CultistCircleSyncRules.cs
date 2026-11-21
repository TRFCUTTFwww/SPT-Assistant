using System;
namespace LocalSpt5Esp;
internal static class CultistCircleSyncRules {
 internal static bool CanApply(string localProfile,string requestProfile,bool inRaid,long capturedItem,long liveItem,long capturedProcess,long liveProcess,double liveDuration,long serverStart,int serverDuration,bool ready,bool inProgress)=>
  !inRaid&&!string.IsNullOrEmpty(localProfile)&&localProfile==requestProfile&&capturedItem!=0&&capturedItem==liveItem&&capturedProcess!=0&&capturedProcess==liveProcess&&double.IsFinite(liveDuration)&&liveDuration>0&&serverStart>0&&serverStart<=int.MaxValue&&serverDuration>0&&ready&&!inProgress;
}
