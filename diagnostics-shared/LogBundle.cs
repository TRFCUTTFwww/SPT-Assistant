using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace AssistantDiagnostics {
 public static class LogBundle {
  public static readonly string[] Levels={"ALL","TRACE","DEBUG","INFO","WARNING","ERROR","FATAL","OFF"};
  static readonly Regex Secret=new Regex("(?i)(\\\"?(?:api[-_]?key|authorization|password|proxyPassword|access[-_]?token|refresh[-_]?token|PHPSESSID)\\\"?\\s*[:=]\\s*)(\\\"[^\\\"]*\\\"|[^\\s,;]+)",RegexOptions.Compiled);
  static readonly Regex Pat=new Regex("(?:github_pat_[A-Za-z0-9_]+|gh[pousr]_[A-Za-z0-9_]+|Bearer\\s+[A-Za-z0-9._~+/-]+)",RegexOptions.Compiled|RegexOptions.IgnoreCase);
  public static string Redact(string text){return Regex.Replace(Pat.Replace(Secret.Replace(text,"$1[REDACTED]"),"[REDACTED]"),"(?i)(https?|socks5h?)://[^\\s/@:]+:[^\\s/@]+@","$1://[REDACTED]@");}
  public static int Rank(string level){int n=Array.IndexOf(Levels,(level??"INFO").ToUpperInvariant());return n<0?3:n;}
  public static bool Include(string level,string minimum){return Rank(minimum)<7&&Rank(level)>=Rank(minimum);}
  public static bool QuietRoute(string text){return text!=null&&text.IndexOf("/local/assistant/chat",StringComparison.OrdinalIgnoreCase)>=0;}
  public static string LevelOf(string line,string previous){string s=line.ToUpperInvariant();foreach(string level in new[]{"FATAL","ERROR","WARNING","WARN","DEBUG","TRACE","INFO","MESSAGE"})if(s.Contains("["+level)||s.Contains("|"+level+"|")||s.Contains(" "+level+" "))return level=="WARN"?"WARNING":level=="MESSAGE"?"INFO":level;return previous;}
  public static string Export(string game,string destination,string minimum,bool spt,bool gameLogs,string hardware,string performance,string report){
   Directory.CreateDirectory(destination);string final=Path.Combine(destination,"SPT-Assistant-logs-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".zip"),temporary=final+".partial";
   try{using(var zip=new ZipArchive(new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None),ZipArchiveMode.Create)){
    Add(zip,"report.txt",Redact(report??""));Add(zip,"export.txt","UTC: "+DateTime.UtcNow.ToString("O")+"\nMinimum level: "+minimum+"\nProfile/config/credential files are excluded. Log tails are limited to 8 MiB per file and 32 files total.\n");
    if(!String.IsNullOrEmpty(hardware))Add(zip,"hardware.json",Redact(hardware));if(!String.IsNullOrEmpty(performance))Add(zip,"performance.json",Redact(performance));
    var files=new List<string>();string bep=Path.Combine(game,"BepInEx","LogOutput.log");if(gameLogs&&File.Exists(bep))files.Add(bep);
    AddDirectory(files,Path.Combine(game,"BepInEx","plugins","LocalSpt5ESP","runtime","logs"),"*.log",false);
    if(gameLogs)AddDirectory(files,Path.Combine(game,"Logs"),"*.log",true);
    if(spt)foreach(string path in new[]{"SPT_Runtime/user/logs","SPT_Runtime/SPT_Data/logs","SPT_Runtime/logs"})AddDirectory(files,Path.Combine(game,path.Replace('/',Path.DirectorySeparatorChar)),"*.log",true);
    var manifest=new StringBuilder();int index=0;
    foreach(string file in files.Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(File.GetLastWriteTimeUtc).Take(32)){
     try{if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)continue;string relative=Path.GetFullPath(file).Substring(Path.GetFullPath(game).TrimEnd(Path.DirectorySeparatorChar).Length+1).Replace('\\','/');var entry=zip.CreateEntry("logs/"+relative,CompressionLevel.Fastest);
      using(var input=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(input,Encoding.UTF8,true))using(var writer=new StreamWriter(entry.Open(),new UTF8Encoding(false))){bool truncated=input.Length>8L*1024*1024;if(truncated){input.Seek(-8L*1024*1024,SeekOrigin.End);reader.DiscardBufferedData();reader.ReadLine();writer.WriteLine("[INFO] Log tail truncated to 8 MiB.");}string current="INFO",line;while((line=reader.ReadLine())!=null){current=LevelOf(line,current);if(QuietRoute(line)&&Rank(current)<=Rank("INFO"))continue;if(Include(current,minimum))writer.WriteLine(Redact(line.Length>65536?line.Substring(0,65536)+" [line truncated]":line));}manifest.AppendLine(relative+(truncated?" [tail truncated]":""));}index++;
     }catch(Exception e){manifest.AppendLine("Skipped "+Path.GetFileName(file)+": "+e.GetType().Name);}
    }Add(zip,"included-files.txt",manifest.ToString());
   }File.Move(temporary,final);return final;}catch{try{File.Delete(temporary);}catch{}throw;}
  }
  static void AddDirectory(List<string> files,string directory,string pattern,bool recursive){if(!Directory.Exists(directory))return;var pending=new Stack<string>();pending.Push(directory);int visited=0;while(pending.Count>0&&visited++<256){string current=pending.Pop();try{if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)continue;files.AddRange(Directory.EnumerateFiles(current,pattern).Where(f=>(File.GetAttributes(f)&FileAttributes.ReparsePoint)==0).OrderByDescending(File.GetLastWriteTimeUtc).Take(24));if(recursive)foreach(string child in Directory.EnumerateDirectories(current))pending.Push(child);}catch{}}}
  static void Add(ZipArchive zip,string name,string value){var entry=zip.CreateEntry(name,CompressionLevel.Fastest);using(var writer=new StreamWriter(entry.Open(),new UTF8Encoding(false)))writer.Write(value);}
 }
}
