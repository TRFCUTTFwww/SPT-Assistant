using System;
using System.IO;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class MineLibrary {
 static AssetBundle? bundle;static AssetBundleCreateRequest? loading;static AssetBundleRequest? prefab;static MineDirectional? model;
 internal static bool Busy=>loading!=null||prefab!=null;
 internal static MineDirectional? Model=>model;
 internal static string Status {get;private set;}="";
 internal static void Request(){if(Busy||model!=null)return;string path=Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","mine-library","claymore.bundle");if(!File.Exists(path))throw new ArgumentException("本机尚未构建阔剑模型库。");loading=AssetBundle.LoadFromFileAsync(path);Status="正在加载阔剑模型，请稍后再次放置。";}
 internal static void Tick(){try{
  if(loading!=null){if(!loading.isDone)return;bundle=loading.assetBundle;loading=null;if(bundle==null)throw new IOException("阔剑模型包加载失败。");prefab=bundle.LoadAssetAsync("claymore.prefab",Il2CppInterop.Runtime.Il2CppType.Of<GameObject>());}
  if(prefab!=null){if(!prefab.isDone)return;var result=prefab.asset?.TryCast<GameObject>();prefab=null;model=result?.GetComponent<MineDirectional>();if(model?._mineData==null||result!.GetComponentsInChildren<Collider>(true).Length==0)throw new IOException("阔剑模型缺少原生参数或触发器。");Status="阔剑模型已就绪。";FeatureToasts.Show(Status);}
 }catch(Exception e){loading=null;prefab=null;model=null;Status=e.GetBaseException().Message;if(bundle!=null)bundle.Unload(false);bundle=null;Plugin.Self.Log.LogWarning(Status);}}
 internal static void Reset(){if(Busy)throw new InvalidOperationException("阔剑模型仍在加载。");model=null;if(bundle!=null)bundle.Unload(false);bundle=null;}
}
