using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using LocalRaidShared;
using SPTarkov.Server.Core.Models.Eft.Common;
namespace LocalRaidControls;
public static class RaidParameters {
 static readonly JsonSerializerOptions Json=new(){Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()}};
 static RaidParameterStore Store=>new(Path.Combine(AppContext.BaseDirectory,"user","mods","LocalRaidControls"));
 public static JsonObject MapSource(LocationBase map){var result=new JsonObject();foreach(var p in typeof(LocationBase).GetProperties().Where(p=>p.CanRead&&p.Name!="SpawnPointParams"&&(p.Name.Contains("Bot")||p.Name.Contains("Spawn")||p.Name.Contains("Wave")||p.Name.Contains("EscapeTime")||p.Name is "ExitAccessTime" or "ExitTime" or "OpenZones")))result[p.Name]=JsonSerializer.SerializeToNode(p.GetValue(map),p.PropertyType,Json);return result;}
 public static string Handle(string action,string map,object location,JsonElement payload){lock(RaidParameterStore.Gate){
  var source=RaidParameterStore.Source(MapSource((LocationBase)location),Patches.Read());var store=Store;
  if(action!="read"){var request=JsonNode.Parse(payload.GetRawText())!.AsObject();store.Execute(action,map,request["group"]?.ToString()??"",request["name"]?.ToString()??"",request["revision"]?.ToString()??"",request["edits"] as JsonObject??new(),source,effective=>ApplyValues((LocationBase)location,source["map"]!.AsObject(),effective["map"]!.AsObject()));}
  string? group=payload.ValueKind==JsonValueKind.Object&&payload.TryGetProperty("group",out var requestedGroup)?requestedGroup.GetString():null;var snapshot=store.Read(map,source,group);var effective=store.Effective(map,source);return JsonSerializer.Serialize(new{ok=true,map,revision=snapshot.Revision,presets=snapshot.Presets,rows=snapshot.Rows.Select(r=>new{id=r.Definition.Id,group=r.Definition.Group,name=r.Definition.Name,help=r.Definition.Help,minimum=r.Definition.Min,maximum=r.Definition.Max,integer=r.Definition.Integer,boolean=r.Definition.Boolean,value=r.Value,defaultValue=r.Default}),effectiveMap=effective["map"],assistant=effective["assistant"]});
 }}
 public static RaidSettings Settings(string map,RaidSettings original)=>Store.EffectiveAssistant(map,original);
 public static LocationBase Duration(LocationBase original,double minutes){if(!double.IsFinite(minutes)||minutes<=0)return original;var result=original with {};foreach(var p in typeof(LocationBase).GetProperties().Where(p=>p.CanWrite&&p.Name.StartsWith("EscapeTimeLimit",StringComparison.Ordinal))){Type target=Nullable.GetUnderlyingType(p.PropertyType)??p.PropertyType;object? value=target==typeof(int)?(int)Math.Clamp(Math.Ceiling(minutes),1,int.MaxValue):target==typeof(long)?(long)Math.Clamp(Math.Ceiling(minutes),1,long.MaxValue):target==typeof(float)?(float)minutes:target==typeof(double)?minutes:JsonSerializer.SerializeToElement(minutes).Deserialize(p.PropertyType);p.SetValue(result,value);}return result;}
 public static LocationBase Apply(string map,LocationBase original){var source=RaidParameterStore.Source(MapSource(original),Patches.Read());return ApplyValues(original,source["map"]!.AsObject(),Store.Effective(map,source)["map"]!.AsObject());}
 public static LocationBase ApplyValues(LocationBase original,JsonObject before,JsonObject after){var result=original with {};foreach(var p in typeof(LocationBase).GetProperties().Where(p=>p.CanWrite&&after.ContainsKey(p.Name)))if(!JsonNode.DeepEquals(before[p.Name],after[p.Name]))try{p.SetValue(result,after[p.Name]?.Deserialize(p.PropertyType,Json));}catch(Exception e){throw new ArgumentException("参数类型不符合服务端要求："+p.Name,e);}return result;}
}
