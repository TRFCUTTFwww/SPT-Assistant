using System;
using System.Diagnostics;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class RuntimePerformance {
 static long updateTicks,lateTicks,guiTicks;static int frames;static double peakMs;static float nextReport;static bool tracing,captured;static float traceUntil;static StackTraceLogType previous;static string? warning;
 internal static long Start()=>Stopwatch.GetTimestamp();
 internal static void UpdateDone(long started){updateTicks+=Stopwatch.GetTimestamp()-started;}
 internal static void GuiDone(long started){guiTicks+=Stopwatch.GetTimestamp()-started;}
 internal static void LateDone(long started){long ticks=Stopwatch.GetTimestamp()-started;lateTicks+=ticks;frames++;peakMs=Math.Max(peakMs,ticks*1000d/Stopwatch.Frequency);float now=Time.unscaledTime;
  if(!captured&&!tracing&&RaidClock.Active){previous=Application.GetStackTraceLogType(LogType.Warning);Application.logMessageReceived += OnLog;Application.SetStackTraceLogType(LogType.Warning,StackTraceLogType.ScriptOnly);tracing=true;traceUntil=now+120;}
  if(warning!=null){Plugin.Self.Log.LogWarning("Physics warning first-source trace: "+warning);warning=null;}
  if(tracing&&now>=traceUntil)StopTrace();
  if(now<nextReport)return;nextReport=now+30;if(RaidClock.Active&&frames>0){double scale=1000d/Stopwatch.Frequency/frames;Plugin.Self.Log.LogInfo($"Mod CPU time per frame: update={updateTicks*scale:F3}ms late={lateTicks*scale:F3}ms gui={guiTicks*scale:F3}ms; late peak={peakMs:F3}ms over {frames} frames; game/GPU time excluded");}updateTicks=lateTicks=guiTicks=0;frames=0;peakMs=0;
 }
 static void OnLog(string message,string stack,LogType type){if(type!=LogType.Warning||!message.Contains("Setting linear velocity of a kinematic body",StringComparison.Ordinal))return;warning=string.IsNullOrWhiteSpace(stack)?"Unity supplied no caller stack":stack;captured=true;StopTrace();}
 static void StopTrace(){if(!tracing)return;Application.logMessageReceived -= OnLog;Application.SetStackTraceLogType(LogType.Warning,previous);tracing=false;captured=true;}
 internal static void Shutdown()=>StopTrace();
}
