namespace LocalQuickhacks;

// A modifier chord stays suppressed until Ctrl is released.
internal sealed class ScannerGesture {
    double since;
    bool heldBefore,blocked;
    internal bool Step(double now,bool held,bool conflict,double delay) {
        if(!held) { Reset(); return false; }
        if(!heldBefore) { since=now; heldBefore=true; }
        blocked|=conflict;
        return !blocked&&now-since>=delay;
    }
    internal void Cancel()=>blocked=true;
    internal void Reset() { heldBefore=blocked=false; since=0; }
}
