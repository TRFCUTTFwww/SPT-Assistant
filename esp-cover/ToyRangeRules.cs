using System;
using System.Numerics;
namespace LocalSpt5Esp;
internal static class ToyRangeRules {
 internal static bool Clip(Vector3 origin,Vector3 previous,Vector3 next,float limit,out Vector3 clipped){clipped=next;if(!float.IsFinite(limit)||limit<=0||!Finite(origin)||!Finite(previous)||!Finite(next))return false;var end=next-origin;if(end.LengthSquared()<=limit*limit)return false;var start=previous-origin;var delta=next-previous;float a=delta.LengthSquared();if(start.LengthSquared()>=limit*limit||a<.0000001f){clipped=origin+Vector3.Normalize(end)*limit;return true;}float b=2*Vector3.Dot(start,delta),c=start.LengthSquared()-limit*limit;float t=(-b+MathF.Sqrt(Math.Max(0,b*b-4*a*c)))/(2*a);clipped=previous+delta*Math.Clamp(t,0,1);return true;}
 static bool Finite(Vector3 p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z);
}
