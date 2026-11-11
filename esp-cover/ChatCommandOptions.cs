using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
namespace LocalSpt5Esp;
// A final JSON object augments existing positional syntax. Never interpreted as code.
internal static class ChatCommandOptions {
 internal const int MaximumInput=16384;
 internal static JsonElement? Take(ref string[] args){if(args.Length==0||!args[^1].StartsWith('{'))return null;using var doc=JsonDocument.Parse(args[^1],new JsonDocumentOptions{MaxDepth=20});var value=doc.RootElement.Clone();args=args[..^1];return value;}
 internal static void Keys(JsonElement value,params string[] allowed){if(value.ValueKind!=JsonValueKind.Object)throw new ArgumentException("配置必须是 JSON 对象。");var seen=new HashSet<string>(StringComparer.Ordinal);foreach(var p in value.EnumerateObject())if(!seen.Add(p.Name)||!allowed.Contains(p.Name,StringComparer.Ordinal))throw new ArgumentException("未知或重复参数："+p.Name+"；支持："+string.Join(", ",allowed));}
 internal static bool Has(JsonElement? value,string key)=>value is {} v&&v.TryGetProperty(key,out _);
 internal static string String(JsonElement v,string key,int max=128){var e=v.GetProperty(key);if(e.ValueKind!=JsonValueKind.String||e.GetString() is not string s||s.Length is <1||s.Length>max)throw new ArgumentException(key+" 需要非空字符串（最多 "+max+" 字符）。");return s;}
 internal static double Number(JsonElement v,string key,double min,double max,bool integer=false){var e=v.GetProperty(key);if(e.ValueKind!=JsonValueKind.Number||!e.TryGetDouble(out double n)||!double.IsFinite(n)||n<min||n>max||integer&&n!=Math.Truncate(n))throw new ArgumentException(key+" 数值超出范围或类型不正确。");return n;}
 internal static bool Bool(JsonElement v,string key){var e=v.GetProperty(key);if(e.ValueKind is not (JsonValueKind.True or JsonValueKind.False))throw new ArgumentException(key+" 需要 true 或 false。");return e.GetBoolean();}
 internal static readonly string[] ItemKeys={"durability","maxDurability","medicalResource","foodResource","keyRemaining","poisonUses","resource","repairKitResource","toggledOn","foundInRaid","attachments","contents","cartridges","weapon","dogtag"};
 internal static readonly string[] ActorKeys={"name","totalHealth","healthPercent","bodyHealth","paused","invincible","cleanEffects","playerRelation","aiRelation","effects","scene"};
 internal static readonly string[] SceneKeys={"pose","yaw","pitch","setFacing","noDoors","noHealing","noMovement","look","follow","followDistance","speed"};
 internal static string[] KeysFor(string command,string[] args)=>command switch{
  "give"=>ItemKeys,
  "loot" when args.FirstOrDefault()=="spawn"=>ItemKeys,
  "item" when args.FirstOrDefault()=="replace"=>ItemKeys,
  "item" when args.FirstOrDefault()=="modify"=>ItemKeys.Except(new[]{"attachments","contents","cartridges"}).ToArray(),
  "summon"=>ActorKeys,
  "ai" when args.FirstOrDefault()=="configure"=>ActorKeys,
  "heal"=>new[]{"amount","percent","clearEffects","hydration","energy"},
  "repair"=>new[]{"durability","maxDurability","percent","recursive"},
  "reload"=>new[]{"count"},"clear"=>new[]{"foundInRaid","durabilityMin","durabilityMax","recursive"},
  "weather"=>new[]{"rain","fog","clouds"},
  "tp" or "teleport"=>new[]{"yaw","pitch"},_=>Array.Empty<string>()};
 internal static bool CanAppend(string command,string[] args){
  try{ChatCommandSyntax.ValidateArguments(command,args);return KeysFor(command,args).Length>0&&(command!="item"||args[0]!="modify"||args.Length==4);}catch(ArgumentException){return false;}
 }
 internal static void Validate(string command,string[] args,JsonElement? options){if(options is {} value){var keys=KeysFor(command,args);if(keys.Length==0)throw new ArgumentException("此命令没有扩展配置。");Keys(value,keys);}}
 internal static string Token(string text,ref int index,bool incomplete){int start=index,depth=0;bool quote=false,escape=false;for(;index<text.Length;index++){char c=text[index];if(escape){escape=false;continue;}if(quote&&c=='\\'){escape=true;continue;}if(c=='"'){quote=!quote;continue;}if(quote)continue;if(c is '{' or '[')depth++;if(c is '}' or ']')depth--;if(depth==0){index++;return text[start..index];}}if(!incomplete)throw new ArgumentException("JSON 配置没有闭合。");return text[start..];}
}
