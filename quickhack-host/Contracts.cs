using BepInEx.Configuration;
using BepInEx.Logging;

namespace LocalQuickhacks.Hosting;

public interface IQuickhackModule {
    int ApiVersion { get; }
    string Version { get; }
    bool CanReload { get; }
    void Initialize(ConfigFile config,ManualLogSource log,string directory);
    void Tick();
    void LateTick();
    void Draw();
    void Shutdown();
}
