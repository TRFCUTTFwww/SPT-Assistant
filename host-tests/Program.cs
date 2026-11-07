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
TestState.FailNextInit=true;host.RequestReload();Pump(()=>Status().ValueKind==JsonValueKind.Object&&Status().GetProperty("error").ValueKind==JsonValueKind.String&&Time.timeScale==.75f);Check(host.CurrentVersion=="fixture"&&config.SaveOnConfigSet,"failed init did not restore last good module and config mode");
Console.WriteLine("PASS partial candidate cleanup, rollback, persisted failure and automatic resume");
File.WriteAllBytes(payload,new byte[2048]);host.RequestReload();int live=TestState.Updates;Pump(()=>TestState.Updates>live+5&&Status().GetProperty("error").GetString()!.Contains("Bad IL format"));Check(host.CurrentVersion=="fixture"&&Time.timeScale==.75f,"corrupt module stopped old runtime");
Console.WriteLine("PASS invalid candidate leaves old module and game running");
var game=new EFT.AbstractGame{GameTimer=new()};Comfort.Common.Singleton<EFT.AbstractGame>.Instance=game;var oldStart=game.GameTimer._start.Value.Value;using(var pause=new ReloadPause()){Thread.Sleep(20);pause.Tick();Check(Time.timeScale==0&&game.GameTimer._start.Value.Value>oldStart,"raid clock was not compensated");}Check(Time.timeScale==.75f,"pause dispose did not restore time scale");
Console.WriteLine("PASS wall-clock raid deadline compensation and exact original time scale restoration");
// Test files remain in an isolated temporary directory for inspection.
