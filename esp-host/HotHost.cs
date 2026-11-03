using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
namespace LocalSpt5Esp.HotReload;

public sealed class RuntimeReloader:IHostServices,IDisposable {
 readonly Action refreshMenu;
 public BepInEx.Configuration.ConfigFile Config { get; }
 public ManualLogSource Log { get; }
 public string? CurrentVersion=>module?.Version;
 IRuntimeModule? module;
 PayloadContext? context;
 byte[]? lastGood;
 string payload="",request="",status="",fallback="",loadedHash="";
 DateTime observedStamp,lastSeenRequest,nextPoll,nextLoad,nextError;
 long observedLength;
 bool pending,manual;
 int generation;
 public string PluginDirectory { get; private set; }="";
 public RuntimeReloader(BepInEx.Configuration.ConfigFile config,ManualLogSource log,string directory,Action? refresh=null){
  Config=config;Log=log;PluginDirectory=directory;refreshMenu=refresh??(()=>{});
 }
 public void Start(){
  string directory=Path.Combine(PluginDirectory,"runtime");Directory.CreateDirectory(directory);
  payload=Path.Combine(directory,"Local.Spt5.ESP.Core.module");request=Path.Combine(directory,"reload.request");status=Path.Combine(directory,"hotreload-status.json");fallback=Path.Combine(directory,"last-good.module");

  if(File.Exists(request))lastSeenRequest=File.GetLastWriteTimeUtc(request);
  TryReload(true);
 }
 public void RequestReload(){pending=manual=true;nextLoad=DateTime.UtcNow;}
 public void Update(){
  Poll();
  try{module?.Update();}catch(Exception e){Report(e);}
 }
 public void LateUpdate(){try{module?.LateUpdate();}catch(Exception e){Report(e);}}
 public void Draw(){try{module?.OnGUI();}catch(Exception e){Report(e);}}
 void Poll(){
  var now=DateTime.UtcNow;
  if(now>=nextPoll){
   nextPoll=now.AddMilliseconds(500);
   try{
    var file=new FileInfo(payload);
    if(file.Exists&&(file.LastWriteTimeUtc!=observedStamp||file.Length!=observedLength)){
     observedStamp=file.LastWriteTimeUtc;observedLength=file.Length;pending=true;if(!manual)nextLoad=now.AddMilliseconds(750);
    }
    if(File.Exists(request)){var stamp=File.GetLastWriteTimeUtc(request);if(stamp!=lastSeenRequest){lastSeenRequest=stamp;RequestReload();}}
   }catch(Exception e){Report(e);}
  }
  if(pending&&now>=nextLoad){bool force=manual;pending=manual=false;TryReload(force);}
 }
 static byte[] ReadPayload(string path){
  using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
  if(stream.Length<1024||stream.Length>64*1024*1024)throw new InvalidDataException("功能文件不完整");
  using var buffer=new MemoryStream();stream.CopyTo(buffer);return buffer.ToArray();
 }
 void TryReload(bool force){
  PayloadContext? candidateContext=null;IRuntimeModule? candidate=null;bool swapped=false;
  bool autoSave=Config.SaveOnConfigSet;
  try{
   byte[] bytes=ReadPayload(payload);string hash=Convert.ToHexString(SHA256.HashData(bytes));
   var file=new FileInfo(payload);observedStamp=file.LastWriteTimeUtc;observedLength=file.Length;
   if(!force&&module!=null&&hash==loadedHash)return;
   (candidateContext,candidate)=Prepare(bytes);
   // Validate and resolve the new assembly before touching the running module.
   if(module!=null)Config.Save();
   swapped=true;StopModule();
   Config.SaveOnConfigSet=false;ReloadConfig();
   candidate.Initialize(this);module=candidate;context=candidateContext;candidate=null;candidateContext=null;
   loadedHash=hash;generation++;lastGood=bytes;
   Config.SaveOnConfigSet=autoSave;Config.Save();
   AtomicWrite(fallback,bytes);RefreshMenu();WriteStatus(null);Log.LogInfo("功能已更新："+module.Version);
  }catch(Exception error){
   try{candidate?.Unload();}catch{}
   candidateContext?.Release();
   if(swapped||module==null){
    try{
     var bytes=lastGood??(File.Exists(fallback)?ReadPayload(fallback):null);
     if(bytes!=null){ReloadConfig();var previous=Prepare(bytes);previous.Module.Initialize(this);context=previous.Context;module=previous.Module;loadedHash=Convert.ToHexString(SHA256.HashData(bytes));RefreshMenu();}
    }catch(Exception rollback){Log.LogError("回退失败："+rollback);}
   }
   Config.SaveOnConfigSet=autoSave;WriteStatus(error.ToString());Report(error);
  }finally{Config.SaveOnConfigSet=autoSave;}
 }
 (PayloadContext Context,IRuntimeModule Module) Prepare(byte[] bytes){
  var loaded=new PayloadContext(PluginDirectory);
  try{
   using var stream=new MemoryStream(bytes,false);var assembly=loaded.LoadFromStream(stream);
   var type=assembly.GetType("LocalSpt5Esp.RuntimeModule",true)!;
   var result=(IRuntimeModule)Activator.CreateInstance(type)!;
   if(result.ApiVersion!=1)throw new InvalidOperationException("热更新接口版本不兼容");
   return(loaded,result);
  }catch{loaded.Release();throw;}
 }
 void ReloadConfig(){Config.Clear();if(File.Exists(Config.ConfigFilePath))Config.Reload();}
 void StopModule(){
  var previous=module;var previousContext=context;module=null;context=null;
  try{previous?.Unload();}finally{previousContext?.Release();}
 }
 static void AtomicWrite(string path,byte[] bytes){string temporary=path+".tmp";File.WriteAllBytes(temporary,bytes);File.Move(temporary,path,true);}
 void WriteStatus(string? error){
  try{AtomicWrite(status,JsonSerializer.SerializeToUtf8Bytes(new{apiVersion=1,hostVersion="1.4.11",coreVersion=module?.Version,generation,sha256=loadedHash,ready=module!=null,error,updatedUtc=DateTime.UtcNow}));}catch{}
 }
 void RefreshMenu(){try{refreshMenu();}catch(Exception e){Report(e);}}
 void Report(Exception error){if(DateTime.UtcNow<nextError)return;nextError=DateTime.UtcNow.AddSeconds(10);Log.LogWarning(error);}
 public void Dispose()=>StopModule();
 sealed class PayloadContext:AssemblyLoadContext {
  readonly string directory;
  // IL2CPP/Harmony generates non-collectible native trampolines. Keep code metadata
  // resident, but explicitly release hooks, scene references and data on every swap.
  internal PayloadContext(string directory):base("SPT5-"+Guid.NewGuid().ToString("N"),false){this.directory=directory;}
  internal void Release(){}
  protected override Assembly? Load(AssemblyName name){
   var shared=Default.Assemblies.FirstOrDefault(a=>a.GetName().Name==name.Name);if(shared!=null)return shared;
   try{return Default.LoadFromAssemblyName(name);}catch(FileNotFoundException){}
   foreach(var path in new[]{Path.Combine(BepInEx.Paths.BepInExRootPath,"core",name.Name+".dll"),Path.Combine(BepInEx.Paths.BepInExRootPath,"interop",name.Name+".dll")})if(File.Exists(path))return Default.LoadFromAssemblyPath(path);
   return null;
  }
 }
}
