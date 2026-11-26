using System.Security.Cryptography;
using System.Text.Json;
using Local.ModHost;
using Local.ModHost.Runtime;
using Local.HotModules;

// Read-only verification of a package produced by the PowerShell publisher.
if(args.Length==2&&args[0]=="--verify-package") {
    string home=Path.GetFullPath(args[1]);
    var package=Package.Read(home,File.ReadAllBytes(Path.Combine(home,"module.json")));
    var module=package.Prepare();
    Console.WriteLine($"PASS published package {package.Manifest.Id} / {module.Version}, hashes, entry contract and private assembly metadata");
    return;
}

int checks=0;
void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
string root=Path.Combine(Path.GetTempPath(),"universal-host-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
byte[] fixture=File.ReadAllBytes(args[0]),dependency=File.ReadAllBytes(args[1]);
List<string> logs=[],unpatched=[];
ModuleManager Make()=>new(root,(level,message)=>logs.Add(message),id=>unpatched.Add(id));
var host=Make();
double now=0;
string Data(string id,string name)=>Path.Combine(root,"state",id,"data",name);
int Count(string id,string name)=>File.Exists(Data(id,name))?int.Parse(File.ReadAllText(Data(id,name))):0;
void Flag(string id,string name,bool active){if(active)File.WriteAllText(Data(id,name),"");else File.Delete(Data(id,name));}
void Step(){host.Tick(now);now+=.3;}
void Until(Func<bool> condition,string message){for(int i=0;i<500;i++){Step();if(condition())return;Thread.Sleep(10);}throw new Exception("Timeout: "+message);}
string Phase(string id){using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,"state",id,"status.json")));return doc.RootElement.GetProperty("module").GetProperty("phase").GetString()!;}
string Publish(string id,string revision,string behavior="",bool enabled=true,string type="Fixture.TestModule",int api=1,string version="1.0.0") {
    string home=Path.Combine(root,"modules",id),release=Path.Combine(home,"releases",revision);
    Directory.CreateDirectory(release);
    File.WriteAllBytes(Path.Combine(release,"Host.TestModule.module"),fixture);
    File.WriteAllBytes(Path.Combine(release,"Fixture.Private.module"),dependency);
    File.WriteAllText(Path.Combine(release,"asset.txt"),revision);
    if(behavior.Length>0)File.WriteAllText(Path.Combine(release,behavior),"");
    var files=Directory.GetFiles(release).ToDictionary(p=>Path.GetFileName(p)!,p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
    var m=new ModuleManifest {Id=id,Name=id,Revision=revision,Version=version,ApiVersion=api,EntryAssembly="Host.TestModule.module",EntryType=type,Enabled=enabled,Files=files!};
    ModuleSlot.AtomicWrite(Path.Combine(home,"module.json"),JsonSerializer.SerializeToUtf8Bytes(m,Package.Json));
    return release;
}
try {
    Step();Check(host.Slots.Count==0,"Empty host starts");
    string first=Publish("alpha","r1");Publish("beta","r1");
    Until(()=>host.Slots.Count==2&&host.Slots.Values.All(s=>s.Ready),"multi-module startup");
    Check(Count("alpha","live")==1&&Count("beta","live")==1,"Two modules are independent");
    Check(File.ReadAllText(Data("alpha","dependency"))=="private-library-loaded","Private .module dependency resolves in isolated context");
    using(var file=new FileStream(Path.Combine(first,"Host.TestModule.module"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))Check(file.Length==fixture.Length,"Live module binary is unlocked");
    for(int i=0;i<10;i++){int starts=Count("alpha","starts");host.Request();Until(()=>Count("alpha","starts")==starts+1,"manual reload");}
    Check(Count("alpha","live")==1&&Count("beta","live")==1,"Ten swaps retain a single live instance per module");
    Check(unpatched.Count==20&&unpatched.Distinct().Count()==20,"Every replaced generation has a unique cleaned Harmony owner");
    Check(Count("alpha","cancelled")==10&&Count("alpha","releases")==10,"Lifetime token and registered cleanup run on replacement");
    Flag("alpha","busy",true);int before=Count("alpha","starts");
    Publish("alpha","r2");Publish("beta","r2");
    Until(()=>Phase("alpha")=="waiting"&&File.ReadAllText(Data("beta","assets"))=="r2","busy module isolated from other update");
    host.Dispatch(m=>m.Tick(),"Tick");
    Check(Count("alpha","starts")==before&&Count("alpha","ticks")==1,"Busy old module continues ticking");
    Flag("alpha","busy",false);Until(()=>Count("alpha","starts")==before+1,"resume waiting update");
    Check(File.ReadAllText(Data("alpha","assets"))=="r2","New immutable assets selected with new code");
    int stops=Count("alpha","stops");string bad=Publish("alpha","bad-hash");File.WriteAllText(Path.Combine(bad,"asset.txt"),"tampered");
    Until(()=>Phase("alpha")=="rejected","hash rejection");
    Check(host.Slots["alpha"].Ready&&Count("alpha","stops")==stops,"Bad hash preserves healthy runtime");
    File.WriteAllText(Path.Combine(root,"modules","alpha","module.json"),"{partial");
    Until(()=>logs.Any(l=>l.Contains("rejected")&&l.Contains("JSON",StringComparison.OrdinalIgnoreCase))||Phase("alpha")=="rejected","partial manifest");
    for(int i=0;i<12;i++){Step();Thread.Sleep(10);}
    Check(host.Slots["alpha"].Ready&&Count("alpha","stops")==stops,"Malformed JSON preserves runtime");
    foreach(var invalid in new[]{("api","Fixture.TestModule",9,"1.0.0"),("version","Fixture.TestModule",1,"9.0.0"),("ctor","Fixture.BadConstructor",1,"1.0.0"),("contract","Fixture.WrongContract",1,"1.0.0")}) {
        Publish("alpha",invalid.Item1,type:invalid.Item2,api:invalid.Item3,version:invalid.Item4);
        host.Request();for(int i=0;i<12;i++){Step();Thread.Sleep(10);}Until(()=>Phase("alpha")=="rejected","invalid candidate");
        Check(host.Slots["alpha"].Ready&&Count("alpha","stops")==stops,"Invalid candidate rejected before shutdown: "+invalid.Item1);
    }
    Publish("alpha","fails-init","init-fault");host.Request();
    Until(()=>Phase("alpha")=="rolled-back","init rollback");
    Check(Count("alpha","live")==1&&File.ReadAllText(Data("alpha","assets"))=="r2","Partial candidate cleaned; last-good code AND assets restored");
    Publish("alpha","r3");Until(()=>Phase("alpha")=="running"&&File.ReadAllText(Data("alpha","assets"))=="r3","recovery update");
    foreach(string callback in new[]{"tick","late","draw"}) {
        Flag("alpha",callback+"-fault",true);
        int healthy=Count("beta",callback=="tick"?"ticks":callback=="late"?"late":"draws");
        if(callback=="tick")host.Dispatch(m=>m.Tick(),"Tick");else if(callback=="late")host.Dispatch(m=>m.LateTick(),"LateTick");else host.Dispatch(m=>m.Draw(),"Draw");
        Check(Phase("alpha")=="faulted"&&Count("alpha","live")==0&&Count("beta",callback=="tick"?"ticks":callback=="late"?"late":"draws")==healthy+1,"Callback fault cleans one module and preserves another: "+callback);
        Flag("alpha",callback+"-fault",false);host.Request();Until(()=>host.Slots["alpha"].Ready,"manual recovery");
    }
    Flag("alpha","busy",true);Publish("alpha","off",enabled:false);
    Until(()=>Phase("alpha")=="waiting","disable waits");Check(host.Slots["alpha"].Ready,"Disable defers while busy");
    Flag("alpha","busy",false);Until(()=>Phase("alpha")=="disabled","disable");Check(Count("alpha","live")==0,"Disable cleanup");
    Publish("alpha","r4");Until(()=>host.Slots["alpha"].Ready,"reenable");
    Flag("alpha","busy",true);host.Automatic=false;
    string removalHome=Path.Combine(root,"modules","alpha");
    FunModulePackage.BeginRemoval(root,"alpha");FunModulePackage.BeginRemoval(root,"alpha");
    Check(FunModulePackage.Removing(root,"alpha")&&!File.Exists(Path.Combine(removalHome,"module.json")),"Removal request is persistent and idempotent");
    Check(!FunModulePackage.CompleteRemoval(root,"alpha")&&Directory.Exists(removalHome),"Removal cannot use a host status from before the request");
    Until(()=>Phase("alpha")=="waiting","uninstall waits while busy with automatic reload off");
    Check(host.Slots["alpha"].Ready&&!FunModulePackage.CompleteRemoval(root,"alpha")&&File.Exists(Path.Combine(removalHome,"releases","r4","asset.txt")),"Busy uninstall retains runtime and all payloads");
    Flag("alpha","busy",false);Until(()=>Phase("alpha")=="removed","manifest removal");Check(Count("alpha","live")==0,"Removal cleanup");
    Until(()=>FunModulePackage.CompleteRemoval(root,"alpha"),"uninstall waits for fresh host confirmation");
    Check(!Directory.Exists(removalHome)&&File.Exists(Data("alpha","starts"))&&File.Exists(Path.Combine(root,"state","alpha","last-good.json")),"Uninstall removes installed releases and preserves persistent data");
    Check(!FunModulePackage.CompleteRemoval(root,"alpha"),"Completed uninstall is idempotent");host.Automatic=true;
    Publish("alpha","r5");Until(()=>host.Slots["alpha"].Ready,"rediscovery");
    Flag("alpha","cleanup-fault",true);host.Request();Until(()=>Phase("alpha")=="cleanup-failed","cleanup blocks replacement");
    before=Count("alpha","starts");host.Request();for(int i=0;i<12;i++)Step();
    Check(!host.Slots["alpha"].Ready&&Count("alpha","starts")==before&&host.Slots["beta"].Ready,"Cleanup error requires restart and does not break other modules");
    Flag("alpha","cleanup-fault",false);host.Dispose();host.Dispose();
    Check(Count("alpha","live")==0&&Count("beta","live")==0,"Idempotent host shutdown");
    // A broken on-disk update can recover the persisted good revision on a fresh process/session.
    Publish("beta","cold-bad","init-fault");host=Make();
    Until(()=>host.Slots.TryGetValue("beta",out var slot)&&slot.Ready,"cold rollback");
    Check(File.ReadAllText(Data("beta","assets"))=="r2","Restart loads saved good revision after failed init");
    foreach(string path in new[]{"../outside","/absolute","C:/outside","a/../b","a\\b","a//b"}) {
        bool rejected=false;try{Package.Inside(root,path);}catch(InvalidDataException){rejected=true;}Check(rejected,"Reject escaping path: "+path);
    }
    Check(!Package.ValidId("..")&&!Package.ValidId("Bad ID"),"Reject invalid module ids");
    host.Automatic=false;
    int alphaStarts=Count("alpha","starts"),betaStarts=Count("beta","starts");
    Publish("alpha","manual-only");
    Publish("beta","manual-only");
    for(int i=0;i<12;i++){Step();Thread.Sleep(10);}
    Check(Count("alpha","starts")==alphaStarts,"Automatic off preserves existing module");
    File.WriteAllText(Path.Combine(root,"modules","alpha","reload.request"),"request");
    Until(()=>Count("alpha","starts")==alphaStarts+1,"per-module explicit reload with automatic off");
    Check(Count("beta","starts")==betaStarts,"Per-module request does not reload siblings");
    host.Request();Until(()=>Count("beta","starts")==betaStarts+1,"global explicit reload with automatic off");
    Check(Count("alpha","live")==1&&Count("beta","live")==1,"Manual reload respects stable publication while automatic is off");
    host.Automatic=true;
    string isolated=Publish("gamma","stability");
    Step();Check(!host.Slots.TryGetValue("gamma",out var newborn)||!newborn.Ready,"Discovery cannot skip publication stability window");
    Until(()=>host.Slots.TryGetValue("gamma",out var added)&&added.Ready,"new module during runtime");
    Check(Count("gamma","live")==1,"New module can be discovered without restarting host");
    var gm=JsonSerializer.Deserialize<ModuleManifest>(File.ReadAllBytes(Path.Combine(root,"modules","gamma","module.json")),Package.Json)!;
    foreach(var mutation in new[]{gm with{Files=new(){{"../escape",new string('0',64)}},EntryAssembly="../escape"},gm with{Id="some-other-id"},gm with{Files=new(){{"Host.TestModule.module",new string('0',64)}}}}) {
        bool rejected=false;try{Package.Read(Path.Combine(root,"modules","gamma"),JsonSerializer.SerializeToUtf8Bytes(mutation));}catch(Exception){rejected=true;}
        Check(rejected,"Reject bad manifest content without executing code");
    }
    var context=new ModuleContext("test",root,root,"owner",(_,_)=>{});List<int> cleanup=[];
    context.OnShutdown(()=>cleanup.Add(1));context.OnShutdown(()=>{cleanup.Add(2);throw new Exception("expected");});context.OnShutdown(()=>cleanup.Add(3));
    try{context.Release();}catch(AggregateException){}
    context.Release();Check(cleanup.SequenceEqual(new[]{3,2,1})&&!context.IsActive&&context.Stopping.IsCancellationRequested,"All LIFO cleanup runs exactly once despite an exception");
    Console.WriteLine($"PASS {checks} universal host lifecycle, isolation, rollback, hashing, discovery and cleanup checks");
    Console.WriteLine("Test artifacts: "+root);
} finally {host.Dispose();}
