using System;
namespace LocalSpt5Esp;
internal static class BallisticAdvantageRules {
 // Positive favors the local player. Mixed coverage never promises a safe hit.
 internal static int Evaluate(float minimum,float maximum,bool defending){
  if(!float.IsFinite(minimum)||!float.IsFinite(maximum)||minimum<0||maximum>1||minimum>maximum)return 0;
  if(defending){if(maximum>=.65f)return -1;if(maximum<=.35f)return 1;}
  else{if(minimum>=.65f)return 1;if(maximum<=.35f)return -1;}
  return 0;
 }
}
