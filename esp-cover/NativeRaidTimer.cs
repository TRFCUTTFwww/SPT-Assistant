using System;
using Comfort.Common;
using EFT;
using EFT.UI.BattleTimer;
using HarmonyLib;
namespace LocalSpt5Esp;
internal static class NativeRaidTimer {
 static IntPtr lastPanel,lastTimer;
 static long lastSecond=-1;
 static double lastDuration;
 static bool hooked,synced;
 static float nextError;
 internal static void Initialize(Plugin p){try{p.Patches.Patch(typeof(MainTimerPanel).GetMethod("UpdateTimer"),postfix:new HarmonyMethod(typeof(NativeRaidTimer),nameof(AfterNativeUpdate)));hooked=true;}catch(Exception e){p.Log.LogError("Native O-key timer sync hook failed: "+e);}}
 internal static void Reset(){lastPanel=lastTimer=IntPtr.Zero;lastSecond=-1;synced=false;}
 internal static void Tick(){
  try{
   var timer=RaidClock.Timer;
   if(timer==null||CurrentPanel() is not { } panel){Reset();return;}
   synced=Apply(panel,timer,false);
  }catch(Exception error){Warn(error);}
 }
 internal static bool Changed(GameTimer timer,double duration){
  if(timer.StartDateTime.HasValue){
   var deadline=timer.StartDateTime.Value.AddSeconds(duration);
   timer._escape=new Il2CppSystem.Nullable<Il2CppSystem.DateTime>(deadline);
  }
  RaidClock.InvalidateDisplayTime();
  Tick();
  return synced;
 }
 static MainTimerPanel? CurrentPanel()=>Singleton<AbstractGame>.Instance?.GameUi?.TimerPanel?._mainTimerPanel;
 static void AfterNativeUpdate(MainTimerPanel __instance){
  try{
   var own=CurrentPanel();
   if(own!=null&&own.Pointer==__instance.Pointer&&RaidClock.Timer is GameTimer timer)
    synced=Apply(__instance,timer,true);
  }catch(Exception error){Warn(error);}
 }
 static bool Apply(MainTimerPanel panel,GameTimer timer,bool nativeTextChanged){
  if(RaidClock.SampleDisplayTime(timer) is not { } time)return false;
  if(!nativeTextChanged&&synced&&panel.Pointer==lastPanel&&timer.Pointer==lastTimer&&time.Second==lastSecond&&time.Duration==lastDuration)return true;
  if(panel.TimerText==null||panel._stringBuilder==null)return false;
  if(timer.StartDateTime.HasValue)panel._dateTime=timer.StartDateTime.Value.AddSeconds(time.Duration);
  panel.TimeSpan=Il2CppSystem.TimeSpan.FromSeconds(Math.Max(0,time.Remaining));
  panel.SetTimerText(Il2CppSystem.TimeSpan.FromSeconds(time.Second));
  lastPanel=panel.Pointer;lastTimer=timer.Pointer;lastSecond=time.Second;lastDuration=time.Duration;
  return true;
 }
 static void Warn(Exception error){
  if(UnityEngine.Time.unscaledTime<nextError)return;
  nextError=UnityEngine.Time.unscaledTime+20;
  Plugin.Self.Log.LogWarning("Native raid timer sync pending: "+error.Message);
 }
 internal static string State=>!hooked?"时间同步暂不可用":synced?"已同步 O 键倒计时":RaidClock.Active?"等待时间面板":"等待战局计时";
}
