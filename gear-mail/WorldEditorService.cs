using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LocalSpt.WorldEditing;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Generators.Loot;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;
public sealed class WorldEditorRequest:IRequestData {
 [JsonPropertyName("apiKey")]public string ApiKey{get;set;}="";
 [JsonPropertyName("action")]public string Action{get;set;}="catalog";
 [JsonPropertyName("map")]public string Map{get;set;}="";
 [JsonPropertyName("template")]public string Template{get;set;}="";
 [JsonPropertyName("role")]public string Role{get;set;}="";
 [JsonPropertyName("options")]public ContainerLootOptions Options{get;set;}=new();
 [JsonPropertyName("parameters")]public JsonElement Parameters{get;set;}
}
[Injectable]
public sealed class WorldEditorRouter:StaticRouter {
 public WorldEditorRouter(JsonUtil json,WorldEditorService service):base(json,[new RouteAction<WorldEditorRequest>("/local/gear/world-editor",(_,r,s,_,ct)=>service.Handle(r,s,ct))]){}
}
[Injectable]
public sealed class WorldEditorService(LocationLootGenerator generator,LocationTable locations,TemplateTable templates,BotTable bots,BotConfig botConfig,PmcConfig pmcConfig,LocationConfig locationConfig,SaveServer saves,HttpResponseUtil http) {
 static readonly MethodInfo AddLoot=typeof(LocationLootGenerator).GetMethod("AddLootToContainer",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!;
 public async ValueTask<string> Handle(WorldEditorRequest request,MongoId session,CancellationToken ct){
  var gate=GearMailService.SessionLock(session);await gate.WaitAsync(ct);
  try{
   string key=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","api-key.txt"),ct);
   if(!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key.Trim()),Encoding.UTF8.GetBytes(request.ApiKey)))throw new ArgumentException("认证失败。");
   var pmc=saves.GetProfile(session)?.CharacterData?.PmcData;if(pmc?.Info==null)throw new ArgumentException("当前角色尚未就绪。");
   if(request.Action=="overview"){
    string[] currencies=["5449016a4bdc2d6f028b456f","5696686a4bdc2da3298b456a","569668774bdc2da2298b4568"];
    return http.GetBody(new{ok=true,profile=pmc.Id.ToString(),money=currencies.Select(t=>pmc.Inventory?.Items?.Where(i=>i.Template.ToString()==t).Sum(i=>(long)(i.Upd?.StackObjectsCount??1))??0).ToArray()});
   }
   var map=locations.GetLocation(request.Map)??throw new ArgumentException("地图不存在。");
   if(request.Action=="parameters"){
    var parametersBridge=ParameterBridge()??throw new ArgumentException("请更新并重启服务端战局规则模块。");
    string action=request.Parameters.TryGetProperty("operation",out var op)?op.GetString()??"read":"read";
    if(action is not ("read" or "apply" or "save" or "load" or "defaults"))throw new ArgumentException("未知参数操作。");
    string response=(string)parametersBridge.Invoke(null,[action,request.Map.ToLowerInvariant(),map.Base,request.Parameters])!;
    return http.GetBody(JsonSerializer.Deserialize<JsonElement>(response));
   }
   if(request.Action=="catalog")return http.GetBody(new{ok=true,map=request.Map,serverLootReady=AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetType("LocalRaidControls.Patches")?.GetMethod("GenerateEditorContainer",BindingFlags.Public|BindingFlags.Static)!=null),serverLootTemplates=(map.StaticLoot?.Value?.Keys??Enumerable.Empty<MongoId>()).Select(k=>k.ToString()).ToArray(),containers=templates.Items.Where(p=>p.Value.Parent.ToString()=="566965d44bdc2d814c8b4571").Select(p=>new{id=p.Key.ToString(),name=p.Value.Name})});
   if(request.Action=="aiInfo"){
    request.Role=(request.Role??"").Trim().ToLowerInvariant();var role=string.IsNullOrWhiteSpace(request.Role)?null:bots.Types.GetValueOrDefault(request.Role);if(request.Role.Length>0&&role==null)throw new ArgumentException("未知角色代号，请在角色目录中查找。");
    JsonElement? effective=null;if(ParameterBridge() is {} parameterBridge)effective=JsonSerializer.Deserialize<JsonElement>((string)parameterBridge.Invoke(null,["read",request.Map.ToLowerInvariant(),map.Base,default(JsonElement)])!);
    return http.GetBody(new{ok=true,map=request.Map,updatedUtc=DateTime.UtcNow,source="live-server",botConfig,pmcConfig,locationConfig,core=bots.Core,mapAi=effective is {} e?(object)e.GetProperty("effectiveMap"):AiMap(map.Base),roles=bots.Types.Keys.Order().ToArray(),role=role==null?null:new{request.Role,configuration=role},assistant=effective is {} a?a.GetProperty("assistant"):AssistantSettings()});
   }
   if(request.Action!="loot")throw new ArgumentException("未知编辑器操作。");
   request.Options??=new();request.Options.Validate();
   var tpl=new MongoId(request.Template);
   if(!templates.Items.TryGetValue(tpl,out var definition)||definition.Parent.ToString()!="566965d44bdc2d814c8b4571")throw new ArgumentException("请选择有效的地图容器。");
   string id=new MongoId().ToString();
   var shell=new StaticContainerData{Probability=1,Template=new SpawnpointTemplate{Id="assistant-editor-"+id,Root=id,IsContainer=true,IsAlwaysSpawn=true,UseGravity=false,RandomRotation=false,Position=new(),Rotation=new(),IsGroupPosition=false,GroupPositions=[],Items=[new SptLootItem{Id=new MongoId(id),Template=tpl}]}};
   if(request.Options.Mode=="empty")return http.GetBody(new{ok=true,root=id,items=shell.Template.Items});
   var distribution=map.StaticLoot?.Value??new Dictionary<MongoId,StaticLootDetails>();
   if(request.Options.Mode=="custom"){
    foreach(var item in request.Options.Items)if(!templates.Items.ContainsKey(new MongoId(item.Template)))throw new ArgumentException("自定义物资包含未知物品。");
    double maximumWeight=request.Options.Items.Select(i=>i.Weight).DefaultIfEmpty(1).Max();
    distribution=new(distribution){[tpl]=new StaticLootDetails{ItemCountDistribution=[new ItemCountDistribution{Count=request.Options.Rolls,RelativeProbability=1}],ItemDistribution=request.Options.Items.Select(i=>new ItemDistribution{Tpl=new MongoId(i.Template),RelativeProbability=(float)Math.Max(float.Epsilon,i.Weight/maximumWeight)}).ToArray()}};
   }else if(!distribution.ContainsKey(tpl))throw new ArgumentException("当前地图的服务端物资表没有此容器；请选择自定义配置或空箱。");
   var bridge=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LocalRaidControls.Patches")?.GetMethod("GenerateEditorContainer",BindingFlags.Public|BindingFlags.Static)).FirstOrDefault(m=>m!=null);
   if(request.Options.Mode=="server"&&bridge==null)throw new ArgumentException("请更新并重启服务端，当前规则模块缺少容器生成接口。");
   var result=(StaticContainerData)(bridge!=null?bridge.Invoke(null,[generator,shell,distribution,map.StaticAmmo,request.Map,request.Options.Mode=="server"]):AddLoot.Invoke(generator,[shell,Array.Empty<StaticForced>(),distribution,map.StaticAmmo,request.Map]))!;
   return http.GetBody(new{ok=true,root=result.Template!.Root,items=result.Template.Items});
  }catch(Exception e){return http.GetBody(new{ok=false},(BackendErrorCodes)228,e.GetBaseException().Message);}
  finally{gate.Release();}
 }
 static object AiMap(LocationBase map)=>map.GetType().GetProperties().Where(p=>p.Name.Contains("Bot",StringComparison.OrdinalIgnoreCase)||p.Name.Contains("Spawn",StringComparison.OrdinalIgnoreCase)||p.Name.Contains("Wave",StringComparison.OrdinalIgnoreCase)||p.Name is "Name" or "Id" or "NewSpawn" or "OldSpawn" or "OpenZones").ToDictionary(p=>p.Name,p=>p.GetValue(map));
 static MethodInfo? ParameterBridge()=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LocalRaidControls.RaidParameters")?.GetMethod("Handle",BindingFlags.Static|BindingFlags.Public)).FirstOrDefault(m=>m!=null);
 static JsonElement? AssistantSettings(){try{using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"user","mods","LocalRaidControls","settings.json")));return d.RootElement.Clone();}catch{return null;}}
}
