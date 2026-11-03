using System;
using System.Numerics;
namespace LocalSpt5Esp;
internal static class TripwireGeometry {
 internal static Vector3 Nearest(Vector3 point,Vector3 from,Vector3 to){var delta=to-from;float length=delta.LengthSquared();return length<=.000001f?from:from+delta*Math.Clamp(Vector3.Dot(point-from,delta)/length,0,1);}
 internal static bool Clip(ref Vector2 from,ref Vector2 to,float width,float height){if(!float.IsFinite(from.X)||!float.IsFinite(from.Y)||!float.IsFinite(to.X)||!float.IsFinite(to.Y)||!float.IsFinite(width)||!float.IsFinite(height)||width<=0||height<=0)return false;var delta=to-from;float start=0,end=1;bool Edge(float p,float q){if(p==0)return q>=0;float t=q/p;if(p<0){if(t>end)return false;start=Math.Max(start,t);}else{if(t<start)return false;end=Math.Min(end,t);}return true;}if(!Edge(-delta.X,from.X)||!Edge(delta.X,width-from.X)||!Edge(-delta.Y,from.Y)||!Edge(delta.Y,height-from.Y))return false;var origin=from;from=origin+delta*start;to=origin+delta*end;return true;}
}
