using System.Reflection;
using System.Text.Json.Nodes;
using Local.HotModules;
using Local.ModHost;
using Mono.Cecil;
[assembly:AssemblyMetadata("HotModuleId","test-extra")]
[assembly:AssemblyMetadata("HotModuleName","Extra module")]
[assembly:AssemblyMetadata("HotModuleEntry","ExtraModule")]
int count=0;
void Check(bool value,string label){if(!value)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
void Reject(Action action,string label){try{action();}catch{Check(true,label);return;}throw new Exception(label);}
string repo=Path.GetFullPath(args[0]),game=Path.GetFullPath(args[1]),root=Path.Combine(Path.GetTempPath(),"fun-module-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
try{
 using(var core=AssemblyDefinition.ReadAssembly(Path.Combine(repo,"esp-cover/bin/Release/net10.0/Local.Spt5.ESP.Core.dll"))){
  Check(!core.MainModule.Types.Any(t=>t.Namespace.StartsWith("LocalBlackwallPulse")||t.Namespace.StartsWith("ProjectE.Client")||t.Namespace.StartsWith("AllTheModium.Client")),"core excludes optional implementations");
  Check(!core.MainModule.AssemblyReferences.Any(a=>a.Name is "Local.BlackwallPulse" or "Local.ProjectE.HotModule" or "Local.AllTheModium.HotModule"),"core has no direct optional assembly dependencies");
 }
 using(var server=AssemblyDefinition.ReadAssembly(Path.Combine(repo,"gear-mail/bin/Release/net10.0/LocalGearMail.dll"))){
  Check(!server.MainModule.Types.Any(t=>t.Namespace.StartsWith("ProjectE")||t.Namespace.StartsWith("AllTheModium")),"main server excludes Minecraft implementations");
  Check(new[]{"DeveloperTrader","OptionalItemCompatibility"}.All(name=>server.MainModule.Types.Single(t=>t.Name==name).CustomAttributes.Any(a=>a.AttributeType.FullName is "SPTarkov.DI.Annotations.Injectable" or "SPTarkov.DI.Annotations.InjectableAttribute")),"generic trader and compatibility hooks remain registered for dependency injection");
 }
 string settings=Path.Combine(root,"misc-settings.json");Check(LocalGearMail.FunItemPolicy.ReadFlag(settings,"DeveloperTraderEnabled"),"missing trader setting retains existing enabled default");File.WriteAllText(settings,"{\"FunItemsEnabled\":false,\"DeveloperTraderEnabled\":true}");Check(LocalGearMail.FunItemPolicy.ReadFlag(settings,"DeveloperTraderEnabled")&&!LocalGearMail.FunItemPolicy.ReadFlag(settings,"FunItemsEnabled"),"developer trader and fun-item switches are independent");File.WriteAllText(settings,System.Text.Json.JsonSerializer.Serialize(new LocalMiscShared.MiscSettings{DeveloperTraderEnabled=false}));Check(!LocalGearMail.FunItemPolicy.ReadFlag(settings,"DeveloperTraderEnabled"),"saved client trader choice is read at server startup");
 Reject(()=>FunModulePackage.SetEnabled(root,"../outside",true),"traversal id rejected");Reject(()=>FunModulePackage.SetEnabled(root,"missing",true),"missing module cannot be enabled");
 string extra=Path.Combine(root,"extra.module");File.Copy(Assembly.GetExecutingAssembly().Location,extra);Check(FunModulePackage.Install(extra,root)=="test-extra"&&FunModulePackage.Manifest(root,"test-extra")!["entryType"]!.GetValue<string>()=="ExtraModule","new modules import through metadata without editing core registration");
 var legacy=FunModulePackage.Manifest(root,"test-extra")!;legacy.Remove("assistantSettings");Check(FunModulePackage.HasSettings(root,"test-extra",legacy),"legacy module remains discoverable through embedded metadata");
 string extraHome=FunModulePackage.Home(root,"test-extra"),extraData=Path.Combine(root,"state","test-extra","data");Directory.CreateDirectory(extraData);File.WriteAllText(Path.Combine(extraData,"saved.json"),"retained");
 FunModulePackage.BeginRemoval(root,"test-extra");
 Reject(()=>FunModulePackage.Install(extra,root),"import cannot race a pending uninstall");
 Check(FunModulePackage.RemovalManifest(root,"test-extra")!["name"]!.GetValue<string>()=="Extra module","pending uninstall retains the displayed module name");
 string hostStatus=Path.Combine(root,"host-status.json");
 void WriteHostStatus(int processId,DateTime updatedUtc,string phase="removed",bool ready=false,bool blocked=false)=>File.WriteAllText(hostStatus,System.Text.Json.JsonSerializer.Serialize(new{processId,updatedUtc,modules=new[]{new{id="test-extra",phase,ready,restartRequired=blocked}},discoveryErrors=new{}}));
 WriteHostStatus(Environment.ProcessId+1,DateTime.UtcNow);Check(!FunModulePackage.CompleteRemoval(root,"test-extra"),"different host process cannot authorize deletion");
 WriteHostStatus(Environment.ProcessId,DateTime.UtcNow.AddMinutes(-1));Check(!FunModulePackage.CompleteRemoval(root,"test-extra"),"stale host status cannot authorize deletion");
 WriteHostStatus(Environment.ProcessId,DateTime.UtcNow,"cleanup-failed",false,true);Check(!FunModulePackage.CompleteRemoval(root,"test-extra")&&Directory.Exists(extraHome),"failed native cleanup retains installed files");
 WriteHostStatus(Environment.ProcessId,DateTime.UtcNow);Check(FunModulePackage.CompleteRemoval(root,"test-extra")&&!Directory.Exists(extraHome)&&File.ReadAllText(Path.Combine(extraData,"saved.json"))=="retained","confirmed uninstall removes package and keeps saved data");
 Check(FunModulePackage.Install(extra,root)=="test-extra"&&File.Exists(Path.Combine(extraData,"saved.json")),"deleted module can be reimported without losing data");
 Reject(()=>FunModulePackage.BeginRemoval(root,"../outside"),"uninstall traversal id rejected");Reject(()=>FunModulePackage.BeginRemoval(root,"missing"),"uninstall does not create missing module folders");
 string corrupt=Path.Combine(root,"corrupt.module");File.WriteAllText(corrupt,"not an assembly");Reject(()=>FunModulePackage.Install(corrupt,root),"corrupt module rejected before activation");
 var owner=new ModuleContext("test",root,Path.Combine(root,"data"),"test",(_,_)=>{});var context=PackagedModuleAssets.Open(owner,Assembly.GetExecutingAssembly());string asset=Path.Combine(context.ModuleDirectory,"Assets/nested/测试.txt");
 Check(File.ReadAllText(asset).Contains("module resource fixture"),"standalone module extracts nested Unicode resources");File.WriteAllText(asset,"modified");var repaired=PackagedModuleAssets.Open(owner,Assembly.GetExecutingAssembly());Check(File.ReadAllText(asset).Contains("module resource fixture"),"modified cached resources are repaired");owner.Release();Check(!context.IsActive&&!repaired.IsActive,"host lifetime closes extracted-resource contexts");
 Console.WriteLine($"PASS {count} optional module checks");
}finally{Directory.Delete(root,true);}
public sealed class ExtraModule:IHotModule {
 public int ApiVersion=>1;public string Version=>"1.0.0";public bool CanReload=>true;
 public void Initialize(ModuleContext context){}public void Tick(){}public void LateTick(){}public void Draw(){}public void Shutdown(){}
}
