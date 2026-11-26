using System;
using System.Collections.Generic;
namespace LocalSpt5Esp;

// Screen-space placement, independent of Unity so dense scenes can be checked offline.
internal sealed class LabelLayout {
 internal readonly record struct Area(float X,float Y,float Width,float Height) {
  internal float Right=>X+Width;
  internal float Bottom=>Y+Height;
 }
 readonly List<Area> used=new();
 internal void Clear()=>used.Clear();
 internal bool Place(float anchorX,float anchorY,float width,float height,float screenWidth,float screenHeight,out Area result){
  const float margin=6,gap=5,top=110;
  result=default;
  if(width<=0||height<=0||width>screenWidth-2*margin||height>screenHeight-top-margin)return false;
  float maxX=screenWidth-margin-width,maxY=screenHeight-margin-height;
  float wantedX=Math.Clamp(anchorX-width/2,margin,maxX),wantedY=Math.Clamp(anchorY,top,maxY);
  Span<float> xs=stackalloc float[]{wantedX,Math.Clamp(wantedX+width+gap,margin,maxX),Math.Clamp(wantedX-width-gap,margin,maxX),margin,maxX};
  int rows=Math.Min(257,2*(int)MathF.Ceiling((maxY-top)/(height+gap))+1);
  Span<float> ys=stackalloc float[rows];ys[0]=wantedY;
  for(int i=1;i<ys.Length;i++){int step=(i+1)/2;ys[i]=Math.Clamp(wantedY+(i%2==1?step:-step)*(height+gap),top,maxY);}
  float best=float.MaxValue;bool found=false;
  foreach(float x in xs)foreach(float y in ys){
   float dx=x-wantedX,dy=y-wantedY,score=dx*dx+dy*dy;
   if(score>=best)continue;
   var candidate=new Area(x,y,width,height);bool blocked=false;
   foreach(var r in used)if(candidate.X<r.Right+gap&&candidate.Right+gap>r.X&&candidate.Y<r.Bottom+gap&&candidate.Bottom+gap>r.Y){blocked=true;break;}
   if(blocked)continue;
   result=candidate;best=score;found=true;
  }
  if(found)used.Add(result);
  return found;
 }
}
