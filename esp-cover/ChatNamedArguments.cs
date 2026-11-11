using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace LocalSpt5Esp;
internal static class ChatNamedArguments {
 internal sealed record Chip(int Start,int End,string Key,string Value);
 internal static Chip[] Chips(string text){
  var result=new List<Chip>();int start=0,depth=0;bool quote=false,escape=false;
  for(int i=0;i<=text.Length;i++){char c=i<text.Length?text[i]:' ';if(escape){escape=false;continue;}if(c=='\\'&&quote){escape=true;continue;}if(c=='"'){quote=!quote;continue;}if(!quote){if(c is '[' or '{')depth++;else if(c is ']' or '}')depth--;}
   if(!quote&&depth==0&&char.IsWhiteSpace(c)){if(i>start){try{var tokens=ChatCommandSyntax.Tokens(text[start..i],true);if(tokens.Length==1&&Assignment(tokens[0])){var pair=Pair(tokens[0]);result.Add(new(start,i,pair.Key,pair.Value));}}catch(ArgumentException){}}start=i+1;}}
  return result.ToArray();
 }
 internal sealed record Field(string Key,string Path,string Label,string Description,string Example,string[] Values);
 static readonly Dictionary<string,string> aliases=new(StringComparer.OrdinalIgnoreCase){["fir"]="foundInRaid",["medical"]="medicalResource",["food"]="foodResource",["uses"]="keyRemaining",["health"]="totalHealth",["healthPercent"]="healthPercent",["pose"]="scene.pose",["look"]="scene.look",["follow"]="scene.follow",["speed"]="scene.speed",["followDistance"]="scene.followDistance",["noHealing"]="scene.noHealing",["noDoors"]="scene.noDoors",["noMovement"]="scene.noMovement",["setFacing"]="scene.setFacing",["ergonomics"]="weapon.values.Ergonomics",["firerate"]="weapon.values.bFirerate",["recoilUp"]="weapon.values.RecoilForceUp",["recoilBack"]="weapon.values.RecoilForceBack",["ignoreAttachments"]="weapon.ignoreAttachments"};
 static readonly Dictionary<string,(string Label,string Description,string Example)> descriptions=new(){
  ["foundInRaid"]=("带钩","搜刮标记；新物品的子项继承此值","true"),["durability"]=("当前耐久","不可超过最大耐久","80"),["maxDurability"]=("最大耐久","设定物品最大耐久","100"),["medicalResource"]=("医疗余量","剩余医疗资源","400"),["foodResource"]=("食物余量","剩余食物资源","100"),["keyRemaining"]=("剩余次数","钥匙或访问卡的剩余次数","32767"),["resource"]=("资源余量","物品剩余资源","100"),["poisonUses"]=("毒素余量","物品剩余毒素","10"),["repairKitResource"]=("维修工具余量","维修工具剩余资源","1000"),["toggledOn"]=("开关","启用或关闭物品","true"),
  ["name"]=("名字","包含空格时用双引号","Test AI"),["totalHealth"]=("总血量","按部位比例设置 AI 总血量","1000"),["healthPercent"]=("当前血量百分比","0 到 100；AI 配置须大于 0","100"),["paused"]=("暂停 AI","暂停或恢复 AI 行动","true"),["invincible"]=("无敌","AI 无敌状态","true"),["cleanEffects"]=("清理效果","清理已有状态效果","true"),["playerRelation"]=("对玩家关系","default 默认 / friendly 友好 / hostile 敌对","friendly"),["aiRelation"]=("对 AI 关系","default 默认 / friendly 友好 / hostile 敌对","default"),
  ["amount"]=("增加血量","恢复指定数量；不可与 percent 混用","20"),["percent"]=("目标百分比","设为最大值的百分比","100"),["clearEffects"]=("清除负面效果","治疗时是否清理负面效果","false"),["hydration"]=("水分","设定目标水分","100"),["energy"]=("能量","设定目标能量","100"),["recursive"]=("包含子物品","搜索或处理附件与内容物","true"),["count"]=("数量","生成数量或本次最多补充发数","1"),["durabilityMin"]=("最低耐久","清除筛选下限，包含边界","0"),["durabilityMax"]=("最高耐久","清除筛选上限，包含边界","50"),["rain"]=("降雨","0 到 1","0.4"),["fog"]=("雾","0 到 1","0.05"),["clouds"]=("云量","-1 到 1","0.8"),["yaw"]=("水平朝向","角度；不可与 facing 混用","90"),["pitch"]=("俯仰","-89 到 89 度","0"),
  ["scene.pose"]=("姿势","站立、蹲下、趴下或保持原生","crouched"),["scene.follow"]=("跟随","跟随本地玩家","true"),["scene.look"]=("注视","看向本地玩家","true"),["scene.speed"]=("移动倍率","0.1 到 5","1"),["scene.followDistance"]=("跟随距离","1 到 30 米","4"),["scene.noHealing"]=("禁止治疗","禁止 AI 自行治疗","true"),["scene.noDoors"]=("禁止开门","禁止 AI 操作门","true"),["scene.noMovement"]=("禁止移动","禁止 AI 自主移动","true"),["scene.setFacing"]=("固定朝向","固定导演朝向","true"),
  ["weapon.values.Ergonomics"]=("人机工效","仅本局该枪实例生效","80"),["weapon.values.bFirerate"]=("自动射速","每分钟发数；仅本局","750"),["weapon.values.RecoilForceUp"]=("垂直后座","仅本局该枪实例生效","20"),["weapon.values.RecoilForceBack"]=("水平后座","仅本局该枪实例生效","20"),["weapon.ignoreAttachments"]=("忽略配件数值","忽略配件的后座、工效等修正","false"),["pos"]=("生成位置","x,y,z；支持 ~ 相对或 ^ 镜头坐标","~,~,~5"),["difficulty"]=("难度","easy / normal / hard / impossible","normal")};
 static readonly HashSet<string> boolean=new(){"foundInRaid","toggledOn","paused","invincible","cleanEffects","clearEffects","recursive","scene.follow","scene.look","scene.noHealing","scene.noDoors","scene.noMovement","scene.setFacing","weapon.ignoreAttachments"};
 internal static string PathFor(string key)=>aliases.TryGetValue(key,out var path)?path:key;
 internal static string KeyFor(string path)=>aliases.FirstOrDefault(p=>p.Value==path).Key??path;
 internal static Field Describe(string key){string path=PathFor(key);var d=descriptions.GetValueOrDefault(path,(path,"详细参数","0"));string[] choices=boolean.Contains(path)?new[]{"true","false"}:path is "playerRelation" or "aiRelation"?new[]{"default","friendly","hostile"}:path=="scene.pose"?new[]{"standing","crouched","prone","original"}:path=="difficulty"?new[]{"easy","normal","hard","impossible"}:Array.Empty<string>();return new(key,path,d.Item1,d.Item2,d.Item3,choices);}
 internal static Field[] Fields(string command,string[] args){var list=new List<string>();foreach(var key in ChatCommandOptions.KeysFor(command,args)){
   if(key=="scene")list.AddRange(ChatCommandOptions.SceneKeys.Select(k=>"scene."+k));
   else if(key=="weapon")list.AddRange(new[]{"weapon.values.Ergonomics","weapon.values.bFirerate","weapon.values.RecoilForceUp","weapon.values.RecoilForceBack","weapon.ignoreAttachments"});
   else if(key is not ("attachments" or "contents" or "cartridges" or "bodyHealth" or "effects" or "dogtag"))list.Add(key);
  }if(command=="summon")list.AddRange(new[]{"pos","count","difficulty"});return list.Select(p=>Describe(KeyFor(p))).ToArray();}
 internal static bool Assignment(string value)=>value.Contains('=')&&!value.StartsWith('@')&&!value.StartsWith('{');
 internal static (string Key,string Value) Pair(string value){int at=value.IndexOf('=');if(at<=0)throw new ArgumentException("参数格式为 名称=值。");return(value[..at],value[(at+1)..]);}
 internal static JsonElement? Take(string command,ref string[] args){
  var json=ChatCommandOptions.Take(ref args);var raw=new List<string>();var named=new Dictionary<string,string>(StringComparer.Ordinal);bool began=false;
  foreach(var a in args){if(Assignment(a)){began=true;var p=Pair(a);string path=PathFor(p.Key);if(!named.TryAdd(path,p.Value))throw new ArgumentException("参数重复："+p.Key);}else{if(began)throw new ArgumentException("位置参数须写在 名称=值 之前。");raw.Add(a);}}
  args=raw.ToArray();if(named.Count==0)return json;
  if(json!=null)throw new ArgumentException("命名参数与旧 JSON 配置不能混用。");
  var fields=Fields(command,args);var allowed=fields.Select(f=>f.Path).ToHashSet(StringComparer.Ordinal);var root=new JsonObject();
  foreach(var original in named){var p=(command is "summon" or "ai")&&original.Key is "yaw" or "pitch"?new KeyValuePair<string,string>("scene."+original.Key,original.Value):original;if(!allowed.Contains(p.Key)&&!WeaponPath(command,args,p.Key)&&!BodyPath(command,args,p.Key))throw new ArgumentException("此命令不支持参数："+p.Key);if(command=="summon"&&p.Key is "pos" or "count" or "difficulty")continue;
   if(p.Value.Length==0)throw new ArgumentException("参数缺少值："+p.Key);object value;if(boolean.Contains(p.Key)){if(!bool.TryParse(p.Value,out bool b))throw new ArgumentException(p.Key+" 需要 true 或 false。");value=b;}
   else if(p.Key is "name" or "scene.pose")value=p.Value;
   else if(p.Key is "playerRelation" or "aiRelation")value=p.Value.ToLowerInvariant() switch{"default"=>0,"friendly" or "neutral"=>1,"hostile"=>2,_=>ChatCommandSyntax.Number(p.Value,0,2)};
   else value=ChatCommandSyntax.Number(p.Value,-float.MaxValue,float.MaxValue);
   Put(root,p.Key,JsonSerializer.SerializeToNode(value));
  }
  if(command=="summon"&&named.Keys.Any(k=>k is "pos" or "count" or "difficulty")){
   if(args.Length!=1)throw new ArgumentException("命名生成参数请使用 /summon <角色> pos=… count=… difficulty=…，不可与位置式坐标/数量混用。");
   var pos=named.GetValueOrDefault("pos","~,~,~3").Split(',');if(pos.Length!=3||pos.Any(string.IsNullOrWhiteSpace))throw new ArgumentException("pos 格式为 x,y,z，例如 ~,~,~5。");
   string count=named.GetValueOrDefault("count","1"),difficulty=named.GetValueOrDefault("difficulty","normal");difficulty=difficulty.ToLowerInvariant() switch{"easy"=>"0","normal"=>"1","hard"=>"2","impossible"=>"3",_=>difficulty};
   CheckInteger(count,1,32);CheckInteger(difficulty,0,3);args=new[]{args[0],pos[0],pos[1],pos[2],count,difficulty};
  }
  return JsonSerializer.SerializeToElement(root);
 }
 static void CheckInteger(string s,int min,int max){double n=ChatCommandSyntax.Number(s,min,max);if(n!=Math.Truncate(n))throw new ArgumentException("数量或难度须为整数。");}
 static bool WeaponPath(string c,string[] a,string p)=>ChatCommandOptions.KeysFor(c,a).Contains("weapon")&&p.StartsWith("weapon.values.")&&p.Length>14;
 static bool BodyPath(string c,string[] a,string p)=>ChatCommandOptions.KeysFor(c,a).Contains("bodyHealth")&&p.Split('.') is var parts&&parts.Length==3&&new[]{"Head","Chest","Stomach","LeftArm","RightArm","LeftLeg","RightLeg"}.Contains(parts[1])&&parts[2] is "maximum" or "current"&&parts[0]=="bodyHealth";
 internal static void Put(JsonObject root,string path,JsonNode? value){var parts=path.StartsWith("weapon.values.")?new[]{"weapon","values",path[14..]}:path.Split('.');for(int i=0;i<parts.Length-1;i++){if(root[parts[i]] is not JsonObject child){if(root.ContainsKey(parts[i]))throw new ArgumentException("参数路径冲突："+path);child=new();root[parts[i]]=child;}root=child;}if(root.ContainsKey(parts[^1]))throw new ArgumentException("参数路径重复："+path);root[parts[^1]]=value;}
 internal static ChatSuggestion[] Complete(string prefix,int start,string command,string[] args,string token){
  var before=token.Length>0&&args.Length>0?args[..^1]:args;var positional=before.TakeWhile(a=>!Assignment(a)&&!a.StartsWith('{')).ToArray();if(!ChatCommandOptions.CanAppend(command,positional))return Array.Empty<ChatSuggestion>();
  var used=args.Where(Assignment).Where(a=>a!=token).Select(a=>PathFor(Pair(a).Key)).ToHashSet();
  int equal=token.IndexOf('=');if(equal==0)return Array.Empty<ChatSuggestion>();if(equal>0){var pair=Pair(token);var f=Fields(command,positional).FirstOrDefault(f=>f.Path==PathFor(pair.Key));if(f==null)return Array.Empty<ChatSuggestion>();return (f.Values.Length>0?f.Values:new[]{f.Example}).Where(v=>v.StartsWith(pair.Value,StringComparison.OrdinalIgnoreCase)).Select(v=>new ChatSuggestion(prefix[..start]+ChatCommandSyntax.Quote(pair.Key+"="+v),f.Label+"："+v,f.Description+" · "+pair.Key+"="+v)).ToArray();}
  return Fields(command,positional).Where(f=>!used.Contains(f.Path)&&(f.Key.StartsWith(token,StringComparison.OrdinalIgnoreCase)||f.Label.Contains(token,StringComparison.OrdinalIgnoreCase))).Select(f=>new ChatSuggestion(prefix[..start]+f.Key+"=",f.Label+"  ·  "+f.Key+"=",f.Description)).ToArray();
 }
}
