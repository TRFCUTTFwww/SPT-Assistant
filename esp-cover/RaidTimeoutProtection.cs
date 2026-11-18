using System;
using Comfort.Common;
using EFT;
namespace LocalSpt5Esp;
internal static partial class RaidClock {
 const string TimeoutHandoff="LocalSpt5Esp.RaidTimeout.Handoff.v1";
 static bool TimeoutProtected=>PreventTimeout?.Value==true||PauseRemaining?.Value==true;
 static bool OwnLivingPlayer(AbstractGame game,string profile){
  var current=Singleton<AbstractGame>.Instance;var player=Singleton<GameWorld>.Instance?.MainPlayer;
  return current!=null&&game.Pointer==current.Pointer&&game.TryCast<LocalGame>()!=null&&player?.IsYourPlayer==true&&player.HealthController?.IsAlive==true&&profile==player.ProfileId;
 }
 internal static bool AllowAutomaticEnd(AbstractGame game,string profile,ExitStatus status)=>
  status!=ExitStatus.MissingInAction||!TimeoutProtected||RaidEnd.ManualEnding||!OwnLivingPlayer(game,profile);
 static bool BeforeStop(AbstractGame __instance,string __0,ExitStatus __1){
  try{if(AllowAutomaticEnd(__instance,__0,__1))return true;BlockedTimeout("Stop");return false;}catch(Exception e){Warn(e);return true;}
 }
 static bool BeforeTimeout(EndByTimerScenario __instance){
  try{
   var game=Singleton<AbstractGame>.Instance;var player=Singleton<GameWorld>.Instance?.MainPlayer;
   if(!TimeoutProtected||game==null||__instance._game==null||__instance._game.Pointer!=game.Pointer||player==null||!OwnLivingPlayer(game,player.ProfileId))return true;
   // The scenario may keep another timer reference. Guard its owning game.
   BlockedTimeout("EndByTimerScenario.Update");return false;
  }catch(Exception e){Warn(e);return true;}
 }
 static float nextBlockedTimeout;
 static void BlockedTimeout(string entry){try{if(UnityEngine.Time.unscaledTime<nextBlockedTimeout)return;nextBlockedTimeout=UnityEngine.Time.unscaledTime+10;Plugin.Self.Log.LogInfo("Raid timeout blocked at "+entry+"; remaining="+Remaining+"; inRaid="+(Singleton<AbstractGame>.Instance?.InRaid==true));}catch(Exception e){try{Warn(e);}catch{}}}
 internal static void Shutdown(){try{PreserveTimeoutForReload();}finally{Reset();}}
 internal static void PreserveTimeoutForReload(){
  var timer=Timer;var world=Singleton<GameWorld>.Instance;
  if(timer?.SessionTime.HasValue!=true||world==null){AppDomain.CurrentDomain.SetData(TimeoutHandoff,null);return;}
  long id=timer.Pointer.ToInt64();double visible=deadline.Duration(id,timer.SessionTime.Value.TotalSeconds);
  if(!TimeoutProtected){WriteDuration(timer,visible);AppDomain.CurrentDomain.SetData(TimeoutHandoff,null);return;}
  // Native scenarios still run while module hooks are being removed/rebound.
  // Keep their deadline ahead, then hand the visible deadline to the next core.
  double native=Math.Max(timer.SessionTime.Value.TotalSeconds,Elapsed(timer)+600);
  WriteDuration(timer,native);deadline.Restore(id,visible,native);
  AppDomain.CurrentDomain.SetData(TimeoutHandoff,new object[]{id,world.Pointer.ToInt64(),visible,native});
 }
 static void RestoreTimeoutAfterReload(){
  var data=AppDomain.CurrentDomain.GetData(TimeoutHandoff) as object[];AppDomain.CurrentDomain.SetData(TimeoutHandoff,null);
  var timer=Timer;var world=Singleton<GameWorld>.Instance;
  if(data is not {Length:4}||timer?.SessionTime.HasValue!=true||world==null||data[0] is not long id||id!=timer.Pointer.ToInt64()||data[1] is not long owner||owner!=world.Pointer.ToInt64()||data[2] is not double visible||data[3] is not double native||Math.Abs(native-timer.SessionTime.Value.TotalSeconds)>.001)return;
  deadline.Restore(id,visible,native);
  // Disabling protection during a reload must restore the real deadline too.
  if(!TimeoutProtected)WriteDuration(timer,visible);
 }
}
