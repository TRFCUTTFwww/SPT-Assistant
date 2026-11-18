using System;
using Comfort.Common;
using EFT;
using EFT.UI.BattleTimer;
using HarmonyLib;
namespace LocalSpt5Esp;
internal static class NativeRaidTimer {
 static IntPtr changedWorld,changedTimer;
 static bool hooked,synced;
 static float nextSync,nextError;
 internal static void Initialize(Plugin p){try{p.Patches.Patch(typeof(MainTimerPanel).GetMethod("UpdateTimer"),postfix:new HarmonyMethod(typeof(NativeRaidTimer),nameof(AfterNativeUpdate)));hooked=true;}catch(Exception e){p.Log.LogError("Native O-key timer sync hook failed: "+e);}}
 internal static void Reset(){changedWorld=changedTimer=IntPtr.Zero;synced=false;nextSync=0;}
 internal static void Tick(){if(!RaidClock.Active){Reset();return;}if(UnityEngine.Time.unscaledTime<nextSync)return;nextSync=UnityEngine.Time.unscaledTime+1f;try{var world=Singleton<GameWorld>.Instance;var timer=RaidClock.Timer;if(world==null||timer==null)return;changedWorld=world.Pointer;changedTimer=timer.Pointer;synced=Sync();}catch(Exception e){if(UnityEngine.Time.unscaledTime>=nextError){nextError=UnityEngine.Time.unscaledTime+20;Plugin.Self.Log.LogWarning("Native raid timer sync pending: "+e.Message);}}}
 static bool Matches(){var world=Singleton<GameWorld>.Instance;var timer=RaidClock.Timer;return world!=null&&timer!=null&&world.Pointer==changedWorld&&timer.Pointer==changedTimer;}
 internal static bool Changed(GameTimer timer,double duration){var world=Singleton<GameWorld>.Instance??throw new ArgumentException("当前战局未就绪。");changedWorld=world.Pointer;changedTimer=timer.Pointer;
  if(timer.StartDateTime.HasValue){var deadline=timer.StartDateTime.Value.AddSeconds(duration);timer._escape=new Il2CppSystem.Nullable<Il2CppSystem.DateTime>(deadline);}
  return synced=Sync();
 }
 static bool Sync(){var panel=Singleton<AbstractGame>.Instance?.GameUi?.TimerPanel?._mainTimerPanel;if(panel==null)return false;return Apply(panel);}
 static void AfterNativeUpdate(MainTimerPanel __instance){if(!Matches())return;var own=Singleton<AbstractGame>.Instance?.GameUi?.TimerPanel?._mainTimerPanel;if(own!=null&&own.Pointer==__instance.Pointer)Apply(__instance);}
 static bool Apply(MainTimerPanel panel){try{var timer=RaidClock.Timer;if(timer==null||RaidClock.Remaining is not double remaining||RaidClock.DisplayDuration is not double duration)return false;double seconds=Math.Max(0,remaining);var span=Il2CppSystem.TimeSpan.FromSeconds(seconds);if(timer.StartDateTime.HasValue)panel._dateTime=timer.StartDateTime.Value.AddSeconds(duration);panel.TimeSpan=span;if(panel.TimerText!=null&&panel._stringBuilder!=null)panel.SetTimerText(span);return Math.Abs(panel.TimeSpan.TotalSeconds-seconds)<1;}catch(Exception e){if(UnityEngine.Time.unscaledTime>=nextError){nextError=UnityEngine.Time.unscaledTime+20;Plugin.Self.Log.LogWarning("Native raid timer sync pending: "+e.Message);}return false;}}
 internal static string State=>!hooked?"时间同步暂不可用":Matches()?(synced?"每秒自动同步 O 键时间":"等待时间面板"):"等待战局计时";
}
