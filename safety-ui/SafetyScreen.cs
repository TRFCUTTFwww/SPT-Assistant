using System;
using UnityEngine;
namespace AssistantSafetyUi;
// Rendering must remain independent of module initialization and game cleanup.
internal static class SafetyScreen {
 static GUIStyle? title,body,small,button,detail;static bool expanded;static Vector2 scroll,detailScroll;static float contentHeight=500;static string incident="",copied="";
 static readonly Color Text=new(.91f,.94f,.97f),Muted=new(.65f,.71f,.78f),Panel=new(.065f,.085f,.12f,.98f),Card=new(.09f,.12f,.17f,.96f);
 static void Styles(){if(title!=null)return;GUIStyle Make(int size,TextAnchor align,bool wrap=false)=>new(GUI.skin.label){fontSize=size,alignment=align,wordWrap=wrap,richText=false};title=Make(24,TextAnchor.MiddleLeft,true);body=Make(15,TextAnchor.UpperLeft,true);small=Make(13,TextAnchor.UpperLeft,true);button=Make(14,TextAnchor.MiddleCenter,true);detail=Make(12,TextAnchor.UpperLeft,true);}
 static void Surface(Rect r,Color color,float radius=10){if(Event.current.type!=EventType.Repaint)return;GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,0,radius);}
 static void Label(Rect r,string text,GUIStyle style,Color color){var old=GUI.color;GUI.color=color;GUI.Label(r,text,style);GUI.color=old;}
 static float Height(string text,GUIStyle style,float width,float minimum=0)=>Math.Max(minimum,style.CalcHeight(new GUIContent(text),Math.Max(1,width)));
 static bool Button(Rect r,string caption,Color accent,bool primary=false,bool enabled=true){bool hover=r.Contains(Event.current.mousePosition);Color fill=primary?accent:Color.Lerp(Card,accent,hover?.15f:.04f);if(!enabled)fill=Color.Lerp(Panel,fill,.35f);Surface(r,Color.Lerp(fill,Color.white,.13f),9);Surface(new(r.x+1,r.y+1,r.width-2,r.height-2),fill,8);Color ink=primary?(accent.r*.299f+accent.g*.587f+accent.b*.114f>.55f?new Color(.025f,.07f,.10f):Color.white):Text;if(!enabled)ink=Color.Lerp(Panel,ink,.45f);Label(new(r.x+8,r.y+2,r.width-16,r.height-4),caption,button!,ink);bool previous=GUI.enabled;GUI.enabled=previous&&enabled;try{return GUI.Button(r,"",GUIStyle.none);}finally{GUI.enabled=previous;}}
 static float Explanation(float y,float width,string caption,string text,Color accent,float x=0){float heading=Height(caption,body!,width-40,24),textHeight=Height(text,small!,width-40,38),height=heading+textHeight+42;Surface(new(x,y,width,height),Card);Surface(new(x,y+16,3,height-32),accent,1);Label(new(x+20,y+14,width-40,heading),caption,body!,accent);Label(new(x+20,y+heading+24,width-40,textHeight),text,small!,Text);return y+height+14;}
 internal static void Draw(SafetyPresentation view,Color accent,Action<SafetyAction> act){Styles();var matrix=GUI.matrix;var color=GUI.color;int depth=GUI.depth;bool enabled=GUI.enabled;GUI.depth=-100000;GUI.enabled=true;
  try{if(incident!=view.Failure){incident=view.Failure;expanded=false;scroll=detailScroll=Vector2.zero;copied="";contentHeight=500;}
   var layout=new SafetyLayout(Screen.width,Screen.height);GUI.matrix=Matrix4x4.Scale(new Vector3(layout.Scale,layout.Scale,1));GUI.color=Color.white;
   float x=layout.X,y=layout.Y,w=layout.Width,h=layout.Height,left=x+layout.Padding,inner=w-2*layout.Padding;
   var backdrop=Color.Lerp(new Color(.015f,.025f,.045f,.94f),accent,.18f);backdrop.a=.90f;Surface(new(0,0,layout.ScreenWidth,layout.ScreenHeight),backdrop,0);Surface(new(x+4,y+8,w,h),new Color(0,0,0,.3f),18);Surface(new(x,y,w,h),Color.Lerp(Panel,accent,.20f),16);Surface(new(x+1,y+1,w-2,h-2),Panel,15);
   Surface(new(left,y+24,4,52),accent,2);
   float headingLeft=left+18,headingWidth=inner-18;Label(new(headingLeft,y+24,headingWidth,22),view.Text("SPT ASSISTANT · 错误保护","SPT ASSISTANT · ERROR PROTECTION"),small!,Muted);Label(new(headingLeft,y+50,headingWidth,layout.Stacked?64:42),view.Title,title!,Text);
   var viewport=new Rect(left,y+layout.HeaderHeight,inner,layout.ContentHeight);float cw=inner-22;
   scroll=GUI.BeginScrollView(viewport,scroll,new Rect(0,0,cw,Math.Max(viewport.height,contentHeight)));
   try{float cy=0;string badge=view.Simulation?view.Text("调试模拟 · 可安全结束测试","CONTROLLED TEST · Safe to end"):view.Text("游戏已暂停","GAME PAUSED");Label(new(0,cy,cw,26),badge,small!,view.Simulation?new Color(1,.75f,.36f):accent);cy+=34;
    float subtitleHeight=Height(view.Subtitle,body!,cw,44);Label(new(0,cy,cw,subtitleHeight),view.Subtitle,body!,Muted);cy+=subtitleHeight+18;
    if(!layout.Stacked){float col=(cw-16)/2;float a=Explanation(cy,col,view.Text("发生了什么","What happened"),view.Reason,accent);float b=Explanation(cy,col,view.RecoveryTitle,view.RecoveryText,view.RecoveryBusy?new Color(1,.75f,.36f):accent,col+16);cy=Math.Max(a,b);}else{cy=Explanation(cy,cw,view.Text("发生了什么","What happened"),view.Reason,accent);cy=Explanation(cy,cw,view.RecoveryTitle,view.RecoveryText,view.RecoveryBusy?new Color(1,.75f,.36f):accent);}
    if(Button(new(0,cy,cw,36),expanded?view.Text("收起技术信息 −","Hide technical details −"):view.Text("查看技术信息 +","Show technical details +"),accent))expanded=!expanded;cy+=48;
    if(expanded){Surface(new(0,cy,cw,180),new Color(.035f,.05f,.075f,.9f),8);var area=new Rect(12,cy+12,cw-24,156);string raw=view.TechnicalDetails;float dh=Height(raw,detail!,area.width-22,area.height);detailScroll=GUI.BeginScrollView(area,detailScroll,new Rect(0,0,area.width-22,dh));try{Label(new(0,0,area.width-22,dh),raw,detail!,Muted);}finally{GUI.EndScrollView();}cy+=194;}
    string advice=view.Advice;float ah=Height(advice,small!,cw,38);Label(new(0,cy,cw,ah),advice,small!,view.CleanupUnsafe||view.RecoveryBusy?new Color(1,.75f,.4f):Muted);contentHeight=cy+ah+14;
   }finally{GUI.EndScrollView();}
   float fy=y+h-layout.FooterHeight+12;Surface(new(left,fy-8,inner,1),new Color(.3f,.4f,.5f,.25f),0);
   bool available=!view.Working;string primary=view.Simulation?view.Text("结束测试，返回游戏","End test & return"):view.ReloadFailure?view.Text("回退到上个正常版本","Roll back to last working version"):view.Text("导出诊断日志","Export diagnostic logs");
   string secondary=view.ReloadFailure||view.Simulation?view.Text("导出诊断日志","Export diagnostic logs"):view.Text("回退辅助功能","Roll back Assistant");
   float gap=10,cell=layout.Stacked?inner:(inner-gap*2)/3;Rect ActionRect(int index)=>new(left+(layout.Stacked?0:index*(cell+gap)),fy+(layout.Stacked?index*50:0),cell,44);
   bool primaryEnabled=view.Simulation|| (view.ReloadFailure?view.CanRollback:!view.ExportBusy);
   if(Button(ActionRect(0),primary,accent,true,available&&primaryEnabled)){copied="";act(view.Simulation?SafetyAction.Continue:view.ReloadFailure?SafetyAction.Rollback:SafetyAction.Export);}
   if(Button(ActionRect(1),secondary,accent,false,available&&(view.ReloadFailure||view.Simulation?!view.ExportBusy:view.CanRollback))){copied="";act(view.ReloadFailure||view.Simulation?SafetyAction.Export:SafetyAction.Rollback);}
   if(Button(ActionRect(2),view.Text("重新启动游戏","Restart game"),accent,false,available&&!view.RecoveryBusy))act(SafetyAction.Restart);
   fy+=layout.Stacked?154:54;Label(new(left,fy,inner,38),copied.Length>0?copied:view.ExportNotice,small!,view.ExportFailed?new Color(1,.75f,.4f):Muted);fy+=42;
   float third=(inner-2*gap)/3;if(Button(new(left,fy,third,34),view.Text("复制技术信息","Copy details"),accent)){act(SafetyAction.CopyDetails);copied=view.Text("技术信息已复制。","Technical details copied.");}
   if(Button(new(left+third+gap,fy,third,34),view.Text("打开日志目录","Open log folder"),accent,false,view.CanOpenLogs))act(SafetyAction.OpenLogs);
   if(Button(new(left+2*(third+gap),fy,third,34),view.Text("退出游戏","Exit game"),accent,false,available&&!view.RecoveryBusy))act(SafetyAction.Close);
   if(Event.current.type is EventType.KeyDown or EventType.KeyUp or EventType.MouseDown or EventType.MouseUp or EventType.ScrollWheel)Event.current.Use();
  }finally{GUI.matrix=matrix;GUI.color=color;GUI.depth=depth;GUI.enabled=enabled;}
 }
}
