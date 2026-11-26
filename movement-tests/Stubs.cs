using System.Reflection;

internal class NativeObject {
 static long next;
 internal IntPtr Pointer {get;}=new(++next);
}
namespace BepInEx.Configuration {
 internal sealed class ConfigEntry<T>(T value) {internal T Value=value;}
}
namespace Comfort.Common {internal static class Singleton<T> {internal static T Instance=default!;}}
namespace UnityEngine {internal static class Time {internal static float unscaledTime=0;}}
namespace EFT.Interactive {
 internal sealed class ObstacleCollider {
  internal float speedMlp=.3f;
  internal bool _hasSwampSpeedLimit=true;
  internal bool _disablesSprint=true,_disablesJump=true;
 }
}
namespace EFT {
 internal sealed class Health {internal bool IsAlive=true;}
 internal sealed class Player {
  internal bool IsYourPlayer=true,Destroyed;
  internal Health HealthController=new();
  internal MovementContext MovementContext=new();
 }
 internal sealed class GameWorld {internal Player MainPlayer=new();}
 internal sealed class MovementContext:NativeObject {
  internal List<Interactive.ObstacleCollider> _enteredObstacles=new();
  internal int _bushZoneCount=2,Refreshes;
  internal float SpeedLimit=1;
  internal bool SprintDisabled,JumpDisabled,ThrowOnRefresh;
  public void RefreshObstacleRestrictions(){
   object? state=HarmonyLib.Harmony.Before(this);
   try{
    Refreshes++;
    SpeedLimit=_enteredObstacles.Select(o=>o.speedMlp).DefaultIfEmpty(1).Min();
    SprintDisabled=_enteredObstacles.Any(o=>o._disablesSprint);
    JumpDisabled=_enteredObstacles.Any(o=>o._disablesJump);
    if(ThrowOnRefresh)throw new InvalidOperationException("native refresh failed");
   }finally{HarmonyLib.Harmony.Finish(state);}
  }
 }
}
namespace HarmonyLib {
 internal sealed class HarmonyMethod(Type type,string name) {
  internal MethodInfo Method=type.GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic)!;
 }
 internal sealed class Harmony {
  static MethodInfo before=null!,finish=null!;
  internal void Patch(MethodInfo original,HarmonyMethod prefix,HarmonyMethod finalizer){before=prefix.Method;finish=finalizer.Method;}
  internal static object? Before(EFT.MovementContext context){object?[] args=[context,null];before.Invoke(null,args);return args[1];}
  internal static void Finish(object? state)=>finish.Invoke(null,[state]);
 }
}
namespace LocalSpt5Esp {
 internal sealed class Plugin {
  internal static Plugin Self=new();
  internal HarmonyLib.Harmony Patches=new();
  internal readonly List<Exception> Errors=new();
  internal void Error(Exception error)=>Errors.Add(error);
 }
 internal static class RaidClock {internal static bool Active=true;}
 internal static class FeatureConfig {
  internal static BepInEx.Configuration.ConfigEntry<bool> Switch=null!,SwampSwitch=null!;
  internal static BepInEx.Configuration.ConfigEntry<bool> Bind(string section,string key,string name,bool value)=>key=="No bush slowdown"?Switch=new(value):SwampSwitch=new(value);
 }
}
