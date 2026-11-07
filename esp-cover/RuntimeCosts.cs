using System;
using System.Diagnostics;
using System.Text;
namespace LocalSpt5Esp;

internal enum CostStage { General, Heartbeat, Bitcoin, Loot, Weapons, Containers, Combat, Exits, Navigation, AiTools, World, Tint, Scan }
// Fixed counters; no delegates, closures or per-frame formatting on the hot path.
internal static class RuntimeCosts {
 static readonly long[] ticks=new long[Enum.GetValues<CostStage>().Length],peaks=new long[ticks.Length];
 internal static void Mark(ref long stamp,CostStage stage){long now=Stopwatch.GetTimestamp(),elapsed=now-stamp;int i=(int)stage;ticks[i]+=elapsed;peaks[i]=Math.Max(peaks[i],elapsed);stamp=now;}
 internal static void Report(int frames){
  if(frames>=30){double scale=1000d/Stopwatch.Frequency;var text=new StringBuilder("CPU stages per frame (average/peak ms): ");
   for(int i=0;i<ticks.Length;i++)if(ticks[i]>0)text.Append((CostStage)i).Append('=').Append((ticks[i]*scale/frames).ToString("F3")).Append('/').Append((peaks[i]*scale).ToString("F3")).Append(' ');
   Plugin.Self.Log.LogInfo(text.ToString());
  }
  Array.Clear(ticks);Array.Clear(peaks);
 }
}
