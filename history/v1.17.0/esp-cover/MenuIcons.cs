using System;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class MenuIcons {
 internal static void Draw(Rect rect,string name,Color color){
  if(Event.current.type!=EventType.Repaint)return;
  var matrix=GUI.matrix;var previous=GUI.color;
  try{
   GUI.color=color;
   float sx=rect.width/32f,sy=rect.height/32f,stroke=Math.Max(1.4f,rect.width*.065f);
   foreach(var line in MenuIconGeometry.For(name)){
    float x=rect.x+line.X1*sx,y=rect.y+line.Y1*sy,dx=(line.X2-line.X1)*sx,dy=(line.Y2-line.Y1)*sy;
    GUI.matrix=matrix;
    GUIUtility.RotateAroundPivot(MathF.Atan2(dy,dx)*Mathf.Rad2Deg,new Vector2(x,y));
    GUI.DrawTexture(new Rect(x-stroke*.15f,y-stroke*.5f,MathF.Sqrt(dx*dx+dy*dy)+stroke*.3f,stroke),Texture2D.whiteTexture);
   }
  }finally{GUI.matrix=matrix;GUI.color=previous;}
 }
 internal static void Shutdown(){}
}
