using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace LocalSpt5Esp;
internal static class ChatCommandSyntax {
 internal static string[] Tokens(string text,bool incomplete=false){var result=new List<string>();var token=new StringBuilder();bool quoted=false,escaped=false,started=false;int brackets=0;foreach(char c in text){if(escaped){if(c is not ('"' or '\\'))throw new ArgumentException("只支持 \\\" 和 \\\\ 转义。");token.Append(c);escaped=false;started=true;continue;}if(c=='\\'&&quoted){escaped=true;continue;}if(c=='"'){quoted=!quoted;started=true;continue;}if(!quoted){if(c=='[')brackets++;if(c==']')brackets--;if(brackets<0)throw new ArgumentException("选择器方括号不匹配。");if(char.IsWhiteSpace(c)&&brackets==0){if(started){result.Add(token.ToString());token.Clear();started=false;}continue;}}token.Append(c);started=true;}if(!incomplete&&(quoted||escaped||brackets!=0))throw new ArgumentException("引号或选择器没有闭合。");if(started)result.Add(token.ToString());return result.ToArray();}
 internal static string Quote(string value)=>value.Any(c=>char.IsWhiteSpace(c)||c=='"'||c=='\\')?"\""+value.Replace("\\","\\\\").Replace("\"","\\\"")+"\"":value;
 internal static int CompletionStart(string prefix){bool quote=false,escape=false;int bracket=0,start=0;for(int i=0;i<prefix.Length;i++){char c=prefix[i];if(escape){escape=false;continue;}if(c=='\\'&&quote){escape=true;continue;}if(c=='"'){quote=!quote;continue;}if(!quote){if(c=='[')bracket++;if(c==']')bracket--;if(char.IsWhiteSpace(c)&&bracket==0)start=i+1;}}return start;}
 internal static double Number(string value,double min,double max){if(!double.TryParse(value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var result)||!double.IsFinite(result)||result<min||result>max)throw new ArgumentException($"数值必须在 {min}～{max} 范围内。");return result;}
 internal static double Duration(string value){if(string.IsNullOrWhiteSpace(value))throw new ArgumentException("时长不能为空。");if(value.Contains(':')){var parts=value.Split(':');if(parts.Length!=3)throw new ArgumentException("时长格式为 hh:mm:ss。");return Number(parts[0],0,1000)*3600+Number(parts[1],0,59)*60+Number(parts[2],0,59);}double multiplier=value.EndsWith('h')?3600:value.EndsWith('m')?60:1;string number=char.IsLetter(value[^1])?value[..^1]:value;if(char.IsLetter(value[^1])&&value[^1] is not ('h' or 'm' or 's'))throw new ArgumentException("时长单位为 h、m 或 s。");return Number(number,0,3600000)*multiplier;}
 // Reject surplus/incomplete arguments before any world mutation.
 internal static void ValidateArguments(string command,string[] a){
  bool Count(int min,int max)=>a.Length>=min&&a.Length<=max;
  string first=a.FirstOrDefault()??"";
  bool valid=command switch{
   "list"=>a.Length==0||a.Length==1&&first=="entities",
   "position" or "copy"=>Count(0,1),"back"=>a.Length==0,
   "tp" or "teleport"=>a.Contains("facing")?Array.IndexOf(a,"facing") is >=1 and <=4&&a.Count(s=>s=="facing")==1&&a.Length-Array.IndexOf(a,"facing")-1 is 1 or 3:Count(1,4),
   "kill" or "remove" or "edit"=>a.Length==1,
   "rename"=>a.Length==2,"damage"=>Count(2,4),"heal"=>Count(0,2),"inspect"=>Count(1,2),
   "effect"=>first=="give"?Count(3,6):first=="clear"&&Count(2,3),
   "summon"=>a.Length is 1 or 4 or 5 or 6,
   "tag"=>Count(2,3)&&(a[1]=="list"?a.Length==2:a.Length==3),
   "ai"=>Count(2,3)&&(first is "pause" or "resume" or "remove"?a.Length==2:first is "pose" or "relation"?a.Length==3:true),
   "paste"=>a.Length is 0 or 3,"spreadplayers"=>a.Length==5,
   "give"=>Count(2,3),"clear"=>Count(1,3),"repair"=>Count(1,2),"reload"=>Count(1,3),
   "item"=>first=="replace"?Count(6,7):first=="modify"&&a.Length==6,
   "loot"=>first=="spawn"?Count(5,6):first=="give"&&a.Length==3,
   "data"=>first=="get"?Count(3,4):first=="modify"&&a.Length==7,
   "attribute"=>Count(4,5)&&(a[3]=="get"?a.Length==4:a.Length==5),
   "time"=>a.Length==2&&(first!="query"||a[1]=="daytime"),"weather"=>Count(1,2),"gamerule"=>Count(0,2),
   "raid"=>first=="extract"?a.Length==2:Count(2,3)&&first=="time"&&(a[1]=="query"?a.Length==2:a.Length==3),
   "btr"=>first=="route"?a.Length==2:a.Length==1,"airdrop"=>a.Length==1,
   "marker"=>first=="list"?a.Length==1:first=="remove"?a.Length==2:first=="set"&&a.Length is 2 or 5,
   "navigate"=>a.Length is 1 or 3,"experience" or "xp"=>first=="query"&&Count(2,3)&&(a.Length==2||a[2] is "points" or "levels"),
   _=>true};
  if(!valid)throw new ArgumentException("参数不匹配；/help "+command+" 查看当前支持的用法。");
 }
}
