using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
namespace LocalSpt5Esp;
internal static class AiSpawnInfoRows {
 internal sealed record Row(string Group,string Name,string Value,string Help,string Path,string NativeSearch="");
 internal static Row Localize(Row row,Func<string,string> text){
  var name=string.Join(" · ",row.Name.Split(" · ").Select(text));
  var value=string.Join(", ",row.Value.Split('、').Select(text));
  return row with {Group=text(row.Group),Name=name,Value=value,Help=text(row.Help)};
 }
 internal static readonly string[] Groups={"全部参数","人数与上限","生成时间","难度与阵营","普通波次","Boss / PMC 波次","助手刷新规则"};
 internal static string NormalizeMap(string? map)=>(map??"").Trim().ToLowerInvariant();
 internal static string MapName(string map)=>map switch{"bigmap"=>"海关","laboratory"=>"实验室","labyrinth"=>"迷宫","woods"=>"森林","shoreline"=>"海岸线","interchange"=>"立交桥","rezervbase"=>"储备站","lighthouse"=>"灯塔","tarkovstreets"=>"塔科夫街区","factory4_day"=>"工厂（日间）","factory4_night"=>"工厂（夜间）","sandbox" or "sandbox_start"=>"中心区","sandbox_high"=>"中心区（高等级）",_=>map};
 internal static JsonElement Get(JsonElement value,string key){if(value.ValueKind==JsonValueKind.Object)foreach(var p in value.EnumerateObject())if(p.Name.Equals(key,StringComparison.OrdinalIgnoreCase))return p.Value;return default;}
 static bool Missing(JsonElement v)=>v.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null;
 static string Text(JsonElement v,string key="")=>Missing(v)?"未提供（不等于 0）":v.ValueKind switch{JsonValueKind.True=>"开启",JsonValueKind.False=>"关闭",JsonValueKind.Array=>v.GetArrayLength()==0?"无条目":string.Join("、",v.EnumerateArray().Select(item=>Text(item,key))),JsonValueKind.String=>v.GetString() is {Length:>0} s?DisplayValue(key,s):"未指定",_=>v.ToString()};
 static bool IsRole(string key)=>key.ToLowerInvariant() is "bossname" or "wildspawntype" or "bossescorttype" or "usectype" or "beartype";
 static string DisplayValue(string key,string value){
  string translated=Translate(value);
  return IsRole(key)&&translated==value?AiSpawnCatalog.RoleName(value):translated;
 }
 internal static string ParameterHelp(string help){
  const string roleMarker="角色：";
  int roleIndex=help.LastIndexOf(roleMarker,StringComparison.Ordinal);
  if(roleIndex<0)return help;
  int start=roleIndex+roleMarker.Length;
  string role=help[start..];
  return help[..start]+(role.Length==0?"未指定":DisplayValue("BossName",role));
 }
 static string Translate(string v)=>v switch{"easy"=>"简单","normal"=>"普通","hard"=>"困难","impossible"=>"极难","AsOnline"=>"跟随原生配置","assault"=>"普通 Scav","marksman"=>"狙击 Scav","pmcUSEC"=>"USEC PMC","pmcBEAR"=>"BEAR PMC","pmcBot"=>"掠夺者","exUsec"=>"游荡者","Savage"=>"Scav","pve"=>"PVE","regular"=>"常规","pvp-season"=>"赛季",_=>v};
 internal static bool Matches(Row row,string search)=>Matches(row,search,row);
 internal static bool Matches(Row source,string search,Row display){
  string fields=source.Name+" "+source.Value+" "+source.Help+" "+source.Path+" "+source.NativeSearch+" "+display.Name+" "+display.Value+" "+display.Help;
  return search.Split(' ',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).All(term=>fields.Contains(term,StringComparison.OrdinalIgnoreCase));
 }
 internal static Row[] Build(JsonElement data){
  var result=new List<Row>();var map=Get(data,"mapAi");if(map.ValueKind!=JsonValueKind.Object)throw new ArgumentException("服务端未返回地图 AI 配置，请更新服务端后重试。");string location=NormalizeMap(Get(data,"map").ToString());
  void Add(string group,JsonElement owner,string key,string name,string help,string prefix,string unit=""){var value=Get(owner,key);result.Add(new(group,name,Text(value,key)+(Missing(value)?"":unit),help,prefix+"."+key,IsRole(key)?value.ToString():""));}
  void Map(string group,string key,string name,string help,string unit="")=>Add(group,map,key,name,help,"mapAi",unit);
  const string cap="人数与上限",time="生成时间",diff="难度与阵营",normal="普通波次",boss="Boss / PMC 波次",assistant="助手刷新规则";
  Map(cap,"BotMax","地图常规 AI 上限","常规模式使用的地图 AI 数量上限。PVE 还要看 BotMaxPvE；允许忽略上限的特殊波次可能突破此值。"," 人");
  Map(cap,"BotMaxPvE","地图 PVE AI 上限","PVE 模式的地图 AI 数量上限，不代表整局累计只会生成这么多人。"," 人");
  Map(cap,"MaxBotPerZone","单区域 AI 上限","限制同一生成区域的 AI 数量；有空余地图名额也可能因区域上限而暂不刷新。"," 人");
  Map(cap,"BotSpawnCountStep","每轮生成步进人数","生成器每轮补充 AI 的人数参数；仍受可用生成点、波次和人数上限约束。"," 人");
  Add(cap,Get(Get(data,"botConfig"),"maxBotCap"),location,"服务端地图人数上限覆盖","服务端可按地图覆盖人数上限。未提供表示这里没有该地图覆盖项，不表示禁止生成。","botConfig.maxBotCap"," 人");
  Map(cap,"NewSpawn","新生成机制","是否启用地图的新生成机制；可能与旧波次机制共同存在，不能据此单独判断实际人数。");
  Map(cap,"OldSpawn","旧波次机制","是否启用地图旧生成机制；普通 Waves 列表描述候选批次。");
  Map(cap,"OfflineNewSpawn","离线新生成机制","离线战局的新生成机制开关，实际采用路径由当前游戏模式决定。");
  Map(cap,"OfflineOldSpawn","离线旧波次机制","离线战局的旧生成机制开关；关闭不代表所有 Boss 或事件生成都关闭。");
  Map(time,"BotStart","开始生成时间","地图生成器的开始时间，单位为战局开始后的秒数。具体波次还可有独立时间条件。"," 秒");
  Map(time,"BotStop","停止常规生成时间","地图生成器停止常规补充的时间，单位为战局开始后的秒数；特殊事件可能另有规则。"," 秒");
  Map(time,"BotSpawnPeriodCheck","检查生成间隔","每隔多少秒检查一次是否需要生成 AI；不是每次检查都会成功生成。"," 秒");
  foreach(var pair in new[]{("BotSpawnTimeOnMin","生成活跃期下限"),("BotSpawnTimeOnMax","生成活跃期上限"),("BotSpawnTimeOffMin","生成休息期下限"),("BotSpawnTimeOffMax","生成休息期上限")})Map(time,pair.Item1,pair.Item2,"生成器交替运行与休息的时间区间参数；是否使用取决于地图启用的生成机制。"," 秒");
  Map(cap,"OpenZones","开放生成区域","可用于生成的区域代号。多个区域不意味着每个区域都会刷一批；还需满足出生点和距离条件。");
  var group=Get(map,"NonWaveGroupScenario");
  foreach(var entry in new[]{("Enabled","非波次组队生成","是否允许非波次生成器以小队形式生成。"),("Chance","非波次组队概率","组成小队的概率，0 为不组队、100 为每次尝试组队。"),("MinToBeGroup","小队最少人数","一次组队生成的最少人数，不是地图总人数。"),("MaxToBeGroup","小队最多人数","一次组队生成的最多人数，仍受其他生成条件约束。")})Add(cap,group,entry.Item1,entry.Item2,entry.Item3,"mapAi.NonWaveGroupScenario",entry.Item1=="Chance"?" %":"");
  foreach(string key in new[]{"BotEasy","BotNormal","BotHard","BotImpossible"})Map(diff,key,"地图难度分配 · "+(key=="BotEasy"?"简单":key=="BotNormal"?"普通":key=="BotHard"?"困难":"极难"),"原生地图的难度分配参数。显式指定难度的波次或服务端难度覆盖可能优先，不应直接当作实际 AI 占比。");
  var pmc=Get(data,"pmcConfig");
  Add(diff,pmc,"useDifficultyOverride","PMC 难度覆盖","开启时使用下面的 PMC 难度设置；关闭时保留原生请求的难度选择。","pmcConfig");
  Add(diff,pmc,"difficulty","PMC 覆盖难度","仅在 PMC 难度覆盖开启时使用。AsOnline 表示跟随原生配置；不会改变场上已经生成的 AI。","pmcConfig");
  Add(diff,pmc,"isUsec","PMC 随机阵营为 USEC 的概率","需要随机选择 PMC 阵营时使用；明确指定 BEAR/USEC 的波次不应按此比例推算。","pmcConfig"," %");
  Add(diff,pmc,"usecType","USEC 角色类型","USEC PMC 生成时使用的角色类型代号。","pmcConfig");
  Add(diff,pmc,"bearType","BEAR 角色类型","BEAR PMC 生成时使用的角色类型代号。","pmcConfig");
  var modifier=Get(map,"BotLocationModifier");
  foreach(var key in new[]{"NonWaveSpawnBotsLimitPerPlayer","NonWaveSpawnBotsLimitPerPlayerPvE","LockSpawnCheckRadius","LockSpawnCheckRadiusPvE","LockSpawnStartTime","LockSpawnStartTimePvE","LockSpawnStepTime","LockSpawnStepTimePvE"})Add(key.Contains("Time")?time:cap,modifier,key,(key.EndsWith("PvE")?"PVE · ":"常规 · ")+(key.Contains("Limit")?"每名玩家的非波次 AI 限额":key.Contains("Radius")?"生成封锁检查半径":key.Contains("Start")?"生成封锁开始时间":"生成封锁检查步长"),key.Contains("Limit")?"非波次生成机制按玩家数计算的 AI 限额；与地图总上限分别生效。":"原生生成封锁检查参数。半径单位为米，时间单位为秒；是否禁止该出生点还取决于原生检查条件。","mapAi.BotLocationModifier");
  var config=Get(data,"locationConfig");
  Add(cap,config,"enableBotTypeLimits","按角色类型限制人数","开启时应用服务端 botTypeLimits 的角色限额；不同于整张地图总上限。","locationConfig");
  Add(cap,config,"addCustomBotWavesToMaps","加入服务端自定义波次","开启时服务端把自定义波次加入地图；地图波次可能已包含这些项，不要重复相加。","locationConfig");
  var localLimits=Get(Get(config,"botTypeLimits"),location);if(!Missing(localLimits))Walk(localLimits,"locationConfig.botTypeLimits."+location,cap,"角色限额","当前地图按角色限制的数量。仅在按角色类型限制人数开启时参与限制。",result);
  var ranges=Get(map,"MinMaxBots");if(!Missing(ranges))Walk(ranges,"mapAi.MinMaxBots",cap,"角色人数范围","WildSpawnType 指角色类型，min/max 为该类型的人数范围；不是已生成数量。",result);
  Waves(Get(map,"Waves"),"mapAi.Waves",normal,false,result);Waves(Get(map,"BossLocationSpawn"),"mapAi.BossLocationSpawn",boss,true,result);
  var rules=Get(data,"assistant");
  Add(assistant,rules,"NoAi","助手 · 禁止 AI","开启后助手阻止自然生成；优先于阵营倍率。此页只查看参数，修改入口在刷新规则。","assistant");
  Add(assistant,rules,"AiEnabled","助手 · 启用阵营倍率","关闭时所有阵营按 1 倍处理，即使保存的 Boss 倍率为 100，也不会应用。","assistant");
  var multipliers=Get(rules,"FactionMultipliers");if(multipliers.ValueKind==JsonValueKind.Object)foreach(var p in multipliers.EnumerateObject())result.Add(new(assistant,"助手倍率 · "+Faction(p.Name),Text(p.Value)+" 倍","仅在阵营倍率开启且未禁止 AI 时生效。普通波次缩放人数；Boss 类条目还会调整生成概率（最高 100%）。0 禁用相应生成，1 保持原值。","assistant.FactionMultipliers."+p.Name));
  if(Missing(rules))result.Add(new(assistant,"助手服务端配置","未提供","服务端没有返回助手刷新配置，不能把缺失值视为关闭或默认值。","assistant"));
  return result.ToArray();
 }
 static string Faction(string key)=>key switch{"scav"=>"Scav","usec"=>"USEC","bear"=>"BEAR","boss"=>"Boss","follower"=>"保镖","cultist"=>"邪教徒","blackDivision"=>"黑色军团","raider"=>"掠夺者","rogue"=>"游荡者",_=>"其他"};
 static void Waves(JsonElement waves,string path,string group,bool boss,List<Row> rows){
  if(waves.ValueKind!=JsonValueKind.Array){rows.Add(new(group,"波次列表","未提供","服务端未提供此列表，不能推断为不生成。",path));return;}
  rows.Add(new(group,"候选波次数量",waves.GetArrayLength()+" 条",boss?"此列表也可能包含 PMC、掠夺者、游荡者和事件 AI；每条还需满足概率、模式和触发条件。":"候选刷新批次数，不是 AI 人数。空列表不代表没有 AI，还可通过非波次和特殊波次生成。",path));
  int index=0;foreach(var wave in waves.EnumerateArray()){
   string roleKey=boss?"BossName":"WildSpawnType";
   var role=Get(wave,roleKey);
   string prefix=path+"["+index+"]",label="第 "+(++index)+" 批 · "+Text(role,roleKey);
   if(wave.ValueKind!=JsonValueKind.Object)continue;
   foreach(var p in wave.EnumerateObject()){
    var (name,help)=WaveHelp(p.Name);
    if(p.Value.ValueKind==JsonValueKind.Object||p.Name=="Supports"&&p.Value.ValueKind==JsonValueKind.Array){Walk(p.Value,prefix+"."+p.Name,group,label+" · "+name,help,rows,nativeSearch:role.ToString());continue;}
    string value=Text(p.Value,p.Name);
    if(p.Name=="BossChance"&&!Missing(p.Value))value+=" %";
    rows.Add(new(group,label+" · "+name,value,help,prefix+"."+p.Name,role+" "+(IsRole(p.Name)?p.Value.ToString():"")));
   }
  }
 }
 static (string,string) WaveHelp(string key)=>key.ToLowerInvariant() switch{
  "bossname" or "wildspawntype"=>("角色类型","这一批生成的 AI 角色。PMC、Boss 和普通 Scav 可使用不同生成路径。"),
  "bosschance"=>("生成概率","该条目的生成概率。100% 仍需满足地图模式、触发条件和可用出生点。"),
  "slots_min"=>("最少人数","这批普通波次的人数下限，不是当前存活人数。"),
  "slots_max"=>("最多人数","这批普通波次的人数上限，实际还受生成限制影响。"),
  "time_min" or "time_max" or "time"=>(key.Equals("time_min",StringComparison.OrdinalIgnoreCase)?"最早生成时间":key.Equals("time_max",StringComparison.OrdinalIgnoreCase)?"最晚生成时间":"生成时间","单位为战局开始后的秒数。-1 是原生特殊时间标记，不表示负数秒倒计时；事件条目还需触发。"),
  "botpreset" or "bossdifficult"=>("难度","这一批指定的 AI 难度档位；服务端覆盖或其他难度 MOD 可能进一步修改。"),
  "bossescortdifficult"=>("随从难度","随同生成的随从使用的难度档位。"),
  "bossescorttype"=>("随从类型","随同生成的角色类型，可能是保镖，也可能是 PMC 或游荡者。"),
  "bossescortamount"=>("随从人数候选","不包含主体。逗号分隔表示多个候选人数，不是把数字相加。"),
  "spawnpoints" or "bosszone" or "openzones"=>("生成区域","候选区域代号；多个区域不意味着每个区域都生成一批。未指定时由原生生成器选择。"),
  "spawnmode"=>("适用模式","只有符合列出的游戏模式时才适用；不能把所有模式的条目都算进当前战局。"),
  "ignoremaxbots"=>("忽略人数上限","开启后可绕过普通 AI 上限，所以地图实际人数可能高于 BotMax。"),
  "forcespawn"=>("强制生成标记","交给原生生成器的强制生成选项，不等于无条件成功；仍需检查事件及模式。"),
  "randomtimespawn"=>("随机生成时间","是否让原生生成器随机安排此条目的生成时间。"),
  "triggerid" or "triggername"=>("事件触发条件","非空时可能需要拉闸或警报等地图事件触发，单看概率无法判断是否生成。"),
  "delay"=>("触发后延迟","满足触发条件后等待的秒数。"),
  "supports"=>("额外支援","额外随从或支援列表；每条有自己的角色、人数和难度。"),
  "botside"=>("所属阵营","这批 AI 的阵营分类，如 Scav、USEC 或 BEAR。"),
  "keepzoneonspawn"=>("保留生成区域","是否保留本波次指定的出生区域约束。"),
  "dependkarma" or "dependkarmapve"=>("受业力影响","是否把玩家业力纳入生成条件；带 PvE 的参数用于 PVE。"),
  "number"=>("波次编号","用于识别批次的编号，不是人数。"),
  "isplayers" or "bossplayer"=>("玩家类生成标记","原生生成器的条目分类标记，不表示场上存在真人联机玩家。"),
  "showontarkovmap" or "showontarkovmappve"=>("地图展示标记","控制条目的地图展示；不直接决定生成概率或人数。"),
  _=>(key,"服务端扩展波次参数。当前版本没有核实具体作用，保留原名和值供核对。")
 };
 static void Walk(JsonElement value,string path,string group,string name,string help,List<Row> rows,string key="",string nativeSearch=""){
  if(value.ValueKind==JsonValueKind.Object){foreach(var p in value.EnumerateObject()){var detail=WaveHelp(p.Name);Walk(p.Value,path+"."+p.Name,group,name+" · "+detail.Item1,detail.Item2.StartsWith("服务端扩展")?help:detail.Item2,rows,p.Name,nativeSearch);}return;}
  if(value.ValueKind==JsonValueKind.Array){int i=0;foreach(var item in value.EnumerateArray())Walk(item,path+"["+i+++"]",group,name,help,rows,key,nativeSearch);if(i==0)rows.Add(new(group,name,"无条目",help,path,nativeSearch));return;}
  rows.Add(new(group,name,Text(value,key),help,path,nativeSearch+(IsRole(key)?" "+value:"")));
 }
}
