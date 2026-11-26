using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using AreaRenderer=Il2CppSystems.Effects.AreaLightRenderer;
using TubeRenderer=Il2CppSystems.Effects.TubeLightRenderer;
namespace LocalSpt5Esp;
// Keep light components enabled: toggling them for every optic render tears down
// native light/shadow resources. Only their intensity is scoped to a camera pass.
// Native lighting buffers already belong to a camera, so mask them until that
// camera leaves suppression instead of detaching/rebuilding them every frame.
internal static class NightVisionLighting {
 static Camera.CameraCallback? before,after;
 static readonly List<Light> lights=new();
 static readonly HashSet<IntPtr> lightIds=new();
 static readonly Dictionary<IntPtr,VolumetricLightRenderer> volumes=new();
 static readonly Dictionary<IntPtr,CommandBufferManager> managers=new();
 static bool cached,acceptSources;
 static float nextScan,nextError;
 static int frame=-1;
 static bool nightResetNeeded;
 sealed record Detached(Camera Camera,CameraEvent Event,CommandBuffer Buffer,CommandBuffer[] Before);
 static readonly List<Detached> detached=new();
 static readonly Dictionary<(IntPtr Camera,IntPtr Buffer),CameraEvent[]> events=new();
 static readonly CameraLightScope<Light,float> scope=new(Lights,l=>l!=null?l.intensity:0,(l,value)=>{if(l!=null)l.intensity=value;},0);
 internal static void Initialize(Plugin p){
  acceptSources=true;
  void After(Type type,string method,string patch)=>p.Patches.Patch(AccessTools.Method(type,method),postfix:new HarmonyMethod(typeof(NightVisionLighting),patch));
  After(typeof(VolumetricLightRenderer),"OnPreRender",nameof(VolumeRendered));
  After(typeof(VolumetricLight),"OnEnable",nameof(WeaponLightEnabled));
  After(typeof(MultiFlare.CameraRenderer),"ApplyRenderCommands",nameof(FlareRendered));
  After(typeof(LightManager),"ManualOnRenderObject",nameof(CustomRendered));
  After(typeof(AreaRenderer),"Render",nameof(AreaRendered));
  After(typeof(TubeRenderer),"Render",nameof(TubeRendered));
 }
 static void Add(Light? light){if(light!=null&&lightIds.Add(light.Pointer))lights.Add(light);}
 static void Scan(){
  if(cached&&Time.unscaledTime<nextScan)return;
  long stamp=RuntimePerformance.Start();
  try{
   lights.Clear();lightIds.Clear();
   foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))Add(light);
   cached=true;nextScan=Time.unscaledTime+10;
  }finally{RuntimeCosts.Mark(ref stamp,CostStage.NightVisionLightScan);}
 }
 static IEnumerable<Light> Lights(){Scan();foreach(var light in lights)if(light!=null&&light.isActiveAndEnabled)yield return light;}
 static void WeaponLightEnabled(VolumetricLight __instance){
  if(!acceptSources||!cached)return;
  try{Add(__instance.Light??__instance.GetComponent<Light>());}catch(Exception e){Warn(e);}
 }
 internal static void Frame(){if(frame!=Time.frameCount){scope.Reset();frame=Time.frameCount;}}
 internal static void Active(bool on){
  if(nightResetNeeded){nightResetNeeded=false;Stop();}
  if(!on){Stop();return;}
  if(before==null){
   before=DelegateSupport.ConvertDelegate<Camera.CameraCallback>(new Action<Camera>(Begin));
   after=DelegateSupport.ConvertDelegate<Camera.CameraCallback>(new Action<Camera>(End));
   Camera.onPreCull+=before;Camera.onPostRender+=after;
  }
  RestoreBuffers(true);
 }
 static bool Suppress(Camera? camera)=>before!=null&&camera!=null&&NightVisionAssist.SuppressLights(camera);
 static void Begin(Camera camera){
  long started=RuntimePerformance.Start(),stamp=started;
  try{
   Frame();bool suppress=Suppress(camera);scope.Begin(camera.Pointer.ToInt64(),suppress);
   if(!suppress)return;
   foreach(var volume in volumes.Values)if(volume!=null&&volume._camera?.Pointer==camera.Pointer)Volume(camera,volume);
   foreach(var manager in managers.Values)Managed(camera,manager);
   var flares=MultiFlare.FlareManager.Instance?.FlareRenderer?.TryCast<MultiFlare.FlareRenderer>();
   if(flares?._cameraRenderers!=null)foreach(var renderer in flares._cameraRenderers)if(renderer?.Camera?.Pointer==camera.Pointer)Detach(camera,renderer.CommandBuffer,MultiFlare.CameraRenderer.InjectionEvent);
  }catch(Exception e){Fail(e);}
  finally{RuntimeCosts.Mark(ref stamp,CostStage.NightVisionLighting);RuntimePerformance.RenderDone(started);}
 }
 static void End(Camera camera){
  long started=RuntimePerformance.Start(),stamp=started;
  try{scope.End(camera.Pointer.ToInt64());}catch(Exception e){Fail(e);}
  finally{RuntimeCosts.Mark(ref stamp,CostStage.NightVisionLighting);RuntimePerformance.RenderDone(started);}
 }
 static void VolumeRendered(VolumetricLightRenderer __instance){
  if(!acceptSources)return;
  volumes[__instance.Pointer]=__instance;
  if(before==null)return;
  try{var camera=Camera.current??__instance._camera;if(Suppress(camera))Volume(camera!,__instance,true);}catch(Exception e){Fail(e);}
 }
 static void FlareRendered(MultiFlare.CameraRenderer __instance){
  if(before==null)return;
  try{var camera=__instance.Camera;if(Suppress(camera))Detach(camera,__instance.CommandBuffer,MultiFlare.CameraRenderer.InjectionEvent,true);}catch(Exception e){Fail(e);}
 }
 static void CustomRendered(LightManager __instance,Camera __0){if(acceptSources)ManagerRendered(__0,__instance._cbManager);}
 static void AreaRendered(AreaRenderer __instance,Camera __0){if(acceptSources)ManagerRendered(__0,__instance._bufferManager);}
 static void TubeRendered(TubeRenderer __instance,Camera __0){if(acceptSources)ManagerRendered(__0,__instance._bufferManager);}
 static void ManagerRendered(Camera camera,CommandBufferManager? manager){
  try{
   if(manager==null)return;
   managers[manager.Pointer]=manager;
   if(Suppress(camera))Managed(camera,manager,true);
  }catch(Exception e){Fail(e);}
 }
 static void Managed(Camera camera,CommandBufferManager? manager,bool updated=false){if(manager?.Cameras!=null&&manager.Cameras.TryGetValue(camera,out var buffer))Detach(camera,buffer,manager.CameraEvent,updated);}
 static void Volume(Camera camera,VolumetricLightRenderer renderer,bool updated=false){Detach(camera,renderer._preLightPass,null,updated);Detach(camera,renderer._postRenderPass,null,updated);}
 static void Detach(Camera camera,CommandBuffer? buffer,CameraEvent? knownEvent=null,bool updated=false){
  if(buffer==null||buffer.m_Ptr==IntPtr.Zero)return;
  var existing=detached.Find(d=>d.Camera.Pointer==camera.Pointer&&d.Buffer.Pointer==buffer.Pointer);
  if(existing!=null){
   // Renderers can recreate/re-register native buffers while a mask is active.
   if(updated&&camera.GetCommandBuffers(existing.Event).Any(b=>b.Pointer==buffer.Pointer))camera.RemoveCommandBuffer(existing.Event,buffer);
   return;
  }
  var key=(camera.Pointer,buffer.Pointer);
  if(!events.TryGetValue(key,out var slots)){
   slots=(knownEvent is {} single?new[]{single}:Enum.GetValues<CameraEvent>()).Where(slot=>camera.GetCommandBuffers(slot).Any(b=>b.Pointer==buffer.Pointer)).ToArray();
   if(slots.Length==0)return;
   events[key]=slots;
  }
  foreach(var slot in slots){
   var prior=camera.GetCommandBuffers(slot).ToArray();
   if(!prior.Any(b=>b.Pointer==buffer.Pointer))continue;
   detached.Add(new(camera,slot,buffer,prior));camera.RemoveCommandBuffer(slot,buffer);
  }
 }
 static void RestoreBuffers(bool onlyInactive=false){
  for(int i=detached.Count-1;i>=0;i--){
   var d=detached[i];
   if(d.Camera!=null&&d.Buffer.m_Ptr!=IntPtr.Zero&&onlyInactive&&Suppress(d.Camera))continue;
   detached.RemoveAt(i);
   try{
    if(d.Camera==null||d.Buffer.m_Ptr==IntPtr.Zero)continue;
    var current=d.Camera.GetCommandBuffers(d.Event).ToArray();
    if(current.Any(b=>b.Pointer==d.Buffer.Pointer))continue;
    var order=CameraBufferOrder.Restore(d.Before.Select(b=>b.Pointer),new[]{d.Buffer.Pointer},current.Select(b=>b.Pointer));
    var buffers=current.Concat(d.Before).GroupBy(b=>b.Pointer).ToDictionary(g=>g.Key,g=>g.First());
    d.Camera.RemoveCommandBuffers(d.Event);foreach(var pointer in order)d.Camera.AddCommandBuffer(d.Event,buffers[pointer]);
   }catch(Exception e){Warn(e);nightResetNeeded=true;}
  }
 }
 static void Warn(Exception error){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+10;Plugin.Self.Log.LogWarning("Night vision lighting restoration: "+error.Message);}
 static void Fail(Exception error){try{Stop();}finally{Warn(error);}}
 static void Stop(){
  try{scope.Reset();}finally{
   RestoreBuffers();
   if(before!=null)Camera.onPreCull-=before;if(after!=null)Camera.onPostRender-=after;
   before=after=null;frame=-1;
  }
 }
 internal static void Reset(){try{Stop();}finally{lights.Clear();lightIds.Clear();volumes.Clear();managers.Clear();events.Clear();cached=false;nextScan=0;}}
 internal static void Shutdown(){acceptSources=false;Reset();}
}
