using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace SptAssistantSetup {
 internal sealed class InstallProgress {public int Percent; public string Phase;}
 internal sealed class InstallResult {public bool Ok;public string Code="INSTALL_FAILED",Message="",Backup="",Log="";public bool RollbackSucceeded=true;}
 internal static class InstallerEngine {
  internal static string NormalizeGameRoot(string selected){
   if(string.IsNullOrWhiteSpace(selected))return "";
   string path=Path.GetFullPath(selected.Trim().Trim('"'));
   if(File.Exists(path))path=Path.GetDirectoryName(path);
   string candidate=path;
   for(int i=0;i<3&&!string.IsNullOrEmpty(candidate);i++,candidate=Path.GetDirectoryName(candidate.TrimEnd(Path.DirectorySeparatorChar))){if(File.Exists(Path.Combine(candidate,"EscapeFromTarkov.exe")))return candidate.TrimEnd(Path.DirectorySeparatorChar);}
   return path.TrimEnd(Path.DirectorySeparatorChar);
  }
  internal static bool IsWithin(string root,string candidate){return Path.GetFullPath(candidate).StartsWith(Path.GetFullPath(root).TrimEnd('\\','/')+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
  internal static string PsLiteral(string text){return "'"+text.Replace("'","''")+"'";}
  internal static string QuoteArgument(string text){var b=new StringBuilder("\"");int slashes=0;foreach(char c in text){if(c=='\\'){slashes++;continue;}if(c=='"'){b.Append('\\',slashes*2+1).Append(c);slashes=0;}else{b.Append('\\',slashes).Append(c);slashes=0;}}b.Append('\\',slashes*2).Append('"');return b.ToString();}
  internal static string BusyProcess(string root,bool clientOnly){
   foreach(string name in clientOnly?new[]{"EscapeFromTarkov"}:new[]{"EscapeFromTarkov","SPT.Server"})foreach(var process in Process.GetProcessesByName(name)){using(process){try{if(IsWithin(root,process.MainModule.FileName))return name;}catch{return name;}}}
   return "";
  }
  internal static string[] DetectGames(){
   var found=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   Action<string> add=delegate(string value){try{string root=NormalizeGameRoot(value);if(File.Exists(Path.Combine(root,"EscapeFromTarkov.exe")))found.Add(root);}catch{}};
   add(AppDomain.CurrentDomain.BaseDirectory);add(Environment.CurrentDirectory);
   try{string state=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SPTAssistant","installer-settings.json");var data=Json(File.ReadAllText(state));add(Text(data,"gameRoot"));}catch{}
   foreach(string name in new[]{"SPT.Launcher","SPT.Server","EscapeFromTarkov"})foreach(var p in Process.GetProcessesByName(name)){using(p){try{add(p.MainModule.FileName);}catch{}}}
   foreach(var drive in DriveInfo.GetDrives().Where(d=>d.DriveType==DriveType.Fixed&&d.IsReady)){
    add(drive.RootDirectory.FullName);
    try{foreach(string folder in Directory.EnumerateDirectories(drive.RootDirectory.FullName).Take(100)){
     string name=Path.GetFileName(folder).ToLowerInvariant();if(!(name.Contains("spt")||name.Contains("tarkov")||name.Contains("eft")||name.Contains("game")||name.Contains("游戏")))continue;
     add(folder);try{foreach(string child in Directory.EnumerateDirectories(folder).Take(100))add(child);}catch{}
    }}catch{}
   }
   return found.ToArray();
  }
  internal static Dictionary<string,object> Json(string raw){return new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(raw);}
  internal static string Text(Dictionary<string,object> data,string key){object v;return data.TryGetValue(key,out v)&&v!=null?Convert.ToString(v):"";}
  internal static string Hash(Stream stream){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
  internal static void ExtractZip(Stream input,string root){
   Directory.CreateDirectory(root);long size=0;int count=0;
   using(var zip=new ZipArchive(input,ZipArchiveMode.Read,true))foreach(var entry in zip.Entries){
    if(++count>4096||(size+=entry.Length)>512L*1024*1024)throw new InvalidDataException("Package is too large.");
    string target=Path.GetFullPath(Path.Combine(root,entry.FullName));if(!IsWithin(root,target)||entry.FullName.IndexOf(':')>=0||((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new InvalidDataException("Unsafe package path.");
    if(entry.FullName.EndsWith("/")){Directory.CreateDirectory(target);continue;}
    Directory.CreateDirectory(Path.GetDirectoryName(target));using(var source=entry.Open())using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write))source.CopyTo(output);
   }
  }
  internal static string ExtractPackage(){
   string root=Path.Combine(Path.GetTempPath(),"SPTAssistantSetup",Guid.NewGuid().ToString("N"));
   try{using(var data=Assembly.GetExecutingAssembly().GetManifestResourceStream("SPT.Assistant.Package.zip")){if(data==null||Hash(data)!=BundleInfo.Sha256)throw new InvalidDataException("Package hash mismatch.");data.Position=0;ExtractZip(data,root);}return root;}
   catch{Cleanup(root);throw;}
  }
  internal static void Cleanup(string root){try{string parent=Path.Combine(Path.GetTempPath(),"SPTAssistantSetup");if(IsWithin(parent,root)&&Directory.Exists(root)&&(new DirectoryInfo(root).Attributes&FileAttributes.ReparsePoint)==0)Directory.Delete(root,true);}catch{}}
  internal static InstallResult Run(string gameRoot,bool clientOnly,string language,bool verifyOnly,Action<InstallProgress> report){
   var result=new InstallResult();string package="";
   string logHome=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SPTAssistant","Logs");
   try{
    gameRoot=NormalizeGameRoot(gameRoot);if(string.IsNullOrEmpty(gameRoot)||!File.Exists(Path.Combine(gameRoot,"EscapeFromTarkov.exe"))){result.Code="GAME_NOT_FOUND";return result;}
    if(!verifyOnly&&BusyProcess(gameRoot,clientOnly)!=""){result.Code="GAME_RUNNING";return result;}
    Directory.CreateDirectory(logHome);result.Log=Path.Combine(logHome,"install-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".log");
    report(new InstallProgress{Percent=1,Phase="EXTRACT"});package=ExtractPackage();
    var native=RunBackend(package,gameRoot,clientOnly,language,verifyOnly,result.Log,report);native.Log=result.Log;result=native;
    if(result.Ok&&!verifyOnly)try{string state=Path.Combine(Path.GetDirectoryName(logHome),"installer-settings.json");File.WriteAllText(state,new JavaScriptSerializer().Serialize(new{gameRoot=gameRoot}),new UTF8Encoding(false));}catch{}
   }catch(InvalidDataException e){result.Code="PACKAGE_DAMAGED";result.Message=e.Message;}
   catch(UnauthorizedAccessException e){result.Code="ACCESS_DENIED";result.Message=e.Message;}
   catch(Exception e){result.Code="INSTALL_FAILED";result.Message=e.Message;}
   finally{if(package.Length>0)Cleanup(package);}
   return result;
  }
  internal static InstallResult RunBackend(string package,string root,bool clientOnly,string language,bool verifyOnly,string logPath,Action<InstallProgress> report){
   string progress=Path.Combine(package,"setup-progress.json"),answer=Path.Combine(package,"setup-result.json");
   string command="$ProgressPreference='SilentlyContinue';[Console]::OutputEncoding=[Text.Encoding]::UTF8;& "+PsLiteral(Path.Combine(package,"Install.ps1"))+" -GameRoot "+PsLiteral(root)+" -Language "+PsLiteral(language)+" -ProgressPath "+PsLiteral(progress)+" -ResultPath "+PsLiteral(answer)+(clientOnly?" -ClientOnly":"")+(verifyOnly?" -VerifyOnly":"");
   var start=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"WindowsPowerShell","v1.0","powershell.exe"),"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(command))){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=package,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
   int exitCode;object logGate=new object();
   using(var log=new StreamWriter(logPath,false,new UTF8Encoding(false)))using(var process=new Process{StartInfo=start}){
    DataReceivedEventHandler receive=delegate(object sender,DataReceivedEventArgs e){try{if(e.Data!=null)lock(logGate){log.WriteLine(e.Data);log.Flush();}}catch{}};
    process.OutputDataReceived+=receive;process.ErrorDataReceived+=receive;process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();
    int last=-1;string lastPhase="";
    while(!process.WaitForExit(200)){try{var data=Json(File.ReadAllText(progress));int percent=Convert.ToInt32(data["percent"]);string phase=Text(data,"phase");if(percent!=last||phase!=lastPhase){last=percent;lastPhase=phase;report(new InstallProgress{Percent=Math.Max(0,Math.Min(100,percent)),Phase=phase});}}catch{}}
    process.WaitForExit();exitCode=process.ExitCode;
   }
   if(!File.Exists(answer))return new InstallResult{Code="INSTALL_FAILED",Message="Installation engine did not return a result."};
   var value=Json(File.ReadAllText(answer,Encoding.UTF8));object rollback;
   return new InstallResult{Ok=exitCode==0&&Convert.ToBoolean(value["ok"]),Code=Text(value,"code"),Message=Text(value,"message"),Backup=Text(value,"backup"),RollbackSucceeded=!value.TryGetValue("rollbackSucceeded",out rollback)||Convert.ToBoolean(rollback)};
  }
 }
}
