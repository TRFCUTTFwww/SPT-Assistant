using System;
using UnityEngine;
namespace AssistantSafetyUi;
// Shared by the permanent host and the live compatibility patch. No game-state
// access belongs in the renderer, so it also works during partial module load.
internal static class SafetyScreen {
 static GUIStyle? title,body,small,button,detail,icon;static bool expanded;static Vector2 scroll;
 static readonly Color Text=new(.91f,.94f,.97f),Muted=new(.59f,.65f,.73f),Panel=new(.065f,.085f,.12f,.98f),Card=new(.09f,.12f,.17f,.96f);
 static void Styles(){if(title!=null)return;GUIStyle Make(int size,TextAnchor align,bool wrap=false)=>new(GUI.skin.label){fontSize=size,alignment=align,wordWrap=wrap,richText=false};title=Make(27,TextAnchor.MiddleLeft);body=Make(15,TextAnchor.UpperLeft,true);small=Make(12,TextAnchor.MiddleLeft,true);button=Make(14,TextAnchor.MiddleCenter);detail=Make(12,TextAnchor.UpperLeft,true);icon=Make(62,TextAnchor.MiddleCenter);}
 static void Surface(Rect r,Color color,float radius=10){if(Event.current.type!=EventType.Repaint)return;GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,0,radius);}
 static void Label(Rect r,string text,GUIStyle style,Color color){var old=GUI.color;GUI.color=color;GUI.Label(r,text,style);GUI.color=old;}
 static bool Button(Rect r,string caption,Color accent,bool primary=false,bool enabled=true){bool hover=r.Contains(Event.current.mousePosition);Color fill=primary?accent:Color.Lerp(Card,accent,hover?.15f:.04f);if(!enabled)fill=Color.Lerp(Panel,fill,.35f);Surface(r,Color.Lerp(fill,Color.white,.13f),10);Surface(new(r.x+1,r.y+1,r.width-2,r.height-2),fill,9);Color ink=primary?(accent.r*.299f+accent.g*.587f+accent.b*.114f>.55f?new Color(.025f,.07f,.10f):Color.white):Text;if(!enabled)ink=Color.Lerp(Panel,ink,.38f);Label(r,caption,button!,ink);bool previous=GUI.enabled;GUI.enabled=previous&&enabled;try{return GUI.Button(r,"",GUIStyle.none);}finally{GUI.enabled=previous;}}
 internal static void Draw(SafetyPresentation view,Color accent,Action<SafetyAction> act){Styles();var matrix=GUI.matrix;var color=GUI.color;int depth=GUI.depth;GUI.depth=-100000;
  try{GUI.color=Color.white;float scale=Math.Min(1,Math.Min(Screen.width/1180f,Screen.height/900f));scale=Math.Max(.5f,scale);GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));float sw=Screen.width/scale,sh=Screen.height/scale,w=Math.Min(1060,sw-48),h=expanded?790:650,x=(sw-w)/2,y=Math.Max(24,(sh-h)/2);
   var backdrop=Color.Lerp(new Color(.015f,.025f,.045f,.94f),accent,.18f);backdrop.a=.88f;Surface(new(0,0,sw,sh),backdrop,0);Surface(new(x+4,y+12,w,h),new Color(0,0,0,.3f),20);Surface(new(x,y,w,h),Color.Lerp(Panel,accent,.18f),18);Surface(new(x+1,y+1,w-2,h-2),Panel,17);
   float left=x+36,inner=w-72;Surface(new(left,y+34,82,82),new Color(accent.r,accent.g,accent.b,.12f),16);Label(new(left,y+29,82,82),":(",icon!,accent);
   Label(new(left+104,y+32,inner-270,24),"SPT ASSISTANT  ·  错误保护",small!,Muted);Label(new(left+104,y+60,inner-110,40),view.Title,title!,Text);
   var badge=view.Simulation?new Color(1,.73f,.3f):accent;Surface(new(x+w-166,y+34,128,28),new Color(badge.r,badge.g,badge.b,.12f),14);Label(new(x+w-166,y+34,128,28),view.Simulation?"调试模拟":"游戏已暂停",button!,badge);
   Label(new(left,y+134,inner,44),view.Subtitle,body!,Muted);
   Surface(new(left,y+196,inner,102),Card);Surface(new(left,y+213,3,66),accent,1);Label(new(left+20,y+210,inner-40,23),"发生了什么",small!,accent);Label(new(left+20,y+241,inner-40,46),view.Reason,body!,Text);
   Surface(new(left,y+314,inner,118),Card);Label(new(left+20,y+329,inner-40,25),view.RecoveryTitle,body!,view.RecoveryBusy?new Color(1,.75f,.36f):accent);Label(new(left+20,y+363,inner-40,57),view.RecoveryText,small!,Muted);
   float content=y+450;if(Button(new(left,content,150,30),expanded?"收起技术信息  −":"查看技术信息  +",accent))expanded=!expanded;Label(new(left+166,content,inner-166,30),view.Working?"正在处理，请稍候…":view.Phase.Length>0?view.Phase:"可先导出日志，便于排查。",small!,Muted);content+=42;
   if(expanded){Surface(new(left,content,inner,128),new Color(.035f,.05f,.075f,.9f),8);var area=new Rect(left+12,content+10,inner-24,108);string raw=view.Failure+"\n\n"+view.Details;float height=Math.Max(area.height,detail!.CalcHeight(new GUIContent(raw),area.width-20));scroll=GUI.BeginScrollView(area,scroll,new Rect(0,0,area.width-20,height));try{Label(new(0,0,area.width-20,height),raw,detail!,Muted);}finally{GUI.EndScrollView();}content+=140;}
   Label(new(left,content,inner,36),view.Advice,small!,view.CleanupUnsafe?new Color(1,.75f,.4f):Muted);content+=48;
   float gap=12,cell=(inner-2*gap)/3;bool available=!view.Working;
   if(view.Simulation){if(Button(new(left,content,cell,44),"结束测试，返回游戏",accent,true,available))act(SafetyAction.Continue);if(Button(new(left+cell+gap,content,cell,44),"导出诊断日志",accent,false,available))act(SafetyAction.Export);if(Button(new(left+2*(cell+gap),content,cell,44),"重新启动游戏",accent,false,available))act(SafetyAction.Restart);}
   else{if(Button(new(left,content,cell,44),view.ReloadFailure?"回退到上个正常版本":"导出诊断日志",accent,true,available&&(!view.ReloadFailure||view.CanRollback)))act(view.ReloadFailure?SafetyAction.Rollback:SafetyAction.Export);if(Button(new(left+cell+gap,content,cell,44),view.ReloadFailure?"导出诊断日志":"回退辅助功能",accent,false,available&&(view.ReloadFailure||view.CanRollback)))act(view.ReloadFailure?SafetyAction.Export:SafetyAction.Rollback);if(Button(new(left+2*(cell+gap),content,cell,44),"重新启动游戏",accent,false,available&&!view.RecoveryBusy))act(SafetyAction.Restart);}
   float bottom=y+h-46;string status=view.ExportStatus.StartsWith("日志已导出",StringComparison.Ordinal)?"日志已导出，可打开日志目录查看。":view.ExportStatus.StartsWith("正在",StringComparison.Ordinal)?"正在整理诊断日志…":view.ExportStatus.Length>0?view.ExportStatus:"日志导出不会包含完整存档或认证配置。";Label(new(left,bottom,inner-300,30),status,small!,Muted);
   if(view.CanOpenLogs&&Button(new(x+w-304,bottom,140,30),"打开日志目录",accent))act(SafetyAction.OpenLogs);if(Button(new(x+w-152,bottom,116,30),"退出游戏",accent,false,available&&!view.RecoveryBusy))act(SafetyAction.Close);
   if(Event.current.type is EventType.KeyDown or EventType.KeyUp or EventType.MouseDown or EventType.MouseUp or EventType.ScrollWheel)Event.current.Use();
  }finally{GUI.matrix=matrix;GUI.color=color;GUI.depth=depth;}
 }
}
