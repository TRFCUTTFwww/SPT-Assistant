using LocalQuickhacks.Hosting;
using BepInEx.Configuration;
using BepInEx.Logging;
using System.Text.Json;

void Check(bool ok,string message) {if(!ok)throw new Exception(message);}
string home=Path.Combine(Path.GetTempPath(),"quickhack-host-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(home,"runtime"));
string path=Path.Combine(home,"runtime","Local.Cp2077.Quickhacks.Core.module");
byte[] valid=File.ReadAllBytes(args[0]);File.WriteAllBytes(path,valid);
using var host=new Reloader(new ConfigFile(),new ManualLogSource(),home);
double now=0;
void Poll() {host.Poll(now);now+=.3;}
void Manual() {host.Request();Poll();}
JsonElement Status() {using var j=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(home,"runtime","hotreload-status.json")));return j.RootElement.Clone();}
Manual();
Check(host.Current?.Version=="fixture"&&TestState.Live==1&&Status().GetProperty("generation").GetInt32()==1,"Startup failed");
// The module can be opened for replacement even while its code is active.
using(var unlocked=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)) Check(unlocked.Length==valid.Length,"Module file remains locked");
for(int i=0;i<20;i++) Manual();
Check(TestState.Live==1&&TestState.Stops==20,"Reload accumulated live modules");
Console.WriteLine("PASS startup, twenty manual swaps, single live instance and unlocked module publication");
TestState.Busy=true;host.Request();Poll();
Check(TestState.Stops==20&&Status().GetProperty("waiting").GetBoolean(),"Busy upload/puzzle was interrupted");
host.Current!.Tick();Check(TestState.Ticks==1,"Deferred runtime stopped ticking");
TestState.Busy=false;Poll();
Check(TestState.Stops==21&&TestState.Live==1,"Deferred swap did not resume");
Console.WriteLine("PASS active-operation deferral and continued old-runtime dispatch");
File.WriteAllBytes(path,new byte[1024]);Manual();
Check(host.Current?.Version=="fixture"&&TestState.Live==1&&TestState.Stops==21,"Corrupt candidate stopped healthy runtime");
Console.WriteLine("PASS corrupt candidate rejected before current module shutdown");
File.WriteAllBytes(path,valid);TestState.FailNext=true;Manual();
Check(host.Current?.Version=="fixture"&&TestState.Live==1&&TestState.Stops==23&&Status().GetProperty("error").GetString()!.Contains("fixture"),"Failed init did not clean candidate and restore previous module");
Console.WriteLine("PASS partial initialization cleanup and previous-version rollback");
int before=TestState.Initializes;
// Automatic file changes need two stable observations, not a partial copy.
File.SetLastWriteTimeUtc(path,DateTime.UtcNow.AddMinutes(1));Poll();
Check(TestState.Initializes==before,"Auto update skipped stability delay");
Poll();Poll();Check(TestState.Initializes==before+1&&TestState.Live==1,"Stable file not auto reloaded");
Console.WriteLine("PASS automatic stable publication reload, including unchanged code with refreshed assets");
host.Dispose();Check(TestState.Live==0,"Shutdown left active runtime");host.Dispose();
Console.WriteLine("PASS idempotent host shutdown");
