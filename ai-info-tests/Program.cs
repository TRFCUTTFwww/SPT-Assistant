using System.Text.Json;
using LocalSpt5Esp;
using Mono.Cecil;
int checks=0;
void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
Check(typeof(IntPtr).IsPrimitive,"old scalar filter accepts IntPtr");
try{JsonSerializer.Serialize(new Dictionary<string,object>{{"Pointer",new IntPtr(1)}});throw new Exception("Pointer unexpectedly serializable");}catch(NotSupportedException){Check(true,"old scalar snapshot fails on inherited native Pointer");}
string game=Environment.GetEnvironmentVariable("SPT_GAME_ROOT")??"D:/SPT-5.0.0-48558";
string source=Environment.GetEnvironmentVariable("SPT_SOURCE_ROOT")??Directory.GetCurrentDirectory();
T ReadLocale<T>(string name)=>JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(source,"esp-cover","Locales",name)))!;
var catalog=new LocalizationCatalog(ReadLocale<Dictionary<string,string>>("en.json"),ReadLocale<Dictionary<string,string>>("en-formats.json"),ReadLocale<Dictionary<string,int[]>>("en-verbatim.json"));
AiSpawnInfoRows.Row English(AiSpawnInfoRows.Row row)=>AiSpawnInfoRows.Localize(row,catalog.Text);
bool HasChinese(string value)=>value.Any(c=>c is >= '\u3400' and <= '\u9fff');
void CheckEnglish(IEnumerable<AiSpawnInfoRows.Row> sourceRows,string name){
 var untranslated=sourceRows.Select(English).Where(r=>HasChinese(r.Group+r.Name+r.Help+r.Value)).Take(5).ToArray();
 Check(untranslated.Length==0,name+(untranslated.Length==0?"":": "+string.Join(" | ",untranslated.Select(r=>r.Path+" "+r.Name+" "+r.Value+" "+r.Help))));
}
var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(Path.Combine(game,"BepInEx/interop"));resolver.AddSearchDirectory(Path.Combine(game,"BepInEx/core"));
using var native=AssemblyDefinition.ReadAssembly(Path.Combine(game,"BepInEx/interop/Assembly-CSharp.dll"),new ReaderParameters{AssemblyResolver=resolver});
var settings=native.MainModule.Types.Single(t=>t.FullName=="EFT.RaidSettings");
foreach(string property in new[]{"BotSettings"}){
 var type=settings.Properties.Single(p=>p.Name==property).PropertyType.Resolve();bool found=false;
 for(var current=type;current!=null;current=current.BaseType?.Resolve())if(current.Properties.Any(p=>p.Name=="Pointer"&&p.PropertyType.FullName=="System.IntPtr")){found=true;break;}
 Check(found,property+" exposes the inherited IntPtr in actual EFT 48558 wrappers");
}
using var sample=JsonDocument.Parse("""
{"map":"bigmap","mapAi":{"BotMax":30,"BotMaxPvE":20,"Waves":[{"WildSpawnType":"assault","slots_min":1,"slots_max":3,"time_min":-1,"time_max":60,"BotPreset":"hard","SpawnMode":["pve"]}],"BossLocationSpawn":[{"BossName":"pmcUSEC","BossChance":100,"BossEscortAmount":"2,3,4","IgnoreMaxBots":true,"Supports":[{"BossEscortType":"followerBigPipe","BossEscortAmount":"1"}]}]},"pmcConfig":{"useDifficultyOverride":false,"difficulty":"hard","isUsec":50},"assistant":{"NoAi":false,"AiEnabled":false,"FactionMultipliers":{"boss":100}},"botConfig":{"maxBotCap":{"bigmap":25,"woods":99}}}
""");
string before=sample.RootElement.GetRawText();var rows=AiSpawnInfoRows.Build(sample.RootElement);
AiSpawnInfoRows.Row Row(string path)=>rows.Single(r=>r.Path==path);
Check(Row("mapAi.BotMaxPvE").Value=="20 人","PVE cap retained independently of regular cap");
Check(Row("botConfig.maxBotCap.bigmap").Value=="25 人"&&!rows.Any(r=>r.Path.EndsWith("woods")),"only current map override selected");
Check(Row("mapAi.BotStart").Value.Contains("未提供"),"missing values are not fabricated as zero");
Check(Row("mapAi.Waves[0].time_min").Value=="-1"&&Row("mapAi.Waves[0].time_min").Help.Contains("特殊"),"special native time is retained and explained");
Check(Row("mapAi.BossLocationSpawn[0].BossEscortAmount").Value=="2,3,4","escort candidates are not incorrectly summed");
Check(Row("mapAi.BossLocationSpawn[0].BossChance").Value=="100 %","spawn chance includes percent units");
Check(Row("mapAi.BossLocationSpawn[0].Supports[0].BossEscortAmount").Value=="1","nested support parameters preserved");
Check(Row("assistant.AiEnabled").Value=="关闭"&&Row("assistant.FactionMultipliers.boss").Value=="100 倍","stored multiplier is distinct from enable state");
Check(AiSpawnInfoRows.Matches(Row("mapAi.BotMaxPvE"),"PVE 上限")&&AiSpawnInfoRows.Matches(Row("mapAi.BotMaxPvE"),"botmaxpve"),"Chinese meaning and native parameter search");
var cap=English(Row("mapAi.BotMaxPvE"));
Check(cap.Value=="20 bots"&&cap.Path=="mapAi.BotMaxPvE"&&AiSpawnInfoRows.Matches(Row(cap.Path),"cap botmaxpve",cap),"translated labels and values remain searchable with native parameter paths");
var escort=Row("mapAi.BossLocationSpawn[0].Supports[0].BossEscortType");
Check(escort.Value=="Big Pipe"&&AiSpawnInfoRows.Matches(escort,"big pipe followerBigPipe",English(escort)),"escort names display without losing role ID search");
Check(catalog.Text("Boss / PMC第 8 批 · 生成概率（%）")=="Boss / PMC wave 8 · Spawn chance (%)","parameter editor translates a Boss wave title");
Check(catalog.Text("默认 开启")=="Default: "+catalog.Text("开启")&&catalog.Text("已存 2 个 · 开启")=="2 saved · 开启","parameter editor translates defaults and preserves preset names");
CheckEnglish(rows,"sample AI information is fully translated");
Check(rows.All(r=>r.Name.Length>0&&r.Help.Length>0)&&rows.All(r=>!r.Path.Contains("SpawnPointParams")),"readable descriptions without raw spawn transforms");
Check(before==sample.RootElement.GetRawText(),"presentation leaves source unchanged");
using var empty=JsonDocument.Parse("{\"map\":\"laboratory\",\"mapAi\":{\"Waves\":[],\"BossLocationSpawn\":[]}}");
var emptyRows=AiSpawnInfoRows.Build(empty.RootElement);Check(emptyRows.Single(r=>r.Path=="mapAi.Waves").Value=="0 条"&&emptyRows.Any(r=>r.Path=="assistant"&&r.Value=="未提供"),"empty waves and absent settings are distinct");
CheckEnglish(emptyRows,"empty AI information is fully translated");
Check(AiSpawnInfoRows.NormalizeMap(" Laboratory ")=="laboratory","map IDs normalized");
try{using var bad=JsonDocument.Parse("{}");AiSpawnInfoRows.Build(bad.RootElement);throw new Exception("invalid accepted");}catch(ArgumentException){Check(true,"missing map data rejected");}
foreach(string file in args){
 using var doc=JsonDocument.Parse(File.ReadAllText(file));
 var root=doc.RootElement;
 if(root.TryGetProperty("err",out var error)){
  Check(error.GetInt32()==0,"live endpoint succeeded");
  var data=root.GetProperty("data");
  var actual=AiSpawnInfoRows.Build(data);
  Check(actual.Length>30&&actual.All(r=>r.Help.Length>0),"live "+data.GetProperty("map").GetString()+" response renders "+actual.Length+" explained parameters");
  CheckEnglish(actual,"live AI information is fully translated");
 }else{
  string map=Path.GetFileName(Path.GetDirectoryName(file))!;
  using var local=JsonDocument.Parse(JsonSerializer.Serialize(new {map,mapAi=root}));
  CheckEnglish(AiSpawnInfoRows.Build(local.RootElement),map+" AI information is fully translated");
 }
}
Console.WriteLine($"Passed {checks} AI information checks.");
