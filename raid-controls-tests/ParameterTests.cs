using System.Text.Json;
using System.Text.Json.Nodes;
using LocalRaidControls;
using LocalRaidShared;
using SPTarkov.Server.Core.Models.Eft.Common;
internal static class ParameterTests {
 internal static void Generation(){
  string home=Path.Combine(AppContext.BaseDirectory,"user","mods","LocalRaidControls");Directory.CreateDirectory(home);var backups=new Dictionary<string,byte[]?>();foreach(string file in new[]{"parameters.json","settings.json","server-status.json"}){string path=Path.Combine(home,file);backups[path]=File.Exists(path)?File.ReadAllBytes(path):null;}
  var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;object? scope=null;
  try{
   File.WriteAllText(Path.Combine(home,"settings.json"),"{}");File.WriteAllText(Path.Combine(home,"parameters.json"),"{}");string id="assistant_parameter_test";
   var json=new SPTarkov.Server.Core.Utils.JsonUtil([new SPTarkov.Server.Core.Utils.Json.SptJsonConverterRegistrator()]);string game=Environment.GetEnvironmentVariable("SPT_GAME_ROOT")??"D:/SPT-5.0.0-48558";var original=json.Deserialize<LocationBase>(File.ReadAllText(Path.Combine(game,"SPT_Runtime/SPT_Data/database/locations/bigmap/base.json")))!;string before=json.Serialize(original)!;
   using var read=JsonDocument.Parse(RaidParameters.Handle("read",id,original,default));string revision=read.RootElement.GetProperty("revision").GetString()!;
   using var applied=JsonDocument.Parse(RaidParameters.Handle("apply",id,original,JsonSerializer.SerializeToElement(new{group="AI",revision,edits=new Dictionary<string,object>{{"map/BotMax",32},{"assistant/NoAi",true}}})));
   revision=applied.RootElement.GetProperty("revision").GetString()!;using var raid=JsonDocument.Parse(RaidParameters.Handle("apply",id,original,JsonSerializer.SerializeToElement(new{group="战局",revision,edits=new Dictionary<string,object>{{"assistant/NextRaidMinutes",55},{"assistant/NoLoot",true}}})));
   object?[] begin={id,null};typeof(Patches).GetMethod("BeginLocation",flags)!.Invoke(null,begin);scope=begin[1];
   var generation=(RaidSettings)typeof(Patches).GetField("generation",flags)!.GetValue(null)!;if(!generation.NoAi||!generation.NoLoot)throw new Exception("Map settings did not enter generation scope");
   object?[] finish={id,original};typeof(Patches).GetMethod("FinishLocation",flags)!.Invoke(null,finish);var result=(LocationBase)finish[1]!;
   if(result.BotMax!=32||result.EscapeTimeLimit!=55||result.Waves!.Count!=0||result.BossLocationSpawn!.Count!=0||json.Serialize(original)!=before)throw new Exception("Native generation failed to consume persisted parameters safely");
   using var status=JsonDocument.Parse(File.ReadAllText(Path.Combine(home,"server-status.json")));if(!status.RootElement.GetProperty("settings").GetProperty("NoAi").GetBoolean())throw new Exception("Client generation snapshot missed override");
   Console.WriteLine("PASS parameter bridge read/apply, persisted generation scope, no-AI/loot rules, duration, client snapshot and immutable native tables");
  }finally{if(scope!=null)typeof(Patches).GetMethod("EndLocation",flags)!.Invoke(null,new[]{scope});foreach(var entry in backups)if(entry.Value==null)File.Delete(entry.Key);else File.WriteAllBytes(entry.Key,entry.Value);}
 }
 internal static void Run(){
  string directory=Path.Combine(Path.GetTempPath(),"assistant-parameter-tests-"+Guid.NewGuid().ToString("N"));
  void Check(bool condition,string name){if(!condition)throw new Exception("Parameters: "+name);}
  void Reject(Action action,string name){try{action();}catch(ArgumentException){return;}throw new Exception("Expected rejection: "+name);}
  try{
   var map=JsonNode.Parse("""{"BotMax":20,"BotMaxPvE":25,"EscapeTimeLimit":40,"NewSpawn":true,"BotSpawnTimeOnMin":5,"BotSpawnTimeOnMax":10,"Waves":[{"slots_min":2,"slots_max":4,"time_min":10,"time_max":60}],"BossLocationSpawn":[{"BossChance":25,"BossName":"bossKilla"}]}""")!.AsObject();
   var original=new RaidSettings{KeepOnDeath=true,ItemWeightsEnabled=true,ItemWeights=new(){["012345678901234567890123"]=3}};var source=RaidParameterStore.Source(map,original);var store=new RaidParameterStore(directory);
   string initial=source.ToJsonString();var first=store.Read("bigmap",source);Check(first.Rows.Any(r=>r.Definition.Id=="map/Waves/0/slots_min"),"wave inputs exposed");
   void Execute(string action,string group,JsonObject? changes=null,string name="方案一")=>store.Execute(action,"bigmap",group,name,store.Read("bigmap",source).Revision,changes??new(),source);
   Execute("apply","AI",new(){["map/BotMax"]=30,["map/BossLocationSpawn/0/BossChance"]=100,["assistant/AiEnabled"]=true,["assistant/FactionMultipliers/scav"]=2});
   var next=store.Effective("bigmap",source);Check(next["map"]!["BotMax"]!.GetValue<int>()==30,"map override applied");Check(source.ToJsonString()==initial,"source untouched");Check(store.Effective("woods",source)["map"]!["BotMax"]!.GetValue<int>()==20,"map isolation");Check(new RaidParameterStore(directory).Read("bigmap",source).Revision==store.Read("bigmap",source).Revision,"reload from disk");
   Reject(()=>store.Execute("apply","bigmap","AI","",first.Revision,new(){["map/BotMax"]=10},source),"stale revision");
   string before=File.ReadAllText(Path.Combine(directory,"parameters.json"));
   Reject(()=>Execute("apply","AI",new(){["map/BotMax"]=31,["map/Waves/0/slots_min"]=8}),"min max");
   Reject(()=>Execute("apply","AI",new(){["assistant/AiEnabled"]="true"}),"boolean string");
   Reject(()=>Execute("apply","AI",new(){["map/BotMax"]=1.5}),"fractional count");
   Reject(()=>Execute("apply","AI",new(){["map/BotMax"]=501}),"cap maximum");
   Reject(()=>Execute("apply","AI",new(){["map/BossLocationSpawn/0/BossChance"]=101}),"probability");
   Reject(()=>Execute("apply","AI",new(){["assistant/NoLoot"]=true}),"group isolation");
   Reject(()=>Execute("apply","AI",new(){["map/unknown"]=1}),"unknown field");
   Check(File.ReadAllText(Path.Combine(directory,"parameters.json"))==before,"invalid operations are atomic");
   Execute("save","AI");Execute("apply","战局",new(){["map/EscapeTimeLimit"]=60,["assistant/NoLoot"]=true});
   Check(store.Read("bigmap",source,"AI").Presets.SequenceEqual(new[]{"方案一"})&&store.Read("woods",source).Presets.Length==0&&store.Read("bigmap",source,"战局").Presets.Length==0,"preset list filters map and group");Reject(()=>Execute("save","战局"),"prevent other-group overwrite");
   Execute("defaults","AI");Check(store.Effective("bigmap",source)["map"]!["BotMax"]!.GetValue<int>()==20,"map defaults restore baseline");Check(store.EffectiveAssistant("bigmap",original).NoLoot,"reset AI preserves raid settings");Check(!store.EffectiveAssistant("bigmap",original).AiEnabled,"reset assistant default");
   Execute("load","AI");Check(store.Effective("bigmap",source)["map"]!["BotMax"]!.GetValue<int>()==30,"preset roundtrip");Check(store.EffectiveAssistant("bigmap",original).KeepOnDeath&&store.EffectiveAssistant("bigmap",original).ItemWeights.Count==1,"other assistant settings preserved");
   Reject(()=>Execute("load","战局"),"wrong preset group");Reject(()=>Execute("save","AI",name:"../escape"),"path traversal");Reject(()=>store.Execute("load","woods","AI","方案一",store.Read("woods",source).Revision,new(),source),"wrong preset map");
   Check(Directory.GetFiles(directory,"*.tmp",SearchOption.AllDirectories).Length==0,"no temporary file leak");
   Execute("defaults","战局");Check(store.Effective("bigmap",source)["map"]!["EscapeTimeLimit"]!.GetValue<int>()==40&&!store.EffectiveAssistant("bigmap",original).NoLoot,"raid defaults");
   var camelMap=JsonNode.Parse("""{"Waves":[{"TimeMin":10,"TimeMax":60}]}""")!.AsObject();var camelSource=RaidParameterStore.Source(camelMap,new());var camelStore=new RaidParameterStore(Path.Combine(directory,"camel"));Reject(()=>camelStore.Execute("apply","bigmap","AI","",camelStore.Read("bigmap",camelSource).Revision,new(){["map/Waves/0/TimeMin"]=70},camelSource),"camelCase wave time min max");
   var native=Activator.CreateInstance<LocationBase>() with {BotMax=20,EscapeTimeLimit=40,EscapeTimeLimitCoop=40,EscapeTimeLimitPVE=40,Waves=[]};var nativeSource=RaidParameters.MapSource(native);Check(nativeSource.ContainsKey("EscapeTimeLimit")&&nativeSource.ContainsKey("BotMax"),"native reflection schema");
   var timed=RaidParameters.Duration(native,90.5);Check(timed.EscapeTimeLimit==90.5&&timed.EscapeTimeLimitCoop==91&&timed.EscapeTimeLimitPVE==91&&native.EscapeTimeLimit==40,"fractional duration clone");
   string game=Environment.GetEnvironmentVariable("SPT_GAME_ROOT")??"D:/SPT-5.0.0-48558";
   var nativeJson=new SPTarkov.Server.Core.Utils.JsonUtil([new SPTarkov.Server.Core.Utils.Json.SptJsonConverterRegistrator()]);int realMaps=0;
   foreach(string id in new[]{"bigmap","woods","shoreline","interchange","rezervbase","lighthouse","tarkovstreets","laboratory","factory4_day","factory4_night","sandbox","sandbox_high"}){
    string file=Path.Combine(game,"SPT_Runtime","SPT_Data","database","locations",id,"base.json");if(!File.Exists(file))throw new Exception("Missing real map "+id);
    var location=nativeJson.Deserialize<LocationBase>(File.ReadAllText(file))!;var raw=RaidParameters.MapSource(location);var data=RaidParameterStore.Source(raw,new());var isolated=new RaidParameterStore(Path.Combine(directory,"native",id));var snap=isolated.Read(id,data);Check(snap.Rows.Length>10,"real map read "+id);
    var edits=new JsonObject{["map/BotMax"]=33};var wave=snap.Rows.FirstOrDefault(r=>r.Definition.Id.StartsWith("map/BossLocationSpawn/")&&r.Definition.Id.EndsWith("/BossChance"));if(wave!=null)edits[wave.Definition.Id]=37.5;
    isolated.Execute("apply",id,"AI","",snap.Revision,edits,data,e=>RaidParameters.ApplyValues(location,raw,e["map"]!.AsObject()));var effective=isolated.Effective(id,data);var changed=RaidParameters.ApplyValues(location,raw,effective["map"]!.AsObject());Check(changed.BotMax==33&&JsonNode.DeepEquals(raw,RaidParameters.MapSource(location)),"native clone "+id);realMaps++;
   }
   Console.WriteLine("PASS "+realMaps+" real EFT 48558 maps: read schema, validate changes, deserialize native AI waves, preserve database tables");
   Console.WriteLine("PASS parameter persistence, presets, defaults, ranges, atomic rejection, concurrent edits, group/map isolation and native duration");
  }finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
 }
}
