using System;
using System.Numerics;
namespace LocalSpt5Esp;
internal static class AimMath {
 internal static float Limit(float distance,float angle,float closeRange,float closeAngle)=>distance<=closeRange?Math.Max(angle,closeAngle):angle;
 internal static float Score(float distance,float angle,float limit,float closeRange)=>angle/Math.Max(1,limit)+(distance<=closeRange?-2+distance/Math.Max(1,closeRange)*0.25f:0);
 internal static bool TryIntercept(Vector3 delta,Vector3 velocity,Vector3 gravity,float speed,out Vector3 direction,out float flightTime){
  direction=Vector3.Zero;flightTime=0;
  if(!float.IsFinite(speed)||speed<=1||delta.LengthSquared()<1e-8f)return false;
  if(!float.IsFinite(velocity.LengthSquared())||velocity.LengthSquared()>400)velocity=Vector3.Zero;
  float Error(float t)=>(delta+velocity*t-gravity*(0.5f*t*t)).LengthSquared()-speed*speed*t*t;
  float lo=0,hi=Math.Max(0.001f,delta.Length()/speed*1.1f);
  for(int i=0;i<32&&Error(hi)>0&&hi<8;i++)hi*=1.2f;
  if(hi>10||Error(hi)>0)return false;
  for(int i=0;i<32;i++){float mid=(lo+hi)*0.5f;if(Error(mid)>0)lo=mid;else hi=mid;}
  flightTime=(lo+hi)*0.5f;direction=Vector3.Normalize(delta+velocity*flightTime-gravity*(0.5f*flightTime*flightTime));
  return float.IsFinite(direction.LengthSquared());
 }
 internal static Vector3 CorrectCloseOrigin(Vector3 origin,Vector3 camera,Vector3 head,float radius,bool insideHead){
  var delta=head-camera;float distance=delta.Length();
  if(distance<0.001f||distance>3)return origin;
  if(!insideHead&&Vector3.Dot(head-origin,delta)>0)return origin;
  var corrected=head-Vector3.Normalize(delta)*(Math.Max(0.1f,radius)+0.05f);
  return Vector3.Distance(origin,corrected)<=1.5f?corrected:origin;
 }
}
