using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Quest;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;
public class HotAdminRequest:IRequestData { [JsonExtensionData] public Dictionary<string,JsonElement> Fields{get;set;}=[]; }
[Injectable]
public class AdminModuleRouter:StaticRouter {
 public AdminModuleRouter(JsonUtil json,AdminModuleHost host):base(json,[
  new RouteAction<HotAdminRequest>("/local/gear/data",(_,r,s,_,ct)=>host.Handle("data",r,s,ct)),
  new RouteAction<HotAdminRequest>("/local/gear/story",(_,r,s,_,ct)=>host.Handle("story",r,s,ct)),
  new RouteAction<HotAdminRequest>("/local/gear/reload",(_,r,s,_,ct)=>host.Handle("reload",r,s,ct))]){}
}
[Injectable]
public sealed class AdminModuleHost {
 sealed class ModuleContext():AssemblyLoadContext(isCollectible:true){protected override Assembly? Load(AssemblyName name)=>AssemblyName.ReferenceMatchesDefinition(typeof(GearMailService).Assembly.GetName(),name)?typeof(GearMailService).Assembly:AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>GetLoadContext(a)?.IsCollectible!=true&&AssemblyName.ReferenceMatchesDefinition(a.GetName(),name));}
 readonly SemaphoreSlim gate=new(1,1);readonly JsonUtil json;readonly HttpResponseUtil http;readonly object[] dataArgs,storyArgs;
 readonly string home=System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail");
 ModuleContext? context;object? data,story;Type? dataRequest,storyRequest;string hash="",error="";int generation;
 public static bool Ready{get;private set;}
 public AdminModuleHost(SaveServer saves,JsonUtil json,HttpResponseUtil http,TemplateTable templates,NotificationSendHelper notifications,MainStoryChapterHelper chapters,QuestVariableHelper variables,QuestConfig config){
  this.json=json;this.http=http;dataArgs=[saves,json,http,templates,notifications];storyArgs=[saves,json,http,templates,chapters,variables,config];try{Load();}catch(Exception e){error=e.GetBaseException().Message;try{LoadFile(System.IO.Path.Combine(home,"last-good-admin.module"));}catch{}WriteStatus();}
 }
 void WriteStatus(){try{Directory.CreateDirectory(home);var path=System.IO.Path.Combine(home,"admin-hotreload-status.json");File.WriteAllText(path+".new",JsonSerializer.Serialize(new{ready=Ready,generation,sha256=hash,error,updatedUtc=DateTime.UtcNow}));File.Move(path+".new",path,true);}catch{}}
 bool Load(){
  return LoadFile(System.IO.Path.Combine(home,"LocalGearAdmin.module"));
 }
 bool LoadFile(string path){
  byte[] bytes=File.ReadAllBytes(path);var digest=Convert.ToHexString(SHA256.HashData(bytes));if(digest==hash){error="";WriteStatus();return false;}
  var candidate=new ModuleContext();try{
   using var stream=new MemoryStream(bytes);var assembly=candidate.LoadFromStream(stream);
   object Build(string name,object[] args)=>Activator.CreateInstance(assembly.GetType("LocalGearMail."+name,true)!,args)!;
   var nextData=Build("CharacterDataService",dataArgs);var nextStory=Build("StoryService",storyArgs);
   var nextDataRequest=assembly.GetType("LocalGearMail.CharacterDataRequest",true)!;var nextStoryRequest=assembly.GetType("LocalGearMail.StoryRequest",true)!;
   foreach(var type in new[]{nextData.GetType(),nextStory.GetType()})if(type.GetMethod("Handle")?.ReturnType!=typeof(ValueTask<string>))throw new InvalidOperationException("模块接口不兼容。");
   File.WriteAllBytes(System.IO.Path.Combine(home,"last-good-admin.module"),bytes);
   var previous=context;context=candidate;data=nextData;story=nextStory;dataRequest=nextDataRequest;storyRequest=nextStoryRequest;hash=digest;generation++;error="";Ready=true;WriteStatus();previous?.Unload();return true;
  }catch{candidate.Unload();throw;}
 }
 public async ValueTask<string> Handle(string route,HotAdminRequest request,MongoId session,CancellationToken ct){
  await gate.WaitAsync(ct);try{
   var key=File.ReadAllText(System.IO.Path.Combine(home,"api-key.txt")).Trim();var supplied=request.Fields.TryGetValue("apiKey",out var secret)?secret.GetString()??"":"";
   if(!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key),Encoding.UTF8.GetBytes(supplied)))throw new ArgumentException("认证失败。");
   // Swaps run between requests, so no profile write can straddle module generations.
   if(route=="reload"){bool changed=Load();return http.GetBody(new{ok=true,ready=Ready,generation,sha256=hash,changed});}
   if(request.Fields.TryGetValue("action",out var action)&&action.GetString()=="reload"){Load();request.Fields["action"]=JsonSerializer.SerializeToElement("status");}
   if(!Ready)throw new ArgumentException("数据模块未加载，请检查服务端。");
   var service=route=="data"?data!:story!;var type=route=="data"?dataRequest!:storyRequest!;
   var payload=json.Deserialize(JsonSerializer.Serialize(request.Fields),type);
   return await (ValueTask<string>)service.GetType().GetMethod("Handle")!.Invoke(service,[payload,session,ct])!;
  }catch(Exception e){error=e.GetBaseException().Message;WriteStatus();return http.GetBody(new{ok=false},(BackendErrorCodes)228,error);}
  finally{gate.Release();}
 }
}
