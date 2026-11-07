using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
namespace LocalSpt5Esp.HotReload;

public sealed class RuntimeReloader:IHostServices,IDisposable {
 readonly Action refreshMenu;
 public BepInEx.Configuration.ConfigFile Config {get;}
 public ManualLogSource Log {get;}
 public string PluginDirectory {get;}
 public string? CurrentVersion=>module?.Version;
 IRuntimeModule? module;
 PayloadContext? context;
 byte[]? lastGood;
 string payload="",request="",status="",fallback="",loadedHash="",phase="",failure="";
 DateTime nextPoll,nextError,observedStamp,seenRequest;long observedLength;
 Task<Observation>? observing;
 Task? persistence;
 Prepared? pending,candidate;
 ReloadSequence? sequence;
 ReloadPause? pause;
 bool manual,disposed,autoSave,rollingBack;
 int generation,painted,stepCount;
 double peakMs;
 record Prepared(PayloadContext Context,IRuntimeModule Module,byte[] Bytes,string Hash);
 record Observation(DateTime Stamp,long Length,DateTime Request,Prepared? Prepared);
 public RuntimeReloader(BepInEx.Configuration.ConfigFile config,ManualLogSource log,string directory,Action? refresh=null){Config=config;Log=log;PluginDirectory=directory;refreshMenu=refresh??(()=>{});}
 public void Start(){
  string directory=Path.Combine(PluginDirectory,"runtime");Directory.CreateDirectory(directory);
  payload=Path.Combine(directory,"Local.Spt5.ESP.Core.module");request=Path.Combine(directory,"reload.request");status=Path.Combine(directory,"hotreload-status.json");fallback=Path.Combine(directory,"last-good.module");autoSave=Config.SaveOnConfigSet;
 }
 public void RequestReload(){manual=true;nextPoll=DateTime.MinValue;}
 public void Update(){if(disposed)return;try{Poll();}catch(Exception e){Failed(e);}if(pause!=null){pause.Tick();return;}try{module?.Update();}catch(Exception e){Report(e);}}
 public void LateUpdate(){if(pause!=null)return;try{module?.LateUpdate();}catch(Exception e){Report(e);}}
 public void Draw(){
  if(pause==null){try{module?.OnGUI();}catch(Exception e){Report(e);}return;}
  int oldDepth=GUI.depth;GUI.depth=-10000;var oldColor=GUI.color;
  try{
   GUI.color=new Color(0,0,0,.8f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;
   var rect=new Rect((Screen.width-580)/2f,(Screen.height-220)/2f,580,220);GUI.Box(rect,"");
   GUI.Label(new Rect(rect.x+24,rect.y+20,532,32),failure.Length==0?"正在热更新 · 游戏已暂停":"热更新失败 · 游戏已暂停");
   GUI.Label(new Rect(rect.x+24,rect.y+64,532,52),phase);
   GUI.Label(new Rect(rect.x+24,rect.y+120,532,32),$"已执行 {stepCount} 个步骤 · 完成后自动恢复");
   if(failure.Length>0&&sequence==null&&persistence==null&&GUI.Button(new Rect(rect.x+180,rect.y+165,220,32),"关闭提示并恢复游戏"))EndPause();
   if(Event.current.type==EventType.Repaint)painted++;
   if(Event.current.type is EventType.KeyDown or EventType.KeyUp or EventType.MouseDown or EventType.MouseUp or EventType.ScrollWheel)Event.current.Use();
  }finally{GUI.color=oldColor;GUI.depth=oldDepth;}
 }
 void Poll(){
  if(pause!=null){
   if(painted<2)return;
   if(persistence!=null){if(!persistence.IsCompleted)return;var done=persistence;persistence=null;done.GetAwaiter().GetResult();if(sequence==null){EndPause();return;}}
   if(sequence!=null){if(sequence.Advance()){stepCount++;peakMs=Math.Max(peakMs,sequence.PeakMs);return;}sequence.Dispose();sequence=null;Finish();}
   return;
  }
  if(pending!=null){var ready=module?.GetType().GetMethod("PrepareForReload");if(ready!=null&&ready.Invoke(module,null) is false)return;var selected=pending;pending=null;Begin(selected);return;}
  if(observing!=null){if(!observing.IsCompleted)return;var task=observing;observing=null;var result=task.GetAwaiter().GetResult();observedStamp=result.Stamp;observedLength=result.Length;seenRequest=result.Request;if(result.Prepared!=null)pending=result.Prepared;}
  if(pending!=null||DateTime.UtcNow<nextPoll)return;nextPoll=DateTime.UtcNow.AddMilliseconds(500);
  bool force=manual;manual=false;var stamp=observedStamp;var length=observedLength;var requestStamp=seenRequest;var hash=loadedHash;
  observing=Task.Run(()=>Observe(force,stamp,length,requestStamp,hash));
 }
 Observation Observe(bool force,DateTime stamp,long length,DateTime requestStamp,string hash){
  var file=new FileInfo(payload);var trigger=File.Exists(request)?File.GetLastWriteTimeUtc(request):requestStamp;
  if(!file.Exists)return new(stamp,length,trigger,null);
  if(!force&&file.LastWriteTimeUtc==stamp&&file.Length==length&&trigger==requestStamp)return new(stamp,length,trigger,null);
  var before=file.LastWriteTimeUtc;long size=file.Length;byte[] bytes=ReadPayload(payload);file.Refresh();if(file.LastWriteTimeUtc!=before||file.Length!=size)throw new IOException("功能文件仍在写入，稍后重试。");
  string incoming=Convert.ToHexString(SHA256.HashData(bytes));if(!force&&trigger==requestStamp&&incoming==hash)return new(before,size,trigger,null);
  return new(before,size,trigger,Prepare(bytes,incoming));
 }
 static byte[] ReadPayload(string path){using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);if(stream.Length<1024||stream.Length>64*1024*1024)throw new InvalidDataException("功能文件不完整");using var buffer=new MemoryStream();stream.CopyTo(buffer);return buffer.ToArray();}
 Prepared Prepare(byte[] bytes,string? hash=null){var loaded=new PayloadContext();try{using var stream=new MemoryStream(bytes,false);var assembly=loaded.LoadFromStream(stream);var type=assembly.GetType("LocalSpt5Esp.RuntimeModule",true)!;var result=(IRuntimeModule)Activator.CreateInstance(type)!;if(result.ApiVersion!=1)throw new InvalidOperationException("热更新接口版本不兼容");type.GetMethod("PrepareInBackground")?.Invoke(result,null);return new(loaded,result,bytes,hash??Convert.ToHexString(SHA256.HashData(bytes)));}catch{loaded.Release();throw;}}
 static IEnumerable<Action> Steps(IRuntimeModule target,string name,Action fallback,IHostServices? host=null)=>target.GetType().GetMethod(name)?.Invoke(target,host==null?null:new object[]{host}) as IEnumerable<Action>??new[]{fallback};
 void Begin(Prepared selected){candidate=selected;autoSave=Config.SaveOnConfigSet;pause=new ReloadPause();painted=stepCount=0;peakMs=0;failure="";rollingBack=false;phase="正在保存设置并准备切换…";persistence=Task.Run(()=>Config.Save());sequence=new ReloadSequence(Swap(selected));}
 IEnumerable<Action> Swap(Prepared selected){
  var previous=module;if(previous!=null)foreach(var step in Steps(previous,"UnloadSteps",previous.Unload))yield return ()=>{phase="正在释放旧功能…";step();};
  yield return ()=>{module=null;context?.Release();context=selected.Context;Config.SaveOnConfigSet=false;Config.Clear();Config.Reload();phase="正在启用新功能…";};
  yield return ()=>module=selected.Module;
  foreach(var step in Steps(selected.Module,"InitializeSteps",()=>selected.Module.Initialize(this),this))yield return step;
  yield return ()=>{loadedHash=selected.Hash;lastGood=selected.Bytes;generation++;candidate=null;phase="正在保存更新结果…";};
 }
 void Finish(){Config.SaveOnConfigSet=autoSave;try{refreshMenu();}catch(Exception error){Report(error);}byte[]? bytes=lastGood;byte[] state=StatusBytes(failure.Length==0?null:failure);persistence=Task.Run(()=>{Config.Save();if(bytes!=null)AtomicWrite(fallback,bytes);AtomicWrite(status,state);});Log.LogInfo($"热更新切换完成: version={module?.Version}, steps={stepCount}, peakStep={peakMs:F1}ms, rollback={rollingBack}");}
 void Failed(Exception error){
  Report(error);nextPoll=DateTime.UtcNow.AddSeconds(3);
  if(pause==null){var state=StatusBytes(error.ToString());_ = Task.Run(()=>AtomicWrite(status,state));return;}
  sequence?.Dispose();sequence=null;persistence=null;failure=error.Message;phase="更新失败，正在恢复上一版本…";
  if(rollingBack){phase="自动恢复失败，请重启游戏。";Config.SaveOnConfigSet=autoSave;return;}
  rollingBack=true;sequence=new ReloadSequence(Rollback());
 }
 IEnumerable<Action> Rollback(){
  if(module!=null)foreach(var step in Steps(module,"UnloadSteps",module.Unload))yield return ()=>{try{step();}catch(Exception error){Report(error);}};
  Task<Prepared>? restore=null;
  yield return ()=>{module=null;context?.Release();context=null;candidate?.Context.Release();candidate=null;var bytes=lastGood;restore=Task.Run(()=>Prepare(bytes??ReadPayload(fallback)));};
  while(!restore!.IsCompleted)yield return ()=>{};
  var selected=restore.GetAwaiter().GetResult();yield return ()=>{Config.Clear();Config.Reload();module=selected.Module;context=selected.Context;};
  foreach(var step in Steps(selected.Module,"InitializeSteps",()=>selected.Module.Initialize(this),this))yield return step;
  yield return ()=>{loadedHash=selected.Hash;lastGood=selected.Bytes;phase="已恢复上一版本，正在保存结果…";};
 }
 void EndPause(){var previous=pause;pause=null;previous?.Dispose();}
 byte[] StatusBytes(string? error)=>JsonSerializer.SerializeToUtf8Bytes(new{apiVersion=1,hostVersion="1.5.0",coreVersion=module?.Version,generation,sha256=loadedHash,ready=module!=null,error,updatedUtc=DateTime.UtcNow,steps=stepCount,peakStepMs=peakMs});
 static void AtomicWrite(string path,byte[] bytes){string temporary=path+".tmp";File.WriteAllBytes(temporary,bytes);File.Move(temporary,path,true);}
 void Report(Exception error){if(DateTime.UtcNow<nextError)return;nextError=DateTime.UtcNow.AddSeconds(10);Log.LogWarning(error);}
 public void Dispose(){disposed=true;try{sequence?.Dispose();module?.Unload();}finally{EndPause();module=null;context?.Release();}}
 sealed class PayloadContext:AssemblyLoadContext {
  // Harmony's native trampolines require non-collectible metadata.
  internal PayloadContext():base("SPT5-"+Guid.NewGuid().ToString("N"),false){}
  internal void Release(){}
  protected override Assembly? Load(AssemblyName name){var shared=Default.Assemblies.FirstOrDefault(a=>a.GetName().Name==name.Name);if(shared!=null)return shared;try{return Default.LoadFromAssemblyName(name);}catch(FileNotFoundException){}foreach(var path in new[]{Path.Combine(BepInEx.Paths.BepInExRootPath,"core",name.Name+".dll"),Path.Combine(BepInEx.Paths.BepInExRootPath,"interop",name.Name+".dll")})if(File.Exists(path))return Default.LoadFromAssemblyPath(path);return null;}
 }
}