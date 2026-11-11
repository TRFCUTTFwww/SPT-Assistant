using System;
namespace LocalSpt5Esp;
internal readonly record struct EditorPopupBounds(float X,float Y,float Width,float Height);
internal static class EditorMenuRules {
 internal static int Next(bool[] available,int selected,int direction){
  int count=available.Length;if(count==0)return -1;
  if(direction==0&&selected>=0&&selected<count&&available[selected])return selected;
  int step=direction<0?-1:1,index=selected>=0&&selected<count?selected:step>0?-1:0;
  for(int i=0;i<count;i++){index=(index+step+count)%count;if(available[index])return index;}return -1;
 }
 internal static EditorPopupBounds Popup(float x,float y,float width,float height,float screenWidth,float screenHeight){
  foreach(float value in new[]{x,y,width,height,screenWidth,screenHeight})if(!float.IsFinite(value))throw new ArgumentException("Invalid popup bounds.");
  float sw=Math.Max(1,screenWidth),sh=Math.Max(1,screenHeight),margin=Math.Min(8,Math.Min(sw,sh)/4);
  width=Math.Clamp(width,1,Math.Max(1,sw-2*margin));height=Math.Clamp(height,1,Math.Max(1,sh-2*margin));
  return new(Math.Clamp(x,margin,Math.Max(margin,sw-width-margin)),Math.Clamp(y,margin,Math.Max(margin,sh-height-margin)),width,height);
 }
}
