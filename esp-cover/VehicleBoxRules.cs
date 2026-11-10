using System;
using System.Numerics;
namespace LocalSpt5Esp;
internal static class VehicleBoxRules {
 // Perspective bounds of an eight-corner box. Clip its twelve original edges
 // at the near plane before dividing; rejecting behind-camera corners alone
 // makes nearby BTR/crate boxes jump or collapse while aiming.
 internal static bool ViewBounds(ReadOnlySpan<Vector3> corners,out ScopeBoxRules.Stripe bounds){
  bounds=default;if(corners.Length!=8)return false;
  foreach(var p in corners)if(!float.IsFinite(p.X)||!float.IsFinite(p.Y)||!float.IsFinite(p.Z))return false;
  float left=float.PositiveInfinity,right=float.NegativeInfinity,bottom=float.PositiveInfinity,top=float.NegativeInfinity;
  const float near=-.05f;
  for(int i=0;i<8;i++){
   var p=corners[i];if(p.Z<=near)Include(p,ref left,ref right,ref bottom,ref top);
   for(int axis=1;axis<=4;axis*=2){int j=i^axis;if(j<i)continue;var q=corners[j];if((p.Z<near)==(q.Z<near)||p.Z==q.Z)continue;
    var clipped=p+(q-p)*((near-p.Z)/(q.Z-p.Z));clipped.Z=near;Include(clipped,ref left,ref right,ref bottom,ref top);
   }
  }
  if(!float.IsFinite(left)||!float.IsFinite(right)||!float.IsFinite(top)||!float.IsFinite(bottom)||right<=left||top<=bottom)return false;
  bounds=new(left,bottom,right-left,top-bottom);return true;
 }
 static void Include(Vector3 p,ref float left,ref float right,ref float bottom,ref float top){float x=p.X/-p.Z,y=p.Y/-p.Z;left=Math.Min(left,x);right=Math.Max(right,x);bottom=Math.Min(bottom,y);top=Math.Max(top,y);}
}
