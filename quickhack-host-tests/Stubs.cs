namespace BepInEx.Configuration { public sealed class ConfigFile {public void Reload() { }} }
namespace BepInEx.Logging { public sealed class ManualLogSource {public void LogInfo(object value) { } public void LogWarning(object value) { }} }
public static class TestState {
    public static int Initializes,Stops,Ticks,Live;
    public static bool Busy,FailNext;
    public static void Initialize() {
        Live++; Initializes++;
        if(FailNext) {FailNext=false;throw new Exception("fixture initialization failure");}
    }
}
