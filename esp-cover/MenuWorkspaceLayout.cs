using System;
namespace LocalSpt5Esp;
internal static class MenuWorkspaceLayout {
 internal static int Columns(float width,float minimum=112,float gap=8)=>Math.Max(1,(int)((width+gap)/(minimum+gap)));
 internal static int Rows(int count,float width,float minimum=112)=>count==0?0:(int)Math.Ceiling(count/(double)Columns(width,minimum));
 internal static float Height(int count,float width,float minimum=112)=>Rows(count,width,minimum)*40;
 internal static (float X,float Y,float Width,float Height) FitBackground(float width,float height,int imageWidth,int imageHeight,bool centerHorizontally=false){
  // Keep the height of the original raid artwork, regardless of the image aspect ratio.
  const float referenceAspect=1325f/1187f;
  float referenceHeight=Math.Min(height,width*.68f/referenceAspect);
  float scale=Math.Min(width/imageWidth,referenceHeight/imageHeight);
  float fittedWidth=imageWidth*scale,fittedHeight=imageHeight*scale;
  float x=(width-fittedWidth)*(centerHorizontally ? .5f : 1f);
  return (x,height-fittedHeight,fittedWidth,fittedHeight);
 }
}
