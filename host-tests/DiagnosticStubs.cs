// OS logging and monitor boundaries only; lifecycle and safety code is production.
using BepInEx.Configuration;
namespace LocalSpt5Esp.HotReload;
public sealed class DiagnosticService:IDisposable {
 public Action<string,string,bool>? Fatal;
 public ConfigEntry<string> Minimum=new("INFO");
 public ConfigEntry<bool> IncludeHardware=new(false),IncludePerformance=new(true),IncludeSpt=new(true),IncludeGame=new(true),DetailedTiming=new(false),DebugEnabled=new(false),PhysicsTracing=new(false);
 public string ExportStatus=>"test export";
 public DiagnosticService(ConfigFile config,string directory){}
 public void Rebind(ConfigFile config){}
 public void Record(string level,string source,string message){}
 public void Fault(string title,string detail){}
 public void BeginExport(){}
 public void Performance(string text){}
 public void Tick(){}
 public void Dispose(){}
}
internal static class CrashWatchClient {
 internal static void Start(string directory,DiagnosticService service){}
 internal static void Tick(){}
 internal static void MarkFatal(string title,string detail){}
 internal static void ClearFatal(){}
 internal static void RequestRestart(){}
 internal static void MarkDismissed(){}
 internal static void MarkNormal(){}
}
