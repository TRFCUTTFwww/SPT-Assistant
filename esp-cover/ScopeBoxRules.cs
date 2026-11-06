using System;
using System.Numerics;
namespace LocalSpt5Esp;

internal static class ScopeBoxRules {
 internal readonly record struct Stripe(float X,float Y,float Width,float Height);
 internal static bool Bounds(Vector3 feet,Vector3 head,float aspect,out Stripe box){
  box=default;
  if(!Finite(feet)||!Finite(head)||feet.Z<=0||head.Z<=0||!float.IsFinite(aspect)||aspect<=0)return false;
  float height=Math.Abs(head.Y-feet.Y),width=height*.45f/aspect,center=(head.X+feet.X)*.5f;
  if(!float.IsFinite(height)||!float.IsFinite(width)||!float.IsFinite(center)||height<.00001f)return false;
  box=new(center-width*.5f,Math.Min(head.Y,feet.Y),width,height);
  return box.X<1&&box.X+width>0&&box.Y<1&&box.Y+height>0;
 }
 // Clip each original edge, never clamp the box itself: clamping would invent
 // a vertical/horizontal border when a nearby actor extends beyond the optic.
 internal static bool Clip(Stripe source,out Stripe clipped){
  clipped=default;
  if(!float.IsFinite(source.X)||!float.IsFinite(source.Y)||!float.IsFinite(source.Width)||!float.IsFinite(source.Height)||source.Width<=0||source.Height<=0)return false;
  float x=Math.Max(0,source.X),y=Math.Max(0,source.Y),right=Math.Min(1,source.X+source.Width),top=Math.Min(1,source.Y+source.Height);
  if(right<=x||top<=y)return false;
  clipped=new(x,y,right-x,top-y);return true;
 }
 static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
}
