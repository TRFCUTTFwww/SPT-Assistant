namespace Comfort.Common {public static class Singleton<T>{public static T? Instance;}}
namespace UnityEngine {public static class Time {public static float unscaledTime;}}
namespace Il2CppSystem {public struct Nullable<T>(T value) where T:struct {public T Value=value;public bool HasValue=true;}public struct DateTime(System.DateTime value){public System.DateTime Value=value;public DateTime AddSeconds(double seconds)=>new(Value.AddSeconds(seconds));}public struct TimeSpan(double seconds){public double TotalSeconds=seconds;public static TimeSpan FromSeconds(double seconds)=>new(seconds);}}
namespace EFT {
 public class GameWorld {public IntPtr Pointer=new(1);}
 public class GameTimer {public IntPtr Pointer=new(2);public Il2CppSystem.Nullable<Il2CppSystem.DateTime> StartDateTime=new(new(System.DateTime.UtcNow));public Il2CppSystem.Nullable<Il2CppSystem.DateTime> _escape;}
 public class AbstractGame {public Ui? GameUi=new();}public class Ui {public TimerPanel? TimerPanel=new();}public class TimerPanel {public EFT.UI.BattleTimer.MainTimerPanel? _mainTimerPanel=new();}
}
namespace EFT.UI.BattleTimer {public class MainTimerPanel {public IntPtr Pointer=new(3);public Il2CppSystem.DateTime _dateTime;public Il2CppSystem.TimeSpan TimeSpan;public object? TimerText=new(),_stringBuilder=new();public int Writes;public void SetTimerText(Il2CppSystem.TimeSpan value){TimeSpan=value;Writes++;}public void UpdateTimer(){}}}
namespace HarmonyLib {public class HarmonyMethod {public HarmonyMethod(System.Type type,string name){}} }
namespace LocalSpt5Esp {
 internal static class RaidClock {internal static bool Active;internal static EFT.GameTimer? Timer;internal static double? Remaining,DisplayDuration;}
 internal class Plugin {public static Plugin Self=new();public PatchSet Patches=new();public Logger Log=new();}
 internal class PatchSet {public void Patch(System.Reflection.MethodInfo? method,HarmonyLib.HarmonyMethod? postfix=null){}}
 internal class Logger {public void LogError(string text){}public void LogWarning(string text){}}
}
