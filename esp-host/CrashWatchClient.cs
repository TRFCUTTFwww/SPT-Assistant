using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssistantDiagnostics;
namespace LocalSpt5Esp.HotReload;
// The separate monitor survives a native crash. Launch arguments remain in a
// user-only pipe and process memory, never in the marker or diagnostic bundle.
internal static class CrashWatchClient {
 static readonly object gate=new();static string marker="",summary="",details="";static bool normal,restart;static long startTicks;static DateTime next;static DiagnosticService? diagnostics;
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetCommandLineW();
 internal static void Start(string directory,DiagnosticService service){
  diagnostics=service;string exe=Path.Combine(directory,"Assistant.CrashMonitor.exe");if(!File.Exists(exe)){service.Record("WARNING","Crash monitor","进程外崩溃提示程序未安装；仅提供进程内错误保护。");return;}
  try{using var current=Process.GetCurrentProcess();startTicks=current.StartTime.ToUniversalTime().Ticks;marker=Path.Combine(directory,"runtime","crash-watch-"+current.Id+".json");
   string pipeName="SPT-Assistant-"+Guid.NewGuid().ToString("N");var pipe=new NamedPipeServerStream(pipeName,PipeDirection.Out,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
   string gameExe=current.MainModule!.FileName!,line=Marshal.PtrToStringUni(GetCommandLineW())??"";string arguments=StripExecutable(line);
   var info=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=directory};
   foreach(string argument in new[]{"--pid",current.Id.ToString(),"--start",startTicks.ToString(),"--root",BepInEx.Paths.GameRootPath,"--state",marker,"--pipe",pipeName})info.ArgumentList.Add(argument);
   Write();Process.Start(info)?.Dispose();
   _=Task.Run(async()=>{using(pipe){using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));try{await pipe.WaitForConnectionAsync(timeout.Token);using var writer=new StreamWriter(pipe,new UTF8Encoding(false));await writer.WriteLineAsync(JsonSerializer.Serialize(new{exe=gameExe,arguments,workingDirectory=BepInEx.Paths.GameRootPath}));await writer.FlushAsync();}catch(Exception e){service.Record("WARNING","Crash monitor handshake",e.GetType().Name);}}});
  }catch(Exception e){service.Record("WARNING","Crash monitor",e.GetType().Name+": "+e.Message);}
 }
 static string StripExecutable(string value){value=value.TrimStart();if(value.StartsWith('"')){int end=value.IndexOf('"',1);return end<0?"":value[(end+1)..].TrimStart();}int space=value.IndexOf(' ');return space<0?"":value[(space+1)..].TrimStart();}
 internal static void Tick(){if(DateTime.UtcNow<next)return;next=DateTime.UtcNow.AddSeconds(5);Write();}
 internal static void MarkFatal(string title,string text){lock(gate){summary=LogBundle.Redact(title);details=LogBundle.Redact(text.Length>32768?text[..32768]:text);normal=false;Write();}}
 internal static void ClearFatal(){lock(gate){summary=details="";Write();}}
 internal static void MarkDismissed(){lock(gate){summary=details="";normal=true;Write();}}
 internal static void MarkNormal(){lock(gate){normal=true;Write();}}
 internal static void RequestRestart(){lock(gate){restart=true;normal=true;Write();}}
 static void Write(){if(marker.Length==0)return;lock(gate)try{var settings=diagnostics;var state=new{pid=Environment.ProcessId,startTicks,normal,restart,summary,details,minimum=settings?.Minimum.Value??"INFO",language=settings?.Language.Value??"zh-CN",includeHardware=settings?.IncludeHardware.Value??false,includePerformance=settings?.IncludePerformance.Value??true,includeSpt=settings?.IncludeSpt.Value??true,includeGame=settings?.IncludeGame.Value??true};string temporary=marker+".next";File.WriteAllBytes(temporary,JsonSerializer.SerializeToUtf8Bytes(state));File.Move(temporary,marker,true);}catch{}}
}
