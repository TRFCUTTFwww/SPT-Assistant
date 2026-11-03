using System;
using System.Numerics;
namespace LocalSpt5Esp;
internal static class SoftAimRules {
 internal static float Delta(float from,float to)=>(float)(Math.IEEERemainder((double)to-from,360));
 internal static Vector2 Step(Vector2 current,Vector2 target,float degreesPerSecond,float deltaTime){if(!float.IsFinite(current.X)||!float.IsFinite(current.Y)||!float.IsFinite(target.X)||!float.IsFinite(target.Y)||!float.IsFinite(degreesPerSecond)||!float.IsFinite(deltaTime)||degreesPerSecond<=0||deltaTime<=0)return current;var difference=new Vector2(Delta(current.X,target.X),target.Y-current.Y);double length=Math.Sqrt((double)difference.X*difference.X+(double)difference.Y*difference.Y);double budget=(double)degreesPerSecond*Math.Min(deltaTime,.05f);float factor=length<=0?0:(float)Math.Min(1,budget/length);return current+difference*factor;}
}
