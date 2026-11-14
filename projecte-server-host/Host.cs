using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Services.Modding.Custom;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
namespace ProjectE.ServerHost;
public record Metadata:IModMetadata {
 public string ModGuid{get;init;}="local.projecte";public string Name{get;init;}="ProjectE Runtime Host";public string Author{get;init;}="Local";public List<string>? Contributors{get;init;}
 public SemanticVersioning.Version Version{get;init;}=new("1.2.0");public SemanticVersioning.Range SptVersion{get;init;}=new("~5.0.0");public bool HasPrepatcher{get;init;}public List<string>? Incompatibilities{get;init;}public Dictionary<string,SemanticVersioning.Range>? ModDependencies{get;init;}public string? Url{get;init;}public string License{get;init;}="MIT";
}
public class Request:IRequestData {[JsonExtensionData]public Dictionary<string,JsonElement> Fields{get;set;}=[];}
[Injectable]
public sealed class Router:StaticRouter {
 public Router(JsonUtil json,RuntimeHost host):base(json,[
  new RouteAction<Request>("/local/projecte/table",(_,r,s,_,ct)=>host.Handle("table",r,s,ct)),
  new RouteAction<Request>("/local/projecte/terminal",(_,r,s,_,ct)=>host.Handle("terminal",r,s,ct)),
  new RouteAction<Request>("/local/projecte/runtime",(_,r,s,_,ct)=>host.Handle("runtime",r,s,ct))]){}
}
[Injectable(TypePriority=OnLoadOrder.Preload+10)]
public sealed class Bootstrap(RuntimeHost host):IOnLoad {public Task OnLoadAsync(CancellationToken ct)=>host.Initialize(ct);}
[Injectable(TypePriority=OnLoadOrder.TraderRegistration+10)]
public sealed class Catalog(RuntimeHost host):IOnLoad {public Task OnLoadAsync(CancellationToken ct)=>host.Rebuild(ct);}
[Injectable]
public sealed class RuntimeHost {
 sealed class Context():AssemblyLoadContext(isCollectible:true){protected override Assembly? Load(AssemblyName name)=>AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>GetLoadContext(a)?.IsCollectible!=true&&AssemblyName.ReferenceMatchesDefinition(a.GetName(),name));}
 readonly object[] arguments;readonly SemaphoreSlim gate=new(1,1);readonly HttpResponseUtil http;readonly ISptLogger<RuntimeHost> log;
 readonly string home=System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","ProjectE");
 Context? context;object? entry;MethodInfo? handler;string hash="",error="",rejected="";int generation;DateTime nextCheck;
 public RuntimeHost(CustomItemService custom,TemplateTable templates,TradersTable traders,SaveServer saves,ItemHelper helper,JsonUtil json,HttpResponseUtil http,LocaleTable locales,TraderConfig traderConfig,ImageRouter images,ISptLogger<RuntimeHost> log){this.http=http;this.log=log;arguments=[custom,templates,traders,saves,helper,json,http,locales,traderConfig,images,(Action<string>)(s=>log.Info(s)),typeof(RuntimeHost).Assembly];}
 public async Task Initialize(CancellationToken ct){await gate.WaitAsync(ct);try{try{await Load(ct);}catch when(File.Exists(System.IO.Path.Combine(home,"last-good-logic.module"))){await Load(ct,System.IO.Path.Combine(home,"last-good-logic.module"));}}finally{gate.Release();}}
 public async Task Rebuild(CancellationToken ct){await gate.WaitAsync(ct);try{if(entry!=null)entry.GetType().GetMethod("Rebuild")!.Invoke(entry,null);}finally{gate.Release();}}
 async Task<bool> Load(CancellationToken ct,string? path=null){
  path??=System.IO.Path.Combine(home,"Local.ProjectE.Logic.module");byte[] bytes=await File.ReadAllBytesAsync(path,ct);string digest=Convert.ToHexString(SHA256.HashData(bytes));if(digest==hash||entry!=null&&digest==rejected)return false;
  var candidate=new Context();try{
   using var stream=new MemoryStream(bytes);var assembly=candidate.LoadFromStream(stream);var type=assembly.GetType("ProjectE.Server.HotEntry",true)!;
   var next=Activator.CreateInstance(type,arguments)!;var nextHandler=type.GetMethod("Handle")??throw new MissingMethodException("HotEntry.Handle");
   if(nextHandler.ReturnType!=typeof(ValueTask<string>))throw new InvalidOperationException("Server module protocol mismatch");
   await (Task)type.GetMethod("Initialize")!.Invoke(next,[ct])!;
   type.GetMethod("Rebuild")!.Invoke(next,null);
   string saved=System.IO.Path.Combine(home,"last-good-logic.module");await File.WriteAllBytesAsync(saved+".new",bytes,ct);File.Move(saved+".new",saved,true);
   var previous=context;context=candidate;entry=next;handler=nextHandler;hash=digest;generation++;error="";rejected="";
   Status();previous?.Unload();log.Success("ProjectE server hot module ready; generation "+generation);return true;
  }catch(Exception e){candidate.Unload();rejected=digest;error=e.GetBaseException().Message;
   if(entry!=null){try{await (Task)entry.GetType().GetMethod("Initialize")!.Invoke(entry,[ct])!;entry.GetType().GetMethod("Rebuild")!.Invoke(entry,null);}catch(Exception restore){error+="; restore: "+restore.GetBaseException().Message;}}
   Status();throw;}
 }
 void Status(){Directory.CreateDirectory(home);var path=System.IO.Path.Combine(home,"server-hotreload-status.json");File.WriteAllText(path+".new",JsonSerializer.Serialize(new{ready=entry!=null,generation,sha256=hash,error,updatedUtc=DateTime.UtcNow}));File.Move(path+".new",path,true);}
 public async ValueTask<string> Handle(string route,Request request,MongoId session,CancellationToken ct){
  await gate.WaitAsync(ct);try{
   if(DateTime.UtcNow>=nextCheck){nextCheck=DateTime.UtcNow.AddSeconds(1);try{await Load(ct);}catch when(entry!=null){log.Warning("ProjectE rejected candidate server module: "+error);}}
   if(entry==null)throw new InvalidOperationException("ProjectE server module is not ready");
   if(route=="runtime")return http.GetBody(new{ok=true,ready=true,generation,sha256=hash,error});
   return await (ValueTask<string>)handler!.Invoke(entry,[route,JsonSerializer.Serialize(request.Fields),session,ct])!;
  }catch(Exception e){return http.GetBody(new{ok=false,error=e.GetBaseException().Message});}
  finally{gate.Release();}
 }
}
