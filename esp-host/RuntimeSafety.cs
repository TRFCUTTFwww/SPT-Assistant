using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using UnityEngine;
namespace LocalSpt5Esp.HotReload;
public sealed partial class RuntimeReloader {
 public DiagnosticService Diagnostics {get;private set;}=null!;
 Task<Prepared>? rollbackPreparing;byte[]? previousGood;string swapStage="idle",fatalDetails="",recoveryStatus="";bool reloadFault,cleanupUnsafe,simulation,rescueAttempted,safetyStarted;
 readonly ConcurrentQueue<(string Summary,string Details,bool Simulation)> faultQueue=new();
 public bool SafetyBlocked=>pause!=null&&failure.Length>0;
 public void ReportFatal(string summary,string details,bool simulated=false)=>faultQueue.Enqueue((summary,details,simulated));
 public void SetPerformanceSnapshot(string json)=>Diagnostics.Performance(json);
 public string[] DiagnosticSettings()=>new[]{Diagnostics.Minimum.Value,Diagnostics.IncludeHardware.Value.ToString(),Diagnostics.IncludePerformance.Value.ToString(),Diagnostics.IncludeSpt.Value.ToString(),Diagnostics.IncludeGame.Value.ToString(),Diagnostics.DetailedTiming.Value.ToString(),Diagnostics.DebugEnabled.Value.ToString(),Diagnostics.PhysicsTracing.Value.ToString()};
 public void SetDiagnosticOption(string key,string value){switch(key){case "level":if(Array.IndexOf(AssistantDiagnostics.LogBundle.Levels,value)<0)throw new ArgumentException("日志等级无效");Diagnostics.Minimum.Value=value;break;case "hardware":Diagnostics.IncludeHardware.Value=bool.Parse(value);break;case "performance":Diagnostics.IncludePerformance.Value=bool.Parse(value);break;case "spt":Diagnostics.IncludeSpt.Value=bool.Parse(value);break;case "game":Diagnostics.IncludeGame.Value=bool.Parse(value);break;case "timing":Diagnostics.DetailedTiming.Value=bool.Parse(value);break;case "debug":Diagnostics.DebugEnabled.Value=bool.Parse(value);break;case "physics":Diagnostics.PhysicsTracing.Value=bool.Parse(value);break;default:throw new ArgumentException("未知诊断设置");}}
 public void ExportLogs()=>Diagnostics.BeginExport();
 public string DiagnosticExportStatus=>Diagnostics.ExportStatus;
 public void SetRecoveryStatus(string text)=>recoveryStatus=text;
 void StartSafety(){if(safetyStarted)return;safetyStarted=true;Diagnostics=new(Config,PluginDirectory);Diagnostics.Fatal=ReportFatal;if(File.Exists(fallback))try{lastGood=ReadPayload(fallback);}catch(Exception e){Diagnostics.Record("WARNING","Safety",e.Message);}string previous=Path.Combine(PluginDirectory,"runtime","previous-good.module");if(File.Exists(previous))try{previousGood=ReadPayload(previous);}catch(Exception e){Diagnostics.Record("WARNING","Safety",e.Message);}AppDomain.CurrentDomain.UnhandledException+=Unhandled;TaskScheduler.UnobservedTaskException+=Unobserved;CrashWatchClient.Start(PluginDirectory,Diagnostics);}
 void Unhandled(object sender,UnhandledExceptionEventArgs args){var error=args.ExceptionObject as Exception;string text=error?.ToString()??args.ExceptionObject?.ToString()??"未知运行时错误";Diagnostics.Fault("未处理异常",text);CrashWatchClient.MarkFatal("未处理异常",text);}
 void Unobserved(object? sender,UnobservedTaskExceptionEventArgs args)=>Diagnostics.Record("ERROR","Task",args.Exception.ToString());
 void SafetyTick(){Diagnostics.Tick();CrashWatchClient.Tick();while(faultQueue.TryDequeue(out var fault)){if(SafetyBlocked){Diagnostics.Record("ERROR","Safety",fault.Details);continue;}reloadFault=false;simulation=fault.Simulation;rescueAttempted=false;recoveryStatus=simulation?"调试模拟：不会保存或结束真实战局。":"正在检查战局救援条件…";fatalDetails=fault.Details;failure=fault.Summary;phase="游戏已暂停，等待修复或回退";pause??=new ReloadPause();Diagnostics.Fault(failure,fatalDetails);CrashWatchClient.MarkFatal(failure,fatalDetails);}
  if(!SafetyBlocked)return;
  if(!reloadFault&&!simulation&&!rescueAttempted&&module!=null&&swapStage=="idle"){rescueAttempted=true;try{var method=module.GetType().GetMethod("EmergencySurvive",BindingFlags.Instance|BindingFlags.Public);recoveryStatus=method?.Invoke(module,null) as string??"当前功能版本不支持战局救援；请先导出日志。";}catch(Exception e){recoveryStatus="无法提交幸存结算："+e.GetBaseException().Message;Diagnostics.Record("ERROR","Recovery",e.ToString());}}
  if(!simulation&&rescueAttempted)try{module?.GetType().GetMethod("EmergencyTick")?.Invoke(module,null);}catch(Exception e){Diagnostics.Record("WARNING","Recovery",e.GetBaseException().Message);}
 }
 void SuspendedPoll(){
  if(rollbackPreparing!=null){ContinueRollback();return;}
  if(observing!=null){if(!observing.IsCompleted)return;var done=observing;observing=null;try{var observation=done.GetAwaiter().GetResult();observedStamp=observation.Stamp;observedLength=observation.Length;seenRequest=observation.Request;if(observation.Prepared!=null){if(cleanupUnsafe){phase="旧模块清理失败，已阻止再次切换；请导出日志并重启。";observation.Prepared.Context.Release();return;}Begin(observation.Prepared);return;}}catch(Exception e){fatalDetails=e.ToString();Diagnostics.Record("ERROR","Reload preflight",fatalDetails);}return;}
  if(DateTime.UtcNow<nextPoll)return;nextPoll=DateTime.UtcNow.AddSeconds(1);try{var file=new FileInfo(payload);var requestTime=File.Exists(request)?File.GetLastWriteTimeUtc(request):DateTime.MinValue;bool changed=file.LastWriteTimeUtc!=observedStamp||file.Length!=observedLength||requestTime!=seenRequest||manual;if(!changed)return;manual=false;var stamp=observedStamp;var length=observedLength;var requestStamp=seenRequest;observedStamp=file.LastWriteTimeUtc;observedLength=file.Length;seenRequest=requestTime;observing=Task.Run(()=>Observe(true,stamp,length,requestStamp,loadedHash));}catch(Exception e){Diagnostics.Record("ERROR","Reload watcher",e.Message);}
 }
 void SafetyFailure(Exception error){
  reloadFault=true;Report(error);sequence?.Dispose();sequence=null;persistence=null;simulation=false;rescueAttempted=false;
  cleanupUnsafe|=swapStage=="unloading";failure="热更新失败："+error.GetBaseException().Message;fatalDetails=error.ToString();phase=cleanupUnsafe?"模块清理未完成，不能保证安全回退；请重启游戏。":"已暂停。可以回退上一版本，或等待新的修复模块。";
  pause??=new ReloadPause();Config.SaveOnConfigSet=autoSave;Diagnostics.Fault(failure,fatalDetails);CrashWatchClient.MarkFatal(failure,fatalDetails);nextPoll=DateTime.UtcNow.AddSeconds(1);
  try{observedStamp=File.GetLastWriteTimeUtc(payload);observedLength=new FileInfo(payload).Length;seenRequest=File.Exists(request)?File.GetLastWriteTimeUtc(request):DateTime.MinValue;}catch{}
  var state=StatusBytes(failure);_=Task.Run(()=>AtomicWrite(status,state));
 }
 public void RollbackPreviousVersion()=>RequestRollback();
 void RequestRollback(){if(cleanupUnsafe||rollbackPreparing!=null||sequence!=null||persistence!=null)return;var bytes=candidate!=null||swapStage!="idle"?lastGood:previousGood??lastGood;if(bytes==null){phase="没有可回退的正常版本。";return;}phase="正在后台校验回退版本…";rollbackPreparing=Task.Run(()=>Prepare(bytes));}
 void ContinueRollback(){if(rollbackPreparing?.IsCompleted!=true)return;var pendingRollback=rollbackPreparing;rollbackPreparing=null;try{var prepared=pendingRollback.GetAwaiter().GetResult();rollingBack=true;candidate=prepared;painted=stepCount=0;peakMs=0;phase="正在清理当前模块并回退…";sequence=new ReloadSequence(Swap(prepared));}catch(Exception e){SafetyFailure(e);}}
 bool RecoveryBusy=>!simulation&&rescueAttempted&&module?.GetType().GetProperty("EmergencyBusy")?.GetValue(module) is true;
 void DrawSafety(){
  if(Event.current.type==EventType.Repaint)painted++;Diagnostics.Tick();int oldDepth=GUI.depth;var oldColor=GUI.color;GUI.depth=-100000;
  try{Color tint=new(.10f,.23f,.30f,.92f);try{if(Config.TryGetEntry<string>("24. Interface","Panel accent",out var accent)&&ColorUtility.TryParseHtmlString(accent.Value,out var color))tint=Color.Lerp(new Color(.03f,.06f,.10f,.92f),color,.24f);}catch{}tint.a=.90f;GUI.color=tint;GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;
   float width=Math.Min(1000,Screen.width-80),x=(Screen.width-width)/2,y=Math.Max(25,Screen.height*.10f);var title=new GUIStyle(GUI.skin.label){fontSize=90};var text=new GUIStyle(GUI.skin.label){fontSize=18,wordWrap=true};var small=new GUIStyle(text){fontSize=14};
   GUI.Label(new Rect(x,y,width,125),":(",title);y+=130;GUI.Label(new Rect(x,y,width,58),simulation?"调试模拟 · 游戏错误保护界面":"游戏遇到了错误，运行已暂停",text);y+=64;GUI.Label(new Rect(x,y,width,68),failure,text);y+=74;GUI.Label(new Rect(x,y,width,58),phase,small);y+=64;GUI.Label(new Rect(x,y,width,68),recoveryStatus,small);y+=74;
   string detail=fatalDetails.Length>1000?fatalDetails[..1000]+"…":fatalDetails;GUI.Label(new Rect(x,y,width,Math.Min(130,Screen.height*.16f)),detail,small);y+=Math.Min(140,Screen.height*.17f);
   bool previous=GUI.enabled;GUI.enabled=sequence==null&&persistence==null;float cell=(width-36)/4;if(GUI.Button(new Rect(x,y,cell,42),"导出日志"))Diagnostics.BeginExport();GUI.enabled=previous&&sequence==null&&persistence==null&&!cleanupUnsafe&&!RecoveryBusy&&rollbackPreparing==null&&(lastGood!=null||previousGood!=null);if(GUI.Button(new Rect(x+cell+12,y,cell,42),"回退上一版本"))RequestRollback();GUI.enabled=previous&&sequence==null&&persistence==null&&!RecoveryBusy;if(GUI.Button(new Rect(x+2*(cell+12),y,cell,42),"重启游戏")){CrashWatchClient.RequestRestart();Application.Quit();}if(GUI.Button(new Rect(x+3*(cell+12),y,cell,42),"关闭游戏")){CrashWatchClient.MarkDismissed();Application.Quit();}GUI.enabled=previous;y+=54;
   if(simulation&&sequence==null&&GUI.Button(new Rect(x,y,220,36),"结束调试模拟并继续")){failure="";fatalDetails="";simulation=false;CrashWatchClient.ClearFatal();EndPause();}
   GUI.Label(new Rect(x,y+45,width,55),Diagnostics.ExportStatus,small);if(Event.current.type is EventType.KeyDown or EventType.KeyUp or EventType.MouseDown or EventType.MouseUp or EventType.ScrollWheel)Event.current.Use();
  }finally{GUI.color=oldColor;GUI.depth=oldDepth;}
 }
 public void SimulateError(string type){if(!Diagnostics.DebugEnabled.Value)throw new InvalidOperationException("请先开启调试总开关。");try{switch(type){case "null":object? value=null;_ = value!.ToString();break;case "bounds":var values=new int[1];_ = values[2];break;case "stack":throw new StackOverflowException("受控模拟：没有真实耗尽线程栈。");case "access":throw new AccessViolationException("受控模拟：没有执行非法内存访问。");case "reload":SafetyFailure(new InvalidOperationException("受控模拟：热更新初始化失败。"));simulation=true;return;default:throw new InvalidOperationException("受控模拟错误。");}}catch(Exception e){ReportFatal("[调试] "+e.GetType().Name,e.ToString(),true);}}
 void StopSafety(){AppDomain.CurrentDomain.UnhandledException-=Unhandled;TaskScheduler.UnobservedTaskException-=Unobserved;CrashWatchClient.MarkNormal();Diagnostics.Dispose();}
}
