namespace ProjectE.Client;

// One native damage call can kill a critical part, invoke Kill, then spread
// its remaining damage to other parts. Preserve one rescue across that entire
// same-frame chain, including reentrant health/inventory event callbacks, then
// preserve a configurable real-time recovery window against subsequent hits.
internal sealed class TotemRescueGate {
 long health;
 int savedFrame=-1;
 bool restoring;
 double started,protectedUntil;
 internal bool Blocks(long candidate,int frame,double now=0)=>candidate!=0&&candidate==health&&(restoring||savedFrame>=0&&savedFrame==frame||Timed(now));
 internal bool BlocksHealthChange(long candidate,int frame,double now,float amount)=>amount<0&&Blocks(candidate,frame,now);
 internal bool Timed(double now)=>double.IsFinite(now)&&now>=started&&now<protectedUntil;
 internal bool Busy(double now)=>restoring||Timed(now);
 internal double Remaining(double now)=>Timed(now)?protectedUntil-now:0;
 internal static double Duration(double seconds)=>double.IsFinite(seconds)&&seconds>0?seconds:0;
 internal void Begin(long candidate){health=candidate;savedFrame=-1;started=protectedUntil=0;restoring=true;}
 internal void Commit(int frame,double now=0,double seconds=0){savedFrame=frame;started=double.IsFinite(now)?now:0;protectedUntil=started+Duration(seconds);}
 internal void End(){restoring=false;}
 internal void Clear(){health=0;savedFrame=-1;restoring=false;started=protectedUntil=0;}
 internal static bool Bindable(bool local,bool totem,bool directPocketOrVest)=>local&&totem&&directPocketOrVest;
}
