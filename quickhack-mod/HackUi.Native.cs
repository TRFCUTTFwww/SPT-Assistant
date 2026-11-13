using UnityEngine;

namespace LocalQuickhacks;

internal static class HackUi {
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
    static Vector2 lastMouse=new(-9999,-9999);
    static float Now=>Time.unscaledTime;
    static float Pulse=>.6f+.4f*Mathf.Sin(Now*4);
    static Color Alpha(Color c,float a)=>new(c.r,c.g,c.b,c.a*a);

    internal static void Draw() {
        if(Runtime.Mode==UiMode.None&&!Runtime.Uploading) return;
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
            else Upload(new Rect(720,155,480,73),Runtime.UploadName,Runtime.UploadProgress);
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
    static void ScanSurface(bool preview) {
        Fill(canvas,new Color(.025f,.075f,.07f,preview?.97f:.22f));
        // The central field remains transparent in live raids.
        Fill(new Rect(0,0,590,1080),new Color(.015f,.04f,.038f,.38f));
        Fill(new Rect(1280,0,640,1080),new Color(.015f,.04f,.038f,.3f));
        for(int y=2;y<1080;y+=5) Fill(new Rect(canvas.x,y,canvas.width,1),new Color(.1f,.3f,.25f,.035f));
        Corners(new Rect(54,49,1812,961),Alpha(Cyan,.55f),38);
        Image(new Rect(82,74,183,54),"scan_fluff_corner_text",Alpha(Cyan,.75f));
        Image(new Rect(1635,74,190,43),"scan_fluff_top_right",Alpha(Cyan,.7f));
        Image(new Rect(64,420,16,164),"scan_fluff_left",Alpha(Cyan,.7f));
        Image(new Rect(1790,346,28,404),"scan_ruller",Alpha(Cyan,.65f));
        Image(new Rect(1450,931,347,44),"scan_fluff_bottom",Alpha(Cyan,.5f));
        Text(new Rect(85,932,420,24),preview?"本地模拟  /  TRAINING LINK":"扫描模式  /  OPTICAL LINK",Cyan,15);
        if(preview) Text(new Rect(695,150,530,26),"施放模拟  ·  未连接真实目标",Alpha(Cyan,.7f),16,0,TextAnchor.MiddleCenter);
    }
    static void Scanner(bool preview) {
        ScanSurface(preview);
        Text(new Rect(630,61,660,28),"赛博甲板内存",Cyan,19,0,TextAnchor.MiddleCenter);
        for(int i=0;i<24;i++) {
            float x=637+i*27;
            Image(new Rect(x,105,21,29),"qh_memory_small_bg",Alpha(Cyan,.25f),true);
            if(i<Runtime.Ram) Image(new Rect(x,105,21,29),"qh_memory_small_fg",i<Runtime.Ram-HackRules.Cost(Runtime.Selected)?Cyan:Red,true);
        }
        Text(new Rect(1310,102,125,28),$"{Runtime.Ram:F0} / 24",Cyan,20,1);
        var actor=Runtime.Target?.Resolve();
        if(preview) Hologram(); else if(actor!=null) TargetBracket(actor); else Crosshair(new Rect(810,365,300,350),Dim);
        const float x0=127,w=432;
        Text(new Rect(x0,267,w,32),"可用快速破解",Red,23);
        Text(new Rect(x0,304,w,23),"QUICKHACKS  /  03",Alpha(Red,.55f),16,2);
        Line(x0,340,x0+w,340,Alpha(Red,.5f));
        Image(new Rect(x0-25,342,16,310),"qh_quickhacks_handle",Alpha(Red,.7f),true);
        for(int i=0;i<3;i++) {
            var kind=(HackKind)i; bool selected=Runtime.Selected==kind; float y=358+i*91;
            var row=new Rect(x0,y,w,81); bool over=row.Contains(Event.current.mousePosition);
            if(selected) {
                Image(row,"qh_selection_plate",Alpha(Red,.24f),true);
                Fill(new Rect(x0,y,3,81),Red); Line(x0,y,x0+w,y,Alpha(Red,.7f));
                Image(new Rect(x0+w-24,y+29,16,20),"qh_arrow",Red);
            } else if(over) Fill(row,Alpha(Red,.07f));
            Image(new Rect(x0+15,y+15,49,49),HackRules.Icon(kind),selected?Red:Alpha(Red,.65f));
            Text(new Rect(x0+82,y+13,268,32),HackRules.Name(kind),selected?Red:Alpha(Red,.8f),23);
            Text(new Rect(x0+83,y+50,252,21),$"{HackRules.Upload(kind):F1} 秒上传",Alpha(Red,.55f),15);
            Text(new Rect(x0+w-70,y+13,35,30),HackRules.Cost(kind).ToString("00"),Red,25,1,TextAnchor.MiddleRight);
            if(Click(row,"hack"+i)) { Runtime.Selected=kind; Assets.Play("press"); }
        }
        const float detail=655;
        CutPanel(new Rect(x0,detail,w,209),Alpha(Red,.6f));
        Text(new Rect(x0+17,detail+17,w-34,29),HackRules.Name(Runtime.Selected),Red,22);
        Text(new Rect(x0+17,detail+54,w-34,23),Runtime.Selected==HackKind.RebootOptics?"控制  /  CONTROL":"终极  /  ULTIMATE",Alpha(Red,.6f),15);
        Line(x0+17,detail+91,x0+w-17,detail+91,Alpha(Red,.3f));
        string desc=Runtime.Selected switch {
            HackKind.Cyberpsychosis=>"扰乱目标的威胁识别。\n使其攻击附近 AI，包括原队友。",
            HackKind.BlackwallGateway=>"释放黑墙，摧毁目标。\n向附近可见 AI 连锁传播。",
            _=>"重启目标的视觉系统。\n暂时阻断视觉，听觉仍然有效。"
        };
        Text(new Rect(x0+17,detail+106,w-34,58),desc,Alpha(Red,.85f),17);
        Text(new Rect(x0+17,detail+176,w-34,22),$"持续时间  {(Runtime.Selected==HackKind.Cyberpsychosis?"20 秒":Runtime.Selected==HackKind.RebootOptics?"10 秒":"最多 5 个目标")}",Alpha(Red,.5f),14);
        if(Button(new Rect(x0,880,w,48),"F",preview?"执行施放模拟":"上传快速破解",Red,"upload")) { if(preview) Runtime.PreviewCast(); else Runtime.Upload(); }
        if(preview&&Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.F) { Runtime.PreviewCast(); Event.current.Use(); }
        const float right=1318;
        string name=preview?"训练目标":actor?.Profile?.Nickname??"搜索目标";
        Text(new Rect(right,303,430,24),"目标数据",Alpha(Red,.6f),17);
        Text(new Rect(right,343,435,48),name,Red,31);
        Line(right,402,right+430,402,Red,2);
        DataRow(right,425,"类型",preview||actor!=null?"人类":"未识别");
        DataRow(right,468,"状态",preview?"模拟链路":actor!=null?"存活":"等待扫描");
        DataRow(right,511,"距离",preview?"—":actor!=null?Vector3.Distance(actor.Position,Runtime.Camera!.transform.position).ToString("F1")+" m":"—");
        Line(right,565,right+430,565,Alpha(Red,.35f));
        Image(new Rect(right,593,61,61),"qh_seal",Alpha(Red,.65f));
        Text(new Rect(right+80,598,350,27),preview||actor!=null?"允许快速破解":"请对准视线内的 AI",Red,19);
        Text(new Rect(right+80,632,350,27),"远程访问  /  REMOTE ACCESS",Alpha(Red,.5f),14,2);
        if(preview&&Button(new Rect(right,755,430,52),"01","入侵协议 · 门禁授权",Yellow,"breach-preview")) Runtime.OpenPreviewBreach();
        if(Runtime.Uploading) Upload(new Rect(698,770,524,78),Runtime.UploadName,Runtime.UploadProgress);
        else if(preview&&Now-Runtime.PreviewStarted<6) Upload(new Rect(698,770,524,78),HackRules.Name(Runtime.PreviewKind),Runtime.PreviewProgress);
        Text(new Rect(630,882,660,50),Runtime.Status,Alpha(Cyan,.85f),16,0,TextAnchor.MiddleCenter);
        if(Button(new Rect(1586,993,240,43),"ESC","退出扫描",Red,"close-scanner")) Runtime.Close();
        Text(new Rect(640,1008,700,26),preview?"F10  关闭     F  施放模拟":"滚轮  切换破解     F  上传     松开 F9  退出",Alpha(Cyan,.7f),16,0,TextAnchor.MiddleCenter);
    }
    static void DataRow(float x,float y,string label,string value) {
        Text(new Rect(x,y,120,30),label,Alpha(Red,.55f),17);
        Text(new Rect(x+132,y,300,30),value,Red,19);
    }
    static void Crosshair(Rect r,Color color) {
        Corners(r,color,22);
        Line(r.center.x-7,r.center.y,r.center.x+7,r.center.y,Alpha(color,.6f));
        Line(r.center.x,r.center.y-7,r.center.x,r.center.y+7,Alpha(color,.6f));
        Image(new Rect(r.x-33,r.center.y-90,14,180),"scan_inner_crosshair",color);
    }
    static void TargetBracket(EFT.Player actor) {
        var camera=Runtime.Camera; if(camera==null) return;
        var feet=camera.WorldToScreenPoint(actor.Position);
        var head=camera.WorldToScreenPoint(actor.PlayerBones?.Head?.Original?.position??actor.Position+Vector3.up*1.7f);
        if(feet.z<=0||head.z<=0) return;
        float height=Math.Clamp(Math.Abs(feet.y-head.y)/scale,100,540),width=height*.52f;
        float x=(head.x-ox)/scale,y=(Screen.height-head.y-oy)/scale;
        var r=new Rect(x-width/2-18,y-26,width+36,height+54);
        Crosshair(r,Red); Text(new Rect(r.x,r.y-31,300,24),"目标已锁定",Red,16);
        Image(new Rect(r.xMax+10,r.y,18,r.height),"scan_ruller",Alpha(Red,.7f));
    }
    static void Hologram() {
        var r=new Rect(771,258,378,451); Crosshair(r,Alpha(Cyan,.75f));
        float age=Now-Runtime.PreviewStarted; var kind=Runtime.PreviewKind;
        bool active=age>=HackRules.Upload(kind)&&age<HackRules.Upload(kind)+2;
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
            Corners(new Rect(cx-42,top+7,84,70),Red,9); Image(new Rect(cx+62,top+18,58,58),"Cyberpsychosis",Red);
        }
    }
    static void Upload(Rect r,string name,float progress) {
        Color c=progress>=1?Cyan:Red; CutPanel(r,Alpha(c,.6f));
        Text(new Rect(r.x+13,r.y+10,r.width-100,25),name+(progress>=1?"  ·  已上传":"  ·  上传中"),c,18);
        Text(new Rect(r.xMax-84,r.y+10,66,25),(progress*100).ToString("F0")+"%",c,20,1,TextAnchor.MiddleRight);
        Fill(new Rect(r.x+14,r.yMax-20,r.width-28,5),Alpha(c,.15f)); Fill(new Rect(r.x+14,r.yMax-20,(r.width-28)*progress,5),c);
    }
    static void Breach() {
        var p=Runtime.Puzzle; if(p==null) return;
        if(!ReferenceEquals(shownPuzzle,p)) { shownPuzzle=p; focusRow=focusCol=shownPicks=0; hovered=""; lastMouse=new(-9999,-9999); }
        if(shownPicks!=p.Picks.Count) { shownPicks=p.Picks.Count; pickedAt=Now; }
        Color accent=p.Lost?Red:Yellow;
        Fill(canvas,new Color(.022f,.035f,.03f,.995f));
        Image(new Rect(0,0,1920,1080),"breach_fluff_pettern",Alpha(Yellow,.022f),true);
        Text(new Rect(242,92,1000,58),"入侵协议",Yellow,43);
        Text(new Rect(247,153,700,24),"BREACH PROTOCOL",Alpha(Yellow,.55f),19,2);
        Image(new Rect(1436,110,231,25),"breach_logo_nettech",Yellow,true);
        Image(new Rect(1500,153,167,15),"breach_fluff_header1",Alpha(Yellow,.5f),true);
        Line(244,205,1675,205,Alpha(Yellow,.35f));
        Image(new Rect(183,390,20,320),"scan_ruller",Alpha(Yellow,.35f),true);
        Text(new Rect(246,238,480,29),"破解时间剩余",Yellow,20);
        double remaining=p.Remaining(Now);
        Text(new Rect(246,278,160,48),remaining.ToString("F2"),accent,39,1);
        Text(new Rect(428,294,342,27),p.Started.HasValue?"协议计时中":"首次选码后启动计时",Alpha(Yellow,.5f),16);
        Fill(new Rect(246,341,524,7),Alpha(Yellow,.14f)); Fill(new Rect(246,341,(float)(524*remaining/p.Duration),7),accent);
        Text(new Rect(246,369,524,25),"代码矩阵",Yellow,19);
        Matrix(p,new Rect(246,411,524,476));
        const float right=906;
        Text(new Rect(right,238,720,29),"缓冲区",Yellow,20);
        for(int i=0;i<p.Capacity;i++) {
            var cell=new Rect(right+i*91,290,69,62);
            Image(cell,"breach_buffer_empty",Alpha(Yellow,.35f),true);
            if(i<p.Buffer.Count) {
                Image(cell,"breach_buffer_activated",Yellow,true);
                Text(cell,p.Buffer[i],Yellow,30,1,TextAnchor.MiddleCenter);
            } else if(i==p.Buffer.Count&&!p.Ended) {
                Image(cell,"breach_buffer_activated",Alpha(Yellow,.45f+.2f*Pulse),true);
                if(p.CanPick(focusRow,focusCol)) Text(cell,p.Matrix[focusRow,focusCol],Alpha(Yellow,.35f),30,1,TextAnchor.MiddleCenter);
                Line(cell.x,cell.yMax+8,cell.xMax,cell.yMax+8,Alpha(Yellow,Pulse),2);
            }
        }
        Text(new Rect(right,371,720,22),$"{p.Buffer.Count:00} / {p.Capacity:00}    BUFFER",Alpha(Yellow,.4f),15,1);
        Fill(new Rect(right,421,764,40),Yellow);
        Image(new Rect(right+13,428,24,24),"breach_ico_programs",Ink);
        Text(new Rect(right+50,421,700,40),"上传魔偶所需序列",Ink,20,0,TextAnchor.MiddleLeft);
        int matched=MatchedPrefix(p);
        for(int i=0;i<p.Sequence.Length;i++) {
            var code=new Rect(right+i*79,490,69,55); bool done=p.Won||i<matched;
            if(done) Fill(code,Alpha(Yellow,.13f));
            if(i==matched&&!p.Ended) Line(code.x,code.yMax,code.xMax,code.yMax,Alpha(Yellow,.65f),2);
            Text(code,p.Sequence[i],done?Yellow:Alpha(Yellow,.6f),32,1,TextAnchor.MiddleCenter);
        }
        Text(new Rect(right+560,504,200,26),p.Won?"已上传":$"{matched} / {p.Sequence.Length}",p.Won?Cyan:Alpha(Yellow,.45f),17,0,TextAnchor.MiddleRight);
        Line(right,571,right+764,571,Alpha(Yellow,.25f));
        Image(new Rect(right+4,603,68,68),"DoorAuthorization",p.Won?Cyan:Yellow);
        Text(new Rect(right+96,599,650,37),"门禁授权",p.Won?Cyan:Yellow,27);
        Text(new Rect(right+96,647,650,23),"DOOR AUTHORIZATION",Alpha(Yellow,.5f),16,2);
        Text(new Rect(right+96,695,650,67),"上传门禁授权魔偶，\n取得当前访问节点的开门权限。",Alpha(Yellow,.8f),19);
        Image(new Rect(right+1,802,239,70),"breach_fluff_protocol2",Alpha(Yellow,.35f));
        Image(new Rect(right+580,800,167,52),"breach_fluff_text6",Alpha(Yellow,.35f));
        Image(new Rect(247,912,263,46),"breach_fluff_barcode1",Alpha(Yellow,.5f));
        Image(new Rect(553,923,216,26),"breach_fluff_code1",Alpha(Yellow,.35f));
        Image(new Rect(245,976,524,14),"breach_fluff_barcode2",Alpha(Yellow,.24f),true);
        Text(new Rect(right,922,764,27),p.Ended?Runtime.Status:p.SelectRow?"选择高亮行中的代码":"选择高亮列中的代码",accent,18);
        Text(new Rect(right,960,764,26),Runtime.PreviewBreach?"模拟接入点  ·  不操作真实门禁":"门禁接入点  ·  本地授权链路",Alpha(Yellow,.4f),15);
        Text(new Rect(245,1032,1300,26),"鼠标 / 方向键  选码      ENTER  确认      行与列交替      每格仅使用一次",Alpha(Yellow,.5f),15);
        if(Button(new Rect(1485,1020,210,42),"ESC","断开连接",Yellow,"close-breach")) Runtime.Close();
        if(p.Ended) Result(p);
    }
    static int MatchedPrefix(BreachPuzzle p) {
        for(int length=Math.Min(p.Buffer.Count,p.Sequence.Length);length>0;length--) {
            bool same=true;
            for(int i=0;i<length;i++) if(p.Buffer[p.Buffer.Count-length+i]!=p.Sequence[i]) { same=false; break; }
            if(same) return length;
        }
        return 0;
    }
    static void Matrix(BreachPuzzle p,Rect board) {
        Fill(board,new Color(.04f,.065f,.048f,.8f)); Frame(board,Alpha(Yellow,.3f)); Corners(board,Yellow,11);
        float pitch=p.Size==6?73:86;
        float bx=board.x+(board.width-p.Size*pitch)/2,by=board.y+(board.height-p.Size*pitch)/2;
        var mouse=Event.current.mousePosition;
        bool mouseMoved=mouse.x!=lastMouse.x||mouse.y!=lastMouse.y;
        for(int r=0;r<p.Size;r++) for(int c=0;c<p.Size;c++) {
            var cell=new Rect(bx+c*pitch,by+r*pitch,pitch,pitch);
            if(mouseMoved&&cell.Contains(mouse)&&p.CanPick(r,c)) { focusRow=r; focusCol=c; }
        }
        lastMouse=mouse;
        if(!p.CanPick(focusRow,focusCol)) for(int i=0;i<p.Size;i++) {
            int r=p.SelectRow?p.ActiveLine:i,c=p.SelectRow?i:p.ActiveLine;
            if(p.CanPick(r,c)) { focusRow=r; focusCol=c; break; }
        }
        Keyboard(p);
        if(!p.Ended) {
            var band=p.SelectRow?new Rect(board.x,by+p.ActiveLine*pitch,board.width,pitch):new Rect(bx+p.ActiveLine*pitch,board.y,pitch,board.height);
            Fill(band,Alpha(Yellow,.095f)); Frame(band,Alpha(Yellow,.17f));
            var next=p.SelectRow?new Rect(bx+focusCol*pitch,board.y,pitch,board.height):new Rect(board.x,by+focusRow*pitch,board.width,pitch);
            Fill(next,Alpha(Yellow,.035f));
        }
        for(int i=1;i<p.Picks.Count;i++) {
            var a=p.Picks[i-1]; var b=p.Picks[i];
            Line(bx+(a.Col+.5f)*pitch,by+(a.Row+.5f)*pitch,bx+(b.Col+.5f)*pitch,by+(b.Row+.5f)*pitch,Alpha(Yellow,.12f));
        }
        for(int r=0;r<p.Size;r++) for(int c=0;c<p.Size;c++) {
            var cell=new Rect(bx+c*pitch+5,by+r*pitch+5,pitch-10,pitch-10);
            bool allowed=p.CanPick(r,c),used=p.Used(r,c),focus=allowed&&r==focusRow&&c==focusCol;
            if(focus) {
                Image(new Rect(cell.x-10,cell.y-10,cell.width+20,cell.height+20),"breach_frame_glow_small",Alpha(Yellow,.24f));
                Fill(cell,Yellow); Corners(new Rect(cell.x-3,cell.y-3,cell.width+6,cell.height+6),Yellow,8);
            }
            Text(cell,used?"··":p.Matrix[r,c],focus?Ink:used?Alpha(Yellow,.21f):allowed?Yellow:Alpha(Yellow,.48f),30,1,TextAnchor.MiddleCenter);
            if(used) Line(cell.x+15,cell.center.y,cell.xMax-15,cell.center.y,Alpha(Yellow,.3f));
            if(Click(cell,"code"+r+":"+c,allowed)) Runtime.Pick(r,c);
        }
        if(Now-pickedAt<.23f&&p.Picks.Count>0) {
            var last=p.Picks[^1];
            Frame(new Rect(bx+last.Col*pitch+4,by+last.Row*pitch+4,pitch-8,pitch-8),Alpha(Yellow,1-(Now-pickedAt)/.23f),2);
        }
    }
    static void Keyboard(BreachPuzzle p) {
        var e=Event.current; if(p.Ended||e.type!=EventType.KeyDown) return;
        int direction=e.keyCode is KeyCode.LeftArrow or KeyCode.UpArrow?-1:e.keyCode is KeyCode.RightArrow or KeyCode.DownArrow?1:0;
        if(direction!=0) {
            int current=p.SelectRow?focusCol:focusRow;
            for(int i=1;i<=p.Size;i++) {
                int index=(current+direction*i+p.Size*2)%p.Size;
                int r=p.SelectRow?p.ActiveLine:index,c=p.SelectRow?index:p.ActiveLine;
                if(p.CanPick(r,c)) { focusRow=r; focusCol=c; Assets.Play("hover"); break; }
            }
            e.Use();
        } else if(e.keyCode is KeyCode.Return or KeyCode.KeypadEnter) { Runtime.Pick(focusRow,focusCol); e.Use(); }
    }
    static void Result(BreachPuzzle p) {
        Color c=p.Won?Yellow:Red;
        Fill(new Rect(218,466,1484,173),new Color(.02f,.035f,.028f,.99f));
        Fill(new Rect(218,466,1484,3),c); Fill(new Rect(218,636,1484,3),c);
        Image(new Rect(270,496,77,87),p.Won?"DoorAuthorization":"qh_warning_tiles",c);
        Text(new Rect(395,487,1190,59),p.Won?"接入已授权":"协议破解失败",c,40);
        Text(new Rect(397,558,1130,31),p.Won?"魔偶上传完成  /  ACCESS GRANTED":"连接已终止  /  ACCESS DENIED",Alpha(c,.65f),20);
    }
}
