using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
namespace LocalSpt5Esp;
internal static partial class RaidCommands {
 static string OptionHelp(string? command){
  if(command==null)return "详细参数：命令末尾可加 JSON 对象；/help give、/help ai 查看支持字段。完整示例见 docs/COMMAND-OPTIONS.md。\n";
  command=command.ToLowerInvariant();var args=command=="ai"?new[]{"configure"}:command=="item"?new[]{"replace"}:command=="loot"?new[]{"spawn"}:Array.Empty<string>();var keys=ChatCommandOptions.KeysFor(command,args);
  if(keys.Length==0)return "";
  return "可在末尾添加 JSON 配置（双引号、字段区分大小写）：\n"+string.Join(", ",keys)+"\n"+(command switch{
   "give"=>"/give @s salewa 1 {\"foundInRaid\":true,\"medicalResource\":400}",
   "item"=>"/item modify entity @s FirstPrimaryWeapon {\"durability\":80,\"maxDurability\":100}\n/item inspect entity @s FirstPrimaryWeapon\n现有物品修改不接受 attachments / contents / cartridges；item replace 仅限空槽。",
   "ai"=>"/ai configure @e[type=scav,limit=1] {\"paused\":true,\"healthPercent\":50}",
   "heal"=>"/heal @s Chest {\"amount\":20,\"clearEffects\":false}",
   _=>"字段值和嵌套结构见 docs/COMMAND-OPTIONS.md；输入 { 或完整配置后使用 Tab 添加顶层字段。"})+"\n";
 }
 static readonly Dictionary<string,string> optionExamples=new(){
  ["durability"]="80",["maxDurability"]="100",["medicalResource"]="400",["foodResource"]="100",["keyRemaining"]="32767",["poisonUses"]="10",["resource"]="100",["repairKitResource"]="1000",["toggledOn"]="true",["foundInRaid"]="true",
  ["attachments"]="{\"mod_magazine\":{\"item\":\"<弹匣名称或ID>\",\"options\":{\"cartridges\":[{\"item\":\"<弹药名称或ID>\",\"count\":30}]}}}",
  ["contents"]="[{\"item\":\"卢布\",\"count\":10000}]",["cartridges"]="[{\"item\":\"<弹药名称或ID>\",\"count\":30}]",
  ["weapon"]="{\"values\":{\"Ergonomics\":80,\"bFirerate\":750},\"ignoreAttachments\":false}",["dogtag"]="{\"nickname\":\"Test\",\"level\":50,\"side\":\"Usec\"}",
  ["name"]="\"Test AI\"",["totalHealth"]="1000",["healthPercent"]="100",["bodyHealth"]="{\"Chest\":{\"maximum\":300,\"current\":300}}",["paused"]="true",["invincible"]="true",["cleanEffects"]="true",["playerRelation"]="1",["aiRelation"]="0",
  ["effects"]="[{\"type\":\"PainKiller\",\"part\":\"Chest\",\"seconds\":120,\"strength\":1}]",["scene"]="{\"pose\":\"crouched\",\"noHealing\":true,\"followDistance\":4,\"speed\":1}",
  ["amount"]="20",["percent"]="100",["clearEffects"]="false",["hydration"]="100",["energy"]="100",["recursive"]="true",["count"]="10",["durabilityMin"]="0",["durabilityMax"]="50",["rain"]="0.5",["fog"]="0.1",["clouds"]="1",["yaw"]="90",["pitch"]="0"};
 internal static ChatSuggestion[] OptionSuggestions(string prefix,int start,string command,string[] args,string token){
  var positional=args.Length>0&&args[^1].StartsWith('{')?args[..^1]:args;if(!ChatCommandOptions.CanAppend(command,positional))return Array.Empty<ChatSuggestion>();
  var keys=ChatCommandOptions.KeysFor(command,positional);var current=new Dictionary<string,JsonElement>();
  try{using var doc=JsonDocument.Parse(token=="{"?"{}":token);ChatCommandOptions.Keys(doc.RootElement,keys);foreach(var p in doc.RootElement.EnumerateObject())current.Add(p.Name,p.Value.Clone());}catch(Exception e) when(e is JsonException or ArgumentException){return Array.Empty<ChatSuggestion>();}
  return keys.Where(k=>!current.ContainsKey(k)).Select(k=>{
   string example=optionExamples.GetValueOrDefault(k,"true");var next=new Dictionary<string,JsonElement>(current);using var value=JsonDocument.Parse(example);next[k]=value.RootElement.Clone();
   return new ChatSuggestion(prefix[..start]+JsonSerializer.Serialize(next),k,example);
  }).ToArray();
 }
}
