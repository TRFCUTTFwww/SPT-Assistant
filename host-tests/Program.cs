using System.Text.Json;
using System.Diagnostics;
using LocalSpt5Esp.HotReload;
using UnityEngine;
void Check(bool condition,string message){if(!condition)throw new Exception(message);}
TestState.MainThread=Environment.CurrentManagedThreadId;
string home=Path.Combine(Path.GetTempPath(),"spt-host-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(home,"runtime"));
string payload=Path.Combine(home,"runtime","Local.Spt5.ESP.Core.module"),status=Path.Combine(home,"runtime","hotreload-status.json");
File.Copy(args[0],payload);
var config=new BepInEx.Configuration.ConfigFile();var log=new BepInEx.Logging.ManualLogSource();
using var host=new RuntimeReloader(config,log,home);host.Start();
JsonElement Status(){try{using var document=JsonDocument.Parse(File.ReadAllBytes(status));return document.RootElement.Clone();}catch{return default;}}
void Pump(Func<bool> until){var timer=Stopwatch.StartNew();while(timer.Elapsed.TotalSeconds<10){int init=TestState.Init,unload=TestState.Unload;host.Update();Check(TestState.Init-init<=1&&TestState.Unload-unload<=1,"multiple lifecycle actions executed in one frame");host.LateUpdate();host.Draw();if(until())return;Thread.Sleep(2);}throw new Exception("Timed out waiting for host");}
bool Ready(int generation){var state=Status();return state.ValueKind==JsonValueKind.Object&&state.GetProperty("generation").GetInt32()==generation&&Time.timeScale==.75f;}
Pump(()=>Ready(1));Check(TestState.Init==3&&TestState.Prepares==1,"staged startup did not complete");Check(Cursor.lockState==CursorLockMode.Locked&&!Cursor.visible&&!AudioListener.pause&&!Application.runInBackground,"pause state not restored");
Console.WriteLine("PASS background prepare, modal presentation, one lifecycle action per frame and prior pause/cursor/audio/background restoration");
TestState.CanReload=false;host.RequestReload();int before=TestState.Updates;Pump(()=>TestState.Prepares==2&&TestState.Updates>before+5);Check(Time.timeScale==.75f&&TestState.Unload==0,"blocked native transactions were interrupted");TestState.CanReload=true;Pump(()=>Ready(2));
Console.WriteLine("PASS transaction deferral keeps old module updating, then reloads after native work completes");
TestState.FailNextInit=true;host.RequestReload();Pump(()=>host.SafetyBlocked);Check(Time.timeScale==0&&config.SaveOnConfigSet,"failed init did not hold pause and restore config mode");
int live=TestState.Updates;for(int i=0;i<20;i++){host.Update();host.Draw();}Check(TestState.Updates==live&&TestState.Rescues==0,"failed reload ran partial code or settled a real raid");
host.RollbackPreviousVersion();Pump(()=>Ready(3));Check(host.CurrentVersion=="fixture"&&!host.SafetyBlocked,"manual rollback did not restore last good module");
Console.WriteLine("PASS failed init holds pause; partial candidate cleanup and manual rollback restore game");
var valid=File.ReadAllBytes(payload);File.WriteAllBytes(payload,new byte[2048]);host.RequestReload();Pump(()=>host.SafetyBlocked);Check(host.CurrentVersion=="fixture"&&Time.timeScale==0&&TestState.Rescues==0,"corrupt candidate ran code or triggered a raid rescue");
File.WriteAllBytes(payload,valid);host.RequestReload();Pump(()=>Ready(4));
Console.WriteLine("PASS corrupt candidate holds pause and repaired replacement resumes safely");
host.SetDiagnosticOption("debug","true");foreach(string kind in new[]{"null","bounds","stack","access","reload"}){host.SimulateError(kind);Pump(()=>host.SafetyBlocked);Check(Time.timeScale==0&&TestState.Rescues==0,"debug simulation changed a real raid");TestState.Click="结束调试模拟并继续";host.Draw();Pump(()=>!host.SafetyBlocked&&Time.timeScale==.75f);}
Console.WriteLine("PASS all five controlled error simulations preserve the real raid and permit debug exit");
var game=new EFT.AbstractGame{GameTimer=new()};Comfort.Common.Singleton<EFT.AbstractGame>.Instance=game;var oldStart=game.GameTimer._start.Value.Value;using(var pause=new ReloadPause()){Thread.Sleep(20);pause.Tick();Check(Time.timeScale==0&&game.GameTimer._start.Value.Value>oldStart,"raid clock was not compensated");}Check(Time.timeScale==.75f,"pause dispose did not restore time scale");
Console.WriteLine("PASS wall-clock raid deadline compensation and exact original time scale restoration");
Comfort.Common.Singleton<EFT.AbstractGame>.Instance=null;TestState.FailNextUnload=true;host.RequestReload();Pump(()=>host.SafetyBlocked);host.RollbackPreviousVersion();for(int i=0;i<20;i++){host.Update();host.Draw();}Check(host.SafetyBlocked&&Time.timeScale==0,"unsafe unload allowed rollback or automatic resume");
Console.WriteLine("PASS failed unload prevents unsafe rollback and retains pause");
// Test files remain in an isolated temporary directory for inspection.
