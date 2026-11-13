using BepInEx.Configuration;
using BepInEx.Logging;
using LocalQuickhacks.Hosting;
namespace LocalQuickhacks;
public sealed class RuntimeModule:IQuickhackModule {
    bool live;
    public int ApiVersion=>1;
    public string Version=>"fixture";
    public bool CanReload=>!TestState.Busy;
    public void Initialize(ConfigFile config,ManualLogSource log,string directory) {live=true;TestState.Initialize();}
    public void Tick()=>TestState.Ticks++;
    public void LateTick() { }
    public void Draw() { }
    public void Shutdown() {if(!live)return;live=false;TestState.Live--;TestState.Stops++;}
}
