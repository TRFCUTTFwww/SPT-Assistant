using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
namespace LocalSpt5Esp;
internal sealed class TerminalInput {
 internal readonly SortedSet<string> Commands=new(StringComparer.OrdinalIgnoreCase){"help","spt","spt give","spt profile","spt profile level","spt profile skill","spt profile examine","spt trader"};
 internal static readonly Dictionary<string,string> Usage=new(){["spt give"]="spt give [模板 ID 或 \"物品名\"] [数量]",["spt profile level"]="spt profile level [等级]",["spt profile skill"]="spt profile skill [技能名] [等级]",["spt profile examine"]="spt profile examine",["spt trader"]="spt trader [商人] rep|spend [数量]",["help"]="help · 查询服务端命令目录"};
 string last="",suffix="";string[] cycle=Array.Empty<string>();int index;
 internal static string Clipboard(string value)=>new string(value.Select(c=>c is '\r' or '\n' or '\t'?' ':c).Where(c=>!char.IsControl(c)).ToArray());
 internal static (string Text,int Cursor) Insert(string text,int cursor,int selection,string paste){cursor=Math.Clamp(cursor,0,text.Length);selection=Math.Clamp(selection,0,text.Length);int start=Math.Min(cursor,selection),end=Math.Max(cursor,selection);paste=Clipboard(paste);return(text[..start]+paste+text[end..],start+paste.Length);}
 internal string[] Suggest(string input)=>Commands.Where(c=>c.StartsWith(input,StringComparison.OrdinalIgnoreCase)).Take(8).ToArray();
 internal (string Text,int Cursor) Complete(string input,int cursor,bool reverse){cursor=Math.Clamp(cursor,0,input.Length);if(input!=last||cycle.Length==0){suffix=input[cursor..];cycle=Suggest(input[..cursor]);index=reverse?cycle.Length-1:0;}else index=(index+(reverse?-1:1)+cycle.Length)%cycle.Length;if(cycle.Length==0)return(input,cursor);string head=cycle[index];last=head+suffix;return(last,head.Length);}
 internal void Learn(IEnumerable<string> messages){foreach(string message in messages)foreach(Match match in Regex.Matches(message,@"(?im)^\s*(spt(?: [a-z][a-z-]*){1,3})(?=\s*\[|\s*$)")){string command=match.Groups[1].Value.Trim();if(command.Length<100)Commands.Add(command);}}
 internal string Hint(string input)=>Usage.Where(pair=>input.StartsWith(pair.Key,StringComparison.OrdinalIgnoreCase)).OrderByDescending(pair=>pair.Key.Length).Select(pair=>pair.Value).FirstOrDefault()??"Tab 补全命令 · Shift+Tab 反向选择 · 点击候选只填入，不执行";
}
