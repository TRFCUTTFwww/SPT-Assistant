// Detached game boundaries; tests execute the production timeout guards/handoff.
namespace Comfort.Common {public static class Singleton<T>{public static T? Instance;}}
namespace UnityEngine {public static class Time{public static float unscaledTime=100;}}
namespace EFT {
 public enum ExitStatus{Survived,Runner,Killed,MissingInAction}
 public enum GameStatus{Stopped,Running,Runned,Starting,Started,Stopping,SoftStopping}
 public class AbstractGame{public IntPtr Pointer=new(10);public bool InRaid=true;public GameStatus Status=GameStatus.Started;public GameTimer? GameTimer;public T? TryCast<T>() where T:class=>this as T;}
 public class LocalGame:AbstractGame{}
 public class GameWorld{public IntPtr Pointer=new(20);public Player? MainPlayer=new();}
 public class Player{public bool IsYourPlayer=true;public string ProfileId="local";public Health? HealthController=new();}
 public class Health{public bool IsAlive=true;}
 public class EndByTimerScenario{public AbstractGame? _game;public GameTimer? _timer;}
 public class GameTimer{public IntPtr Pointer=new(30);public TimeSpan? SessionTime;public DateTime? _stop;}
}
namespace LocalSpt5Esp {
 internal class Setting{public bool Value;}
 internal static partial class RaidEnd{public static bool ManualEnding;}
 internal class Plugin{public static Plugin Self=new();public Log Log=new();}
 internal class Log{public bool Throw;public List<string> Entries=new();public void LogInfo(string text){if(Throw)throw new Exception("log unavailable");Entries.Add(text);}}
 internal static class RaidRules{public static double Duration(double elapsed,double remaining)=>elapsed+remaining;}
 internal static partial class RaidClock{
  internal static Setting PreventTimeout=new(){Value=true},PauseRemaining=new();
  internal static EFT.GameTimer? Timer;
  internal static double Now;
  internal static readonly RaidDeadlineState deadline=new();
  internal static double? Remaining=>Timer?.SessionTime.HasValue==true?deadline.Duration(Timer.Pointer.ToInt64(),Timer.SessionTime.Value.TotalSeconds)-Now:null;
  static double Elapsed(EFT.GameTimer timer)=>Now;
  static void WriteDuration(EFT.GameTimer timer,double duration)=>timer.SessionTime=TimeSpan.FromSeconds(duration);
  internal static void Reset()=>deadline.Reset();
  static void Warn(Exception error){}
 }
}
