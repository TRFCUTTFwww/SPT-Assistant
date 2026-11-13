using UnityEngine;

namespace LocalQuickhacks;

internal static class HackUi {
    static readonly Color Red=new(1,.25f,.31f), Yellow=new(.95f,.95f,.55f), Cyan=new(.3f,.94f,1), Dim=new(.44f,.51f,.53f), White=new(.86f,.9f,.89f), Dark=new(.025f,.045f,.055f,.96f);
    static GUIStyle? style;
    static Font? body,mono;
    static float scale,ox,oy;
    static Rect canvas;
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
            if(Runtime.Mode==UiMode.Breach) Breach();
            else if(Runtime.Mode is UiMode.Scanner or UiMode.Preview) Scanner(Runtime.Mode==UiMode.Preview);
            else Upload(new Rect(720,170,480,65));
        } catch(Exception e) { Runtime.Warn("Quickhack UI",e); }
        finally { GUI.matrix=saved; GUI.color=color; GUI.depth=depth; }
    }
    static void EnsureFonts() {
        if(style!=null) return;
        body=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",20);
        mono=Font.CreateDynamicFontFromOSFont("Consolas",24);
        style=new GUIStyle(GUI.skin.label) { richText=false,wordWrap=false };
    }
    static void Fill(Rect r,Color c) { GUI.color=c; GUI.DrawTexture(r,Texture2D.whiteTexture); GUI.color=Color.white; }
    static void Text(Rect r,string text,Color c,int size=20,bool code=false,TextAnchor align=TextAnchor.UpperLeft) {
        style!.font=code?mono:body; style.fontSize=size; style.normal.textColor=c; style.alignment=align; GUI.Label(r,text,style);
    }
    static void Frame(Rect r,Color c,float width=1) { Fill(new Rect(r.x,r.y,r.width,width),c); Fill(new Rect(r.x,r.yMax-width,r.width,width),c); Fill(new Rect(r.x,r.y,width,r.height),c); Fill(new Rect(r.xMax-width,r.y,width,r.height),c); }
    static void Panel(Rect r,Color accent) { Fill(r,Dark); Fill(new Rect(r.x,r.y,r.width,2),accent); Fill(new Rect(r.x,r.y,3,24),accent); Fill(new Rect(r.xMax-24,r.yMax-3,24,3),accent); }
    static bool Button(Rect r,string label,bool selected=false) {
        bool hover=r.Contains(Event.current.mousePosition);
        Fill(r,selected?new Color(Red.r,Red.g,Red.b,.2f):hover?new Color(.3f,.6f,.65f,.15f):new Color(.04f,.075f,.085f,.95f));
        Frame(r,selected?Red:hover?Cyan:Dim);
        Text(r,label,selected?Red:White,19,false,TextAnchor.MiddleCenter);
        if(Event.current.type==EventType.MouseDown&&Event.current.button==0&&hover) { Event.current.Use(); Assets.Play("press"); return true; }
        return false;
    }
    static void Image(Rect r,string key,Color color) { var texture=Assets.Texture(key); if(texture==null) return; GUI.color=color; GUI.DrawTexture(r,texture,ScaleMode.ScaleToFit,true); GUI.color=Color.white; }
    static void Header(string title,string subtitle,Color accent) {
        Text(new Rect(100,62,1500,50),title,accent,34);
        Text(new Rect(103,117,1400,30),subtitle,Dim,17);
        Fill(new Rect(100,156,1720,1),accent);
        if(Button(new Rect(1670,70,150,44),"ESC  关闭")) Runtime.Close();
    }
    static void Scanner(bool preview) {
        Fill(canvas,new Color(.015f,.04f,.055f,preview?.98f:.55f));
        Header("扫描器  /  SCANNER",preview?"界面与施放预览":"目标分析  ·  快速破解",Red);
        Text(new Rect(1120,192,650,28),"赛博甲板  /  RAM",Cyan,18);
        for(int i=0;i<24;i++) Fill(new Rect(1120+i*27,233,21,13),i<(int)Runtime.Ram?Cyan:new Color(.14f,.25f,.27f));
        Text(new Rect(1120,262,650,28),$"可用 {Runtime.Ram:F0} / 24",Dim,17);
        var target=Runtime.Target?.Resolve();
        string name=preview?"训练目标  /  TRAINING TARGET":target?.Profile?.Nickname??"未锁定目标";
        Text(new Rect(120,204,850,45),name,Red,27);
        Text(new Rect(120,255,850,30),preview?"人类  ·  允许快速破解":target!=null?"AI  ·  "+Vector3.Distance(target.Position,Runtime.Camera!.transform.position).ToString("F1")+" m":"将准星对准视线内的 AI",Cyan,18);
        if(preview) Silhouette(); else if(target!=null) Brackets(target);
        var panel=new Rect(1090,312,720,525); Panel(panel,Red);
        Text(new Rect(1115,330,660,30),"可用快速破解",Red,19);
        for(int i=0;i<3;i++) {
            var kind=(HackKind)i; bool selected=Runtime.Selected==kind; float y=381+i*104;
            var row=new Rect(1112,y,675,90);
            if(selected) { Fill(row,new Color(.8f,.11f,.16f,.16f)); Fill(new Rect(row.x,row.y,4,row.height),Red); }
            Image(new Rect(1128,y+14,60,60),HackRules.Icon(kind),selected?Red:Dim);
            Text(new Rect(1205,y+12,450,31),HackRules.Name(kind),selected?Red:White,24);
            Text(new Rect(1205,y+50,500,27),$"{HackRules.Cost(kind)} RAM  ·  上传 {HackRules.Upload(kind):F1} s",Dim,17);
            if(Event.current.type==EventType.MouseDown&&Event.current.button==0&&row.Contains(Event.current.mousePosition)) { Runtime.Selected=kind; Assets.Play("hover"); Event.current.Use(); }
        }
        Text(new Rect(1120,710,650,30),HackRules.Description(Runtime.Selected),White,17);
        if(Button(new Rect(1120,763,650,51),preview?"F  预览施放":"F  上传快速破解",true)) { if(preview) Runtime.PreviewCast(); else Runtime.Upload(); }
        if(preview&&Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.F) { Runtime.PreviewCast(); Event.current.Use(); }
        if(Runtime.Uploading) Upload(new Rect(120,817,860,78));
        else if(preview&&Time.unscaledTime-Runtime.PreviewStarted<6) {
            Text(new Rect(120,820,850,32),HackRules.Name(Runtime.Selected)+(Runtime.PreviewProgress<1?"  ·  上传中":"  ·  已上传"),Cyan,22);
            Fill(new Rect(120,868,850,5),Dim); Fill(new Rect(120,868,850*Runtime.PreviewProgress,5),Cyan);
        }
        Text(new Rect(120,933,1640,32),Runtime.Status,Yellow,19);
        Text(new Rect(120,1000,1500,30),preview?"F10 / ESC  关闭     F  预览施放":"按住 F9  扫描     滚轮  选择破解     F  上传     松开 F9  返回",Dim,17);
        if(preview&&Button(new Rect(1090,875,720,55),"入侵协议  /  门禁授权预览")) Runtime.OpenPreviewBreach();
    }
    static void Brackets(EFT.Player target) {
        var camera=Runtime.Camera; if(camera==null) return;
        var p=camera.WorldToScreenPoint(target.Position+Vector3.up); if(p.z<=0) return;
        float x=(p.x-ox)/scale,y=(Screen.height-p.y-oy)/scale;
        var rect=new Rect(x-90,y-130,180,260);
        foreach(var r in new[]{new Rect(rect.x,rect.y,25,2),new Rect(rect.x,rect.y,2,25),new Rect(rect.xMax-25,rect.y,25,2),new Rect(rect.xMax-2,rect.y,2,25),new Rect(rect.x,rect.yMax-2,25,2),new Rect(rect.x,rect.yMax-25,2,25),new Rect(rect.xMax-25,rect.yMax-2,25,2),new Rect(rect.xMax-2,rect.yMax-25,2,25)}) Fill(r,Red);
    }
    static void Silhouette() {
        float age=Time.unscaledTime-Runtime.PreviewStarted;
        bool active=age>=HackRules.Upload(Runtime.Selected)&&age<HackRules.Upload(Runtime.Selected)+2;
        Color c=active&&Runtime.Selected==HackKind.BlackwallGateway?Red:new Color(.22f,.7f,.75f,.7f);
        Panel(new Rect(120,320,850,465),Dim);
        Text(new Rect(145,340,650,30),"目标识别  /  HUMAN",Cyan,18,true);
        for(int i=0;i<12;i++) Fill(new Rect(150,395+i*28,785,1),new Color(.14f,.3f,.32f,.4f));
        var parts=new[]{new Rect(493,385,88,87),new Rect(464,483,146,157),new Rect(418,489,34,150),new Rect(622,489,34,150),new Rect(467,649,59,104),new Rect(549,649,59,104)};
        foreach(var r in parts) { Fill(r,new Color(c.r,c.g,c.b,.12f)); Frame(r,c); }
        if(active) {
            Image(new Rect(696,399,110,110),HackRules.Icon(Runtime.Selected),Runtime.Selected==HackKind.RebootOptics?Cyan:Red);
            if(Runtime.Selected==HackKind.BlackwallGateway) {
                Image(new Rect(411,370,260,380),"blackwall",new Color(1,.1f,.1f,.7f));
                for(int i=0;i<8;i++) Fill(new Rect(450+Mathf.Sin(age*31+i)*75,390+i*42,40+Mathf.Abs(Mathf.Cos(age*23+i))*90,3),Red);
            } else if(Runtime.Selected==HackKind.RebootOptics) {
                Fill(new Rect(492,416,90,17),Cyan); Text(new Rect(684,524,238,30),"视觉离线",Cyan,22);
            } else { Frame(new Rect(480,376,114,107),Red,3); Text(new Rect(684,524,238,30),"威胁识别异常",Red,22); }
        }
    }
    static void Upload(Rect rect) {
        Panel(rect,Cyan); Text(new Rect(rect.x+14,rect.y+8,rect.width-28,30),Runtime.UploadName+"  ·  "+(Runtime.UploadProgress*100).ToString("F0")+"%",Cyan,21);
        Fill(new Rect(rect.x+14,rect.yMax-17,rect.width-28,4),Dim); Fill(new Rect(rect.x+14,rect.yMax-17,(rect.width-28)*Runtime.UploadProgress,4),Cyan);
    }
    static void Breach() {
        var puzzle=Runtime.Puzzle; if(puzzle==null) return;
        Fill(canvas,new Color(.012f,.022f,.025f,.985f));
        Header("入侵协议  /  BREACH PROTOCOL",Runtime.PreviewBreach?"模拟门禁  ·  上传魔偶":"门禁访问  ·  "+(Runtime.Door?.Id??""),Yellow);
        Text(new Rect(120,204,760,35),"破解协议",Yellow,23);
        double remaining=puzzle.Remaining(Time.unscaledTime);
        Text(new Rect(120,254,240,58),remaining.ToString("F2"),puzzle.Lost?Red:Yellow,44,true);
        Text(new Rect(360,270,500,35),puzzle.Started.HasValue?"剩余时间":"选择首个代码后开始计时",Dim,17);
        Fill(new Rect(120,326,760,3),Dim); Fill(new Rect(120,326,(float)(760*remaining/puzzle.Duration),3),Yellow);
        Text(new Rect(990,205,810,32),"缓冲区  /  BUFFER",Yellow,20);
        for(int i=0;i<puzzle.Capacity;i++) {
            var cell=new Rect(990+i*104,257,88,55); Frame(cell,i<puzzle.Buffer.Count?Yellow:Dim);
            if(i<puzzle.Buffer.Count) Text(cell,puzzle.Buffer[i],Yellow,30,true,TextAnchor.MiddleCenter);
        }
        var board=new Rect(120,385,760,508); Panel(board,Yellow);
        float step=puzzle.Size==6?60:70,gap=18,total=puzzle.Size*step+(puzzle.Size-1)*gap;
        float bx=board.x+(board.width-total)/2,by=board.y+(board.height-total)/2;
        var mouse=Event.current.mousePosition;
        for(int r=0;r<puzzle.Size;r++) for(int c=0;c<puzzle.Size;c++) {
            var cell=new Rect(bx+c*(step+gap),by+r*(step+gap),step,step);
            bool allowed=puzzle.CanPick(r,c),used=puzzle.Used(r,c),hover=allowed&&cell.Contains(mouse);
            if(allowed) Fill(cell,new Color(Yellow.r,Yellow.g,Yellow.b,hover?.24f:.08f));
            if(hover) Frame(cell,Yellow,2);
            Text(cell,used?"--":puzzle.Matrix[r,c],used?new Color(.2f,.3f,.3f):allowed?Yellow:Dim,30,true,TextAnchor.MiddleCenter);
            if(Event.current.type==EventType.MouseDown&&Event.current.button==0&&cell.Contains(mouse)) { Runtime.Pick(r,c); Event.current.Use(); }
        }
        Panel(new Rect(990,385,810,225),Yellow);
        Image(new Rect(1015,412,80,80),"DoorAuthorization",Yellow);
        Text(new Rect(1115,410,640,35),"门禁授权",Yellow,26);
        Text(new Rect(1115,454,640,30),"上传魔偶  /  DOOR AUTHORIZATION",Dim,18,true);
        Text(new Rect(1020,527,750,47),string.Join("  ",puzzle.Sequence),puzzle.Won?Cyan:Yellow,32,true);
        Text(new Rect(990,658,810,36),puzzle.Ended?(puzzle.Won?"访问已授权  /  ACCESS GRANTED":"访问被拒绝  /  ACCESS DENIED"):"匹配序列以取得门禁权限",puzzle.Ended?(puzzle.Won?Cyan:Red):White,23);
        Text(new Rect(990,719,810,36),"从首行开始；随后交替选择所在列、所在行。",Dim,19);
        Text(new Rect(990,760,810,36),"每个格子只能使用一次。",Dim,19);
        Text(new Rect(120,948,1650,32),puzzle.Ended?Runtime.Status:"首行 → 所在列 → 所在行     缓冲区容量 7     ESC  取消",Yellow,19);
    }
}
