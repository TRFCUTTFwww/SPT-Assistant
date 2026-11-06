using System;
namespace LocalSpt5Esp;
internal static class ForceLootRules {
 internal static bool InRange(double squaredDistance,double radius)=>double.IsFinite(squaredDistance)&&squaredDistance>=0&&double.IsFinite(radius)&&radius>0&&Math.Sqrt(squaredDistance)<=radius;
 internal static bool Confirmed(bool nativeSuccess,bool owned,long stackGain,int sourceBefore,int sourceAfter,bool sourceStillPresent)=>nativeSuccess&&(owned||stackGain>0&&(!sourceStillPresent||sourceAfter<sourceBefore));
}
