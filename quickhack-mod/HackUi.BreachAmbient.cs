using UnityEngine;

namespace LocalQuickhacks;

internal static partial class HackUi {
    static readonly string[] terminalSuccess=["//ROOT","//访问_请求","//访问_请求_成功","","","","","//登录","//登录_成功","//","//正在_上传","//上传_完成!"];
    static readonly string[] terminalFailure=["//ROOT","//访问_请求","//访问_请求_拒绝","//","//协议_中断","//授权_未提交","//","//连接_已终止"];

    // Small procedural overlays only: no new textures, materials or objects.
    // Decorative motion follows real time and never controls a click target.
    static void BreachPacket(Rect r,float phase,Color color) {
        if(Event.current.type!=EventType.Repaint) return;
        float perimeter=2*(r.width+r.height);
        for(int i=0;i<4;i++) {
            float d=Mathf.Repeat(phase*perimeter-i*7,perimeter),x,y;
            if(d<r.width) {x=r.x+d;y=r.y;}
            else if((d-=r.width)<r.height) {x=r.xMax;y=r.y+d;}
            else if((d-=r.height)<r.width) {x=r.xMax-d;y=r.yMax;}
            else {d-=r.width;x=r.x;y=r.yMax-d;}
            Fill(new Rect(x-1,y-1,3,3),Alpha(color,1-i*.22f));
        }
    }
    static void BreachReadout(Rect r,float offset,Color color) {
        if(Event.current.type!=EventType.Repaint) return;
        float phase=Mathf.Repeat(OpeningAge*.22f+offset,1);
        for(int i=0;i<3;i++) {
            float x=r.x+Mathf.Repeat(phase-i*.035f,1)*(r.width-3);
            Fill(new Rect(x,r.y,3,r.height),Alpha(color,.26f-i*.07f));
        }
    }
    static void BreachAmbient(BreachPuzzle p) {
        if(Event.current.type!=EventType.Repaint||OpeningAge<.72f) return;
        float saved=opacity;
        opacity*=Ease(OpeningAge,.72f,.4f);
        Color c=p.Ended?(p.Won?Emerald:Salmon):Lime;
        float calm=p.Ended?.42f:1;
        BreachPacket(new Rect(124,112,1672,864),Mathf.Repeat(OpeningAge*.075f,1),Alpha(c,.7f*calm));
        BreachReadout(new Rect(124,982,206,26),0,c);
        BreachReadout(new Rect(1228,976,568,15),.5f,c);
        BreachReadout(new Rect(884,118,196,7),.3f,c);
        // Activity traces stay in the empty side gutters, away from real codes.
        for(int i=0;i<6;i++) {
            float y=337+Mathf.Repeat(OpeningAge*32+i*78,462);
            Fill(new Rect(185+i%2*8,y,3,10+i%3*6),Alpha(c,(.08f+i*.018f)*calm));
            float h=5+13*(.5f+.5f*Mathf.Sin(OpeningAge*1.8f+i));
            Fill(new Rect(1510+i*13,90-h,5,h),Alpha(c,.3f*calm));
        }
        opacity=saved;
    }
    static void BreachTimerMotion(BreachPuzzle p) {
        if(Event.current.type!=EventType.Repaint||p.Ended) return;
        float remaining=(float)p.Remaining(Now),width=572*remaining/(float)p.Duration;
        if(width>2) {
            float x=142+Mathf.Repeat(OpeningAge*.2f,1)*(width-2);
            Fill(new Rect(x,240,2,9),Alpha(Cyan,.7f));
            Fill(new Rect(142+width-2,238,2,13),Alpha(Cyan,.35f+.25f*Pulse));
        }
        if(p.Started.HasValue&&remaining<=10) {
            float warning=.5f+.5f*Mathf.Sin(OpeningAge*3.5f);
            Frame(new Rect(604,187,112,47),Alpha(Salmon,.25f+.4f*warning),2);
        }
    }
    static void BreachBufferMotion(BreachPuzzle p,int index,Rect cell) {
        if(Event.current.type!=EventType.Repaint) return;
        if(!p.Ended&&index==p.Buffer.Count) {
            float pulse=.5f+.5f*Mathf.Sin(OpeningAge*2.7f);
            Frame(new Rect(cell.x-3,cell.y-3,38,38),Alpha(Cyan,.25f+.35f*pulse));
            Fill(new Rect(cell.x+6,cell.yMax+7,20,2),Alpha(Lime,.35f+.4f*pulse));
        }
        foreach(var flight in codeFlights) if(flight.Index==index) {
            float age=Now-flight.Started-.28f;
            if(age<0||age>.42f) continue;
            float t=age/.42f;
            Frame(new Rect(cell.x-2-5*t,cell.y-2-5*t,cell.width+4+10*t,cell.height+4+10*t),Alpha(Cyan,(1-t)*.75f),1);
            Fill(new Rect(cell.x,cell.y,cell.width,cell.height),Alpha(Cyan,(1-t)*.12f));
        }
    }
    static void BreachMatrixMotion(BreachPuzzle p) {
        if(Event.current.type!=EventType.Repaint) return;
        BreachReadout(new Rect(345,817,316,25),.2f,Lime);
        BreachReadout(new Rect(770,807,69,13),.6f,Lime);
        float flash=1-Phase(Now-pickedAt,0,.32f);
        if(!p.Ended&&flash>0) {
            const float pitch=64.5f;
            var band=p.SelectRow?new Rect(142,346+p.ActiveLine*pitch,696,pitch):new Rect(263+p.ActiveLine*pitch,337,pitch,467);
            Frame(band,Alpha(Cyan,flash*.5f));
        }
    }
    static void BreachProgramMotion(int index,float y,bool installed,bool result,Color ink) {
        if(Event.current.type!=EventType.Repaint) return;
        if(result) {
            float reveal=Ease(ResultAge,1.9f+index*.24f,.24f);
            float x=904+890*reveal;
            if(reveal<1) Fill(new Rect(x-2,y+2,2,58),Alpha(Lime,.7f));
            return;
        }
        Color c=installed?Emerald:Lime;
        float pulse=.5f+.5f*Mathf.Sin(OpeningAge*1.8f-index*.8f);
        // Packets flow through the whitespace between sequence and program icon.
        Line(1080,y+25,1280,y+25,Alpha(c,.12f));
        float phase=Mathf.Repeat(OpeningAge*(installed?.18f:.32f)+index*.3f,1);
        for(int i=0;i<3;i++) {
            float x=1080+Mathf.Repeat(phase-i*.06f,1)*194;
            Fill(new Rect(x,y+23,6,4),Alpha(c,.5f-i*.13f));
        }
        Frame(new Rect(1301,y+5,56,55),Alpha(c,.12f+.16f*pulse));
        BreachPacket(new Rect(1301,y+5,56,55),Mathf.Repeat(OpeningAge*.2f+index*.33f,1),Alpha(c,.65f));
        Fill(new Rect(1799,y+8,3,46),Alpha(ink,.08f+.12f*pulse));
    }
}
