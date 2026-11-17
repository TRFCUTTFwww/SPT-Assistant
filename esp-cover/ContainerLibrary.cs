using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using UnityEngine;
using EFT.Interactive;
namespace LocalSpt5Esp;
// Assets are generated locally from the user's installation. No map scenes run.
internal static class ContainerLibrary {
 static string DirectoryPath=>Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","container-library");
 static readonly Dictionary<string,GameObject> loaded=new(StringComparer.Ordinal);
 static readonly Dictionary<string,AssetBundle> bundles=new(StringComparer.Ordinal);
 static readonly Dictionary<string,string> errors=new(StringComparer.Ordinal);
 static AssetBundleCreateRequest? loading;static AssetBundleRequest? prefab;static AssetBundle? bundle;static string template="";static bool probe;
 internal static bool Busy=>loading!=null||prefab!=null;
 static readonly HashSet<string> available=new(StringComparer.Ordinal);static bool indexed;static float nextProbe;
 internal static bool Available(string id){
  if(!indexed){indexed=true;try{using var catalog=JsonDocument.Parse(File.ReadAllText(Path.Combine(DirectoryPath,"catalog.json")));foreach(var entry in catalog.RootElement.EnumerateObject())if(entry.Name.Length==24&&entry.Value.TryGetProperty("version",out var version)&&version.GetInt32()==2&&File.Exists(Path.Combine(DirectoryPath,entry.Name+".bundle")))available.Add(entry.Name);}catch(Exception e){Plugin.Self.Log.LogWarning("Container library index unavailable: "+e.GetBaseException().Message);}}
  return available.Contains(id);
 }
 internal static string Status(string id)=>errors.TryGetValue(id,out var error)?error:Busy&&id==template?"正在异步加载跨地图模型…":loaded.ContainsKey(id)?"跨地图模型已加载。":Available(id)?"本机模型库可加载；无需进入来源地图。":"模型库尚未收录此容器。";
 internal static GameObject? Get(string id)=>loaded.GetValueOrDefault(id);
 internal static void Request(string id){if(Busy||loaded.ContainsKey(id))return;if(!Available(id))throw new ArgumentException("本机模型库中没有此容器，请先构建容器模型库。");template=id;errors.Remove(id);loading=AssetBundle.LoadFromFileAsync(Path.Combine(DirectoryPath,id+".bundle"));}
 internal static void Tick(){
  try{
   string request=Path.Combine(DirectoryPath,"probe.request");
   if(!Busy&&Time.unscaledTime>=nextProbe&&ProbeRequested(request)){string id=File.ReadAllText(request).Trim();File.Delete(request);probe=true;Request(id);if(loaded.ContainsKey(id))Report(id,loaded[id]);}
   if(loading!=null){if(!loading.isDone)return;bundle=loading.assetBundle;loading=null;if(bundle==null)throw new IOException("跨地图模型包加载失败。");prefab=bundle.LoadAssetAsync(template+".prefab",Il2CppInterop.Runtime.Il2CppType.Of<GameObject>());}
   if(prefab!=null){if(!prefab.isDone)return;var go=prefab.asset?.TryCast<GameObject>();prefab=null;if(go==null||go.GetComponentsInChildren<LootableContainer>(true).Length==0||!go.GetComponentsInChildren<MeshFilter>(true).Any(m=>m.sharedMesh!=null))throw new IOException("跨地图模型缺少容器交互或网格。");loaded[template]=go;bundles[template]=bundle!;bundle=null;if(probe)Report(template,go);Plugin.Self.Log.LogInfo("Container library loaded: "+template);}
  }catch(Exception e){errors[template]="模型加载失败："+e.GetBaseException().Message;if(bundle!=null)bundle.Unload(true);bundle=null;loading=null;prefab=null;if(probe)Report(template,null,errors[template]);Plugin.Self.Log.LogWarning(errors[template]);}
 }
 static bool ProbeRequested(string path){nextProbe=Time.unscaledTime+1;return File.Exists(path);}
 static void Report(string id,GameObject? go,string? error=null){File.WriteAllText(Path.Combine(DirectoryPath,"probe-result.json"),JsonSerializer.Serialize(new{template=id,ok=go!=null,error,containers=go?.GetComponentsInChildren<LootableContainer>(true).Length??0,meshes=go?.GetComponentsInChildren<MeshRenderer>(true).Length??0,colliders=go?.GetComponentsInChildren<Collider>(true).Length??0,utc=DateTime.UtcNow}));probe=false;}
 internal static void Reset(){if(Busy)throw new InvalidOperationException("容器模型仍在加载。");foreach(var value in bundles.Values)if(value!=null)value.Unload(false);bundles.Clear();loaded.Clear();errors.Clear();available.Clear();indexed=false;nextProbe=0;bundle=null;probe=false;template="";}
}
