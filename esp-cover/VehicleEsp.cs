using System;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class VehicleEsp {
 internal static void Draw(Camera camera,Vector3 center,Vector3 size,string title,string detail,Color color){
  var anchor=OverlayProjection.ScreenPoint(camera,center);if(anchor.z<=.1f)return;
  float minX=float.MaxValue,minY=float.MaxValue,maxX=float.MinValue,maxY=float.MinValue;
  for(int i=0;i<8;i++){
   var corner=center+new Vector3((i&1)==0?-size.x/2:size.x/2,(i&2)==0?-size.y/2:size.y/2,(i&4)==0?-size.z/2:size.z/2);
   var point=OverlayProjection.ScreenPoint(camera,corner);if(point.z<=.1f)continue;
   minX=Math.Min(minX,point.x);maxX=Math.Max(maxX,point.x);minY=Math.Min(minY,Screen.height-point.y);maxY=Math.Max(maxY,Screen.height-point.y);
  }
  if(minX>Screen.width||maxX<0||minY>Screen.height||maxY<0)return;
  float left=MathF.Round(Math.Clamp(minX,0,Screen.width)),top=MathF.Round(Math.Clamp(minY,0,Screen.height));
  float right=MathF.Round(Math.Clamp(maxX,0,Screen.width)),bottom=MathF.Round(Math.Clamp(maxY,0,Screen.height));
  var old=GUI.color;try{
   GUI.color=color;GUI.DrawTexture(new Rect(left,top,right-left,1),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(left,bottom,right-left,1),Texture2D.whiteTexture);
   GUI.DrawTexture(new Rect(left,top,1,bottom-top),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(right,top,1,bottom-top),Texture2D.whiteTexture);
  }finally{GUI.color=old;}
  float width=Math.Min(430,Screen.width-12),x=MathF.Round(Math.Clamp(anchor.x-width/2,6,Math.Max(6,Screen.width-width-6))),y=MathF.Round(Math.Clamp(top-49,6,Math.Max(6,Screen.height-52)));
  Shadow(new Rect(x,y,width,24),title,color,14);if(detail.Length>0)Shadow(new Rect(x,y+24,width,24),detail,Color.white,12);
 }
 static void Shadow(Rect rect,string text,Color color,int size){
  InterfaceMenu.Label(new Rect(rect.x+1,rect.y+1,rect.width,rect.height),text,Color.black,size);
  InterfaceMenu.Label(rect,text,color,size);
 }
}
