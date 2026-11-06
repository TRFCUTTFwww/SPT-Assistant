using System;
using System.Diagnostics;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class RuntimePerformance {
 static long frameStarted,frameUpdate,frameLate,frameGui,frameRender;
 internal static void Frame(){long now=Stopwatch.GetTimestamp();if(frameStarted!=0){double scale=1000d/Stopwatch.Frequency;PerformanceHud.Frame(Time.frameCount,(now-frameStarted)*scale,new(0,frameUpdate*scale,frameLate*scale,frameGui*scale,frameRender*scale));}frameStarted=now;frameUpdate=frameLate=frameGui=frameRender=0;}
 static long updateTicks,lateTicks,guiTicks,combatTicks;static int frames,combatEdges;static double peakMs,combatPeakMs;static float nextReport;static bool tracing,captured;static float traceUntil;static StackTraceLogType previous;static string? warning;
 internal static long Start()=>Stopwatch.GetTimestamp();
 internal static void UpdateDone(long started){long elapsed=Stopwatch.GetTimestamp()-started;updateTicks+=elapsed;frameUpdate+=elapsed;}
 internal static void GuiDone(long started){long elapsed=Stopwatch.GetTimestamp()-started;guiTicks+=elapsed;frameGui+=elapsed;}
 internal static void RenderDone(long started)=>frameRender+=Stopwatch.GetTimestamp()-started;
 internal static void AiCombatDone(long started,int edges){long ticks=Stopwatch.GetTimestamp()-started;combatTicks+=ticks;combatEdges+=edges;combatPeakMs=Math.Max(combatPeakMs,ticks*1000d/Stopwatch.Frequency);}
 internal static void LateDone(long started){long ticks=Stopwatch.GetTimestamp()-started;lateTicks+=ticks;frameLate+=ticks;frames++;peakMs=Math.Max(peakMs,ticks*1000d/Stopwatch.Frequency);float now=Time.unscaledTime;
  if(!captured&&!tracing&&RaidClock.Active){previous=Application.GetStackTraceLogType(LogType.Warning);Application.logMessageReceived += OnLog;Application.SetStackTraceLogType(LogType.Warning,StackTraceLogType.ScriptOnly);tracing=true;traceUntil=now+120;}
  if(warning!=null){Plugin.Self.Log.LogWarning("Physics warning first-source trace: "+warning);warning=null;}
  if(tracing&&now>=traceUntil)StopTrace();
  if(now<nextReport)return;nextReport=now+30;if(RaidClock.Active&&frames>0){double scale=1000d/Stopwatch.Frequency/frames;Plugin.Self.Log.LogInfo($"Mod CPU time per frame: update={updateTicks*scale:F3}ms late={lateTicks*scale:F3}ms gui={guiTicks*scale:F3}ms; late peak={peakMs:F3}ms over {frames} frames; AI sync={combatTicks*scale:F3}ms/frame peak={combatPeakMs:F3}ms edges={combatEdges}; game/GPU time excluded");}updateTicks=lateTicks=guiTicks=0;frames=0;peakMs=combatPeakMs=0;combatTicks=0;combatEdges=0;
 }
 static void OnLog(string message,string stack,LogType type){if(type!=LogType.Warning||!message.Contains("Setting linear velocity of a kinematic body",StringComparison.Ordinal))return;warning=string.IsNullOrWhiteSpace(stack)?"Unity supplied no caller stack":stack;captured=true;StopTrace();}
 static void StopTrace(){if(!tracing)return;Application.logMessageReceived -= OnLog;Application.SetStackTraceLogType(LogType.Warning,previous);tracing=false;captured=true;}
 internal static void Shutdown(){StopTrace();frameStarted=frameUpdate=frameLate=frameGui=frameRender=0;PerformanceHud.Reset();}
}
