using System;
using System.IO;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class MineLibrary {
 static AssetBundle? bundle;static AssetBundleCreateRequest? loading;static AssetBundleRequest? prefab;static MineDirectional? model;
 internal const string PlacedName="Assistant placed claymore";
 const string VisualName="Assistant claymore visual";
 static IntPtr scannedWorld;
 static MeshFilter? visibleMesh;
 static Material? fallback;
 static MeshFilter ReadMesh(GameObject source){
  return source.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f=>
   f.sharedMesh!=null&&f.sharedMesh.vertexCount>0&&
   (string.Equals(f.name,"mine_mon50_LOD0",StringComparison.OrdinalIgnoreCase)||
    string.Equals(f.sharedMesh.name,"mine_mon50_LOD0",StringComparison.OrdinalIgnoreCase)))??
   throw new IOException("阔剑模型缺少可显示的 LOD0 网格。");
 }
 internal static void BindVisual(MineDirectional mine){
  var source=visibleMesh??throw new IOException("阔剑显示模型尚未就绪。");
  var root=mine.transform;
  var visual=root.Find(VisualName);
  if(visual==null){visual=new GameObject(VisualName).transform;visual.SetParent(root,false);}
  visual.gameObject.layer=0;
  var donorRoot=model!.transform;
  visual.localRotation=Quaternion.Inverse(donorRoot.rotation)*source.transform.rotation;
  visual.localPosition=donorRoot.InverseTransformPoint(source.transform.position);
  var scale=source.transform.lossyScale;var parentScale=donorRoot.lossyScale;
  visual.localScale=new Vector3(scale.x/parentScale.x,scale.y/parentScale.y,scale.z/parentScale.z);
  var filter=visual.GetComponent<MeshFilter>()??visual.gameObject.AddComponent<MeshFilter>();
  filter.sharedMesh=source.sharedMesh;
  var renderer=visual.GetComponent<MeshRenderer>()??visual.gameObject.AddComponent<MeshRenderer>();
  var original=source.GetComponent<MeshRenderer>();
  var materials=original?.sharedMaterials;
  if(materials!=null&&materials.Length>0&&materials.All(m=>m!=null&&m.shader!=null&&m.shader.isSupported&&m.shader.name!="Hidden/InternalErrorShader"))renderer.sharedMaterials=materials;
  else{
   var shader=Shader.Find("Standard")??Shader.Find("Unlit/Color")??throw new IOException("阔剑缺少可用材质。");
   fallback??=new Material(shader){name="Assistant claymore olive",color=new Color(.28f,.31f,.18f)};
   renderer.sharedMaterial=fallback;
  }
  renderer.forceRenderingOff=false;renderer.enabled=true;renderer.allowOcclusionWhenDynamic=false;
  visual.gameObject.SetActive(true);
  // Use the local mesh corners even while the object is inactive during placement.
  var bounds=filter.sharedMesh.bounds;float lowest=float.PositiveInfinity;
  for(int i=0;i<8;i++){
   var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
   lowest=Math.Min(lowest,visual.TransformPoint(corner).y);
  }
  visual.position+=Vector3.up*(root.position.y-lowest);
  foreach(var group in mine.GetComponentsInChildren<LODGroup>(true))group.enabled=false;
  foreach(var old in mine.GetComponentsInChildren<MeshRenderer>(true))if(old.Pointer!=renderer.Pointer)old.enabled=false;
  Plugin.Self.Log.LogInfo($"Claymore visual: mesh={filter.sharedMesh.name} vertices={filter.sharedMesh.vertexCount} shader={renderer.sharedMaterial?.shader?.name}");
 }
 static void RepairPlaced(){
  var world=Comfort.Common.Singleton<EFT.GameWorld>.Instance;
  if(!RaidClock.Active||world==null){scannedWorld=IntPtr.Zero;return;}
  if(scannedWorld==world.Pointer||world.MineManager?.Mines is not {} mines)return;
  var placed=mines.Where(m=>m!=null&&m.name==PlacedName&&!m._cleared&&!m._exploded).ToArray();
  if(placed.Length>0&&model==null){scannedWorld=world.Pointer;Request();return;}
  scannedWorld=world.Pointer;
  foreach(var mine in placed)try{BindVisual(mine);}catch(Exception error){Plugin.Self.Log.LogWarning(error.Message);}
 }
 internal static bool Busy=>loading!=null||prefab!=null;
 internal static MineDirectional? Model=>model;
 internal static string Status {get;private set;}="";
 internal static void Request(){if(Busy||model!=null)return;string path=Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","mine-library","claymore.bundle");if(!File.Exists(path))throw new ArgumentException("本机尚未构建阔剑模型库。");loading=AssetBundle.LoadFromFileAsync(path);Status="正在加载阔剑模型，请稍后再次放置。";}
 internal static void Tick(){try{
  if(loading!=null){if(!loading.isDone)return;bundle=loading.assetBundle;loading=null;if(bundle==null)throw new IOException("阔剑模型包加载失败。");prefab=bundle.LoadAssetAsync("claymore.prefab",Il2CppInterop.Runtime.Il2CppType.Of<GameObject>());}
  if(prefab!=null){if(!prefab.isDone)return;var result=prefab.asset?.TryCast<GameObject>();prefab=null;model=result?.GetComponent<MineDirectional>();if(model?._mineData==null||result!.GetComponentsInChildren<Collider>(true).Length==0)throw new IOException("阔剑模型缺少原生参数或触发器。");visibleMesh=ReadMesh(result);scannedWorld=IntPtr.Zero;Status="阔剑模型已就绪。";FeatureToasts.Show(Status);}
  RepairPlaced();
 }catch(Exception e){visibleMesh=null;loading=null;prefab=null;model=null;Status=e.GetBaseException().Message;if(bundle!=null)bundle.Unload(false);bundle=null;Plugin.Self.Log.LogWarning(Status);}}
 internal static void Reset(){if(Busy)throw new InvalidOperationException("阔剑模型仍在加载。");scannedWorld=IntPtr.Zero;visibleMesh=null;fallback=null;model=null;if(bundle!=null)bundle.Unload(false);bundle=null;}
}
