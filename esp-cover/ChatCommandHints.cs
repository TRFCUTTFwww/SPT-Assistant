using System;
using System.Collections.Generic;
using System.Linq;
namespace LocalSpt5Esp;
internal static class ChatCommandHints {
 internal static readonly Dictionary<string,string> Usage=new(StringComparer.OrdinalIgnoreCase){
 ["help"]="[命令]",["list"]="[entities]",["msg"]="<玩家> <消息>",["tell"]="<玩家> <消息>",["w"]="<玩家> <消息>",["reply"]="<消息>",["r"]="<消息>",["me"]="<动作>",["clearhistory"]="",["identity"]="[verify <玩家或档案 ID> <指纹>]",["chat"]="reconnect",["permission"]="list",
 ["tp"]="[来源] <目标|x y z> [facing <目标|x y z>]",["teleport"]="[来源] <目标|x y z>",["position"]="[目标]",["back"]="",["spreadplayers"]="<x> <z> <最小间距> <范围> <目标>",
 ["inspect"]="<目标> [health|equipment|effects|position]",["kill"]="<目标>",["damage"]="<目标> <数值> [伤害类型] [部位]",["heal"]="[目标] [部位]",["effect"]="give <目标> <效果> [秒] [强度] [部位] | clear <目标> [效果]",
 ["summon"]="<AI 角色|template:场景名> [x y z] [数量] [难度0-3]",["tag"]="<目标> add|remove|list [标签]",["rename"]="<AI> <名称>",["ai"]="pause|resume|follow|look|pose|relation|anchor|remove <AI> [参数] | configure <AI> [参数=值 ...]",
 ["edit"]="on|off",["copy"]="[AI|@selected]",["paste"]="[x y z]",["remove"]="<AI|@selected>",["data"]="get entity <目标> [字段] | modify entity <AI> <字段> set value <值>",
 ["give"]="<玩家或AI> <物品名称或item:模板ID或preset:预设> [数量] [edit]",["clear"]="<目标> [物品名称或item:模板ID] [最大数量]",["item"]="replace entity <目标> <装备槽> with <物品> [数量] | modify entity <目标> <槽/附件路径> <参数> <值> | inspect entity <目标> <槽/附件路径>",["loot"]="spawn <x y z> <物品> [数量] | give <目标> loadout:<场景名>",["repair"]="<目标> [装备槽]",["reload"]="<目标> [装备槽] [弹药名称或item:模板ID]",
 ["time"]="set day|noon|night|midnight|<ticks> | add <ticks> | query daytime",["weather"]="clear|rain|thunder [持续秒数]",["gamerule"]="[规则] [值]",["raid"]="time query|set|add [时长] | extract @s",["airdrop"]="summon",["btr"]="status|route <站点ID>|stop",["marker"]="set <名称> [x y z] | remove <名称> | list",["navigate"]="<目标标签|x y z>|stop",
 ["experience"]="query <玩家> [points|levels]",["xp"]="query <玩家> [points|levels]",["attribute"]="<目标> <属性> base get|set [数值]"};
 internal sealed record Preview(string Syntax,string Current);
 internal static bool HasArgument(string command,string[] args,int at)=>Usage.ContainsKey(command)&&at>=0&&(at<Parts(Pattern(command,args)).Length||ChatCommandOptions.KeysFor(command,args).Length>0&&at<=Parts(Pattern(command,args)).Length);
 internal static Preview For(string text,int cursor){
  if(!ChatCommandSyntax.IsCommand(text))return new("","");
  string prefix=text[..Math.Clamp(cursor,0,text.Length)];string[] words;
  try{words=ChatCommandSyntax.Tokens(prefix.Length>0?prefix[1..]:"",true);}catch(ArgumentException){return new("引号、转义或选择器格式有误","");}
  string command=words.FirstOrDefault()?.ToLowerInvariant()??"";
  if(!Usage.ContainsKey(command))return new(command.Length==0?"输入 /命令，下面显示可补全的候选":"继续输入命令名称，或按 Tab 补全","");
  bool trailing=ChatCommandSyntax.CompletionStart(prefix)==prefix.Length;
  int at=words.Length-2+(trailing?1:0);var args=words.Skip(1).ToArray();
  var alternatives=Usage[command].Split(" | ",StringSplitOptions.None);
  if(at==0&&alternatives.Length>1&&!alternatives.Any(p=>p.Split(' ')[0].Equals(args.FirstOrDefault(),StringComparison.OrdinalIgnoreCase)))return new("/"+command+" "+Usage[command],"当前参数："+string.Join(" | ",alternatives.Select(p=>p.Split(' ')[0])));
  var parts=Parts(Pattern(command,args));if(ChatCommandOptions.KeysFor(command,args).Length>0)parts=parts.Append("[参数=值 ...]").ToArray();
  var current=trailing?"":args.LastOrDefault()??"";if(args.Any(ChatNamedArguments.Assignment)){var positional=args.TakeWhile(a=>!ChatNamedArguments.Assignment(a)).ToArray();var f=ChatNamedArguments.Assignment(current)&&current.IndexOf('=')>0?ChatNamedArguments.Describe(ChatNamedArguments.Pair(current).Key):null;return new("/"+command+" "+Pattern(command,positional)+" <color=#77D9C7><b>[参数=值 ...]</b></color>",f==null?"可继续添加参数；Tab 选择，Enter 执行":f.Label+" · "+f.Description);}
  if(args.LastOrDefault()?.StartsWith('{')==true)return new("/"+command+" "+Pattern(command,args.Take(args.Length-1).ToArray())+" <color=#77D9C7><b>{详细配置}</b></color>","可写字段："+string.Join(", ",ChatCommandOptions.KeysFor(command,args)));
  if(command is "msg" or "tell" or "w" or "reply" or "r" or "me"&&at>=parts.Length)at=parts.Length-1;
  string Highlight(string value)=>"<color=#77D9C7><b>"+value+"</b></color>";
  string syntax=(at<0?Highlight("/"+command):"/"+command)+string.Concat(parts.Select((p,i)=>" "+(i==at?Highlight(p):p)));
  return new(syntax,at<0?"命令名称":at<parts.Length?"当前参数："+parts[at]:"参数已填完 · Enter 执行");
 }
 static bool Coordinate(string value)=>value.StartsWith('~')||value.StartsWith('^')||double.TryParse(value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out _);
 static string Pattern(string command,string[] args){
  string Arg(int n)=>n<args.Length?args[n]:"";
  switch(command){
   case "identity":return "[verify] <玩家或档案ID> <指纹>";
   case "tp":case "teleport":{
    int facing=Array.IndexOf(args,"facing");var move=facing<0?args:args[..facing];
    string pattern=facing>=0?move.Length switch{1=>"<目标>",2=>"<来源> <目标>",3=>"<x> <y> <z>",_=>"<来源> <x> <y> <z>"}:move.Length>0&&Coordinate(move[0])?"<x> <y> <z>":move.Length>1&&Coordinate(move[1])?"<来源> <x> <y> <z>":move.Length>1?"<来源> <目标>":"<来源或目标|x> [目标|x] [y] [z]";
    return pattern+(facing>=0&&facing+1<args.Length&&Coordinate(args[facing+1])?" facing <x> <y> <z>":" [facing] <朝向目标|x> [y] [z]");
   }
   case "summon":return "<AI角色|template:场景名> [x] [y] [z] [数量] [难度0-3]";
   case "paste":return "[x] [y] [z]";
   case "navigate":return args.Length>0&&Coordinate(args[0])?"<x> <y> <z>":"<目标标签|x|stop>";
   case "marker":if(Arg(0)=="set")return "set <名称> [x] [y] [z]";break;
   case "loot":if(Arg(0)=="spawn")return "spawn <x> <y> <z> <物品> [数量]";break;
   case "tag":return Arg(1)=="list"?"<目标> list":"<目标> add|remove|list [标签]";
   case "item":if(Arg(0)=="inspect")return "inspect entity <目标> <槽/附件路径>";if(Arg(0)=="modify"&&(args.Length<=4||Arg(4).StartsWith('{')))return "modify entity <目标> <槽/附件路径>";break;
   case "ai":return Arg(0) switch{
    "configure"=>"configure <AI>",
    "pause" or "resume" or "remove"=>Arg(0)+" <AI>",
    "follow" or "look"=>Arg(0)+" <AI> [@s]",
    "pose"=>"pose <AI> <standing|crouched|prone|original>",
    "relation"=>"relation <AI> <neutral|hostile|default>",
    "anchor"=>"anchor <AI> [on|off]",_=>Usage[command]};
   case "raid":return Arg(0)=="extract"?"extract @s":Arg(1)=="query"?"time query":Arg(1) is "set" or "add"?"time "+Arg(1)+" <时长:15m|900s|hh:mm:ss>":"time|extract [query|set|add|@s] [时长]";
   case "btr":return Arg(0)=="route"?"route <站点ID>":"status|route|stop";
   case "attribute":return Arg(3)=="get"?"<目标> <属性> base get":"<目标> <属性> base get|set [数值]";
  }
  var alternatives=Usage[command].Split(" | ",StringSplitOptions.None);
  return alternatives.FirstOrDefault(p=>p.Split(' ')[0].Equals(Arg(0),StringComparison.OrdinalIgnoreCase))??alternatives[0];
 }
 // Angle/square-bracket labels may contain spaces; each remains one parameter.
 static string[] Parts(string pattern){
  var parts=new List<string>();int depth=0,start=0;
  for(int i=0;i<pattern.Length;i++){char c=pattern[i];if(c is '<' or '[')depth++;else if(c is '>' or ']')depth--;else if(c==' '&&depth==0){if(i>start)parts.Add(pattern[start..i]);start=i+1;}}
  if(start<pattern.Length)parts.Add(pattern[start..]);return parts.ToArray();
 }
}
