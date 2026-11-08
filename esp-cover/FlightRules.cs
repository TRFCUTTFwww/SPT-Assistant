using System;
using System.Numerics;
namespace LocalSpt5Esp;
internal static class FlightRules {
 internal static Vector3 Velocity(float horizontal,float forward,float vertical,float yaw,float speed,bool fast){
  if(!float.IsFinite(horizontal)||!float.IsFinite(forward)||!float.IsFinite(vertical)||!float.IsFinite(yaw)||!float.IsFinite(speed)||speed<=0)return Vector3.Zero;
  var input=new Vector3(Math.Clamp(horizontal,-1,1),Math.Clamp(vertical,-1,1),Math.Clamp(forward,-1,1));
  if(input.LengthSquared()>1)input=Vector3.Normalize(input);
  float a=yaw*MathF.PI/180;
  return new Vector3(input.X*MathF.Cos(a)+input.Z*MathF.Sin(a),input.Y,-input.X*MathF.Sin(a)+input.Z*MathF.Cos(a))*Math.Clamp(speed,.1f,30)*(fast?3:1);
 }
 internal static float Step(float delta)=>float.IsFinite(delta)&&delta>0?Math.Min(delta,.05f):0;
}
