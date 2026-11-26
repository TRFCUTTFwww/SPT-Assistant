using BepInEx.Configuration;
using BepInEx.Logging;
namespace LocalSpt5Esp {
 public enum AimMode { Off, Snap, Silent }
}
namespace LocalSpt5Esp.HotReload {
 public interface IHostServices {
  ConfigFile Config { get; }
  ManualLogSource Log { get; }
  string PluginDirectory { get; }
  void RequestReload();
 }
 public interface IRuntimeModule {
  int ApiVersion { get; }
  string Version { get; }
  void Initialize(IHostServices host);
  void Update();
  void LateUpdate();
  void OnGUI();
  void Unload();
 }
}
