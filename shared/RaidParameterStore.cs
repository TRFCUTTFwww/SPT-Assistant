using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace LocalRaidShared;

// One atomic override file: database tables and other mods' settings are never rewritten.
public sealed class RaidParameterStore(string directory) {
 public static readonly object Gate=new();
 static readonly JsonSerializerOptions Pretty=new(){WriteIndented=true};
 string Active=>Path.Combine(directory,"parameters.json");
 public sealed record Definition(string Id,string Group,string Name,string Help,double Min=0,double Max=100000,bool Integer=false,bool Boolean=false);
 public sealed record Row(Definition Definition,JsonNode? Value,JsonNode? Default);
 public sealed record Snapshot(string Revision,Row[] Rows,string[] Presets);
 static JsonObject Object(string path)=>File.Exists(path)?JsonNode.Parse(File.ReadAllText(path)) as JsonObject??throw new InvalidDataException("参数文件必须是 JSON 对象。"):new();
 static JsonNode? At(JsonNode root,string path){JsonNode? n=root;foreach(string part in path.Split('/')){if(n is JsonObject o)n=o[part];else if(n is JsonArray a&&int.TryParse(part,out int i)&&i>=0&&i<a.Count)n=a[i];else return null;}return n;}
 static void Set(JsonNode root,string path,JsonNode? value){var parts=path.Split('/');JsonNode n=root;for(int i=0;i<parts.Length-1;i++)n=At(n,parts[i])??throw new ArgumentException("参数路径已失效："+path);if(n is JsonArray a)a[int.Parse(parts[^1],CultureInfo.InvariantCulture)]=value?.DeepClone();else n[parts[^1]]=value?.DeepClone();}
 public static JsonObject Source(JsonObject map,RaidSettings assistant){var a=JsonSerializer.SerializeToNode(assistant)!.AsObject();foreach(string f in RaidSpawnRules.Factions)if(a["FactionMultipliers"]![f]==null)a["FactionMultipliers"]![f]=1;return new(){["map"]=map.DeepClone(),["assistant"]=a};}
 public static Definition[] Definitions(JsonObject source){
  var list=new List<Definition>();
  void Add(string id,string group,string name,string help,double min=0,double max=100000,bool integer=false,bool boolean=false){if(At(source,id) is JsonValue)list.Add(new(id,group,name,help,min,max,integer,boolean));}
  foreach(var d in new[]{("BotMax","常规 AI 上限"),("BotMaxPvE","PVE AI 上限"),("MaxBotPerZone","单区域 AI 上限"),("BotSpawnCountStep","每轮补充人数")})Add("map/"+d.Item1,"AI",d.Item2,"限制地图或生成区域的人数；特殊波次可能忽略上限，不等于本局累计生成数量。",0,500,true);
  foreach(string key in new[]{"NewSpawn","OldSpawn","OfflineNewSpawn","OfflineOldSpawn"})Add("map/"+key,"AI",key.Contains("New")?"新生成机制 · "+key:"旧波次机制 · "+key,"控制对应生成机制是否启用；离线与在线使用不同选项。关闭一种机制不等于禁止所有 AI。",boolean:true);
  foreach(var d in new[]{("BotStart","开始生成时间"),("BotStop","停止生成时间"),("BotSpawnPeriodCheck","生成检查间隔"),("BotSpawnTimeOnMin","生成活跃期最短时间"),("BotSpawnTimeOnMax","生成活跃期最长时间"),("BotSpawnTimeOffMin","生成休息期最短时间"),("BotSpawnTimeOffMax","生成休息期最长时间")})Add("map/"+d.Item1,"AI",d.Item2,"单位：秒。控制生成器运行时间；波次还可能有单独的触发条件。",-1,604800);
  foreach(var d in new[]{("EscapeTimeLimit","战局时长"),("EscapeTimeLimitPVE","PVE 战局时长"),("EscapeTimeLimitCoop","合作战局时长")})Add("map/"+d.Item1,"战局",d.Item2,"单位：分钟。地图生成时传给客户端；已经进入的战局不会重新计时。",1,10080);
  foreach(var d in new[]{("ExitAccessTime","撤离开放时间"),("ExitTime","撤离等待时间")})Add("map/"+d.Item1,"战局",d.Item2,"单位：秒。具体撤离点可能有自己的条件和等待时间。",0,604800);
  Add("assistant/NoAi","AI","禁止自然 AI","优先于倍率与地图波次；开启后不生成自然 AI。",boolean:true);
  Add("assistant/AiEnabled","AI","启用阵营倍率","开启后才应用下面的倍率。0 禁用该阵营，1 保持原值。",boolean:true);
  foreach(string f in RaidSpawnRules.Factions)Add("assistant/FactionMultipliers/"+f,"AI","阵营倍率 · "+Faction(f),"普通波次缩放人数；Boss 类条目缩放概率（最高 100%）。过高倍率会增加客户端负载。",0,100);
  Add("assistant/NoLoot","战局","禁止物资","关闭自然散落物、容器内容与空投；下次生成生效。",boolean:true);
  Add("assistant/NextRaidMinutes","战局","助手战局时长覆盖","分钟；0 跟随地图时长。大于 0 时优先于地图时长。",0,10080);
  Add("assistant/LootEnabled","战局","启用物资倍率","开启后应用散落物、容器和高价值权重设置。",boolean:true);
  Add("assistant/LooseMultiplier","战局","散落物数量倍率","1 为原数量，0 不生成；实际数量受可用点位限制。",0,100);
  Add("assistant/ContainerMultiplier","战局","容器物资数量倍率","1 为原数量，实际数量仍受容器格数限制。",0,100);
  Add("assistant/HighValueMultiplier","战局","高价值物资权重倍率","改变抽中概率，不是物品价格；仅针对达到下方价值门槛的物品。",0.001,100);
  Add("assistant/ValueThreshold","战局","高价值门槛","单位：卢布估值。达到门槛的候选物品才受到高价值权重倍率影响。",0,1000000000);
  foreach(string listName in new[]{"Waves","BossLocationSpawn"})if(source["map"]?[listName] is JsonArray waves)for(int i=0;i<waves.Count;i++)if(waves[i] is JsonObject wave)foreach(var p in wave){string key=p.Key.ToLowerInvariant();var detail=key switch{
   "slots_min" or "slotsmin"=>("最少人数",0d,500d,true),"slots_max" or "slotsmax"=>("最多人数",0d,500d,true),
   "bosschance"=>("生成概率（%）",0d,100d,false),"time_min" or "timemin"=>("最早时间（秒）",-1d,604800d,false),"time_max" or "timemax"=>("最晚时间（秒）",-1d,604800d,false),"time"=>("生成时间（秒）",-1d,604800d,false),_=>default};
   if(detail.Item1!=null)Add($"map/{listName}/{i}/{p.Key}","AI",$"{(listName=="Waves"?"普通":"Boss / PMC")}第 {i+1} 批 · {detail.Item1}","候选批次参数；仍受模式、区域、触发条件和人数上限约束。时间 -1 为原生特殊标记。角色："+(wave["BossName"]??wave["WildSpawnType"]??wave["wildSpawnType"])?.ToString(),detail.Item2,detail.Item3,detail.Item4);
  }
  return list.ToArray();
 }
 static string Faction(string f)=>f switch{"scav"=>"Scav","usec"=>"USEC","bear"=>"BEAR","boss"=>"Boss","follower"=>"保镖","cultist"=>"邪教徒","blackDivision"=>"黑色军团","raider"=>"掠夺者","rogue"=>"游荡者",_=>f};
 static void Validate(Definition d,JsonNode? value){if(d.Boolean){if(value is not JsonValue b||!b.TryGetValue<bool>(out _))throw new ArgumentException(d.Name+"必须为开关。");return;}if(value==null||!double.TryParse(value.ToJsonString(),NumberStyles.Float,CultureInfo.InvariantCulture,out double v)||!double.IsFinite(v)||d.Integer&&v!=Math.Truncate(v))throw new ArgumentException($"{d.Name}请输入有效的{(d.Integer?"整数":"数字")}。");}
 static void Ranges(JsonObject result){
  void Pair(JsonNode owner,string lo,string hi){var a=owner[lo];var b=owner[hi];if(a!=null&&b!=null&&double.TryParse(a.ToString(),NumberStyles.Float,CultureInfo.InvariantCulture,out double x)&&double.TryParse(b.ToString(),NumberStyles.Float,CultureInfo.InvariantCulture,out double y)&&x>y)throw new ArgumentException(lo+"不能大于 "+hi+"。");}
  foreach(string stem in new[]{"BotSpawnTimeOn","BotSpawnTimeOff"})Pair(result["map"]!,stem+"Min",stem+"Max");
  if(result["map"]?["Waves"] is JsonArray waves)foreach(var w in waves)if(w!=null){Pair(w,"slots_min","slots_max");Pair(w,"SlotsMin","SlotsMax");Pair(w,"time_min","time_max");Pair(w,"TimeMin","TimeMax");}
  result["assistant"]!.Deserialize<RaidSettings>()!.Validate();
 }
 static JsonObject Apply(JsonObject source,JsonObject changes){var result=source.DeepClone().AsObject();var defs=Definitions(source).ToDictionary(d=>d.Id);foreach(var p in changes){if(!defs.TryGetValue(p.Key,out var d))throw new ArgumentException("参数在当前地图或版本中不可用："+p.Key);Validate(d,p.Value);Set(result,p.Key,p.Value);}Ranges(result);return result;}
 static JsonObject Changes(JsonObject state,string map)=>state[map] as JsonObject??new();
 string Revision(JsonObject state,JsonObject source)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state.ToJsonString()+source.ToJsonString())));
 string Preset(string name){if(string.IsNullOrWhiteSpace(name)||name.Length>48||name.Any(c=>!(char.IsLetterOrDigit(c)||c=='-'||c=='_')))throw new ArgumentException("方案名只能用文字、数字、短横线和下划线，最多 48 字。");return Path.Combine(directory,"parameter-presets",name+".json");}
 static void Write(string path,JsonObject value){Directory.CreateDirectory(Path.GetDirectoryName(path)!);string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temp,value.ToJsonString(Pretty),new UTF8Encoding(false));File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}}
 public Snapshot Read(string map,JsonObject source,string? group=null){lock(Gate){var state=Object(Active);var values=Apply(source,Changes(state,map));var defaults=Source(source["map"]!.AsObject(),new());return new(Revision(state,source),Definitions(source).Select(d=>new Row(d,At(values,d.Id)?.DeepClone(),At(defaults,d.Id)?.DeepClone())).ToArray(),Directory.Exists(Path.Combine(directory,"parameter-presets"))?Directory.GetFiles(Path.Combine(directory,"parameter-presets"),"*.json").Where(p=>MatchesPreset(p,map,group)).Select(p=>Path.GetFileNameWithoutExtension(p)).Order().ToArray():Array.Empty<string>());}}
 static bool MatchesPreset(string path,string map,string? group){try{var value=Object(path);return value["schema"]?.ToString()=="1"&&value["map"]?.ToString()==map&&(group==null||value["group"]?.ToString()==group);}catch{return false;}}
 public void Execute(string action,string map,string group,string name,string revision,JsonObject edits,JsonObject source,Action<JsonObject>? validate=null){lock(Gate){
  if(group is not ("AI" or "战局"))throw new ArgumentException("未知参数分组。");var state=Object(Active);if(revision!=Revision(state,source))throw new ArgumentException("服务端参数已改变，请刷新后重试。");var defs=Definitions(source).Where(d=>d.Group==group).ToDictionary(d=>d.Id);var changes=Changes(state,map).DeepClone().AsObject();
  if(action=="delete"){string path=Preset(name);if(!File.Exists(path)||!MatchesPreset(path,map,group))throw new ArgumentException("当前地图与分组下不存在该方案。");File.Delete(path);return;}
  if(action=="save"){string path=Preset(name);if(File.Exists(path)&&!MatchesPreset(path,map,group))throw new ArgumentException("此名称已用于其他地图或分组，请更换方案名。");var stored=new JsonObject();var current=Apply(source,changes);foreach(var d in defs.Values)stored[d.Id]=At(current,d.Id)?.DeepClone();Write(path,new(){["schema"]=1,["map"]=map,["group"]=group,["values"]=stored});return;}
  if(action=="applyAll"){
   if(edits.Count==0)throw new ArgumentException("请先修改至少一项通用参数。");
   foreach(var p in edits){if(!defs.TryGetValue(p.Key,out var d))throw new ArgumentException("不允许修改此分组以外的参数："+p.Key);if(p.Key.StartsWith("map/Waves/",StringComparison.Ordinal)||p.Key.StartsWith("map/BossLocationSpawn/",StringComparison.Ordinal))throw new ArgumentException("波次明细仅限单图修改，不能批量应用到全部地图："+p.Key);Validate(d,p.Value);}
   foreach(string targetMap in new[]{"bigmap","woods","shoreline","interchange","rezervbase","lighthouse","tarkovstreets","laboratory","labyrinth","factory4_day","factory4_night","sandbox","sandbox_high"}){
    var targetChanges=Changes(state,targetMap).DeepClone().AsObject();
    foreach(var p in edits)targetChanges[p.Key]=p.Value?.DeepClone();
    if(targetMap==map){var effectiveCurrent=Apply(source,targetChanges);validate?.Invoke(effectiveCurrent);}
    state[targetMap]=targetChanges;
   }
   Write(Active,state);return;
  }
  if(action=="load"){var path=Preset(name);if(!File.Exists(path))throw new ArgumentException("方案文件不存在。");var saved=Object(path);if(saved["schema"]?.ToString()!="1"||saved["map"]?.ToString()!=map||saved["group"]?.ToString()!=group)throw new ArgumentException("方案的版本、地图或分组不匹配。");edits=saved["values"] as JsonObject??throw new ArgumentException("方案缺少参数。");}
  else if(action=="defaults"){var defaults=Source(source["map"]!.AsObject(),new());foreach(var d in defs.Values){changes.Remove(d.Id);if(d.Id.StartsWith("assistant/",StringComparison.Ordinal))changes[d.Id]=At(defaults,d.Id)?.DeepClone();}}
  else if(action!="apply")throw new ArgumentException("未知参数操作。");
  if(action is "apply" or "load")foreach(var p in edits){if(!defs.TryGetValue(p.Key,out var d))throw new ArgumentException("不允许修改此分组以外的参数："+p.Key);Validate(d,p.Value);changes[p.Key]=p.Value?.DeepClone();}
  var effective=Apply(source,changes);validate?.Invoke(effective);state[map]=changes;Write(Active,state);
 }}
 public JsonObject Effective(string map,JsonObject source){lock(Gate)return Apply(source,Changes(Object(Active),map));}
 public RaidSettings EffectiveAssistant(string map,RaidSettings original){lock(Gate){var changes=new JsonObject();foreach(var p in Changes(Object(Active),map))if(p.Key.StartsWith("assistant/",StringComparison.Ordinal))changes[p.Key]=p.Value?.DeepClone();return Apply(Source(new(),original),changes)["assistant"]!.Deserialize<RaidSettings>()!;}}
}
