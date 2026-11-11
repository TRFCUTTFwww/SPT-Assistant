using System;
using System.Linq;
using AssistantChat;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class RaidChat {
 static readonly ChatCompletion completion=new();
 static GUIStyle? hintStyle,syntaxStyle,captionStyle,titleStyle,candidateStyle,badgeStyle;
 static string hintPrefix="";static ChatCommandHints.Preview hint=new("","");
 static readonly Color ink=new(.91f,.94f,.96f),muted=new(.57f,.63f,.68f),accent=new(.49f,.83f,.74f),panelBorder=new(.24f,.32f,.35f,.72f);
 static void Card(Rect rect,Color fill,float radius=8){InterfaceMenu.Surface(new Rect(rect.x+1,rect.y+4,rect.width,rect.height),new Color(0,0,0,.23f),radius);InterfaceMenu.Surface(rect,panelBorder,radius);InterfaceMenu.Surface(new Rect(rect.x+1,rect.y+1,Math.Max(0,rect.width-2),Math.Max(0,rect.height-2)),fill,Math.Max(1,radius-1));}
 static void Badge(Rect rect,string text,Color color){InterfaceMenu.Surface(rect,new Color(color.r,color.g,color.b,.13f),4);Label(rect,text,badgeStyle!,color);}
 static string Text(string chinese,string english)=>I18n.English?english:chinese;
 internal static Rect DefaultBounds(){float width=Math.Min(820,Screen.width-32),height=Math.Min(440,Screen.height*.55f);return new(16,Screen.height-24-height,width,height);}
 static ChatSuggestion[] Complete(string prefix)=>RaidCommands.Complete(prefix,client?.Peers??Array.Empty<ChatPeer>());
 static void ReplaceInput(string value,int position){input=value;caret=Math.Clamp(position,0,input.Length);}
 static void ApplyCaret(TextEditor editor){editor.text=input;editor.cursorIndex=editor.selectIndex=Math.Clamp(caret,0,input.Length);}
 static void AcceptSuggestion(){var result=completion.Accept(input,Editor().cursorIndex);ReplaceInput(result.Text,result.Cursor);}
 static void Keys(){
  var e=Event.current;if(e.type!=EventType.KeyDown)return;
  if(Time.frameCount==openedFrame){e.Use();return;}
  if(GUI.GetNameOfFocusedControl()!="AssistantChatInput"||!string.IsNullOrEmpty(Input.compositionString))return;
  int position=Editor().cursorIndex;completion.Refresh(input,position,Complete);
  if(e.keyCode is KeyCode.Return or KeyCode.KeypadEnter){if(completion.Pending)AcceptSuggestion();else Submit();e.Use();return;}
  if(e.keyCode==KeyCode.Escape){SetOpen(false);e.Use();return;}
  if(e.keyCode is KeyCode.UpArrow or KeyCode.DownArrow){
   int direction=e.keyCode==KeyCode.UpArrow?-1:1;
   if(!e.control&&!e.command&&completion.MoveSelection(direction)){e.Use();return;}
   if(index==history.Count)draft=input;index=Math.Clamp(index+direction,0,history.Count);
   string next=index<history.Count?history[index]:draft;ReplaceInput(next,next.Length);completion.Reset();e.Use();return;
  }
  if(e.keyCode==KeyCode.Tab){var result=completion.Cycle(input,position,e.shift,Complete);ReplaceInput(result.Text,result.Cursor);e.Use();}
 }
 static void Styles(){
  if(style!=null)return;
  style=new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true,richText=false,padding=new RectOffset(0,0,0,0),clipping=TextClipping.Clip};
  hintStyle=new GUIStyle(style){wordWrap=false,fontSize=14};captionStyle=new GUIStyle(hintStyle){fontSize=11};titleStyle=new GUIStyle(hintStyle){fontSize=15,fontStyle=FontStyle.Bold};
  syntaxStyle=new GUIStyle(style){fontSize=13,richText=true};
  candidateStyle=new GUIStyle(style){fontSize=13,wordWrap=false};badgeStyle=new GUIStyle(captionStyle){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};
  field=new GUIStyle(GUI.skin.textField){fontSize=16,richText=false,padding=new RectOffset(0,0,5,4),border=new RectOffset(0,0,0,0)};
  field.normal.background=field.focused.background=field.hover.background=field.active.background=null;
  field.normal.textColor=field.focused.textColor=field.hover.textColor=field.active.textColor=ink;
 }
 static void Label(Rect rect,string value,GUIStyle font,Color color){font.normal.textColor=color;GUI.Label(rect,value,font);}
 internal static void Draw(){
  if(InterfaceMenu.Open||WorldEditor.ModalOpen)return;
  int oldDepth=GUI.depth;using var opacity=InterfaceMenu.BeginWindow("chat");
  try{GUI.depth=-9000;Styles();var bounds=InterfaceMenu.Place("chat",DefaultBounds());if(Open)DrawOpen(bounds);else DrawRecent(bounds);}
  finally{GUI.depth=oldDepth;}
 }
 static void DrawOpen(Rect bounds){
  KeepCursor();Keys();if(!Open)return;
  Card(bounds,new Color(.043f,.057f,.069f,.97f),10);
  InterfaceMenu.Surface(new Rect(bounds.x+1,bounds.y+1,bounds.width-2,43),new Color(.075f,.095f,.109f,.95f),9);
  Badge(new Rect(bounds.x+13,bounds.y+11,25,23),">_",accent);
  Label(new Rect(bounds.x+48,bounds.y+12,bounds.width-180,23),Text("聊天与命令","Chat & commands"),titleStyle!,ink);
  Badge(new Rect(bounds.xMax-90,bounds.y+11,76,23),"ESC",muted);
  InterfaceMenu.Surface(new Rect(bounds.x+1,bounds.y+44,bounds.width-2,1),panelBorder,0);
  var inputBox=new Rect(bounds.x+12,bounds.yMax-67,bounds.width-24,39);
  InterfaceMenu.Surface(inputBox,new Color(accent.r,accent.g,accent.b,.45f),6);
  InterfaceMenu.Surface(new Rect(inputBox.x+1,inputBox.y+1,inputBox.width-2,inputBox.height-2),new Color(.025f,.036f,.043f,.98f),5);
  InterfaceMenu.Surface(new Rect(inputBox.x,inputBox.y+8,2,inputBox.height-16),accent,1);
  Label(new Rect(inputBox.x+10,inputBox.y+7,22,26),">",titleStyle!,accent);
  var inputRect=new Rect(inputBox.x+34,inputBox.y+4,inputBox.width-94,31);
  Badge(new Rect(inputBox.xMax-51,inputBox.y+9,40,21),"↵",accent);
  if(caret>=0&&GUI.GetNameOfFocusedControl()=="AssistantChatInput")ApplyCaret(Editor());
  GUI.SetNextControlName("AssistantChatInput");input=GUI.TextField(inputRect,input,ChatCommandOptions.MaximumInput,field);
  if(focus){GUI.FocusControl("AssistantChatInput");var editor=Editor();editor.OnFocus();if(caret<0)caret=input.Length;ApplyCaret(editor);focus=false;}
  else if(caret>=0&&GUI.GetNameOfFocusedControl()=="AssistantChatInput")ApplyCaret(Editor());
  caret=-1;
  if(input.Length==0&&string.IsNullOrEmpty(Input.compositionString))Label(new Rect(inputRect.x,inputRect.y+5,inputRect.width,22),Text("输入消息，或输入 / 搜索命令…","Type a message, or / to find a command…"),hintStyle!,muted);
  int position=GUI.GetNameOfFocusedControl()=="AssistantChatInput"?Math.Clamp(Editor().cursorIndex,0,input.Length):input.Length;
  completion.Refresh(input,position,Complete);
  string prefix=input[..position];if(prefix!=hintPrefix){hintPrefix=prefix;hint=ChatCommandHints.For(input,position);}
  var body=new Rect(bounds.x+12,bounds.y+48,bounds.width-24,Math.Max(1,inputBox.y-bounds.y-56));
  float hintHeight=DrawHints(body,position);
  var viewport=new Rect(body.x,body.y,body.width,Math.Max(1,body.height-hintHeight));
  DrawMessages(viewport);
  string help=completion.Candidates.Length>0?Text("↑↓ 选择   Tab 填入   Enter 执行   Ctrl+↑↓ 历史","↑↓ Select   Tab Fill   Enter Run   Ctrl+↑↓ History"):Text("Enter 发送 / 执行   ↑↓ 历史   Esc 关闭","Enter Send / Run   ↑↓ History   Esc Close");
  if(completion.Pending)help=Text("↑↓ 选择   Tab / Enter 填入   Ctrl+↑↓ 历史","↑↓ Select   Tab / Enter Fill   Ctrl+↑↓ History");
  Label(new Rect(bounds.x+16,bounds.yMax-22,bounds.width-32,17),help,captionStyle!,muted);
  Input.imeCompositionMode=IMECompositionMode.On;Input.compositionCursorPos=new Vector2(inputRect.x,inputRect.y);
 }
 static void DrawMessages(Rect viewport){
  if(lines.Count==0){Label(new Rect(viewport.x+10,viewport.y+10,viewport.width-20,24),Text("输入 / 开始","Start with /"),titleStyle!,muted);if(viewport.height>80)Label(new Rect(viewport.x+10,viewport.y+40,viewport.width-20,40),Text("按名称查找命令与物品。执行结果会显示在这里。","Find commands and items by name. Results appear here."),style!,muted);}
  float width=Math.Max(1,viewport.width-24),content=0;
  float Height(string value)=>Math.Max(28,style!.CalcHeight(new GUIContent(value),Math.Max(1,width-26))+14);
  foreach(var line in lines)content+=Height(line.Text);
  scroll=GUI.BeginScrollView(viewport,scroll,new Rect(0,0,width,Math.Max(viewport.height,content)));
  try{float y=0;int row=0;foreach(var line in lines){float h=Height(line.Text);bool error=line.Color.g<.7f;InterfaceMenu.Surface(new Rect(1,y,width-2,h-3),error?new Color(.55f,.17f,.13f,.12f):new Color(.27f,.35f,.39f,row++%2==0?.075f:.025f),4);InterfaceMenu.Surface(new Rect(3,y+8,2,Math.Min(14,h-12)),error?new Color(.94f,.47f,.37f,.8f):new Color(accent.r,accent.g,accent.b,.36f),1);Label(new Rect(14,y+6,width-26,h-10),line.Text,style!,line.Color);y+=h;}}
  finally{GUI.EndScrollView();}
 }
 static float DrawHints(Rect area,int position){
  if(!ChatCommandSyntax.IsCommand(input)||hint.Syntax.Length==0)return 0;
  float syntaxHeight=Math.Min(54,Math.Max(20,syntaxStyle!.CalcHeight(new GUIContent(hint.Syntax),area.width-20)));
  var layout=ChatPanelLayout.Fit(area.height,syntaxHeight,completion.Candidates.Length);float height=layout.Height;
  var panel=new Rect(area.x,area.yMax-height,area.width,height);
  Card(panel,new Color(.062f,.083f,.096f,.99f),7);
  GUI.BeginGroup(panel);
  try{
   InterfaceMenu.Surface(new Rect(1,1,panel.width-2,syntaxHeight+31),new Color(.085f,.116f,.128f,.85f),6);
   Label(new Rect(12,8,panel.width-24,syntaxHeight),hint.Syntax,syntaxStyle,ink);
   Label(new Rect(10,syntaxHeight+9,panel.width-88,18),hint.Current,captionStyle!,accent);
   if(completion.Candidates.Length>0)Label(new Rect(panel.width-76,syntaxHeight+9,66,18),(completion.Selected<0?"–":(completion.Selected+1).ToString())+" / "+completion.Candidates.Length,captionStyle!,muted);
   float rowY=syntaxHeight+34;int first=Math.Clamp(completion.Selected-layout.Rows/2,0,Math.Max(0,completion.Candidates.Length-layout.Rows));
   for(int i=0;i<layout.Rows;i++){
    int candidate=first+i;bool selected=candidate==completion.Selected;var row=new Rect(5,rowY,panel.width-10,ChatPanelLayout.RowHeight);var option=completion.Candidates[candidate];
    var e=Event.current;bool hovered=row.Contains(e.mousePosition);
    InterfaceMenu.Surface(row,selected?new Color(.18f,.34f,.32f,.88f):hovered?new Color(.18f,.24f,.27f,.65f):new Color(.12f,.17f,.19f,i%2==0?.25f:.06f),4);
    if(selected)InterfaceMenu.Surface(new Rect(row.x,row.y+5,2,row.height-10),accent,1);
    Label(new Rect(15,rowY+4,panel.width-76,26),option.Label,candidateStyle!,selected?ink:new Color(.77f,.84f,.88f));
    Label(new Rect(15,rowY+31,panel.width-32,17),option.Detail,captionStyle!,selected?accent:muted);
    if(selected)Badge(new Rect(panel.width-57,rowY+7,40,21),"TAB",accent);
    if(e.type==EventType.MouseDown&&e.button==0&&hovered){completion.Select(candidate);var result=completion.Accept(input,position);ReplaceInput(result.Text,result.Cursor);focus=true;e.Use();}
    rowY+=ChatPanelLayout.RowHeight;
   }
   var ev=Event.current;if(ev.type==EventType.ScrollWheel&&new Rect(0,0,panel.width,panel.height).Contains(ev.mousePosition)){completion.MoveSelection(ev.delta.y<0?-1:1);ev.Use();}
  }finally{GUI.EndGroup();}
  return height+8;
 }
 static void DrawRecent(Rect bounds){
  float total=0;var visible=lines.Where(l=>Time.unscaledTime-l.Time<10).TakeLast(6).Reverse().TakeWhile(l=>{total+=Math.Min(80,Math.Max(24,style!.CalcHeight(new GUIContent(l.Text),bounds.width-28)+10));return total<=bounds.height;}).Reverse().ToArray();
  float y=bounds.yMax-visible.Sum(l=>Math.Min(80,Math.Max(24,style!.CalcHeight(new GUIContent(l.Text),bounds.width-28)+10)));
  foreach(var line in visible){float h=Math.Min(80,Math.Max(24,style!.CalcHeight(new GUIContent(line.Text),bounds.width-28)+10)),alpha=Math.Clamp((10-(Time.unscaledTime-line.Time))/2,0,1);InterfaceMenu.Surface(new Rect(bounds.x,y,bounds.width,h-2),new Color(.035f,.055f,.07f,.7f*alpha),5);Label(new Rect(bounds.x+12,y+4,bounds.width-24,h-8),line.Text,style,new Color(line.Color.r,line.Color.g,line.Color.b,alpha));y+=h;}
 }
}
