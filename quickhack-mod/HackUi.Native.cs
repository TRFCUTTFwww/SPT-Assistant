using UnityEngine;

namespace LocalQuickhacks;

internal static partial class HackUi {
    // Yellow is from the original hacking_minigame_colors.inkstyle.
    static readonly Color Red=new(1,.29f,.32f), Yellow=new(.875f,1,.369f), Cyan=new(.42f,.91f,.94f),
        Dim=new(.36f,.48f,.46f), Ink=new(.025f,.045f,.043f,.96f);
    static GUIStyle? style;
    static Font? body,mono,latin;
    static float scale,ox,oy,opacity=1,nextHover;
    static Rect canvas;
    static string hovered="";
    static BreachPuzzle? shownPuzzle;
    static int focusRow,focusCol,shownPicks;
    static float pickedAt=-100;
    static HackKind lastSelected;
    static float selectedAt;
    static Vector2 lastMouse=new(-9999,-9999);
    static float Now=>Time.unscaledTime;
    static float Pulse=>.6f+.4f*Mathf.Sin(Now*4);
    static Color Alpha(Color c,float a)=>new(c.r,c.g,c.b,c.a*a);

    internal static void Draw() {
        if(Runtime.Mode==UiMode.None&&!Runtime.Uploading&&Effects.Markers.Count==0) return;
        var saved=GUI.matrix; var color=GUI.color; int depth=GUI.depth;
        try {
            GUI.depth=-500;
            scale=Math.Min(Screen.width/1920f,Screen.height/1080f);
            ox=(Screen.width-1920*scale)/2; oy=(Screen.height-1080*scale)/2;
            GUI.matrix=Matrix4x4.TRS(new Vector3(ox,oy,0),Quaternion.identity,new Vector3(scale,scale,1));
            canvas=new Rect(-ox/scale,-oy/scale,Screen.width/scale,Screen.height/scale);
            EnsureFonts();
            opacity=Runtime.Mode==UiMode.None?1:Math.Clamp((Now-Runtime.ModeStarted)/.22f,0,1);
            if(Runtime.Mode==UiMode.Breach) Breach();
            else if(Runtime.Mode is UiMode.Scanner or UiMode.Preview) Scanner(Runtime.Mode==UiMode.Preview);
            TargetStatuses();
        } catch(Exception e) { Runtime.Warn("Quickhack UI",e); }
        finally { GUI.matrix=saved; GUI.color=color; GUI.depth=depth; opacity=1; }
    }
    static void EnsureFonts() {
        if(style!=null) return;
        body=Font.CreateDynamicFontFromOSFont("Microsoft YaHei UI",20);
        mono=Font.CreateDynamicFontFromOSFont("Consolas",24);
        latin=Font.CreateDynamicFontFromOSFont("Bahnschrift",24);
        style=new GUIStyle(GUI.skin.label) { richText=false,wordWrap=false,clipping=TextClipping.Clip };
        style.padding=new RectOffset(0,0,0,0);
    }
    internal static void Clear() {
        foreach(var font in new[]{body,mono,latin}) if(font!=null) UnityEngine.Object.Destroy(font);
        body=mono=latin=null; style=null; shownPuzzle=null; hovered=""; lastMouse=new(-9999,-9999);
    }
    static void Fill(Rect r,Color c) { GUI.color=Alpha(c,opacity); GUI.DrawTexture(r,Texture2D.whiteTexture); GUI.color=Color.white; }
    static void Text(Rect r,string value,Color c,int size=20,int face=0,TextAnchor align=TextAnchor.UpperLeft) {
        style!.font=face==1?mono:face==2?latin:body; style.fontSize=size;
        style.normal.textColor=Alpha(c,opacity); style.alignment=align; GUI.color=Color.white;
        GUI.Label(r,value,style);
    }
    static void Image(Rect r,string key,Color color,bool stretch=false) {
        var texture=Assets.Texture(key); if(texture==null) return;
        GUI.color=Alpha(color,opacity); GUI.DrawTexture(r,texture,stretch?ScaleMode.StretchToFill:ScaleMode.ScaleToFit,true); GUI.color=Color.white;
    }
    static void Line(float x,float y,float x2,float y2,Color c,float width=1) {
        var matrix=GUI.matrix; float dx=x2-x,dy=y2-y;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(dy,dx)*Mathf.Rad2Deg,new Vector2(x,y));
        Fill(new Rect(x,y,Mathf.Sqrt(dx*dx+dy*dy),width),c); GUI.matrix=matrix;
    }
    static void Frame(Rect r,Color c,float w=1) {
        Fill(new Rect(r.x,r.y,r.width,w),c); Fill(new Rect(r.x,r.yMax-w,r.width,w),c);
        Fill(new Rect(r.x,r.y,w,r.height),c); Fill(new Rect(r.xMax-w,r.y,w,r.height),c);
    }
    static void CutPanel(Rect r,Color accent) {
        const float cut=14;
        Fill(new Rect(r.x,r.y,r.width,r.height-cut),Ink); Fill(new Rect(r.x,r.yMax-cut,r.width-cut,cut),Ink);
        Line(r.x,r.y,r.xMax,r.y,accent); Line(r.x,r.y,r.x,r.yMax,Alpha(accent,.35f));
        Line(r.x,r.yMax,r.xMax-cut,r.yMax,Alpha(accent,.35f));
        Line(r.xMax-cut,r.yMax,r.xMax,r.yMax-cut,accent); Line(r.xMax,r.y,r.xMax,r.yMax-cut,Alpha(accent,.35f));
    }
    static bool Click(Rect r,string id,bool sound=true) {
        bool over=r.Contains(Event.current.mousePosition);
        if(over&&Event.current.type==EventType.Repaint&&hovered!=id) {
            hovered=id; if(sound&&Now>=nextHover) { nextHover=Now+.07f; Assets.Play("hover"); }
        }
        if(!over||Event.current.type!=EventType.MouseDown||Event.current.button!=0) return false;
        Event.current.Use(); return true;
    }
    static bool Button(Rect r,string key,string label,Color c,string id) {
        bool over=r.Contains(Event.current.mousePosition);
        Fill(r,Alpha(c,over?.16f:.04f)); Line(r.x,r.yMax,r.xMax,r.yMax,Alpha(c,over?1:.4f));
        Frame(new Rect(r.x+11,r.y+9,40,r.height-18),c);
        Text(new Rect(r.x+11,r.y+9,40,r.height-18),key,c,17,1,TextAnchor.MiddleCenter);
        Text(new Rect(r.x+64,r.y,r.width-70,r.height),label,c,18,0,TextAnchor.MiddleLeft);
        if(!Click(r,id)) return false; Assets.Play("press"); return true;
    }
    static void Corners(Rect r,Color c,float len=27) {
        foreach(float x in new[]{r.x,r.xMax}) foreach(float y in new[]{r.y,r.yMax}) {
            Line(x,y,x+(x==r.x?len:-len),y,c,2); Line(x,y,x,y+(y==r.y?len:-len),c,2);
        }
    }
    // Measured against the user's 1920x1080 CP2077 recording (6s and 18s).
    static bool showTargetData=true;
    static readonly Color ScanCyan=new(.369f,.965f,1), ScanRed=new(1,.31f,.29f), ScanPanel=new(.07f,.025f,.06f,.9f);
    static void ScanSurface(bool preview) {
        Fill(canvas,new Color(.015f,.06f,.04f,preview?.96f:.14f));
        for(int i=0;i<24;i++) {
            float a=(24-i)/24f;
            Fill(new Rect(canvas.x+i*7,canvas.y,7,canvas.height),new Color(.055f,.015f,.025f,a*.13f));
            Fill(new Rect(canvas.xMax-(i+1)*7,canvas.y,7,canvas.height),new Color(.055f,.015f,.025f,a*.13f));
        }
        for(int y=1;y<1080;y+=4) Fill(new Rect(canvas.x,y,canvas.width,1),new Color(.1f,.25f,.2f,.025f));
        Image(new Rect(65,47,172,32),"scan_fluff_corner_text",Alpha(ScanRed,.3f));
        Image(new Rect(1632,55,187,32),"scan_fluff_top_right",Alpha(ScanRed,.3f));
        Image(new Rect(35,483,12,139),"scan_fluff_left",Alpha(ScanRed,.45f));
        Image(new Rect(1510,1010,305,29),"scan_fluff_bottom",Alpha(ScanRed,.25f));
        Line(170,947,1750,947,Alpha(ScanRed,.12f));
        if(preview) Text(new Rect(770,979,380,25),"F10  /  施放预览",Alpha(ScanCyan,.5f),14,0,TextAnchor.MiddleCenter);
    }
    static void Scanner(bool preview) {
        ScanSurface(preview);
        Text(new Rect(746,110,500,29),$"网络接入仓 RAM: {Runtime.Ram:0}/24",Alpha(ScanRed,.72f),22);
        const float cell=14,gap=4;
        const float ramLeft=746;
        Line(720,140,1230,140,Alpha(ScanRed,.3f));
        for(int i=0;i<24;i++) {
            var r=new Rect(ramLeft+i*(cell+gap),150,cell,48);
            // The atlas bg is the solid shape; fg is its outline.
            bool available=i<Runtime.Ram,reserved=i<HackRules.Cost(Runtime.Selected);
            if(available) Image(r,"qh_memory_small_bg",Alpha(ScanCyan,reserved?.32f:1),true);
            Image(r,"qh_memory_small_fg",available?Alpha(ScanCyan,.7f):Alpha(ScanRed,.45f),true);
        }
        if(Runtime.Ram<HackRules.Cost(Runtime.Selected)) {
            Fill(new Rect(746,207,428,30),Alpha(ScanRed,.16f));
            Text(new Rect(754,207,415,29),"可用 RAM 不足",ScanRed,21);
        }
        var actor=Runtime.Target?.Resolve();
        if(preview) Hologram(); else if(actor!=null) TargetBracket(actor);
        ScannerReticle();
        const float x=194,w=430,top=373;
        Text(new Rect(x-16,top-71,340,17),"CYBERDECK  V  S53 332",Alpha(ScanRed,.55f),11,2);
        Text(new Rect(x+4,top-57,400,29),"可用快速破解:",ScanCyan,23);
        Line(x,top-18,x+w-26,top-18,Alpha(ScanRed,.8f));
        if(lastSelected!=Runtime.Selected) { lastSelected=Runtime.Selected; selectedAt=Now; }
        for(int i=0;i<HackRules.All.Length;i++) {
            var kind=HackRules.All[i]; bool selected=Runtime.Selected==kind;
            float y=top+i*64,reveal=Math.Clamp((Now-Runtime.ModeStarted-i*.025f)/.18f,0,1);
            // The selected row steps to the right in the reference.
            float offset=selected?22:i%3==2?-7:0;
            var row=new Rect(x+offset+(1-reveal)*15,y,w-(selected?22:0),55);
            bool over=row.Contains(Event.current.mousePosition);
            bool enough=Runtime.Ram>=HackRules.Cost(kind);
            Color accent=enough?ScanCyan:ScanRed;
            Color c=Alpha(accent,selected?1:over?.9f:.73f);
            Fill(new Rect(row.x+3,row.y+2,row.width-8,row.height-4),enough?new Color(.015f,.045f,.06f,.76f):new Color(.24f,.025f,.035f,.72f));
            QuickhackRowFrame(row,Alpha(c,selected?1:.48f),selected?2:1);
            if(selected) {
                for(int glow=1;glow<=3;glow++) QuickhackRowFrame(new Rect(row.x-glow*2,row.y-glow,row.width+glow*4,row.height+glow*2),Alpha(c,.09f/glow),2);
                Image(row,"qh_selection_glow",Alpha(accent,.26f),true);
                float flash=Math.Clamp(1-(Now-selectedAt)/.16f,0,1);
                Fill(row,Alpha(accent,flash*.15f));
            }
            Text(new Rect(row.x+23,y+1,row.width-117,28),HackRules.Name(kind),c,23);
            string state=Runtime.Uploading&&Runtime.UploadKind==kind?"正在上传":enough?"就绪":"可用 RAM 不足";
            float stateWidth=state.Length*14+8;
            Frame(new Rect(row.x+24,y+32,stateWidth,17),Alpha(c,.6f));
            Text(new Rect(row.x+28,y+32,stateWidth-4,18),state,c,13);
            Text(new Rect(row.x+34+stateWidth,y+32,140,18),HackRules.Category(kind),Alpha(c,.65f),13);
            Text(new Rect(row.xMax-105,y+7,42,38),HackRules.Cost(kind).ToString(),c,30,2,TextAnchor.MiddleRight);
            Image(new Rect(row.xMax-59,y+17,6,23),"qh_memory_small_fg",c,true);
            Image(new Rect(row.xMax-47,y+7,43,43),"qh_frame_qh_fg",c,true);
            Image(new Rect(row.xMax-42,y+12,32,32),HackRules.Icon(kind),c);
            if(Runtime.Uploading&&Runtime.UploadKind==kind) Fill(new Rect(row.x+3,y+52,(row.width-8)*Runtime.UploadProgress,2),c);
            if(Click(row,"hack"+i)) { Runtime.Selected=kind; showTargetData=false; Assets.Play("press"); }
        }
        if(Click(new Rect(x,top+384,w,35),"execute")) { if(preview) Runtime.PreviewCast(); else Runtime.Upload(); }
        Text(new Rect(x+8,top+385,w-16,29),"F  执行",Alpha(ScanCyan,.7f),17);
        if(preview&&Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.F) { Runtime.PreviewCast(); Event.current.Use(); }
        ScanDetails(preview,actor);
        // Ordinary scanner text has no centre-screen upload card in the source HUD.
        if(!string.IsNullOrEmpty(Runtime.Status)) Text(new Rect(690,877,540,44),Runtime.Status,Alpha(ScanCyan,.7f),15,0,TextAnchor.MiddleCenter);
        if(preview) {
            if(Button(new Rect(1380,864,425,42),"01","入侵协议 · 门禁授权",Yellow,"breach-preview")) Runtime.OpenPreviewBreach();
            if(Button(new Rect(1555,964,250,40),"ESC","退出预览",ScanRed,"close-scanner")) Runtime.Close();
        }
    }
    static void QuickhackRowFrame(Rect r,Color c,float width=1) {
        Vector2[] points=[new(r.x,r.y),new(r.xMax-9,r.y),new(r.xMax,r.y+9),new(r.xMax,r.yMax),new(r.x+5,r.yMax),new(r.x+5,r.y+29),new(r.x,r.y+24),new(r.x,r.y)];
        for(int i=1;i<points.Length;i++) Line(points[i-1].x,points[i-1].y,points[i].x,points[i].y,c,width);
    }
    static void ScanDetails(bool preview,EFT.Player? actor) {
        const float x=1390,y=342,w=414,h=487;
        Fill(new Rect(x,y,w,h-11),ScanPanel);
        Fill(new Rect(x,y+h-11,w-11,11),ScanPanel);
        Line(x,y,x,y+h,Alpha(ScanCyan,.6f)); Line(x,y+h,x+w-11,y+h,Alpha(ScanCyan,.6f));
        Line(x+w-11,y+h,x+w,y+h-11,Alpha(ScanCyan,.6f)); Line(x+w,y,x+w,y+h-11,Alpha(ScanCyan,.6f));
        for(int i=0;i<2;i++) {
            float tabX=x+i*207; bool active=i==0?showTargetData:!showTargetData;
            Fill(new Rect(tabX,y-46,207,46),active?ScanPanel:Alpha(ScanRed,.19f));
            Line(tabX,y-46,tabX+198,y-46,Alpha(active?ScanCyan:ScanRed,.65f));
            Line(tabX+198,y-46,tabX+207,y-37,Alpha(active?ScanCyan:ScanRed,.65f));
            if(!active) Line(tabX,y,tabX+207,y,Alpha(ScanCyan,.65f));
        }
        Text(new Rect(x+14,y-36,170,30),"数据",showTargetData?ScanCyan:ScanRed,22);
        Text(new Rect(x+221,y-36,180,30),"破解",showTargetData?ScanRed:ScanCyan,22);
        if(Click(new Rect(x,y-46,207,46),"data-tab")) showTargetData=true;
        if(Click(new Rect(x+207,y-46,207,46),"hacks-tab")) showTargetData=false;
        if(showTargetData) {
            Text(new Rect(x+20,y+18,w-40,22),"扫描结果",ScanRed,15);
            Text(new Rect(x+20,y+52,w-40,67),preview?"训练目标":actor?.Profile?.Nickname??"未识别目标",Yellow,27);
            Text(new Rect(x+20,y+130,w-40,25),"人物资料",ScanRed,15);
            Text(new Rect(x+20,y+170,w-40,27),preview?"模拟链路":actor!=null?"本地 AI":"等待扫描",ScanCyan,20);
            Text(new Rect(x+20,y+224,w-40,23),"距离",ScanRed,15);
            Text(new Rect(x+20,y+261,w-40,29),preview?"—":actor!=null?Vector3.Distance(actor.Position,Runtime.Camera!.transform.position).ToString("F1")+" m":"—",ScanCyan,23,2);
        } else {
            Text(new Rect(x+20,y+18,w-70,34),HackRules.Name(Runtime.Selected),ScanRed,23);
            Image(new Rect(x+w-58,y+18,33,33),HackRules.Icon(Runtime.Selected),ScanRed);
            Text(new Rect(x+20,y+68,w-40,22),HackRules.Category(Runtime.Selected)+"快速破解",Alpha(ScanRed,.65f),14);
            Text(new Rect(x+20,y+112,w-40,25),Runtime.Selected==HackKind.BlackwallGateway?"传播上限：5 个目标":$"持续时间：{HackRules.Duration(Runtime.Selected):0} 秒",new Color(.78f,.76f,.76f),18);
            Text(new Rect(x+20,y+143,w-40,25),$"上传时间：{HackRules.Upload(Runtime.Selected):0.0} 秒",new Color(.78f,.76f,.76f),18);
            Line(x+20,y+183,x+w-20,y+183,Alpha(ScanRed,.25f));
            Text(new Rect(x+20,y+205,w-40,92),HackRules.Description(Runtime.Selected),ScanRed,18);
            Line(x+20,y+326,x+w-20,y+326,Alpha(ScanRed,.2f));
            Text(new Rect(x+20,y+344,w-40,32),$"{HackRules.Cost(Runtime.Selected)}  RAM 消耗",ScanCyan,19);
        }
        Image(new Rect(x,y+h+8,210,18),"scan_fluff_bottom",Alpha(ScanCyan,.32f));
    }
    static void ScannerReticle() {
        const float cx=960,cy=540;
        for(int side=-1;side<=1;side+=2) {
            float x=cx+side*249;
            for(int i=0;i<12;i++) { float yy=cy-78+i*14; Line(x,yy,x-side*10,yy+4,Alpha(ScanRed,.58f)); }
            Image(new Rect(x-8,cy-7,16,14),"scan_inner_crosshair",Alpha(ScanRed,.55f),true);
            for(int i=0;i<5;i++) Frame(new Rect(cx-185+i*93-7,cy+side*108-7,14,14),Alpha(ScanRed,.5f));
        }
        Text(new Rect(672,526,45,25),"1x",ScanRed,19,2);
        var actor=Runtime.Target?.Resolve();
        if(actor!=null&&Runtime.Camera!=null) Text(new Rect(1222,532,90,25),Vector3.Distance(actor.Position,Runtime.Camera.transform.position).ToString("F1"),ScanRed,21,2);
        for(int i=0;i<40;i++) {
            float a=i*Mathf.PI/20,b=(i+1)*Mathf.PI/20;
            Line(cx+Mathf.Cos(a)*28,cy+Mathf.Sin(a)*28,cx+Mathf.Cos(b)*28,cy+Mathf.Sin(b)*28,Alpha(ScanRed,.7f));
        }
        Line(cx-5,cy,cx+5,cy,Alpha(ScanRed,.45f)); Line(cx,cy-5,cx,cy+5,Alpha(ScanRed,.45f));
    }
    static void Crosshair(Rect r,Color color) {
        // Kept for the harmless training target outline.
        Corners(r,Alpha(color,.25f),9);
    }
    static void TargetBracket(EFT.Player actor) {
        var camera=Runtime.Camera; if(camera==null) return;
        var head=camera.WorldToScreenPoint(actor.PlayerBones?.Head?.Original?.position??actor.Position+Vector3.up*1.7f);
        if(head.z<=0) return;
        float x=(head.x-ox)/scale,y=(Screen.height-head.y-oy)/scale;
        Image(new Rect(x-8,y-33,16,16),"qh_arrow",ScanRed);
        if(!Runtime.Uploading) Text(new Rect(x-190,y-76,380,29),actor.Profile?.Nickname??"AI",ScanCyan,18,0,TextAnchor.MiddleCenter);
    }
    static void Hologram() {
        var r=new Rect(771,258,378,451); Crosshair(r,Alpha(Cyan,.75f));
        float age=Now-Runtime.PreviewStarted; var kind=Runtime.PreviewKind;
        bool active=age>=HackRules.Upload(kind)&&age<HackRules.Upload(kind)+2;
        if(age>=0&&age<HackRules.Upload(kind)+2) HeadBadge(new Vector2(960,260),1,kind,active?1:Runtime.PreviewProgress,active?HackRules.Duration(kind):0,!active,age:age);
        Color color=active?(kind==HackKind.RebootOptics?Cyan:Red):Alpha(Cyan,.6f);
        Vector2[] contour=[new(-19,0),new(-29,12),new(-31,42),new(-24,64),new(-21,77),new(-57,91),new(-76,112),new(-89,183),new(-100,249),new(-91,258),new(-79,246),new(-64,191),new(-53,157),new(-51,227),new(-44,256),new(-47,320),new(-49,383),new(-30,389),new(-21,374),new(-15,314),new(-6,269),new(6,269),new(15,314),new(21,374),new(30,389),new(49,383),new(47,320),new(44,256),new(51,227),new(53,157),new(64,191),new(79,246),new(91,258),new(100,249),new(89,183),new(76,112),new(57,91),new(21,77),new(24,64),new(31,42),new(29,12),new(19,0),new(-19,0)];
        const float cx=960,top=283;
        for(int i=1;i<contour.Length;i++) Line(cx+contour[i-1].x,top+contour[i-1].y,cx+contour[i].x,top+contour[i].y,color,1.4f);
        foreach(int y in new[]{91,123,167,212,256,310,355}) Line(cx-39,top+y,cx+39,top+y,Alpha(color,.25f));
        Line(cx,top+85,cx,top+260,Alpha(color,.4f));
        Line(cx-50,top+104,cx+38,top+209,Alpha(color,.2f)); Line(cx+50,top+104,cx-38,top+209,Alpha(color,.2f));
        float scanY=top+(Now*.18f%1)*389;
        Fill(new Rect(816,scanY,288,1),Alpha(Cyan,.7f)); Fill(new Rect(816,scanY-9,288,9),Alpha(Cyan,.04f));
        Text(new Rect(810,724,300,23),"光学目标模拟  /  01",Alpha(Cyan,.6f),14,0,TextAnchor.MiddleCenter);
        if(!active) return;
        if(kind==HackKind.BlackwallGateway) {
            Image(new Rect(843,330,236,310),"blackwall",new Color(1,.08f,.12f,.8f));
            for(int i=0;i<11;i++) Fill(new Rect(877+Mathf.Sin(age*28+i)*46,top+25+i*31,48+Mathf.Sin(age*19+i)*35,3),Alpha(Red,.8f));
        } else if(kind==HackKind.RebootOptics) {
            Fill(new Rect(cx-27,top+31,54,5),Cyan); Corners(new Rect(cx-42,top+7,84,70),Cyan,9);
        } else {
            float effectY=kind==HackKind.CrippleMovement?top+290:kind==HackKind.WeaponGlitch?top+140:top+7;
            Corners(new Rect(cx-42,effectY,84,70),Red,9); Image(new Rect(cx+62,effectY+11,58,58),HackRules.Icon(kind),Red);
            for(int i=0;i<4;i++) Fill(new Rect(cx-30+Mathf.Sin(age*23+i)*12,effectY+12+i*14,52,2),Alpha(Red,.7f));
        }
    }
}
