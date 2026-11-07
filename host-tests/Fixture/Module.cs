using LocalSpt5Esp.HotReload;
namespace LocalSpt5Esp;
public class RuntimeModule:IRuntimeModule {
 public int ApiVersion=>1;
 public string Version=>"fixture";
 public void PrepareInBackground(){if(Environment.CurrentManagedThreadId==TestState.MainThread)throw new Exception("Preparation ran on main thread");Interlocked.Increment(ref TestState.Prepares);}
 public bool PrepareForReload()=>TestState.CanReload;
 public IEnumerable<Action> InitializeSteps(IHostServices host){yield return ()=>TestState.Init++;yield return ()=>{if(TestState.FailNextInit){TestState.FailNextInit=false;throw new Exception("fixture init failure");}TestState.Init++;};yield return ()=>TestState.Init++;}
 public void Initialize(IHostServices host){foreach(var step in InitializeSteps(host))step();}
 public IEnumerable<Action> UnloadSteps(){yield return ()=>TestState.Unload++;yield return ()=>TestState.Unload++;}
 public void Unload(){foreach(var step in UnloadSteps())step();}
 public void Update()=>TestState.Updates++;
 public void LateUpdate(){}
 public void OnGUI(){}
}
