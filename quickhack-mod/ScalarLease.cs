namespace LocalQuickhacks;

// Restore only a value that this scanner still owns. External pause/speed changes win.
internal sealed class ScalarLease {
    bool engaged,owned;
    float original,applied;
    internal float Update(bool enabled,float current,float factor) {
        if(!enabled) {
            float restored=owned&&Math.Abs(current-applied)<.00001f?original:current;
            engaged=owned=false; return restored;
        }
        if(!engaged) { engaged=true; original=current; applied=current; owned=current>0&&float.IsFinite(current); }
        if(!owned) return current;
        if(Math.Abs(current-applied)>.00001f) { owned=false; return current; }
        applied=original*Math.Clamp(factor,.02f,1); return applied;
    }
}
