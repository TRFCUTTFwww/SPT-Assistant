using System;
using System.Numerics;
namespace LocalSpt5Esp;
internal static class OverlayProjectionRules {
 // Unity indexes matrices by column. TAA changes only the two lens offsets
 // (translation for orthographic cameras); reject stale FOV/depth matrices.
 internal static bool CanUseStable(ReadOnlySpan<float> rendered,ReadOnlySpan<float> stable,bool orthographic){
  if(rendered.Length!=16||stable.Length!=16||Math.Abs(stable[0])<.000001f||Math.Abs(stable[5])<.000001f)return false;
  int offset=orthographic?12:8;
  for(int i=0;i<16;i++){
   if(!float.IsFinite(rendered[i])||!float.IsFinite(stable[i]))return false;
   float difference=Math.Abs(rendered[i]-stable[i]);
   if(difference>(i==offset||i==offset+1?.1f:.0001f*Math.Max(1,Math.Abs(rendered[i]))))return false;
  }
  return true;
 }
 internal static Vector2 Pixels(float x,float y,float rectX,float rectY,float rectWidth,float rectHeight,int width,int height)
  =>new((rectX+x*rectWidth)*width,(rectY+y*rectHeight)*height);
}
