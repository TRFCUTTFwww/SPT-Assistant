namespace LocalQuickhacks;

// Each target has one upload in progress and up to three waiting programs.
// A completed entry is removed before its effect runs, so it cannot fire twice.
internal sealed class UploadQueue<T> where T:notnull {
    internal sealed class Entry(T target,HackKind kind,float paid,float started) {
        internal T Target=target;
        internal HackKind Kind=kind;
        internal float Paid=paid,Started=started;
        internal float Completed=-1,PromotedAt=float.NegativeInfinity;
        internal float Elapsed(float now)=>Math.Max(0,now-Started);
        internal float Progress(float now)=>Math.Clamp(Elapsed(now)/HackRules.Upload(Kind),0,1);
    }
    readonly Dictionary<T,List<Entry>> targets=[];
    internal const int PerTargetLimit=4,TotalLimit=32;
    internal static float HandoffDuration=>UploadAnimation.CompleteDuration+UploadAnimation.QueueMoveDuration;
    internal int Count=>targets.Values.Sum(q=>q.Count);
    internal T[] Targets=>targets.Keys.ToArray();
    internal Entry[] For(T? target)=>target!=null&&targets.TryGetValue(target,out var q)?q.ToArray():[];
    internal Entry? Current(T? target)=>target!=null&&targets.TryGetValue(target,out var q)?q[0]:null;
    internal bool TryAdd(T target,HackKind kind,float paid,float now) {
        if(Count>=TotalLimit) return false;
        if(!targets.TryGetValue(target,out var q)) targets[target]=q=[];
        if(q.Count>=PerTargetLimit) return false;
        q.Add(new(target,kind,paid,q.Count==0?now:float.PositiveInfinity));
        return true;
    }
    internal Entry? Complete(T target,float now) {
        var entry=Current(target);
        if(entry==null) return null;
        if(entry.Completed>=0) {
            if(now-entry.Completed<UploadAnimation.CompleteDuration) return null;
            var q=targets[target]; q.RemoveAt(0);
            if(q.Count==0) targets.Remove(target);
            else { q[0].PromotedAt=now; q[0].Started=now+UploadAnimation.QueueMoveDuration; }
            return null;
        }
        if(entry.Progress(now)<1) return null;
        entry.Completed=now;
        return entry;
    }
    internal float Cancel(T target) {
        if(!targets.Remove(target,out var q)) return 0;
        return q.Where(e=>e.Completed<0).Sum(e=>e.Paid);
    }
    internal float Clear() { float paid=targets.Values.SelectMany(q=>q).Where(e=>e.Completed<0).Sum(e=>e.Paid); targets.Clear(); return paid; }
}
