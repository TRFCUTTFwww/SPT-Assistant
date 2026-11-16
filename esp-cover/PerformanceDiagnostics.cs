using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class PerformanceDiagnostics {
 static readonly PerformanceWindow window=new();static readonly Queue<object> slow=new();static float next,lastSlow;static long allocated;static int gc0,gc1,gc2;
 internal static void Frame(double ms,double update,double late,double gui,double render){window.Add(new(ms,update,late,gui,render));if(ms>50&&Time.unscaledTime>=lastSlow){lastSlow=Time.unscaledTime+1;if(slow.Count>=20)slow.Dequeue();slow.Enqueue(new{utc=DateTime.UtcNow,frameMs=ms,assistantCpuMs=update+late+gui+render,updateMs=update,lateMs=late,guiMs=gui,renderMs=render});}if(Time.unscaledTime<next)return;next=Time.unscaledTime+2;var stats=window.Read();long now=GC.GetTotalAllocatedBytes(false);int a=GC.CollectionCount(0),b=GC.CollectionCount(1),c=GC.CollectionCount(2);DiagnosticsPanel.Performance(JsonSerializer.Serialize(new{utc=DateTime.UtcNow,sampleSeconds=stats.Seconds,fps=stats.Fps,frameMs=stats.Frame,p99Ms=stats.P99,peakMs=stats.Peak,slowFrames=stats.Slow,assistantCpuMs=stats.Mod,updateMs=stats.Update,lateMs=stats.Late,guiMs=stats.Gui,renderMs=stats.Render,managedAllocatedBytesSincePrevious=allocated==0?0:now-allocated,gcCollections=new[]{a-gc0,b-gc1,c-gc2},detailed=DiagnosticsPanel.DetailedTiming,features=DiagnosticsPanel.DetailedTiming?RuntimeCosts.Snapshot():Array.Empty<object>(),recentSlowFrames=slow.ToArray(),limitation="CPU elapsed time inside Assistant callbacks only. Native game, other mods and GPU costs are not independently attributed."}));allocated=now;gc0=a;gc1=b;gc2=c;}
 internal static void Reset(){window.Clear();slow.Clear();allocated=0;next=lastSlow=0;}
}
