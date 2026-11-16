using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Modding.Custom;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;
public sealed class MinecraftRequest:IRequestData {[JsonExtensionData]public Dictionary<string,JsonElement> Fields{get;set;}=[];}
[Injectable]
public sealed class MinecraftService {
 readonly ProjectE.Server.HotEntry entry;readonly HttpResponseUtil http;
 public MinecraftService(CustomItemService custom,TemplateTable templates,TradersTable traders,SaveServer saves,ItemHelper helper,JsonUtil json,HttpResponseUtil http,LocaleTable locales,TraderConfig config,ImageRouter images,ISptLogger<MinecraftService> log){this.http=http;entry=new(custom,templates,traders,saves,helper,json,http,locales,config,images,s=>log.Info(s),typeof(MinecraftService).Assembly);}
 public Task Initialize(CancellationToken ct)=>entry.Initialize(ct);
 public void Rebuild()=>entry.Rebuild();
 public async ValueTask<string> Handle(string route,MinecraftRequest request,MongoId session,CancellationToken ct){try{if(route=="runtime")return http.GetBody(new{ok=true,ready=true,integrated=true,hotReload=false,generation=1,sha256="assistant-2.3.1",version="2.3.1",funItemsEnabled=FunItemPolicy.EnabledAtStartup,error=""});if(!FunItemPolicy.EnabledAtStartup)return http.GetBody(new{ok=false,error="趣味物品已停用；修改开关并重启服务端后启用。"});return await entry.Handle(route,JsonSerializer.Serialize(request.Fields),session,ct);}catch(Exception error){return http.GetBody(new{ok=false,error=error.GetBaseException().Message});}}
}
[Injectable(TypePriority=OnLoadOrder.Preload+10)]
public sealed class MinecraftBootstrap(MinecraftService service):IOnLoad {public Task OnLoadAsync(CancellationToken ct)=>service.Initialize(ct);}
[Injectable(TypePriority=OnLoadOrder.TraderRegistration+10)]
public sealed class MinecraftCatalog(MinecraftService service):IOnLoad {public Task OnLoadAsync(CancellationToken ct){service.Rebuild();return Task.CompletedTask;}}
[Injectable]
public sealed class MinecraftRouter:StaticRouter {
 public MinecraftRouter(JsonUtil json,MinecraftService service):base(json,[new RouteAction<MinecraftRequest>("/local/projecte/table",(_,r,s,_,ct)=>service.Handle("table",r,s,ct)),new RouteAction<MinecraftRequest>("/local/projecte/terminal",(_,r,s,_,ct)=>service.Handle("terminal",r,s,ct)),new RouteAction<MinecraftRequest>("/local/projecte/runtime",(_,r,s,_,ct)=>service.Handle("runtime",r,s,ct))]){}
}
