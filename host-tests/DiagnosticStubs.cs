// OS logging is stubbed; host and reload lifecycle use production code.
using BepInEx.Configuration;
namespace LocalSpt5Esp.HotReload;
public sealed class DiagnosticService:IDisposable {
 public int FrameErrors;
 public ConfigEntry<string> Minimum=new("INFO"),Language=new("zh-CN");
 public ConfigEntry<bool> IncludeHardware=new(false),IncludePerformance=new(true),IncludeSpt=new(true),IncludeGame=new(true),DetailedTiming=new(false),PhysicsTracing=new(false);
 public string LastExport=>"";
 public string ExportStatus=>"test export";
 public DiagnosticService(ConfigFile config,string directory){}
 public void Rebind(ConfigFile config){}
 public void Record(string level,string source,string message){if(level=="ERROR"&&source.StartsWith("Module "))FrameErrors++;}
 public void Fault(string title,string detail){}
 public void BeginExport(){}
 public void Performance(string text){}
 public void Tick(){}
 public void Dispose(){}
}
