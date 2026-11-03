using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Models.Utils;
using Path=System.IO.Path;
namespace LocalGearMail;
public record Metadata:IModMetadata{
 public string ModGuid{get;init;}="local.spt5.gear-mail";
 public string Name{get;init;}="Local Gear Mail API";
 public string Author{get;init;}="Local";
 public List<string>? Contributors{get;init;}
 public SemanticVersioning.Version Version{get;init;}=new("1.5.0");
 public SemanticVersioning.Range SptVersion{get;init;}=new("~5.0.0");
 public bool HasPrepatcher{get;init;}
 public List<string>? Incompatibilities{get;init;}
 public Dictionary<string,SemanticVersioning.Range>? ModDependencies{get;init;}
 public string? Url{get;init;}
 public string License{get;init;}="MIT";
}
public class MailRequest:IRequestData{
 [JsonPropertyName("apiKey")] public string ApiKey{get;set;}="";
 [JsonPropertyName("deliveryId")] public string DeliveryId{get;set;}="";
 [JsonPropertyName("message")] public string Message{get;set;}="装备已送达，请领取。";
 [JsonPropertyName("items")] public List<GearSpec> Items{get;set;}=[];
 [JsonPropertyName("allowIncomplete")] public bool AllowIncomplete{get;set;}
}
public class GearSpec{
 [JsonPropertyName("ref")] public string Ref{get;set;}="";
 [JsonPropertyName("template")] public string Template{get;set;}="";
 [JsonPropertyName("parent")] public string? Parent{get;set;}
 [JsonPropertyName("slot")] public string? Slot{get;set;}
 [JsonPropertyName("count")] public int Count{get;set;}=1;
 [JsonPropertyName("attributes"),JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public GearAttributes? Attributes{get;set;}
 [JsonPropertyName("location"),JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? Location{get;set;}
 [JsonPropertyName("poisonUses"),JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public double? PoisonUses{get;set;}
}
public record Receipt(string Digest,string MessageId,int RootCount,int ItemCount);
public class EmptyRequest:IRequestData{}
[Injectable]
public class GearMailRouter:StaticRouter{
 public GearMailRouter(JsonUtil json,GearMailService service):base(json,[
  new RouteAction<MailRequest>("/local/gear/mail",(_,r,s,_,ct)=>service.Send(r,s,ct)),
  new RouteAction<MailRequest>("/local/gear/validate",(_,r,_,_,_)=>new ValueTask<string>(service.Validate(r))),
  new RouteAction<EmptyRequest>("/local/gear/status",(_,_,_,_,_)=>new ValueTask<string>(service.Status()))
 ]){}
}
[Injectable]
public class GearMailService(
 TemplateTable templates,ItemHelper helper,MailSendService mail,SaveServer saves,
 JsonUtil json,HttpResponseUtil http,ISptLogger<GearMailService> log){
 static readonly ConcurrentDictionary<string,SemaphoreSlim> Locks=new();
 internal static SemaphoreSlim SessionLock(MongoId session)=>Locks.GetOrAdd(session.ToString(),_=>new SemaphoreSlim(1,1));
 static readonly Regex IdPattern=new("^[a-zA-Z0-9_.-]{1,80}$");
 static readonly Regex MongoPattern=new("^[0-9a-f]{24}$");
 const string ReceiptKey="localGearMailReceipts";
 readonly string home=Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail");
 public string Status()=>http.GetBody(new{ok=true,version="1.5.0",delivery="system-mail",assembledItems=true,chamberAttachments=true,customPoisonUses=true,customItemProperties=true,extendedItemProperties=true,magazineContents=true,profileManagement=true,scavPresets=true,maxPoisonUses=2147483520d});
 public string Validate(MailRequest request){
  try{
   var key=File.ReadAllText(Path.Combine(home,"api-key.txt")).Trim();
   if(!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key),Encoding.UTF8.GetBytes(request.ApiKey??"")))throw new ArgumentException("Invalid API key");
   var specifications=ValidateAndExpand(request.Items,request.AllowIncomplete);
   var result=specifications.Select(spec=>{var props=Props(Template(spec.Template));var upd=helper.GenerateUpdForItem(templates.Items[new MongoId(spec.Template)])??new Upd();upd.StackObjectsCount=spec.Count;GearAttributeRules.Apply(upd,spec.Attributes,props,spec.Template);int? keyMaximum=props.TryGetProperty("MaximumNumberOfUsage",out var maximum)&&maximum.ValueKind==JsonValueKind.Number?maximum.GetInt32():null;return new{reference=spec.Ref,template=spec.Template,upd,keyMaximum};}).ToList();
   return http.GetBody(new{ok=true,rootCount=specifications.Count(item=>item.Parent==null),itemCount=result.Count,items=result});
  }catch(ArgumentException error){return http.GetBody(new{ok=false},(BackendErrorCodes)228,error.Message);}
 }
 public async ValueTask<string> Send(MailRequest request,MongoId session,CancellationToken ct){
  var gate=Locks.GetOrAdd(session.ToString(),_=>new SemaphoreSlim(1,1));
  await gate.WaitAsync(ct);
  try{
   var secretPath=Path.Combine(home,"api-key.txt");
   if(!File.Exists(secretPath))throw new ArgumentException("API key is not configured");
   var expected=Encoding.UTF8.GetBytes(File.ReadAllText(secretPath).Trim());
   var supplied=Encoding.UTF8.GetBytes(request.ApiKey??"");
   if(!CryptographicOperations.FixedTimeEquals(expected,supplied))throw new ArgumentException("Invalid API key");
   if(!IdPattern.IsMatch(request.DeliveryId??""))throw new ArgumentException("Invalid deliveryId");
   if(request.Message is null||request.Message.Length>1000)throw new ArgumentException("Message must be at most 1000 characters");
   var profile=saves.GetProfile(session);
   if(profile?.CharacterData?.PmcData?.Info is null)throw new ArgumentException("Unknown player profile");
   string canonical=request.AllowIncomplete?JsonSerializer.Serialize(new{request.DeliveryId,request.Message,request.Items,request.AllowIncomplete}):JsonSerializer.Serialize(new{request.DeliveryId,request.Message,request.Items});
   var digest=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
   var receipts=new Dictionary<string,Receipt>();
   if(profile.ExtensionData?.TryGetValue(ReceiptKey,out var stored)==true)
    receipts=JsonSerializer.Deserialize<Dictionary<string,Receipt>>(JsonSerializer.Serialize(stored))??[];
   if(receipts.TryGetValue(request.DeliveryId!,out var previous)){
    if(previous.Digest!=digest)throw new ArgumentException("deliveryId already used with a different payload");
    await saves.SaveProfileAsync(session);
    return http.GetBody(new{ok=true,alreadyDelivered=true,deliveryId=request.DeliveryId,messageId=previous.MessageId,rootCount=previous.RootCount,itemCount=previous.ItemCount});
   }
   var specs=ValidateAndExpand(request.Items,request.AllowIncomplete);
   var ids=specs.ToDictionary(x=>x.Ref,_=>new MongoId());
   var items=new List<Item>();
   foreach(var spec in specs){
    var template=templates.Items[new MongoId(spec.Template)];
    var upd=helper.GenerateUpdForItem(template)??new Upd();
    upd.StackObjectsCount=spec.Count;
    GearAttributeRules.Apply(upd,spec.Attributes,Props(Template(spec.Template)),spec.Template);
    if(spec.PoisonUses.HasValue)upd.SideEffect=new UpdSideEffect{Value=spec.PoisonUses.Value};
    var item=new Item{Id=ids[spec.Ref],Template=template.Id,Upd=upd};
    if(spec.Parent is not null){item.ParentId=ids[spec.Parent].ToString();item.SlotId=spec.Slot;}
    if(spec.Location.HasValue)item.Location=spec.Location.Value;
    items.Add(item);
   }
   var backup=Path.Combine(home,"backups",$"{session}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{request.DeliveryId}.json");
   Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
   await File.WriteAllTextAsync(backup,json.Serialize(profile,true),ct);
   var before=(profile.DialogueRecords??[]).Values.SelectMany(d=>d.Messages??[]).Select(m=>m.Id).ToHashSet();
   mail.SendSystemMessageToPlayer(session,request.Message,items,30L*24*60*60);
   var delivered=(profile.DialogueRecords??[]).Values.SelectMany(d=>d.Messages??[]).Single(m=>!before.Contains(m.Id)&&m.Text==request.Message&&m.HasRewards==true);
   var roots=specs.Count(x=>x.Parent is null);
   receipts[request.DeliveryId!]=new Receipt(digest,delivered.Id.ToString(),roots,items.Count);
   profile.ExtensionData??=[];profile.ExtensionData[ReceiptKey]=receipts;
   await saves.SaveProfileAsync(session);
   log.Success($"Gear mail delivered: {request.DeliveryId}; roots={roots}, items={items.Count}");
   return http.GetBody(new{ok=true,alreadyDelivered=false,deliveryId=request.DeliveryId,messageId=delivered.Id.ToString(),rootCount=roots,itemCount=items.Count});
  }catch(ArgumentException e){return http.GetBody(new{ok=false},(BackendErrorCodes)228,e.Message);}
   catch(Exception e){log.Error("Gear mail failed: "+e);return http.GetBody(new{ok=false},(BackendErrorCodes)228,"Gear mail delivery failed; consult the server log and retry with the same deliveryId");}
  finally{gate.Release();}
 }
 JsonElement Template(string id){
  if(!MongoPattern.IsMatch(id??"")||!templates.Items.TryGetValue(new MongoId(id!),out var t))throw new ArgumentException("Unknown item template: "+id);
  return JsonSerializer.Deserialize<JsonElement>(json.Serialize(t)??throw new InvalidOperationException("Cannot serialize template"));
 }
 static JsonElement Props(JsonElement t)=>t.GetProperty("_props");
 static IEnumerable<JsonElement> Slots(JsonElement t){
  foreach(var key in new[]{"Slots","Chambers","Cartridges"})
   if(Props(t).TryGetProperty(key,out var slots)&&slots.ValueKind==JsonValueKind.Array)
    foreach(var slot in slots.EnumerateArray())yield return slot;
 }
 bool IsA(string id,string ancestor){
  var seen=new HashSet<string>();
  while(seen.Add(id)){
   if(id==ancestor)return true;
   if(!templates.Items.TryGetValue(new MongoId(id),out var template)||template.Parent.ToString()==id)return false;
   id=template.Parent.ToString();if(!MongoPattern.IsMatch(id))return false;
  }return false;
 }
 bool Allowed(string item,JsonElement slot){
  foreach(var filter in slot.GetProperty("_props").GetProperty("filters").EnumerateArray()){
   var included=filter.TryGetProperty("Filter",out var values)&&values.EnumerateArray().Any(v=>IsA(item,v.GetString()!));
   var excluded=filter.TryGetProperty("ExcludedFilter",out var denied)&&denied.EnumerateArray().Any(v=>IsA(item,v.GetString()!));
   if(included&&!excluded)return true;
  }return false;
 }
 List<GearSpec> ValidateAndExpand(List<GearSpec> input,bool allowIncomplete){
  if(input is null||input.Count is <1 or >100)throw new ArgumentException("Provide 1 to 100 items");
  var result=input.Select(s=>s is null?throw new ArgumentException("Null item"):new GearSpec{Ref=s.Ref,Template=s.Template,Parent=string.IsNullOrEmpty(s.Parent)?null:s.Parent,Slot=s.Slot,Count=s.Count,PoisonUses=s.PoisonUses,Attributes=s.Attributes,Location=s.Location}).ToList();
  if(result.Any(s=>!IdPattern.IsMatch(s.Ref??""))||result.Select(s=>s.Ref).Distinct().Count()!=result.Count)throw new ArgumentException("Item refs must be unique safe names");
  for(int i=0;i<result.Count;i++){
   if(result.Count>100)throw new ArgumentException("Too many required parts");
   var spec=result[i];var t=Template(spec.Template);var props=Props(t);
   if(t.GetProperty("_type").GetString()!="Item")throw new ArgumentException("Template is not an item");
   // Some non-stackable attachment templates use zero for their inherited stack limit.
   int max=props.TryGetProperty("StackMaxSize",out var stack)?Math.Max(1,(int)stack.GetDouble()):1;
   if(spec.Count<1||spec.Count>Math.Min(max,100000))throw new ArgumentException("Invalid stack count: "+spec.Ref);
   if(spec.Parent is not null&&spec.Slot!="cartridges"&&spec.Count!=1)throw new ArgumentException("Attached items must have count=1");
   GearAttributeRules.Apply(helper.GenerateUpdForItem(templates.Items[new MongoId(spec.Template)])??new Upd(),spec.Attributes,props,spec.Template);
   if(spec.PoisonUses is double poison){
    if(spec.Template!="5fc64ea372b0dd78d51159dc")throw new ArgumentException("poisonUses is only supported for the cultist knife");
    if(!double.IsFinite(poison)||poison<0||poison>2147483520d||poison!=Math.Truncate(poison))throw new ArgumentException("poisonUses must be an integer from 0 to 2147483520 (safe float32 maximum below Int32 overflow)");
   }
   foreach(var slot in Slots(t).Where(s=>s.TryGetProperty("_required",out var r)&&r.ValueKind==JsonValueKind.True)){
    var name=slot.GetProperty("_name").GetString()!;
    if(result.Any(s=>s.Parent==spec.Ref&&s.Slot==name))continue;
    string? plate=null;
    foreach(var f in slot.GetProperty("_props").GetProperty("filters").EnumerateArray())
     if(f.TryGetProperty("Plate",out var part)&&part.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(part.GetString())){plate=part.GetString();break;}
    if(plate is null){if(allowIncomplete)continue;throw new ArgumentException("Missing required slot "+name+" on "+spec.Ref);}
    var key="part_"+i+"_"+result.Count;
    if(result.Any(s=>s.Ref==key))throw new ArgumentException("Reserved auto-part ref collision");
    result.Add(new GearSpec{Ref=key,Template=plate,Parent=spec.Ref,Slot=name,Attributes=spec.Attributes?.FoundInRaid is bool found?new GearAttributes{FoundInRaid=found}:null});
   }
  }
  var byRef=result.ToDictionary(s=>s.Ref);var used=new HashSet<(string,string)>();
  foreach(var spec in result){
   if(spec.Parent is null){if(!string.IsNullOrEmpty(spec.Slot))throw new ArgumentException("Root item cannot specify a slot");continue;}
   if(!byRef.TryGetValue(spec.Parent,out var parent)||string.IsNullOrEmpty(spec.Slot))throw new ArgumentException("Invalid parent/slot: "+spec.Ref);
   if(!used.Add((spec.Parent,spec.Slot+(spec.Slot=="cartridges"?":"+spec.Location:""))))throw new ArgumentException("Duplicate attachment slot");
   var slot=Slots(Template(parent.Template)).FirstOrDefault(s=>s.GetProperty("_name").GetString()==spec.Slot);
   if(slot.ValueKind==JsonValueKind.Undefined||!Allowed(spec.Template,slot))throw new ArgumentException("Incompatible attachment: "+spec.Ref);
   if(spec.Slot=="cartridges"){
    if(spec.Location is not int position||position<0)throw new ArgumentException("Invalid cartridge position");
    int capacity=slot.GetProperty("_max_count").GetInt32();
    if(capacity<=0||result.Where(s=>s.Parent==spec.Parent&&s.Slot=="cartridges").Sum(s=>s.Count)>capacity)throw new ArgumentException("Magazine capacity exceeded");
   }else if(spec.Location.HasValue)throw new ArgumentException("Only cartridges may specify a position");
   var seen=new HashSet<string>{spec.Ref};var walk=spec;
   while(walk.Parent is not null){if(!seen.Add(walk.Parent))throw new ArgumentException("Attachment cycle");walk=byRef[walk.Parent];}
  }
  foreach(var root in result.Where(s=>s.Parent is null)){
   bool Descendant(GearSpec s){while(s.Parent is not null){s=byRef[s.Parent];}return s.Ref==root.Ref;}
   var group=result.Where(Descendant).ToList();var selected=group.Select(s=>s.Template).ToHashSet();
   foreach(var spec in group){var props=Props(Template(spec.Template));if(props.TryGetProperty("ConflictingItems",out var conflicts)&&conflicts.EnumerateArray().Any(v=>selected.Contains(v.GetString()!)))throw new ArgumentException("Conflicting equipment parts");}
  }
  return result;
 }
}
