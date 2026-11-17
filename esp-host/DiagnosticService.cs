using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssistantDiagnostics;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
namespace LocalSpt5Esp.HotReload;
public sealed class DiagnosticService:ILogListener {
 public LogLevel LogLevelFilter {get;set;}=LogLevel.All;
 readonly string home,game;readonly ConcurrentQueue<string> queue=new();readonly CancellationTokenSource stop=new();readonly Task worker;
 readonly object writeLock=new(),exceptionLock=new();string repeatedException="";long exceptionTicks;int exceptionCount;public Action<string,string,bool>? Fatal;int queued,dropped;bool disposed;Task<string>? exporting;string performance="",hardware="",lastFault="",lastExport="",writtenPerformance="",writtenHardware="";
 public ConfigEntry<string> Minimum {get;private set;}=null!;public ConfigEntry<bool> IncludeHardware {get;private set;}=null!;public ConfigEntry<bool> IncludePerformance {get;private set;}=null!;public ConfigEntry<bool> IncludeSpt {get;private set;}=null!;public ConfigEntry<bool> IncludeGame {get;private set;}=null!;public ConfigEntry<bool> DetailedTiming {get;private set;}=null!;public ConfigEntry<bool> DebugEnabled {get;private set;}=null!;public ConfigEntry<bool> PhysicsTracing {get;private set;}=null!;
 public string ExportStatus {get;private set;}="";
 public ConfigEntry<string> Language {get;private set;}=null!;
 public DiagnosticService(ConfigFile config,string directory){game=BepInEx.Paths.GameRootPath;home=Path.Combine(directory,"runtime","logs");Directory.CreateDirectory(home);
  Rebind(config);
  BepInEx.Logging.Logger.Listeners.Add(this);Application.logMessageReceivedThreaded+=UnityLog;worker=Task.Run(WriteLoop);
 }
 public void Rebind(ConfigFile config){
  Language=config.Bind("24. Interface","Language","zh-CN","界面语言 / Interface language");
  Minimum=config.Bind("67. 日志与诊断","Minimum level","INFO",new ConfigDescription("标准日志最低等级：ALL、TRACE、DEBUG、INFO、WARNING、ERROR、FATAL、OFF。",new AcceptableValueList<string>(LogBundle.Levels)));
  IncludeHardware=config.Bind("67. 日志与诊断","Attach hardware",false,"导出时附加 CPU、GPU、内存、系统与驱动信息，不包含用户名或计算机名。");IncludePerformance=config.Bind("67. 日志与诊断","Attach performance",true,"附加帧时间、辅助功能 CPU 耗时和最近的慢帧记录。");IncludeSpt=config.Bind("67. 日志与诊断","Attach SPT logs",true,"一起打包 SPT 服务端日志；不包含存档和认证文件。");IncludeGame=config.Bind("67. 日志与诊断","Attach game logs",true,"一起打包游戏与 BepInEx 日志。");DetailedTiming=config.Bind("67. 日志与诊断","Detailed feature timing",false,"记录各辅助功能的 CPU 平均耗时与峰值；GPU 时间不归因到某个 MOD。");PhysicsTracing=config.Bind("67. 日志与诊断","Trace physics warnings",false,"调试物理警告时采集调用栈；可能增加开销，默认关闭。");DebugEnabled=config.Bind("68. 调试","Master switch",false,"调试总开关。错误测试均带明显标识，危险原生错误仅作受控模拟，不会自动结束真实战局。");
 }
 public void LogEvent(object sender,LogEventArgs e){if(disposed||e==null)return;string text=e.Data?.ToString()??"";string level=e.Level switch{LogLevel.Fatal=>"FATAL",LogLevel.Error=>"ERROR",LogLevel.Warning=>"WARNING",LogLevel.Debug=>"DEBUG",_=>"INFO"};if(!LogBundle.Include(level,Minimum.Value)||LogBundle.QuietRoute(text)&&LogBundle.Rank(level)<=LogBundle.Rank("INFO"))return;if(e.Source.SourceName is "Unity" or "RequestHandler"&&level=="INFO")return;Enqueue(level,e.Source.SourceName,text);}
 void UnityLog(string message,string stack,LogType type){if(type is not (LogType.Error or LogType.Exception or LogType.Assert))return;Enqueue("ERROR","Unity",message+"\n"+stack);if(type!=LogType.Exception)return;bool repeated;lock(exceptionLock){long now=Environment.TickCount64;if(message!=repeatedException||now-exceptionTicks>2000){repeatedException=message;exceptionCount=0;}exceptionTicks=now;repeated=++exceptionCount==3;}if(repeated)Fatal?.Invoke("持续发生的运行时异常："+message,stack,false);}
 public void Record(string level,string source,string message)=>Enqueue(level,source,message);
 void Enqueue(string level,string source,string message){if(disposed||!LogBundle.Include(level,Minimum.Value)||LogBundle.QuietRoute(message)&&LogBundle.Rank(level)<=LogBundle.Rank("INFO"))return;if(Interlocked.Increment(ref queued)>2048){Interlocked.Decrement(ref queued);Interlocked.Increment(ref dropped);return;}queue.Enqueue(DateTime.UtcNow.ToString("O")+" ["+level+"] ["+source+"] "+LogBundle.Redact(message.Length>16384?message[..16384]+" [truncated]":message));}
 async Task WriteLoop(){while(!stop.IsCancellationRequested){try{Flush();await Task.Delay(1000,stop.Token);}catch(OperationCanceledException){break;}catch{try{await Task.Delay(2000,stop.Token);}catch{break;}}}try{Flush();}catch{}}
 void Flush(){lock(writeLock)FlushLocked();}
 void FlushLocked(){
  string perf=IncludePerformance.Value?Volatile.Read(ref performance):"",hw=IncludeHardware.Value?Volatile.Read(ref hardware):"";
  Persist("performance.json",perf,ref writtenPerformance);Persist("hardware.json",hw,ref writtenHardware);if(queue.IsEmpty)return;string file=Path.Combine(home,"assistant.log");if(File.Exists(file)&&new FileInfo(file).Length>4*1024*1024){for(int n=3;n>=1;n--){string from=n==1?file:Path.Combine(home,"assistant."+(n-1)+".log"),to=Path.Combine(home,"assistant."+n+".log");if(File.Exists(from))File.Move(from,to,true);}}
  using var output=new StreamWriter(new FileStream(file,FileMode.Append,FileAccess.Write,FileShare.ReadWrite),new UTF8Encoding(false));int limit=2048;while(limit-->0&&queue.TryDequeue(out var line)){Interlocked.Decrement(ref queued);output.WriteLine(line);}
 }
 void Persist(string name,string value,ref string written){if(value==written)return;string file=Path.Combine(Path.GetDirectoryName(home)!,name);if(value.Length==0){if(File.Exists(file))File.Delete(file);}else{File.WriteAllText(file+".next",value,new UTF8Encoding(false));File.Move(file+".next",file,true);}written=value;}
 public void Performance(string snapshot)=>Volatile.Write(ref performance,snapshot??"");
 public void Fault(string summary,string details){lastFault=summary+"\n"+details;Enqueue("FATAL","Safety",lastFault);}
 void CaptureHardware(){if(IncludeHardware.Value){try{hardware=JsonSerializer.Serialize(new{os=SystemInfo.operatingSystem,cpu=SystemInfo.processorType,cpuThreads=SystemInfo.processorCount,ramMiB=SystemInfo.systemMemorySize,gpu=SystemInfo.graphicsDeviceName,gpuVendor=SystemInfo.graphicsDeviceVendor,gpuVersion=SystemInfo.graphicsDeviceVersion,vramMiB=SystemInfo.graphicsMemorySize,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),width=Screen.width,height=Screen.height});}catch(Exception e){hardware=JsonSerializer.Serialize(new{error=e.GetType().Name});}}}
 public void BeginExport(){if(exporting!=null)return;if(IncludeHardware.Value)CaptureHardware();
  string level=Minimum.Value,hw=IncludeHardware.Value?hardware:"",perf=IncludePerformance.Value?Volatile.Read(ref performance):"",report=lastFault+"\nDropped diagnostic records: "+dropped;bool spt=IncludeSpt.Value,logs=IncludeGame.Value;ExportStatus="正在导出日志…";
  exporting=Task.Run(()=>{Flush();return LogBundle.Export(game,Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","exports"),level,spt,logs,hw,perf,report);});
 }
 public void Tick(){if(IncludeHardware.Value&&hardware.Length==0)CaptureHardware();if(!IncludeHardware.Value)hardware="";if(exporting?.IsCompleted!=true)return;try{lastExport=exporting.GetAwaiter().GetResult();ExportStatus="日志已导出："+lastExport;}catch(Exception e){ExportStatus="日志导出失败："+e.Message;}exporting=null;}
 public string LastExport=>lastExport;
 public void Dispose(){if(disposed)return;BepInEx.Logging.Logger.Listeners.Remove(this);Application.logMessageReceivedThreaded-=UnityLog;stop.Cancel();try{worker.Wait(2000);}catch{}disposed=true;stop.Dispose();}
}
