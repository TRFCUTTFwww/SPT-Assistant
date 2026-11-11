using System;
using System.Linq;
using AssistantChat;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class RaidChat {
 static readonly ChatCompletion completion=new();
 static GUIStyle? hintStyle,syntaxStyle;
 static string hintPrefix="";static ChatCommandHints.Preview hint=new("","");
 internal static Rect DefaultBounds(){float width=Math.Min(900,Screen.width-32),height=Math.Min(320,Screen.height*.42f);return new(16,Screen.height-24-height,width,height);}
 static string[] Complete(string prefix)=>RaidCommands.Complete(prefix,client?.Peers??Array.Empty<ChatPeer>());
 static void ReplaceInput(string value,int position){input=value;caret=Math.Clamp(position,0,input.Length);}
 static void ApplyCaret(TextEditor editor){editor.text=input;editor.cursorIndex=editor.selectIndex=Math.Clamp(caret,0,input.Length);}
 static void Keys(){
  var e=Event.current;if(e.type!=EventType.KeyDown)return;
  if(Time.frameCount==openedFrame){e.Use();return;}
  if(GUI.GetNameOfFocusedControl()!="AssistantChatInput")return;
  // Let the IME finish its composition before interpreting editing shortcuts.
  if(!string.IsNullOrEmpty(Input.compositionString))return;
  if(e.keyCode is KeyCode.Return or KeyCode.KeypadEnter){Submit();e.Use();return;}
  if(e.keyCode==KeyCode.Escape){SetOpen(false);e.Use();return;}
  if(e.keyCode is KeyCode.UpArrow or KeyCode.DownArrow){
   if(index==history.Count)draft=input;index=Math.Clamp(index+(e.keyCode==KeyCode.UpArrow?-1:1),0,history.Count);
   string next=index<history.Count?history[index]:draft;ReplaceInput(next,next.Length);completion.Reset();e.Use();return;
  }
  if(e.keyCode==KeyCode.Tab){var result=completion.Cycle(input,Editor().cursorIndex,e.shift,Complete);ReplaceInput(result.Text,result.Cursor);e.Use();}
 }
 internal static void Draw(){
  if(InterfaceMenu.Open||WorldEditor.ModalOpen)return;
  int oldDepth=GUI.depth;using var opacity=InterfaceMenu.BeginWindow("chat");
  try{
   GUI.depth=-9000;
   style??=new GUIStyle(GUI.skin.label){fontSize=15,wordWrap=true,richText=false};
   field??=new GUIStyle(GUI.skin.textField){fontSize=16,richText=false};
   hintStyle??=new GUIStyle(GUI.skin.label){fontSize=13,wordWrap=false,richText=false,clipping=TextClipping.Clip};
   syntaxStyle??=new GUIStyle(hintStyle){richText=true,wordWrap=true};
   var bounds=InterfaceMenu.Place("chat",DefaultBounds());
   if(Open)DrawOpen(bounds);else DrawRecent(bounds);
  }finally{GUI.depth=oldDepth;}
 }
 static void DrawOpen(Rect bounds){
  KeepCursor();Keys();if(!Open)return;
  InterfaceMenu.Surface(bounds,new Color(.02f,.025f,.04f,.9f),5);
  var inputRect=new Rect(bounds.x+8,bounds.yMax-40,bounds.width-16,30);
  if(caret>=0&&GUI.GetNameOfFocusedControl()=="AssistantChatInput")ApplyCaret(Editor());
  GUI.SetNextControlName("AssistantChatInput");input=GUI.TextField(inputRect,input,2048,field);
  if(focus){
   GUI.FocusControl("AssistantChatInput");var editor=Editor();
   // Complete Unity's focus transition now; its deferred OnFocus selects all.
   editor.OnFocus();if(caret<0)caret=input.Length;ApplyCaret(editor);focus=false;
  }else if(caret>=0&&GUI.GetNameOfFocusedControl()=="AssistantChatInput")ApplyCaret(Editor());
  caret=-1;
  int position=GUI.GetNameOfFocusedControl()=="AssistantChatInput"?Math.Clamp(Editor().cursorIndex,0,input.Length):input.Length;
  completion.Refresh(input,position,Complete);
  string prefix=input[..position];
  if(prefix!=hintPrefix){hintPrefix=prefix;hint=ChatCommandHints.For(input,position);}
  float hintHeight=DrawHints(new Rect(bounds.x+8,bounds.y+8,bounds.width-16,bounds.height-58));
  var viewport=new Rect(bounds.x+8,bounds.y+8,bounds.width-16,Math.Max(1,bounds.height-58-hintHeight));
  float content=0;foreach(var line in lines)content+=Math.Max(22,style!.CalcHeight(new GUIContent(line.Text),bounds.width-40));
  scroll=GUI.BeginScrollView(viewport,scroll,new Rect(0,0,bounds.width-38,Math.Max(viewport.height,content)));
  float y=0;foreach(var line in lines){float h=Math.Max(22,style!.CalcHeight(new GUIContent(line.Text),bounds.width-40));style.normal.textColor=line.Color;GUI.Label(new Rect(0,y,bounds.width-40,h),line.Text,style);y+=h;}GUI.EndScrollView();
  Input.imeCompositionMode=IMECompositionMode.On;Input.compositionCursorPos=new Vector2(inputRect.x+8,inputRect.y);
 }
 static float DrawHints(Rect area){
  if(!ChatCommandSyntax.IsCommand(input)||hint.Syntax.Length==0)return 0;
  float syntaxHeight=Math.Min(60,Math.Max(22,syntaxStyle!.CalcHeight(new GUIContent(hint.Syntax),area.width-12)));
  int count=Math.Min(3,completion.Candidates.Length);
  // Leave at least one history line in compact layouts; hints wrap within the chat window.
  count=Math.Min(count,Math.Max(0,(int)((area.height-30-syntaxHeight-24)/22)));
  float height=syntaxHeight+24+count*22+8,y=area.yMax-height;
  InterfaceMenu.Surface(new Rect(area.x,y,area.width,height),new Color(.04f,.06f,.085f,.92f),3);
  syntaxStyle.normal.textColor=new Color(.85f,.9f,.95f);GUI.Label(new Rect(area.x+6,y,area.width-12,syntaxHeight),hint.Syntax,syntaxStyle);y+=syntaxHeight;
  hintStyle!.normal.textColor=new Color(.47f,.85f,.78f);GUI.Label(new Rect(area.x+6,y,area.width-12,24),hint.Current,hintStyle);y+=24;
  int first=Math.Max(0,completion.Selected-count+1);
  for(int i=0;i<count;i++){
   int candidate=first+i;bool selected=candidate==completion.Selected;
   if(selected)InterfaceMenu.Surface(new Rect(area.x+3,y,area.width-6,22),new Color(.2f,.45f,.45f,.55f),2);
   string value=completion.Candidates[candidate];value=value[Math.Min(completion.Start,value.Length)..];
   string counter=i==count-1?"   ["+(candidate+1)+"/"+completion.Candidates.Length+" · Tab / Shift+Tab]":"";
   hintStyle.normal.textColor=selected?Color.white:new Color(.72f,.77f,.83f);GUI.Label(new Rect(area.x+6,y,area.width-12,22),(selected?"› ":"  ")+value+counter,hintStyle);y+=22;
  }
  return height+4;
 }
 static void DrawRecent(Rect bounds){
  float available=bounds.height,total=0;var visible=lines.Where(l=>Time.unscaledTime-l.Time<10).TakeLast(6).Reverse().TakeWhile(l=>{total+=Math.Min(80,Math.Max(22,style!.CalcHeight(new GUIContent(l.Text),bounds.width-20)));return total<=available;}).Reverse().ToArray();
  float y=bounds.yMax-visible.Sum(l=>Math.Min(80,Math.Max(22,style!.CalcHeight(new GUIContent(l.Text),bounds.width-20))));
  foreach(var line in visible){float h=Math.Min(80,Math.Max(22,style!.CalcHeight(new GUIContent(line.Text),bounds.width-20))),alpha=Math.Clamp((10-(Time.unscaledTime-line.Time))/2,0,1);InterfaceMenu.Surface(new Rect(bounds.x,y,bounds.width,h),new Color(0,0,0,.45f*alpha),1);style.normal.textColor=new Color(line.Color.r,line.Color.g,line.Color.b,alpha);GUI.Label(new Rect(bounds.x+8,y,bounds.width-16,h),line.Text,style);y+=h;}
 }
}
