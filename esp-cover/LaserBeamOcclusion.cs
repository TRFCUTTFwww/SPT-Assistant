using System;
using System.Collections.Generic;
using EFT;
using EFT.CameraControl;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
namespace LocalSpt5Esp;

// Wall penetration is a visual layer with its own character-only depth buffer.
// Never clear or write the game's depth/stencil, replace actor materials or
// change damage rays to solve a rendering problem.
internal static class LaserBeamOcclusion {
 sealed record Surface(Renderer Renderer,int Parts);
 sealed class View {
  internal readonly Camera Camera;
  internal readonly CommandBuffer Commands;
  internal readonly Mesh[] Beams=new Mesh[4];
  internal View(Camera camera){
   Camera=camera;Commands=new CommandBuffer{name="SPT Assistant laser character occlusion"};
   for(int i=0;i<Beams.Length;i++)Beams[i]=new Mesh{hideFlags=HideFlags.HideAndDontSave};
   camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha,Commands);
  }
  internal void Dispose(){
   try{if(Camera!=null)Camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha,Commands);}finally{
    Commands.Clear();Commands.Dispose();foreach(var mesh in Beams)UnityEngine.Object.Destroy(mesh);
   }
  }
 }
 static readonly Dictionary<IntPtr,View> views=new();
 static readonly List<Surface> surfaces=new();
 static readonly HashSet<IntPtr> seen=new();
 static Camera.CameraCallback? callback;
 static Material? depth,beam,composite;
 static LineRenderer?[]? lines;static bool[]? visible;
 static GameWorld? world;static IntPtr worldPointer;
 static float nextScan,nextError;static bool reported;
 static readonly int texture=Shader.PropertyToID("_LocalSptLaserCharacterLayer");
 internal static bool Enable(GameWorld current,LineRenderer?[] sources,bool[] visibility){
  if(!Materials())return false;
  if(worldPointer!=current.Pointer){Disable();worldPointer=current.Pointer;}
  world=current;lines=sources;visible=visibility;
  if(callback==null){callback=DelegateSupport.ConvertDelegate<Camera.CameraCallback>(new Action<Camera>(BeforeRender));Camera.onPreRender+=callback;}
  var main=OverlayProjection.MainCamera;var optic=CameraManager.Instance?.OpticCameraManager?.Camera;
  var removed=new List<IntPtr>();foreach(var entry in views)if(entry.Key!=main?.Pointer&&entry.Key!=optic?.Pointer)removed.Add(entry.Key);
  foreach(var key in removed){views[key].Dispose();views.Remove(key);}
  return true;
 }
 static bool Materials(){
  if(depth!=null&&beam!=null&&composite!=null)return true;
  var solid=Shader.Find("Hidden/Internal-Colored");var rgba=Shader.Find("Sprites/Default");var alpha=Shader.Find("Unlit/Transparent");
  if(solid==null||!solid.isSupported||rgba==null||!rgba.isSupported||alpha==null||!alpha.isSupported){
   if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+15;Plugin.Self.Log.LogWarning("Laser character occlusion shaders unavailable; keeping ordinary scene depth instead of drawing over actors");}
   return false;
  }
  depth??=Make(solid,true);depth.color=Color.clear;
  // Internal-Colored has ColorMask RGB: on a transparent target its alpha stays
  // zero, so a subsequent alpha composite discards the entire beam. Sprites
  // writes RGBA, respects ordinary LEqual depth and accepts our vertex colors.
  beam??=new Material(rgba){hideFlags=HideFlags.HideAndDontSave};beam.color=Color.white;beam.mainTexture=Texture2D.whiteTexture;
  composite??=new Material(alpha){hideFlags=HideFlags.HideAndDontSave};
  return true;
 }
 static Material Make(Shader shader,bool writeDepth){
  var material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
  material.SetInt("_SrcBlend",(int)BlendMode.One);material.SetInt("_DstBlend",(int)BlendMode.Zero);
  material.SetInt("_Cull",(int)CullMode.Off);material.SetInt("_ZWrite",writeDepth?1:0);material.SetInt("_ZTest",(int)CompareFunction.LessEqual);
  return material;
 }
 static void Scan(){
  if(world==null||Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+.5f;surfaces.Clear();seen.Clear();
  void Add(Player? actor){
   if(actor==null||actor.Destroyed)return;
   foreach(var renderer in actor.GetComponentsInChildren<Renderer>(true)){
    if(renderer==null||!seen.Add(renderer.Pointer)||renderer.gameObject.name.StartsWith("SPT Assistant tint ",StringComparison.Ordinal))continue;
    var skin=renderer.TryCast<SkinnedMeshRenderer>();var mesh=skin!=null?skin.sharedMesh:renderer.TryCast<MeshRenderer>()!=null?renderer.GetComponent<MeshFilter>()?.sharedMesh:null;
    if(mesh!=null&&mesh.subMeshCount>0)surfaces.Add(new(renderer,mesh.subMeshCount));
   }
  }
  Add(world.MainPlayer);
  var registered=world.RegisteredPlayers;if(registered!=null)for(int i=0;i<registered.Count;i++)Add(registered[i]?.TryCast<Player>());
 }
 static void BeforeRender(Camera camera){
  if(world==null||lines==null||visible==null||!RaidClock.Active||camera==null)return;
  var main=OverlayProjection.MainCamera;var local=world.MainPlayer;
  var optic=local?.ProceduralWeaponAnimation?.IsAiming==true?CameraManager.Instance?.OpticCameraManager?.Camera:null;
  if(camera.Pointer!=main?.Pointer&&camera.Pointer!=optic?.Pointer)return;
  View? view=null;
  try{
   Scan();
   if(!views.TryGetValue(camera.Pointer,out view))views[camera.Pointer]=view=new View(camera);
   var commands=view.Commands;commands.Clear();
   // -1 follows the camera's actual render dimensions, including scaled main
   // and optic targets. Inherit its native view/projection matrices.
   commands.GetTemporaryRT(texture,-1,-1,24,FilterMode.Bilinear,RenderTextureFormat.ARGB32);
   var target=new RenderTargetIdentifier(texture);
   commands.SetRenderTarget(target);commands.ClearRenderTarget(true,true,Color.clear);
   foreach(var surface in surfaces){
    var renderer=surface.Renderer;
    if(renderer==null||!renderer.enabled||renderer.forceRenderingOff||!renderer.gameObject.activeInHierarchy||(camera.cullingMask&(1<<renderer.gameObject.layer))==0)continue;
    for(int part=0;part<surface.Parts;part++)commands.DrawRenderer(renderer,depth!,part,0);
   }
   for(int i=0;i<lines.Length;i++){
    var line=lines[i];if(line==null||!visible[i/2])continue;
    // Build against this exact camera so billboard width and eye-ray geometry
    // remain correct in both the main view and the native optic texture.
    BuildBeam(view.Beams[i],line,camera);
    commands.DrawMesh(view.Beams[i],Matrix4x4.identity,beam!,0,0);
   }
   commands.Blit(target,new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget),composite!,0);
   commands.ReleaseTemporaryRT(texture);
   if(!reported){reported=true;Plugin.Self.Log.LogInfo("Laser wall rendering ready: RGBA sprite beams, explicit colored geometry, native scene fallback and character-only depth; shader="+beam!.shader.name+", vertices="+view.Beams[0].vertexCount);}
  }catch(Exception error){
   view?.Commands.Clear();if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+15;Plugin.Self.Log.LogWarning("Laser character occlusion: "+error);}
  }
 }
 // BakeMesh on a disabled LineRenderer can yield empty or colorless geometry.
 // Own all four vertices and their colors rather than depending on its internal
 // renderer state. Native scene beams remain enabled as a visible fallback.
 static void BuildBeam(Mesh mesh,LineRenderer line,Camera camera){
  var start=line.GetPosition(0);var end=line.GetPosition(1);var along=end-start;
  var side=Vector3.Cross(along,(start+end)*.5f-camera.transform.position);
  if(side.sqrMagnitude<.0000001f)side=camera.transform.right;else side.Normalize();
  var a=side*(line.startWidth*.5f);var b=side*(line.endWidth*.5f);
  mesh.Clear();mesh.SetVertices(new Il2CppStructArray<Vector3>(new[]{start-a,start+a,end+b,end-b}));
  mesh.SetColors(new Il2CppStructArray<Color>(new[]{line.startColor,line.startColor,line.endColor,line.endColor}));
  mesh.SetTriangles(new Il2CppStructArray<int>(new[]{0,1,2,0,2,3}),0);mesh.RecalculateBounds();
 }
 internal static void Disable(){
  if(callback!=null){Camera.onPreRender-=callback;callback=null;}
  foreach(var view in views.Values)view.Dispose();views.Clear();surfaces.Clear();seen.Clear();world=null;lines=null;visible=null;nextScan=0;
 }
 internal static void Reset(){
  Disable();worldPointer=IntPtr.Zero;reported=false;
  foreach(var material in new[]{depth,beam,composite})if(material!=null)UnityEngine.Object.Destroy(material);
  depth=beam=composite=null;
 }
}
