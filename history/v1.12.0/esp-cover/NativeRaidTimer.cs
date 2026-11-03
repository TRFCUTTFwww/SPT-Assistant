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
 internal static void Tick(){if(!RaidClock.Active){Reset();return;}if(!Matches()||UnityEngine.Time.unscaledTime<nextSync)return;nextSync=UnityEngine.Time.unscaledTime+.25f;synced=Sync();}
 static bool Matches(){var world=Singleton<GameWorld>.Instance;var timer=RaidClock.Timer;return world!=null&&timer!=null&&world.Pointer==changedWorld&&timer.Pointer==changedTimer;}
 internal static bool Changed(GameTimer timer,double duration){var world=Singleton<GameWorld>.Instance??throw new ArgumentException("当前战局未就绪。");changedWorld=world.Pointer;changedTimer=timer.Pointer;
  if(timer.StartDateTime.HasValue){var deadline=timer.StartDateTime.Value.AddSeconds(duration);timer._escape=new Il2CppSystem.Nullable<Il2CppSystem.DateTime>(deadline);}
  return synced=Sync();
 }
 static bool Sync(){var panel=Singleton<AbstractGame>.Instance?.GameUi?.TimerPanel?._mainTimerPanel;if(panel==null)return false;return Apply(panel);}
 static void AfterNativeUpdate(MainTimerPanel __instance){if(!Matches())return;var own=Singleton<AbstractGame>.Instance?.GameUi?.TimerPanel?._mainTimerPanel;if(own!=null&&own.Pointer==__instance.Pointer)Apply(__instance);}
 static bool Apply(MainTimerPanel panel){try{var timer=RaidClock.Timer;if(timer==null||!timer.SessionTime.HasValue)return false;double seconds=Math.Max(0,timer.SessionTime.Value.TotalSeconds-timer.PastTime.TotalSeconds);var span=Il2CppSystem.TimeSpan.FromSeconds(seconds);if(timer.StartDateTime.HasValue)panel._dateTime=timer.StartDateTime.Value.AddSeconds(timer.SessionTime.Value.TotalSeconds);panel.TimeSpan=span;if(panel.TimerText!=null&&panel._stringBuilder!=null)panel.SetTimerText(span);return Math.Abs(panel.TimeSpan.TotalSeconds-seconds)<1;}catch(Exception e){if(UnityEngine.Time.unscaledTime>=nextError){nextError=UnityEngine.Time.unscaledTime+20;Plugin.Self.Log.LogWarning("Native raid timer sync pending: "+e.Message);}return false;}}
 internal static string State=>!hooked?"原生时间同步接口未就绪":Matches()?(synced?"原生 O 键计时已同步":"等待原生计时面板就绪"):"尚未修改本局时间";
}
