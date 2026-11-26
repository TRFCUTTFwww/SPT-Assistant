using UnityEngine;

namespace LocalSpt5Esp;

// The popup is drawn after every panel, outside any list's scroll clip. Input is
// consumed before panels draw so a selection cannot click a control underneath.
internal static class ClickSelect {
 const float Row=30;
 static string open="",pending="";
 static string[] labels=[];
 static int result=-1,top,highlight;
 static bool seen;
 static Rect anchor,popup;
 internal static bool IsOpen=>open.Length>0;

 internal static void Begin(){
  seen=false;
  if(open.Length==0)return;
  var e=Event.current;
  if(e.type==EventType.ScrollWheel){
   if(popup.Contains(e.mousePosition))top=Math.Clamp(top+(int)Math.Sign(e.delta.y)*3,0,Math.Max(0,labels.Length-Visible));
   else Close();
   e.Use();return;
  }
  if(e.type==EventType.KeyDown){
   if(e.keyCode==KeyCode.Escape)Close();
   else if(e.keyCode is KeyCode.Return or KeyCode.KeypadEnter){pending=open;result=highlight;Close();}
   else if(e.keyCode is KeyCode.UpArrow or KeyCode.DownArrow){
    highlight=Math.Clamp(highlight+(e.keyCode==KeyCode.UpArrow?-1:1),0,Math.Max(0,labels.Length-1));
    top=Math.Clamp(highlight<top?highlight:highlight>=top+Visible?highlight-Visible+1:top,0,Math.Max(0,labels.Length-Visible));
   }
   e.Use();return;
  }
  if(e.type==EventType.MouseDown){
   if(e.button==0&&popup.Contains(e.mousePosition)){
    int index=top+(int)((e.mousePosition.y-popup.y-4)/Row);
    if(index>=top&&index<Math.Min(labels.Length,top+Visible)&&e.mousePosition.y>=popup.y+4){pending=open;result=index;}
   }
   Close();e.Use();return;
  }
  if(e.isMouse)e.Use();
 }
 static int Visible=>Math.Min(labels.Length,Math.Max(1,Math.Min(10,(int)((Screen.height-24)/Row))));
 internal static void Close(){open="";labels=[];}

 internal static int Draw(Rect rect,string id,string caption,string[] choices,int selected=-1){
  int chosen=-1;
  if(pending==id){chosen=result>=0&&result<choices.Length?result:-1;pending="";result=-1;}
  if(open==id&&choices.Length==0)Close();
  if(!GUI.enabled){if(open==id)Close();chosen=-1;}
  bool clicked=InterfaceMenu.Button(rect,"",open==id);
  InterfaceMenu.Label(new(rect.x+8,rect.y,Math.Max(1,rect.width-32),rect.height),caption,null,12);
  InterfaceMenu.Label(new(rect.xMax-24,rect.y,20,rect.height),open==id?"▴":"▾",InterfaceMenu.Accent,13,false,false);
  if(clicked&&choices.Length>0){
   if(open==id)Close();
   else{open=id;labels=choices;highlight=Math.Clamp(selected,0,choices.Length-1);top=Math.Max(0,highlight-4);GUI.FocusControl("");}
  }
  if(open==id){
   seen=true;labels=choices;
   // GUIToScreenPoint includes the current scroll view's transform. Convert to
   // the root OnGUI coordinate space, whose origin is captured by BeginFrame.
   var position=GUIUtility.GUIToScreenPoint(rect.position)-rootOrigin;
   anchor=new(position.x,position.y,rect.width,rect.height);
   float width=Math.Min(Math.Max(220,rect.width),Screen.width-16),height=Visible*Row+8;
   float y=anchor.yMax+4;
   if(y+height>Screen.height-8)y=Math.Max(8,anchor.y-height-4);
   popup=new(Math.Clamp(anchor.x,8,Math.Max(8,Screen.width-width-8)),y,width,height);
   top=Math.Clamp(top,0,Math.Max(0,labels.Length-Visible));
  }
  return chosen;
 }
 static Vector2 rootOrigin;
 internal static void BeginFrame(){rootOrigin=GUIUtility.GUIToScreenPoint(Vector2.zero);Begin();}
 internal static void EndFrame(){
  pending="";result=-1;
  if(open.Length==0)return;
  if(!seen){Close();return;}
  bool enabled=GUI.enabled;int depth=GUI.depth;GUI.enabled=true;GUI.depth=-20000;
  try{
   InterfaceMenu.Surface(new(popup.x+2,popup.y+4,popup.width,popup.height),new Color(0,0,0,.6f),7);
   var background=InterfaceMenu.Background;background.a=1;
   InterfaceMenu.Surface(popup,background,7);
   for(int i=0;i<Visible;i++){
    int index=top+i;var r=new Rect(popup.x+4,popup.y+4+i*Row,popup.width-12,Row-2);
    bool hover=r.Contains(Event.current.mousePosition);
    if(hover||index==highlight)InterfaceMenu.Surface(r,new(InterfaceMenu.Accent.r,InterfaceMenu.Accent.g,InterfaceMenu.Accent.b,.18f),4);
    InterfaceMenu.Label(new(r.x+6,r.y,r.width-12,r.height),labels[index],hover?InterfaceMenu.Accent:InterfaceMenu.Text,12);
   }
   if(labels.Length>Visible){
    float track=popup.height-8,thumb=track*Visible/labels.Length;
    InterfaceMenu.Surface(new(popup.xMax-5,popup.y+4+top*(track-thumb)/(labels.Length-Visible),3,thumb),InterfaceMenu.Accent,1);
   }
  }finally{GUI.enabled=enabled;GUI.depth=depth;}
 }
}
