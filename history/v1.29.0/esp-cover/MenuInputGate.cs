namespace LocalSpt5Esp;
internal sealed class MenuInputGate {
 internal bool Open {get;private set;}
 bool waitingForRelease;
 int closedFrame;
 internal bool Blocked=>Open||waitingForRelease;
 internal void SetOpen(bool value,int frame){Open=value;if(!value){waitingForRelease=true;closedFrame=frame;}}
 internal void Tick(int frame,bool mouseHeld){if(!Open&&waitingForRelease&&frame>closedFrame&&!mouseHeld)waitingForRelease=false;}
 internal void Reset(){Open=false;waitingForRelease=false;}
}
