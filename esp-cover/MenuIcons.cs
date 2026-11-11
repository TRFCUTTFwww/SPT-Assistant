using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class MenuIcons {
 static readonly Dictionary<string,Texture2D> textures=new();
 static Texture2D Texture(string name){
  if(textures.TryGetValue(name,out var cached))return cached;
  const int size=96;var pixels=new Color32[size*size];
  foreach(var line in MenuIconGeometry.For(name)){
   float ax=line.X1*3,ay=line.Y1*3,dx=(line.X2-line.X1)*3,dy=(line.Y2-line.Y1)*3,length=dx*dx+dy*dy;
   for(int y=Math.Max(0,(int)MathF.Floor(Math.Min(ay,ay+dy)-4));y<Math.Min(size,(int)MathF.Ceiling(Math.Max(ay,ay+dy)+4));y++)
    for(int x=Math.Max(0,(int)MathF.Floor(Math.Min(ax,ax+dx)-4));x<Math.Min(size,(int)MathF.Ceiling(Math.Max(ax,ax+dx)+4));x++){
     float t=length>0?Math.Clamp(((x+.5f-ax)*dx+(y+.5f-ay)*dy)/length,0,1):0;
     float ex=x+.5f-ax-t*dx,ey=y+.5f-ay-t*dy;
     byte alpha=(byte)(Math.Clamp(3.5f-MathF.Sqrt(ex*ex+ey*ey),0,1)*255);int index=(size-1-y)*size+x;
     if(alpha>pixels[index].a)pixels[index]=new Color32(255,255,255,alpha);
    }
  }
  var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
  texture.SetPixels32(new Il2CppStructArray<Color32>(pixels));texture.Apply(false,true);textures[name]=texture;return texture;
 }
 internal static void Draw(Rect rect,string name,Color color){
  if(Event.current.type!=EventType.Repaint)return;
  var previous=GUI.color;
  try{
   GUI.color=InterfaceMenu.WindowTint(color);
   // Avoid Matrix4x4 operator interop in the repaint path (observed crash site).
   GUI.DrawTexture(rect,Texture(name));
  }finally{GUI.color=previous;}
 }
 internal static void Shutdown(){foreach(var texture in textures.Values)if(texture!=null)UnityEngine.Object.Destroy(texture);textures.Clear();}
}
