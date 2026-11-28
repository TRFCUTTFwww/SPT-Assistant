using LocalSpt5Esp.HotReload;
namespace LocalSpt5Esp;
public class RuntimeModule:IRuntimeModule {
 public int ApiVersion=>1;
 public string Version=>"fixture";
 public double ReloadFrameBudgetMs=>TestState.FastMode?25.0:0.0;
 public bool SilentFastReload=>TestState.FastMode;
 public void PrepareInBackground(){if(Environment.CurrentManagedThreadId==TestState.MainThread)throw new Exception("Preparation ran on main thread");Interlocked.Increment(ref TestState.Prepares);}
 public bool PrepareForReload()=>TestState.CanReload;
 public IEnumerable<Action> InitializeSteps(IHostServices host){yield return ()=>TestState.Init++;yield return ()=>{if(TestState.FailNextInit){TestState.FailNextInit=false;throw new Exception("fixture init failure");}TestState.Init++;};yield return ()=>TestState.Init++;}
 public void Initialize(IHostServices host){foreach(var step in InitializeSteps(host))step();}
 public IEnumerable<Action> UnloadSteps(){yield return ()=>{TestState.Unload++;if(TestState.FailNextUnload){TestState.FailNextUnload=false;throw new Exception("fixture unload failure");}};yield return ()=>{TestState.Unload++;if(TestState.FailNextUnload){TestState.FailNextUnload=false;throw new Exception("fixture unload failure");}};}
 public void Unload(){foreach(var step in UnloadSteps())step();}
 public string EmergencySurvive(){TestState.Rescues++;return "fixture rescue";}
 public void EmergencyTick(){}
 public bool EmergencyBusy=>false;
 public void Update(){FailIfRequested();TestState.Updates++;}
 public void LateUpdate()=>FailIfRequested();
 public void OnGUI()=>FailIfRequested();
 static void FailIfRequested(){if(TestState.FailFrameCallbacks)throw new InvalidOperationException("fixture frame callback failure");}
}
