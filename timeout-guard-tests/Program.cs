using System.Reflection;
using Comfort.Common;
using EFT;
using LocalSpt5Esp;
int checks=0;
void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
bool Prefix(string name,params object[] args)=>(bool)typeof(RaidClock).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args)!;
void Restore()=>typeof(RaidClock).GetMethod("RestoreTimeoutAfterReload",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,null);
LocalGame game=new();GameWorld world=new();
Singleton<AbstractGame>.Instance=game;Singleton<GameWorld>.Instance=world;
RaidClock.Timer=new(){SessionTime=TimeSpan.FromSeconds(60)};RaidClock.Now=100;
Check(!Prefix("BeforeStop",game,"local",ExitStatus.MissingInAction),"expired automatic MIA escaped");
// InRaid can already be false by the time a queued Stop is dispatched.
game.InRaid=false;RaidClock.Timer=null;
Check(!Prefix("BeforeStop",game,"local",ExitStatus.MissingInAction),"transition with unavailable timer escaped");
RaidClock.Timer=new(){SessionTime=TimeSpan.FromSeconds(1000)};
Check(!Prefix("BeforeStop",game,"local",ExitStatus.MissingInAction),"reset/future countdown incorrectly allowed queued MIA");
Check(!Prefix("BeforeTimeout",new EndByTimerScenario{_game=game,_timer=new(){Pointer=new(99)}}),"scenario with stale timer escaped");
Check(Prefix("BeforeTimeout",new EndByTimerScenario{_game=new LocalGame{Pointer=new(99)}}),"other game's scenario intercepted");
Check(Prefix("BeforeStop",game,"other",ExitStatus.MissingInAction),"another player's exit intercepted");
Check(Prefix("BeforeStop",new LocalGame{Pointer=new(99)},"local",ExitStatus.MissingInAction),"another game's exit intercepted");
Check(!RaidEnd.SettlementStarted(game),"blocked MIA or unrelated stop entered settlement");
foreach(var status in new[]{GameStatus.Stopping,GameStatus.SoftStopping,GameStatus.Stopped}){
 game.Status=status;
 Check(RaidEnd.SettlementStarted(game),"missed native status: "+status);
}
game.Status=GameStatus.Started;
game.GameTimer=new(){_stop=DateTime.UtcNow};
Check(RaidEnd.SettlementStarted(game),"missed stopped native timer");
game.GameTimer=null;
foreach(var status in new[]{ExitStatus.Survived,ExitStatus.Runner,ExitStatus.Killed})Check(Prefix("BeforeStop",game,"local",status),"normal end blocked: "+status);
Check(RaidEnd.SettlementStarted(game),"accepted native stop was not recorded");
world.Pointer=new(21);
Check(!RaidEnd.SettlementStarted(game),"previous world blocked the next raid");
world.Pointer=new(20);
Check(!RaidEnd.SettlementStarted(new LocalGame{Pointer=new(11)}),"one game blocked a different session");
RaidEnd.ManualEnding=true;
Check(Prefix("BeforeStop",game,"local",ExitStatus.MissingInAction),"explicit manual MIA blocked");RaidEnd.ManualEnding=false;
RaidClock.PreventTimeout.Value=false;
Check(Prefix("BeforeStop",game,"local",ExitStatus.MissingInAction),"disabled guard still intercepted");
RaidClock.PauseRemaining.Value=true;
Check(!Prefix("BeforeStop",game,"local",ExitStatus.MissingInAction),"paused remaining time escaped protection");RaidClock.PauseRemaining.Value=false;
RaidClock.PreventTimeout.Value=true;world.MainPlayer!.HealthController!.IsAlive=false;
Check(Prefix("BeforeStop",game,"local",ExitStatus.MissingInAction),"dead player's outcome modified");world.MainPlayer.HealthController.IsAlive=true;
world.MainPlayer.IsYourPlayer=false;
Check(Prefix("BeforeStop",game,"local",ExitStatus.MissingInAction),"remote actor intercepted");world.MainPlayer.IsYourPlayer=true;
Plugin.Self.Log.Throw=true;UnityEngine.Time.unscaledTime+=20;
Check(!Prefix("BeforeStop",game,"local",ExitStatus.MissingInAction),"logging failure disabled guard");Plugin.Self.Log.Throw=false;
Console.WriteLine("PASS automatic MIA guards across native transitions, timer mismatch/unavailability, explicit manual outcomes, ownership and logging failures");
game.InRaid=true;RaidClock.Now=100;RaidClock.Timer=new(){SessionTime=TimeSpan.FromSeconds(60)};RaidClock.Reset();
for(int reload=0;reload<3;reload++){
 RaidClock.PreserveTimeoutForReload();
 Check(RaidClock.Timer.SessionTime!.Value.TotalSeconds>=RaidClock.Now+600,"preflight left expired native watchdog");
 Check(RaidClock.Remaining==-40,"preflight changed visible deadline");
 RaidClock.Shutdown(); // Production unload runs preservation again before unpatch.
 Check(RaidClock.Timer.SessionTime.Value.TotalSeconds>=RaidClock.Now+600,"unload restored expired deadline");
 RaidClock.Reset();Restore(); // Next core has a fresh deadline state.
 Check(RaidClock.Remaining==-40,"reload lost original countdown");
 Check(!Prefix("BeforeStop",game,"local",ExitStatus.MissingInAction),"post-reload MIA escaped");
}
RaidClock.PreserveTimeoutForReload();RaidClock.Reset();RaidClock.PreventTimeout.Value=false;Restore();
Check(RaidClock.Timer.SessionTime!.Value.TotalSeconds==60,"disabled protection failed to restore native deadline");
RaidClock.PreventTimeout.Value=true;RaidClock.PreserveTimeoutForReload();RaidClock.Reset();
world.Pointer=new(88);Restore();
Check(RaidClock.Remaining>0,"previous world's handoff applied to replacement world");
world.Pointer=new(20);RaidClock.Timer=new(){SessionTime=TimeSpan.FromSeconds(60)};RaidClock.Now=100;RaidClock.PreserveTimeoutForReload();RaidClock.Reset();
RaidClock.Timer.SessionTime=TimeSpan.FromSeconds(2000);Restore();
Check(RaidClock.Remaining==1900,"external timer edit overwritten by stale handoff");
RaidClock.Timer=new(){SessionTime=TimeSpan.FromSeconds(60)};RaidClock.Reset();RaidClock.PreserveTimeoutForReload();RaidClock.PreventTimeout.Value=false;RaidClock.Shutdown();
Check(RaidClock.Timer.SessionTime!.Value.TotalSeconds==60,"disabled protection at preflight retained extension");
Console.WriteLine($"PASS repeated reloads during overtime, protected unload, countdown continuity, disable, world isolation and external edits ({checks} checks total)");
