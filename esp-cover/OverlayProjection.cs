using System;
using BepInEx.Configuration;
using EFT.CameraControl;
using Il2CppInterop.Runtime;
using UnityEngine;
namespace LocalSpt5Esp;

internal static class OverlayProjection {
 static Camera.CameraCallback? callback;
 static ConfigEntry<bool> markersInViewOnly=null!;
 static IntPtr pointer;static Matrix4x4 view,projection;static Rect viewport;
 static int frame=-10,guiFrame=-10,outputWidth,outputHeight;static float nextReport,nextCaptureError;
 internal static Camera? MainCamera {
  get { var camera=CameraManager.Instance?.Camera;return camera!=null&&camera.isActiveAndEnabled?camera:Camera.main; }
 }
 internal static void Initialize(){
  markersInViewOnly=FeatureConfig.Bind("14. Exfiltration","Markers in view only","地点标记仅显示镜头内",false,
   "按当前镜头视野过滤撤离点、BTR 站点、追踪物品和传送标记。");
  callback=DelegateSupport.ConvertDelegate<Camera.CameraCallback>(new Action<Camera>(Capture));Camera.onPreRender+=callback;
 }
 static void Capture(Camera camera){
  try{
   var main=MainCamera;if(camera==null||main==null||camera.Pointer!=main.Pointer)return;
   view=camera.worldToCameraMatrix;var rendered=camera.projectionMatrix;var stable=camera.nonJitteredProjectionMatrix;
   Span<float> renderValues=stackalloc float[16],stableValues=stackalloc float[16];
   for(int i=0;i<16;i++){renderValues[i]=rendered[i];stableValues[i]=stable[i];}
   bool useStable=OverlayProjectionRules.CanUseStable(renderValues,stableValues,camera.orthographic);
   projection=useStable?stable:rendered;viewport=camera.rect;pointer=camera.Pointer;frame=Time.frameCount;
   if(Time.unscaledTime>=nextReport){nextReport=Time.unscaledTime+30;Plugin.Self.Log.LogInfo($"ESP stable projection: camera={camera.name}, nonJittered={useStable}, sampleOffset=({rendered.m02-projection.m02:F7},{rendered.m12-projection.m12:F7}), output={Screen.width}x{Screen.height}");}
  }catch(Exception e){pointer=IntPtr.Zero;if(Time.unscaledTime>=nextCaptureError){nextCaptureError=Time.unscaledTime+20;Plugin.Self.Log.LogWarning("ESP camera capture: "+e);}}
 }
 internal static void Shutdown(){if(callback!=null){Camera.onPreRender-=callback;callback=null;}pointer=IntPtr.Zero;frame=guiFrame=-10;}
 internal static void BeginGui(){guiFrame=Time.frameCount;outputWidth=Screen.width;outputHeight=Screen.height;}
 static bool HasCapturedView(Camera camera)=>pointer==camera.Pointer&&frame>=guiFrame-1&&guiFrame>=0;
 internal static bool ShouldDrawMarker(Camera camera,Vector3 screen)=>!markersInViewOnly.Value||InView(camera,screen);
 internal static bool InView(Camera camera,Vector3 screen){
  bool captured=HasCapturedView(camera);
  var rect=captured?viewport:camera.rect;
  return OverlayProjectionRules.InViewport(screen.x,screen.y,screen.z,rect.x,rect.y,rect.width,rect.height,
   captured?outputWidth:Screen.width,captured?outputHeight:Screen.height);
 }
 // IMGUI runs after camera rendering and image effects can reset camera state.
 // Reuse the scene's matrix, rather than projecting against a different view.
 internal static Vector3 ScreenPoint(Camera camera,Vector3 position){
  if(HasCapturedView(camera)){
   float x=view.m00*position.x+view.m01*position.y+view.m02*position.z+view.m03;
   float y=view.m10*position.x+view.m11*position.y+view.m12*position.z+view.m13;
   float z=view.m20*position.x+view.m21*position.y+view.m22*position.z+view.m23;
   float cx=projection.m00*x+projection.m01*y+projection.m02*z+projection.m03;
   float cy=projection.m10*x+projection.m11*y+projection.m12*z+projection.m13;
   float w=projection.m30*x+projection.m31*y+projection.m32*z+projection.m33;
   if(!float.IsFinite(w)||Math.Abs(w)<.000001f)return new(0,0,-1);
   var pixel=OverlayProjectionRules.Pixels(cx/w*.5f+.5f,cy/w*.5f+.5f,viewport.x,viewport.y,viewport.width,viewport.height,outputWidth,outputHeight);
   return new(pixel.X,pixel.Y,-z);
  }
  var point=camera.WorldToViewportPoint(position);var rect=camera.rect;
  var fallback=OverlayProjectionRules.Pixels(point.x,point.y,rect.x,rect.y,rect.width,rect.height,Screen.width,Screen.height);
  return new(fallback.X,fallback.Y,point.z);
 }
}
