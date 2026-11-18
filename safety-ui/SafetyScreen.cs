using System;
using UnityEngine;
namespace AssistantSafetyUi;

// Shared by the host and live compatibility bridge; independent of game UI
// initialization, feature resources and externally loaded icons or fonts.
internal static class SafetyScreen {
 static GUIStyle? title,heading,body,small,button,detail,eyebrow;
 static bool expanded;static Vector2 scroll,detailScroll;static float contentHeight=540;
 static string incident="",copied="";
 static readonly Color Text=new(.92f,.94f,.96f),Muted=new(.57f,.63f,.70f),Panel=new(.055f,.068f,.087f),Card=new(.075f,.091f,.115f),Line=new(.15f,.18f,.22f),Amber=new(.96f,.72f,.36f);
 static void Styles(){
  if(title!=null)return;
  GUIStyle Make(int size,TextAnchor align,bool wrap=true,bool bold=false)=>new(GUI.skin.label){fontSize=size,alignment=align,wordWrap=wrap,richText=false,fontStyle=bold?FontStyle.Bold:FontStyle.Normal};
  title=Make(30,TextAnchor.MiddleLeft,true,true);heading=Make(16,TextAnchor.UpperLeft,true,true);
  body=Make(14,TextAnchor.UpperLeft);small=Make(12,TextAnchor.UpperLeft);eyebrow=Make(11,TextAnchor.MiddleLeft,false,true);
  button=Make(14,TextAnchor.MiddleCenter,true,true);detail=Make(12,TextAnchor.UpperLeft);
 }
 static void Surface(Rect r,Color color,float radius=0){if(Event.current.type==EventType.Repaint)GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,0,radius);}
 static void Label(Rect r,string text,GUIStyle style,Color color){var previous=GUI.color;try{GUI.color=color;GUI.Label(r,text,style);}finally{GUI.color=previous;}}
 static float Height(string text,GUIStyle style,float width,float minimum=0)=>Math.Max(minimum,style.CalcHeight(new GUIContent(text),Math.Max(1,width)));
 static bool Button(Rect r,string caption,Color accent,bool primary=false,bool enabled=true,bool quiet=false){
  bool hover=enabled&&r.Contains(Event.current.mousePosition);Color fill=primary?accent:hover?Color.Lerp(Card,accent,.13f):Card;
  if(!enabled)fill=Color.Lerp(Panel,fill,.35f);
  if(!quiet||hover){Surface(r,primary?fill:Line,7);Surface(new(r.x+1,r.y+1,r.width-2,r.height-2),fill,6);}
  Color ink=primary?(accent.r*.299f+accent.g*.587f+accent.b*.114f>.55f?new Color(.025f,.065f,.075f):Color.white):quiet?Muted:Text;
  if(!enabled)ink=Color.Lerp(Panel,ink,.45f);Label(new(r.x+8,r.y+2,r.width-16,r.height-4),caption,button!,ink);
  bool previous=GUI.enabled;GUI.enabled=previous&&enabled;
  try{return GUI.Button(r,"",GUIStyle.none);}finally{GUI.enabled=previous;}
 }
 static void Rule(float x,float y,float width)=>Surface(new(x,y,width,1),Line);
 static void WarningMark(float x,float y){Surface(new(x,y,42,42),new Color(Amber.r,Amber.g,Amber.b,.1f),10);Surface(new(x+19,y+10,4,14),Amber,2);Surface(new(x+19,y+28,4,4),Amber,2);}
 static float Summary(float y,float width,SafetyPresentation view){
  float rh=Height(view.Reason,body!,width-100,30),h=rh+62;Surface(new(0,y,width,h),Card,10);WarningMark(18,y+20);
  Label(new(78,y+17,width-100,22),view.Text("问题摘要","ERROR SUMMARY"),eyebrow!,Amber);Label(new(78,y+44,width-100,rh),view.Reason,body!,Text);return y+h+16;
 }
 static float Recovery(float y,float width,SafetyPresentation view,Color accent){
  float th=Height(view.RecoveryTitle,heading!,width-60,25),rh=Height(view.RecoveryText,body!,width-60,42),h=th+rh+76;
  Color state=view.RecoveryBusy||view.CleanupUnsafe?Amber:accent;Surface(new(0,y,width,h),Card,10);
  Label(new(22,y+16,width-44,20),view.Text("存档与物品保护","PROFILE & INVENTORY"),eyebrow!,Muted);Surface(new(22,y+47,4,th+rh+10),state,2);
  Label(new(38,y+44,width-60,th),view.RecoveryTitle,heading!,Text);Label(new(38,y+th+51,width-60,rh),view.RecoveryText,body!,Muted);return y+h+16;
 }
 static void Footer(SafetyLayout layout,SafetyPresentation view,Color accent,Action<SafetyAction> act){
  float x=layout.X+layout.Padding,w=layout.Width-2*layout.Padding,y=layout.Y+layout.Height-layout.FooterHeight;Rule(x,y,w);y+=16;
  bool rollbackPrimary=view.ReloadFailure&&view.CanRollback&&!view.Simulation;
  string primary=view.Simulation?view.Text("结束测试，返回游戏","End test & return"):rollbackPrimary?view.Text("回退到正常版本","Roll back to working version"):view.Text("导出诊断日志","Export diagnostic logs");
  string secondary=rollbackPrimary||view.Simulation?view.Text("导出诊断日志","Export diagnostic logs"):view.Text("回退辅助功能","Roll back Assistant");
  float gap=10,cell=layout.Stacked?w:(w-2*gap)/3;Rect Main(int i)=>new(x+(layout.Stacked?0:i*(cell+gap)),y+(layout.Stacked?i*50:0),cell,42);bool available=!view.Working;
  if(Button(Main(0),primary,accent,true,available&&(view.Simulation||rollbackPrimary||!view.ExportBusy))){copied="";act(view.Simulation?SafetyAction.Continue:rollbackPrimary?SafetyAction.Rollback:SafetyAction.Export);}
  if(Button(Main(1),secondary,accent,enabled:available&&(rollbackPrimary||view.Simulation?!view.ExportBusy:view.CanRollback))){copied="";act(rollbackPrimary||view.Simulation?SafetyAction.Export:SafetyAction.Rollback);}
  if(Button(Main(2),view.Text("重新启动游戏","Restart game"),accent,enabled:available&&!view.RecoveryBusy))act(SafetyAction.Restart);
  y+=layout.Stacked?151:51;Label(new(x,y,w,30),copied.Length>0?copied:view.ExportNotice,small!,view.ExportFailed?Amber:Muted);y+=32;float utility=(w-3*gap)/4;
  if(Button(new(x,y,utility,32),expanded?view.Text("返回错误概览","Back to overview"):view.Text("查看技术详情","Technical details"),accent,quiet:true)){expanded=!expanded;scroll=detailScroll=Vector2.zero;}
  if(Button(new(x+utility+gap,y,utility,32),view.Text("复制技术信息","Copy details"),accent,quiet:true)){act(SafetyAction.CopyDetails);copied=view.Text("技术信息已复制。","Technical details copied.");}
  if(Button(new(x+2*(utility+gap),y,utility,32),view.Text("打开日志目录","Open log folder"),accent,enabled:view.CanOpenLogs,quiet:true))act(SafetyAction.OpenLogs);
  if(Button(new(x+3*(utility+gap),y,utility,32),view.Text("退出游戏","Exit game"),accent,enabled:available&&!view.RecoveryBusy,quiet:true))act(SafetyAction.Close);
 }
 internal static void Draw(SafetyPresentation view,Color accent,Action<SafetyAction> act){
  Styles();var matrix=GUI.matrix;var color=GUI.color;int depth=GUI.depth;bool enabled=GUI.enabled;GUI.depth=-100000;GUI.enabled=true;
  try{
   string key=view.Failure+"\n"+view.Details+"\n"+view.Simulation+"\n"+view.ReloadFailure;
   if(incident!=key){incident=key;expanded=false;scroll=detailScroll=Vector2.zero;copied="";contentHeight=540;}
   var layout=new SafetyLayout(Screen.width,Screen.height);GUI.matrix=Matrix4x4.Scale(new Vector3(layout.Scale,layout.Scale,1));GUI.color=Color.white;
   float x=layout.X,y=layout.Y,w=layout.Width,h=layout.Height,left=x+layout.Padding,inner=w-2*layout.Padding;
   Surface(new(0,0,layout.ScreenWidth,layout.ScreenHeight),new Color(.015f,.022f,.034f,.94f));Surface(new(x+4,y+10,w,h),new Color(0,0,0,.28f),15);
   Surface(new(x,y,w,h),Line,14);Surface(new(x+1,y+1,w-2,h-2),Panel,13);Surface(new(left,y+26,22,3),accent,1);
   Label(new(left+33,y+15,inner-33,24),view.Text("SPT ASSISTANT  /  运行保护","SPT ASSISTANT  /  RECOVERY CENTER"),eyebrow!,Muted);
   float bw=view.English?145:106;Surface(new(left+inner-bw,y+52,bw,28),new Color(Amber.r,Amber.g,Amber.b,.1f),14);
   Label(new(left+inner-bw+12,y+56,bw-24,20),view.Simulation?view.Text("模拟测试","CONTROLLED TEST"):view.Text("游戏已暂停","GAME PAUSED"),eyebrow!,Amber);
   float tw=layout.Stacked?inner:inner-bw-24;Label(new(left,y+(layout.Stacked?83:48),tw,45),view.Title,title!,Text);
   if(!layout.Stacked)Label(new(left,y+102,inner,34),view.Subtitle,small!,Muted);
   var viewport=new Rect(left,y+layout.HeaderHeight,inner,layout.ContentHeight-12);float cw=inner-22;
   if(expanded){
    float dh=Height(view.TechnicalDetails,detail!,cw-32,viewport.height-32);Surface(viewport,new Color(.035f,.045f,.060f),8);
    detailScroll=GUI.BeginScrollView(viewport,detailScroll,new Rect(0,0,cw,dh+32));
    try{Label(new(16,16,cw-32,dh),view.TechnicalDetails,detail!,Muted);}finally{GUI.EndScrollView();}
   }else{
    scroll=GUI.BeginScrollView(viewport,scroll,new Rect(0,0,cw,Math.Max(viewport.height,contentHeight)));
    try{
    float cy=0;if(layout.Stacked){float sh=Height(view.Subtitle,small!,cw,38);Label(new(0,cy,cw,sh),view.Subtitle,small!,Muted);cy+=sh+16;}
    cy=Summary(cy,cw,view);cy=Recovery(cy,cw,view,accent);float ah=Height(view.Advice,small!,cw-4,34);
     Label(new(2,cy,cw-4,ah),view.Advice,small!,view.CleanupUnsafe||view.RecoveryBusy?Amber:Muted);contentHeight=cy+ah+16;
    }finally{GUI.EndScrollView();}
   }
   Footer(layout,view,accent,act);if(Event.current.type is EventType.KeyDown or EventType.KeyUp or EventType.MouseDown or EventType.MouseUp or EventType.ScrollWheel)Event.current.Use();
  }finally{GUI.matrix=matrix;GUI.color=color;GUI.depth=depth;GUI.enabled=enabled;}
 }
}
