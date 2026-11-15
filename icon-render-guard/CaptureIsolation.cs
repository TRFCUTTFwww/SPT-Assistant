using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine;
using ItemCreator=IconCreatorBase<EFT.InventoryLogic.Item,ItemIcon>;
namespace IconRenderGuard;
public sealed partial class IconRenderModule {
 int correctedSizes,isolatedCaptures,hiddenRenderers,captureErrors;double maxIsolationScanMs;
 CaptureScope? captureScope;
 sealed class CaptureScope {
  internal readonly List<Renderer> Hidden=[];
  internal GameObject? Model;
  internal Camera? Camera;internal bool Active,Enabled,Keyword;
  internal RenderTexture? Target,RenderTarget;
  internal readonly List<(Light Light,bool Active,bool Enabled)> Lights=[];
 }
 void InitializeCaptureIsolation(Harmony harmony){
  harmony.Patch(AccessTools.Method(typeof(ItemIconCreator),nameof(ItemIconCreator.GetItemIcon)),prefix:new HarmonyMethod(typeof(IconRenderModule),nameof(ValidateSize)){priority=Priority.First});
  harmony.Patch(AccessTools.Method(typeof(ItemCreator),nameof(ItemCreator.CaptureSpriteOfModel)),prefix:new HarmonyMethod(typeof(IconRenderModule),nameof(BeforeCapture)){priority=Priority.Last},finalizer:new HarmonyMethod(typeof(IconRenderModule),nameof(AfterCapture)));
  harmony.Patch(AccessTools.Method(typeof(IconCamera),nameof(IconCamera.Render)),prefix:new HarmonyMethod(typeof(IconRenderModule),nameof(IsolatePreview)){priority=Priority.Last});
 }
 static void ValidateSize(Item __0,ref IntVec2 __1){
  var a=active;if(a?.context?.IsActive!=true||__0==null||__1.X>0&&__1.Y>0)return;
  var native=ItemViewFactory.GetCellPixelSize(__0.CalculateCellSize());
  var size=IconRenderRules.Size(__1.X,__1.Y,native.X,native.Y);__1=new(size.Width,size.Height);a.correctedSizes++;a.statusDirty=true;
 }
 static void BeforeCapture(ItemCreator __instance,GameObject __0,ref IntVec2 __1,out CaptureScope? __state){
  __state=null;var a=active;
  if(a?.context?.IsActive!=true||a.current?.Creator.Pointer!=__instance.Pointer||__0==null)return;
  ValidateSize(a.current.Item,ref __1);
  var iconCamera=__instance._renderCamera;var camera=iconCamera?.CameraObject;
  if(camera==null)return;
  var scope=new CaptureScope{Model=__0,Camera=camera,Active=camera.gameObject.activeSelf,Enabled=camera.enabled,Target=camera.targetTexture,RenderTarget=RenderTexture.active,Keyword=Shader.IsKeywordEnabled("ICON_RENDERING_ON")};__state=scope;a.captureScope=scope;
  if(iconCamera!._cameraLights!=null)foreach(var light in iconCamera._cameraLights)if(light!=null)scope.Lights.Add((light,light.gameObject.activeSelf,light.enabled));
 }
 static void IsolatePreview(IconCamera __instance){
  var a=active;var scope=a?.captureScope;var camera=__instance.CameraObject;
  if(a?.context?.IsActive!=true||scope?.Model==null||camera==null||scope.Camera?.Pointer!=camera.Pointer)return;
  var watch=System.Diagnostics.Stopwatch.StartNew();
  // SetupScene parents the requested model to the inactive icon camera AFTER
  // BeforeCapture. When Render enables that camera, stale sibling previews also
  // become visible. Inspect this small hierarchy including inactive objects;
  // an active-scene search misses precisely those siblings (and DontSave ones).
  foreach(var renderer in camera.GetComponentsInChildren<Renderer>(true)){
   if(renderer==null)continue;
   bool inMask=(camera.cullingMask&(1<<renderer.gameObject.layer))!=0;
   if(!inMask)continue;
   if(!IconRenderRules.Hide(renderer.enabled,renderer.forceRenderingOff,renderer.transform.IsChildOf(scope.Model.transform),inMask))continue;
   scope.Hidden.Add(renderer);renderer.forceRenderingOff=true;
  }
  a.isolatedCaptures++;a.hiddenRenderers+=scope.Hidden.Count;a.maxIsolationScanMs=Math.Max(a.maxIsolationScanMs,watch.Elapsed.TotalMilliseconds);a.statusDirty=true;
 }
 static Exception? AfterCapture(Exception? __exception,CaptureScope? __state){
  if(__state==null)return __exception;
  if(active?.captureScope==__state)active.captureScope=null;
  foreach(var renderer in __state.Hidden)if(renderer!=null)renderer.forceRenderingOff=false;
  if(__exception!=null){
   var a=active;if(a!=null){a.captureErrors++;a.statusDirty=true;}
   // The native method does not guarantee camera/global cleanup on every error.
   // Restore only the state belonging to this capture; never edit pooled models.
   var camera=__state.Camera;
   if(camera!=null){camera.targetTexture=__state.Target;camera.enabled=__state.Enabled;camera.gameObject.SetActive(__state.Active);}
   RenderTexture.active=__state.RenderTarget;
   if(__state.Keyword)Shader.EnableKeyword("ICON_RENDERING_ON");else Shader.DisableKeyword("ICON_RENDERING_ON");
   foreach(var saved in __state.Lights)if(saved.Light!=null){saved.Light.enabled=saved.Enabled;saved.Light.gameObject.SetActive(saved.Active);}
  }
  return __exception;
 }
}
