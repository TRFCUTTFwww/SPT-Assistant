using System;
using System.Collections.Generic;
using System.Diagnostics;
namespace LocalSpt5Esp.HotReload;

// One indivisible Unity operation per frame. An operation is never moved to a
// worker merely to meet a time budget: native Unity/Harmony calls need this thread.
internal sealed class ReloadSequence:IDisposable {
 readonly IEnumerator<Action> steps;
 internal int Completed {get;private set;}
 internal double PeakMs {get;private set;}
 internal ReloadSequence(IEnumerable<Action> source)=>steps=source.GetEnumerator();
 internal bool Advance(){if(!steps.MoveNext())return false;long start=Stopwatch.GetTimestamp();try{steps.Current();Completed++;return true;}finally{PeakMs=Math.Max(PeakMs,Stopwatch.GetElapsedTime(start).TotalMilliseconds);}}
 public void Dispose()=>steps.Dispose();
}
