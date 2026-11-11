using System;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class AiEditorViewport {
 internal static void Draw(Rect rect,ref Vector2 scroll,string page,bool embedded,Action<Rect> draw){
  if(rect.width<=0||rect.height<=0)return;
  var frame=AiEditorLayout.Frame(rect.width,rect.height,page,embedded);
  if(frame.Scroll){
   scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,frame.Width,frame.Height));
   try{draw(new Rect(0,0,frame.Width,frame.Height));}finally{GUI.EndScrollView();}
  }else{
   scroll=Vector2.zero;GUI.BeginGroup(rect);
   try{draw(new Rect(0,0,rect.width,rect.height));}finally{GUI.EndGroup();}
  }
 }
}
