namespace ProjectE.Client;

// One native damage call can kill a critical part, invoke Kill, then spread
// its remaining damage to other parts. Preserve one rescue across that entire
// same-frame chain, including reentrant health/inventory event callbacks.
internal sealed class TotemRescueGate {
 long health;
 int savedFrame=-1;
 bool restoring;
 internal bool Blocks(long candidate,int frame)=>candidate!=0&&candidate==health&&(restoring||savedFrame>=0&&savedFrame==frame);
 internal void Begin(long candidate){health=candidate;savedFrame=-1;restoring=true;}
 internal void Commit(int frame)=>savedFrame=frame;
 internal void End(){restoring=false;}
 internal void Clear(){health=0;savedFrame=-1;restoring=false;}
 internal static bool Bindable(bool local,bool totem,bool directPocketOrVest)=>local&&totem&&directPocketOrVest;
}
