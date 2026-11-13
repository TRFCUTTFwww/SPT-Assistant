using UnityEngine;

namespace LocalQuickhacks;

internal static partial class HackUi {
    // Reference layout: D:/aaa, 1920 x 1080. All geometry is in that canvas.
    static readonly Color Lime=new(.86f,.94f,.33f), Purple=new(.095f,.082f,.12f,.94f),
        Emerald=new(.055f,.75f,.51f), Salmon=new(1,.40f,.33f), Terminal=new(.075f,.16f,.125f);
    static bool breachHelp;
    static void Breach() {
        var p=Runtime.Puzzle; if(p==null) return;
        if(!ReferenceEquals(shownPuzzle,p)) {
            shownPuzzle=p; focusRow=focusCol=shownPicks=0; hovered="";
            lastMouse=new(-9999,-9999); breachHelp=false; keyboardFocus=false;
            ResetBreachMotion();
        }
        ObserveBreach(p);
        Fill(canvas,Purple);
        BreachFrame(p.Ended);
        float savedOpacity=opacity;
        opacity*=Ease(OpeningAge,.08f,.25f);
        if(p.Ended) opacity*=1-.65f*Ease(ResultAge,0,.3f);
        Fill(new Rect(canvas.x,168,canvas.width,85),Alpha(Lime,.09f));
        Text(new Rect(140,184,435,48),"剩余入侵时间",Lime,32);
        Frame(new Rect(606,189,108,43),Lime);
        Text(new Rect(607,186,106,45),p.Remaining(Now).ToString("F2"),Lime,34,2,TextAnchor.MiddleCenter);
        Fill(new Rect(142,240,572,9),Alpha(Lime,.16f));
        Fill(new Rect(142,240,(float)(572*p.Remaining(Now)/p.Duration),9),Lime);
        BreachTimerMotion(p);
        Image(new Rect(140,173,290,8),"breach_fluff_text2",Alpha(Lime,.5f),true);
        Text(new Rect(884,122,366,45),"缓冲",Lime,32);
        Image(new Rect(884,118,196,7),"breach_fluff_barcode2",Lime,true);
        Frame(new Rect(884,177,42*p.Capacity+30,72),Lime);
        for(int i=0;i<p.Capacity;i++) {
            var cell=BufferCell(i);
            float bufferOpacity=opacity;
            opacity*=Ease(OpeningAge,.12f+i*.035f,.24f);
            Image(cell,"breach_buffer_empty",Alpha(Lime,.65f),true);
            if(i<p.Buffer.Count&&BufferLanded(i)) {
                Image(cell,"breach_buffer_activated",Lime,true);
                BufferCode(cell,p.Buffer[i]);
            }
            BreachBufferMotion(p,i,cell);
            opacity=bufferOpacity;
        }
        opacity=savedOpacity;
        if(p.Ended&&ResultAge>=0) BreachTerminal(p); else BreachMatrix(p);
        BreachPrograms(p);
        DrawCodeFlights();
        BreachSweep(p);
        BreachAmbient(p);
        BreachShortcuts();
        opacity=savedOpacity;
        if(breachHelp&&!p.Ended) {
            Fill(new Rect(886,645,880,216),new Color(.08f,.07f,.105f,.99f));
            Frame(new Rect(886,645,880,216),Alpha(Lime,.6f));
            Text(new Rect(910,664,830,175),"三条魔偶序列分别匹配，可安排顺序并利用重叠代码。\n从首行开始，交替在所在列、所在行选码，每格仅用一次。\n缓冲区共 10 格，限时 60 秒；门禁授权决定开门权限。\n完成全部序列、缓冲区用尽或计时结束后结算。",Lime,23);
        }
    }
    static void BreachFrame(bool ended) {
        Image(new Rect(38,51,180,20),"breach_logo_nettech",Lime,true);
        if(ended) for(int i=0;i<7;i++) {
            float y=37+i*8; Line(0,y,31,y,Lime); Line(230,y,500,y,Lime);
        }
        // The wire globe is part of NETTECH's visual identity, not a scanner reticle.
        const float cx=65,cy=123,radius=24;
        for(int axis=0;axis<5;axis++) {
            float angle=OpeningAge*.42f+axis*Mathf.PI/4;
            float squeeze=axis==0?1:Mathf.Cos(angle);
            for(int i=0;i<48;i++) {
                float a=i*Mathf.PI/24,b=(i+1)*Mathf.PI/24;
                Line(cx+Mathf.Cos(a)*radius*squeeze,cy+Mathf.Sin(a)*radius,
                    cx+Mathf.Cos(b)*radius*squeeze,cy+Mathf.Sin(b)*radius,Alpha(Lime,axis==0?.65f:.3f+.35f*(.5f+.5f*Mathf.Sin(angle))));
            }
        }
        for(int i=-3;i<=3;i++) {
            float y=cy+i*6,w=Mathf.Sqrt(radius*radius-i*i*36);
            Line(cx-w,y,cx+w,y,Alpha(Lime,.6f));
        }
        for(int row=0;row<3;row++) for(int col=0;col<12;col++) {
            float signal=.5f+.5f*Mathf.Sin(OpeningAge*2.1f-col*.48f+row*1.6f);
            Fill(new Rect(515+col*27,37+row*26,4+signal*5,2),Alpha(Lime,.18f+signal*.65f));
        }
        Fill(new Rect(830,39,922,30),Lime);
        Text(new Rect(830,40,810,22),"入侵协议界面",Alpha(Purple,.65f),11,0,TextAnchor.UpperCenter);
        Image(new Rect(836,59,220,6),"breach_fluff_text2",Alpha(Purple,.8f),true);
        Image(new Rect(1510,58,180,7),"breach_fluff_header1",Alpha(Purple,.6f),true);
        Image(new Rect(1632,37,120,26),"breach_fluff_barcode1",Lime,true);
        float expand=Ease(OpeningAge,0,.35f);
        Frame(new Rect(124,112,1672*expand,864*expand),Alpha(Lime,.8f));
        Frame(new Rect(121,167,1678*expand,765*expand),Alpha(Lime,.45f));
        for(int i=0;i<4;i++) { float x=i==0?144:i==1?165:i==2?1754:1775; Line(x,113,x,113+862*expand,Alpha(Lime,.45f)); }
        Line(116,487,116,711,Lime,2); Line(122,289,122,490,Lime,3);
        foreach(float y in new[]{485,673}) { Fill(new Rect(107,y,5,6),Alpha(Lime,.6f)); Fill(new Rect(116,y,5,6),Alpha(Lime,.6f)); }
        Line(1805,137,1805,288,Lime,2); Fill(new Rect(1811,137,5,6),Lime);
        Line(1805,728,1805,754,Lime,2); Fill(new Rect(1811,728,5,6),Lime);
        Image(new Rect(127,872,40,102),"breach_fluff_3",Alpha(Lime,.65f),true);
        Image(new Rect(124,982,206,26),"breach_fluff_text2",Alpha(Lime,.75f),true);
        Image(new Rect(1228,976,568,15),"breach_fluff_barcode2",Alpha(Lime,.7f),true);
    }
    static void BreachMatrix(BreachPuzzle p) {
        float savedOpacity=opacity;
        opacity*=Ease(OpeningAge,.16f,.24f);
        Fill(new Rect(141,291,698,514),new Color(.092f,.08f,.12f,.92f));
        Frame(new Rect(141,291,698,514),Alpha(Lime,.85f));
        // Angled leading corner of the filled title strip.
        Fill(new Rect(153,291,686*Ease(OpeningAge,.18f,.3f),45),Lime); Fill(new Rect(141,303,12,33),Lime);
        for(int i=0;i<12;i++) Fill(new Rect(152-i,291+i,1,12-i),Lime);
        Image(new Rect(161,301,25,25),"breach_fluff_1",Purple);
        Text(new Rect(201,294,540,40),"代码矩阵",Purple,28);
        Image(new Rect(737,313,56,14),"breach_fluff_text6",Alpha(Purple,.75f),true);
        const float bx=263,by=346,pitch=64.5f;
        var mouse=Event.current.mousePosition;
        bool moved=mouse.x!=lastMouse.x||mouse.y!=lastMouse.y;
        for(int r=0;r<p.Size;r++) for(int c=0;c<p.Size;c++) {
            var cell=new Rect(bx+c*pitch,by+r*pitch,pitch,pitch);
            if(moved&&cell.Contains(mouse)&&p.CanPick(r,c)) { focusRow=r; focusCol=c; }
        }
        lastMouse=mouse;
        if(!p.CanPick(focusRow,focusCol)) for(int i=0;i<p.Size;i++) {
            int r=p.SelectRow?p.ActiveLine:i,c=p.SelectRow?i:p.ActiveLine;
            if(p.CanPick(r,c)) { focusRow=r; focusCol=c; break; }
        }
        if(OpeningAge>=.85f) BreachKeyboard(p);
        var band=p.SelectRow?new Rect(142,by+p.ActiveLine*pitch,696,pitch):new Rect(bx+p.ActiveLine*pitch,337,pitch,467);
        Fill(band,new Color(.27f,.30f,.36f,.64f));
        if(!p.Ended) {
            float sweep=Now*.35f%1;
            if(p.SelectRow) Fill(new Rect(band.x+sweep*(band.width-24),band.y,24,band.height),Alpha(Lime,.035f));
            else Fill(new Rect(band.x,band.y+sweep*(band.height-24),band.width,24),Alpha(Lime,.035f));
        }
        bool hasFocus=mouse.x>=bx&&mouse.x<bx+p.Size*pitch&&mouse.y>=by&&mouse.y<by+p.Size*pitch||keyboardFocus;
        for(int r=0;r<p.Size;r++) for(int c=0;c<p.Size;c++) {
            var cell=new Rect(bx+c*pitch,by+r*pitch,pitch,pitch);
            bool allowed=p.CanPick(r,c),used=p.Used(r,c),focus=hasFocus&&allowed&&r==focusRow&&c==focusCol;
            float settle=Phase(OpeningAge,.25f+r*.045f+c*.012f,.22f);
            float rowOpacity=opacity; opacity*=settle;
            if(focus) {
                Fill(cell,Lime); Frame(new Rect(cell.x-3,cell.y-3,cell.width+6,cell.height+6),Alpha(Cyan,.6f+.4f*Pulse));
            }
            string code=used?"··":settle<1?BreachPuzzle.Symbols[((int)(Now*30)+r*7+c)%BreachPuzzle.Symbols.Length]:p.Matrix[r,c];
            Text(cell,code,focus?Purple:used?Alpha(Lime,.25f):Lime,34,2,TextAnchor.MiddleCenter);
            foreach(var flight in codeFlights) if(flight.Row==r&&flight.Col==c) {
                float flash=1-Phase(Now-flight.Started,0,.4f);
                Fill(cell,Alpha(Cyan,flash*.25f)); Frame(cell,Alpha(Cyan,flash),2);
            }
            if(OpeningAge>=.85f&&allowed&&Click(cell,"code"+r+":"+c)) { keyboardFocus=false; Runtime.Pick(r,c); }
            opacity=rowOpacity;
        }
        Image(new Rect(141,815,118,38),"breach_fluff_protocol2",Lime,true);
        Image(new Rect(345,817,316,25),"breach_fluff_code1",Alpha(Lime,.8f),true);
        Image(new Rect(770,807,69,13),"breach_fluff_barcode1",Lime,true);
        BreachMatrixMotion(p);
        opacity=savedOpacity;
    }
    static bool keyboardFocus;
    static void BreachKeyboard(BreachPuzzle p) {
        var e=Event.current; if(p.Ended||e.type!=EventType.KeyDown) return;
        int direction=e.keyCode is KeyCode.LeftArrow or KeyCode.UpArrow?-1:e.keyCode is KeyCode.RightArrow or KeyCode.DownArrow?1:0;
        if(direction!=0) {
            int current=p.SelectRow?focusCol:focusRow;
            for(int i=1;i<=p.Size;i++) {
                int index=(current+direction*i+p.Size*2)%p.Size;
                int r=p.SelectRow?p.ActiveLine:index,c=p.SelectRow?index:p.ActiveLine;
                if(p.CanPick(r,c)) { focusRow=r; focusCol=c; keyboardFocus=true; Assets.Play("hover"); break; }
            }
            e.Use();
        } else if(e.keyCode is KeyCode.F or KeyCode.Return or KeyCode.KeypadEnter) { Runtime.Pick(focusRow,focusCol); e.Use(); }
    }
    static void BreachPrograms(BreachPuzzle p) {
        float savedOpacity=opacity;
        opacity*=Ease(OpeningAge,.25f,.3f);
        Frame(new Rect(884,287,924,273),Alpha(Lime,.65f));
        Line(884,299,896,287,Lime); Fill(new Rect(884,287,11,1),Purple);
        Image(new Rect(904,298,25,25),"breach_ico_programs",Lime,true);
        Text(new Rect(946,291,770,41),"上传所需序列",Lime,29);
        Image(new Rect(1533,317,75,10),"breach_fluff_header1",Lime,true);
        Image(new Rect(1707,309,52,15),"breach_fluff_text6",Lime,true);
        Line(885,332,1807,332,Alpha(Lime,.65f));
        // Independent optional programs and the required door authorization.
        for(int i=0;i<3;i++) {
            string[] sequence=p.Programs[i];
            int length=sequence.Length;
            bool installed=p.Uploaded(i);
            float y=342+i*65;
            float rowReveal=Ease(ResultAge,1.9f+i*.24f,.24f);
            bool result=p.Ended&&rowReveal>0;
            Color ink=result?Terminal:Lime;
            if(result) BreachCutFill(new Rect(904,y,890*rowReveal,64),installed?Emerald:Salmon);
            else Fill(new Rect(904,y,32,64),new Color(.15f,.135f,.19f,.6f));
            if(result) Text(new Rect(930,y+9,350,44),installed?"已安装":"失败",Alpha(ink,rowReveal),24);
            else {
                int prefix=SequencePrefix(p,sequence);
                float flash=1-Phase(Now-programPulse[i],0,.45f);
                if(flash>0) Fill(new Rect(904,y,890,64),Alpha(Emerald,flash*.22f));
                for(int n=0;n<length;n++) {
                    Text(new Rect(905+n*42,y-3,42,43),sequence[n],installed?Emerald:n<prefix?Lime:Color.white,29,2);
                    if(n==prefix&&!installed) Line(908+n*42,y+38,936+n*42,y+38,Alpha(Lime,Pulse),2);
                }
                if(p.Ended&&ResultAge>.25f) {
                    float progress=Ease(ResultAge,.25f,2.1f);
                    Line(1137,y+44,1137+143*progress,y+44,p.Won?Emerald:Salmon,2);
                }
            }
            Image(new Rect(1299,y+3,60,59),"DoorAuthorization",ink);
            BreachProgramMotion(i,y,installed,result,ink);
            string name=i==0?"网络诊断":i==1?"访问映射":"门禁授权";
            string desc=i==0?"辅助目标：提取门禁连接诊断。":i==1?"辅助目标：解析访问节点映射。":"必要目标：取得当前门禁的开门权限。";
            Text(new Rect(1377,y-1,414,31),name,result?ink:Color.white,24);
            Text(new Rect(1377,y+26,414,32),desc,ink,22);
        }
        Image(new Rect(884,569,205,25),"breach_fluff_text2",Lime,true);
        Image(new Rect(1740,563,68,15),"breach_fluff_barcode1",Lime,true);
        opacity=savedOpacity;
    }
    static void BreachTerminal(BreachPuzzle p) {
        float age=ResultAge,savedOpacity=opacity;
        opacity*=Ease(age,0,.2f);
        Color c=p.Won?Emerald:Salmon;
        Fill(new Rect(174,291,634*Ease(age,0,.18f),39),c);
        float bodyHeight=315*Ease(age,.08f,.24f);
        BreachCutFill(new Rect(175,330,632,Math.Max(12,bodyHeight)),Terminal);
        Line(174,330,808,330,c); Line(174,330,174,330+bodyHeight,c);
        Line(808,330,808,330+bodyHeight-12,c);
        Image(new Rect(190,297,25,25),"breach_fluff_1",Terminal,true);
        Text(new Rect(553,303,180,15),"MODEL LINE     12.12AA",Alpha(Terminal,.8f),8,1);
        Image(new Rect(740,302,42,17),"breach_fluff_text6",Alpha(Terminal,.8f),true);
        var lines=p.Won?terminalSuccess:terminalFailure;
        for(int i=0;i<lines.Length;i++) {
            float start=terminalStarts[i]; if(age<start) continue;
            float y=334+i*24;
            if(p.Won&&i>=3&&i<=6) {
                Text(new Rect(189,y,185,32),Typed("//正在收集封包_"+(i-2),age,start),c,20);
                Text(new Rect(370,y+2,278,30),new string('·',(int)(29*Phase(age,start+.04f,.2f))),c,16,1);
                if(age>=start+.24f) Text(new Rect(653,y,65,32),"完成",c,22);
            } else Text(new Rect(189,y,600,32),Typed(lines[i],age,start),c,22);
        }
        if(age>.32f) {
            float scan=330+Mathf.Repeat(age*.18f,1)*313;
            Fill(new Rect(175,scan,632,1),Alpha(c,age<2.5f?.16f:.045f));
            Fill(new Rect(747,613,13,2),Alpha(c,.25f+.45f*(.5f+.5f*Mathf.Sin(age*3))));
        }
        float complete=Ease(age,2.4f,.3f);
        if(complete>0) {
            BreachCutFill(new Rect(174,645,634*complete,136),c);
            Text(new Rect(175,645,632,136),p.Won?"魔偶已上传":"入侵失败",Alpha(Terminal,complete),25,0,TextAnchor.MiddleCenter);
            Text(new Rect(553,661,180,15),"MODEL LINE     12.12AA",Alpha(Terminal,.8f*complete),8,1);
            Image(new Rect(740,660,42,17),"breach_fluff_text6",Alpha(Terminal,.8f*complete),true);
        }
        opacity*=Ease(age,2.65f,.25f);
        var exit=new Rect(582,798,248,53);
        BreachCutFill(exit,Alpha(c,exit.Contains(Event.current.mousePosition)?.8f:.58f));
        Line(582,798,830,798,c,2); Line(582,798,582,851,c,2);
        Line(582,851,818,851,c,2); Line(818,851,830,839,c,2); Line(830,798,830,839,c,2);
        Text(new Rect(597,801,220,46),"退出界面",c,29);
        if(age>=2.65f) BreachPacket(new Rect(584,800,242,47),Mathf.Repeat((age-2.65f)*.16f,1),Alpha(c,.65f));
        if(age>=2.65f&&Click(exit,"exit-breach")) Runtime.ExitBreach();
        opacity=savedOpacity;
    }
    static void BreachShortcuts() {
        var e=Event.current;
        if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.H) { breachHelp=!breachHelp; e.Use(); }
        else if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.C) { Runtime.ExitBreach(); e.Use(); }
    }
    static void BreachCutFill(Rect r,Color c) {
        const int cut=12;
        Fill(new Rect(r.x,r.y,r.width,r.height-cut),c);
        for(int i=0;i<cut;i++) Fill(new Rect(r.x,r.yMax-cut+i,r.width-i,1),c);
    }
}
