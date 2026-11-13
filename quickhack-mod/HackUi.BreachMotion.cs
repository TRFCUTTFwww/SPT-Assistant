using UnityEngine;

namespace LocalQuickhacks;

internal static partial class HackUi {
    sealed record CodeFlight(int Index,int Row,int Col,string Code,float Started);
    static readonly List<CodeFlight> codeFlights=[];
    static readonly float[] programPulse=[-100,-100,-100];
    static readonly bool[] programMatched=[false,false,false];
    static readonly float[] terminalStarts=[.15f,.25f,.42f,.62f,.80f,.98f,1.16f,1.38f,1.52f,1.68f,1.86f,2.35f];
    static float resultAt=-100;
    static int resultSoundStage;
    static float OpeningAge=>Now-Runtime.ModeStarted;
    static float ResultAge=>Now-resultAt-.32f;
    static float Phase(float age,float start,float duration)=>Math.Clamp((age-start)/duration,0,1);
    static float Ease(float age,float start,float duration) { float t=Phase(age,start,duration); return t*t*(3-2*t); }
    static string Typed(string text,float age,float start,float speed=95)=>text[..Math.Clamp((int)((age-start)*speed),0,text.Length)];
    static void ResetBreachMotion() {
        codeFlights.Clear(); resultAt=-100; resultSoundStage=0;
        for(int i=0;i<3;i++) { programPulse[i]=-100; programMatched[i]=false; }
    }
    static void ObserveBreach(BreachPuzzle p) {
        while(shownPicks<p.Picks.Count) {
            int index=shownPicks++; var cell=p.Picks[index];
            codeFlights.Add(new CodeFlight(index,cell.Row,cell.Col,p.Buffer[index],Now));
            pickedAt=Now;
        }
        codeFlights.RemoveAll(f=>Now-f.Started>.75f);
        if(p.Ended&&resultAt<0) resultAt=Now;
        for(int i=0;i<3;i++) {
            bool matched=p.Uploaded(i);
            if(matched&&!programMatched[i]) programPulse[i]=Now;
            programMatched[i]=matched;
        }
        // Sound cues advance on the render event only, once per visual stage.
        if(p.Ended&&Event.current.type==EventType.Repaint) {
            int stage=ResultAge>=2.75f?4:ResultAge>=1.5f?3:ResultAge>=1?2:ResultAge>=.6f?1:0;
            if(stage>resultSoundStage) {
                resultSoundStage=stage;
                Assets.Play(stage==4?(p.Won?"granted":"denied"):"hover");
            }
        }
    }
    static bool BufferLanded(int index)=>!codeFlights.Any(f=>f.Index==index&&Now-f.Started<.28f);
    static Rect BufferCell(int index)=>new(905+index*42,197,32,32);
    static void BufferCode(Rect cell,string code,int size=27) {
        // Shared optical alignment for resting codes and the arriving flight.
        Text(new Rect(cell.x,cell.y-3,cell.width,cell.height),code,Lime,size,2,TextAnchor.MiddleCenter);
    }
    static void DrawCodeFlights() {
        foreach(var flight in codeFlights) {
            float age=Now-flight.Started;
            if(age>=.28f) continue;
            float t=Ease(age,0,.28f);
            float sx=295.25f+flight.Col*64.5f,sy=378.25f+flight.Row*64.5f;
            var destination=BufferCell(flight.Index);
            float ex=destination.center.x,ey=destination.center.y;
            float x=sx+(ex-sx)*t,y=sy+(ey-sy)*t-Mathf.Sin(t*Mathf.PI)*72;
            for(int trail=1;trail<=3;trail++) {
                float k=Math.Max(0,t-trail*.045f);
                float tx=sx+(ex-sx)*k,ty=sy+(ey-sy)*k-Mathf.Sin(k*Mathf.PI)*72;
                Fill(new Rect(tx-16,ty-16,32,32),Alpha(Cyan,(4-trail)*.035f));
            }
            var cell=new Rect(x-25+t*9,y-27+t*11,50-t*18,54-t*22);
            Fill(cell,Purple); Frame(cell,Alpha(Cyan,.85f),2);
            BufferCode(cell,flight.Code,(int)(34-7*t));
        }
    }
    static void BreachSweep(BreachPuzzle p) {
        float opening=Phase(OpeningAge,0,.72f);
        if(opening<1) {
            float y=112+864*opening;
            Fill(new Rect(124,y,1672,2),Alpha(Cyan,.7f));
            Fill(new Rect(124,y-18,1672,18),Alpha(Lime,.06f));
        }
        if(resultAt>0&&ResultAge<.18f) {
            float fade=1-Phase(ResultAge,-.32f,.5f);
            for(int i=0;i<5;i++) {
                float offset=Mathf.Sin(Now*70+i*9)*27*fade;
                Fill(new Rect(141+offset,346+i*88,698,3+i%2*2),Alpha(p.Lost?Salmon:Lime,fade*.4f));
            }
        }
    }
    static int SequencePrefix(BreachPuzzle p,string[] sequence) {
        int length=sequence.Length;
        for(int n=Math.Min(length,p.Buffer.Count);n>0;n--) {
            bool same=true;
            for(int i=0;i<n;i++) if(p.Buffer[p.Buffer.Count-n+i]!=sequence[i]) { same=false; break; }
            if(same) return n;
        }
        return 0;
    }
}
