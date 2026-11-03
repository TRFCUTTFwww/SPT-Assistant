using System;
using System.Numerics;
namespace LocalSpt5Esp;
internal static class EntertainmentRules {
 internal static Vector3 HeadPose(Vector3 jitter,bool down)=>down?new(Math.Clamp(80+jitter.X*.15f,65,88),jitter.Y,jitter.Z):jitter;
 internal static float MovementFactor(bool normal,float normalFactor,bool crouch,float crouchFactor,bool duck,bool prone){
  double factor=normal&&float.IsFinite(normalFactor)&&normalFactor>=1?normalFactor:1;
  if(crouch&&duck&&!prone&&float.IsFinite(crouchFactor)&&crouchFactor>=1)factor*=crouchFactor;
  return(float)Math.Min(factor,float.MaxValue);
 }
 internal static Vector3 Jitter(double seconds,float frequency,float yaw,float pitch,float roll){
  if(!double.IsFinite(seconds)||seconds<0||!float.IsFinite(frequency)||frequency<=0||!float.IsFinite(yaw)||!float.IsFinite(pitch)||!float.IsFinite(roll))return Vector3.Zero;
  // Time-sampled deterministic jitter avoids frame-rate-dependent rotation and accumulated drift.
  double tick=Math.Floor(seconds*Math.Min(frequency,60));
  if(!double.IsFinite(tick))return Vector3.Zero;
  float Wave(double seed)=>(float)(2*(Math.Sin((tick+seed)*12.9898)*43758.5453-Math.Floor(Math.Sin((tick+seed)*12.9898)*43758.5453))-1);
  return new(Wave(11)*Math.Clamp(pitch,0,80),Wave(23)*Math.Clamp(yaw,0,160),Wave(37)*Math.Clamp(roll,0,80));
 }
}
