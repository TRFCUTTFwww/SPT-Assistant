using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using EFT;
using EFT.CameraControl;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
namespace LocalSpt5Esp;

// Draw into the actual optic texture. The game's lens shader performs the
// texture mapping, lens mask and eye relief; no guessed screen-space lens rect.
internal sealed class ScopeBoxes {
 internal static ConfigEntry<bool> Enabled=null!;
 Camera? camera;CommandBuffer? commands;Material? material;Mesh? mesh;
 Camera.CameraCallback? renderCallback;IReadOnlyList<Player>? actors;Player? local;
 int lastRenderFrame=-10;bool renderReported;
 readonly List<Vector3> vertices=new();readonly List<Color> colors=new();readonly List<int> indices=new();
 Il2CppStructArray<Vector3>? vertexBuffer;Il2CppStructArray<Color>? colorBuffer;Il2CppStructArray<int>? indexBuffer;
 readonly Dictionary<IntPtr,(float Current,float Maximum,float Expires)> healthCache=new();
 float nextError;bool active;
 internal bool Active=>active&&camera!=null&&camera.isActiveAndEnabled;
 internal static void Initialize()=>Enabled=FeatureConfig.Bind("1. ESP","Optical scope boxes","瞄准镜内自适应方框",true,"使用光学镜自身摄像机绘制方框，跟随倍率并由原生镜片裁切；光学开镜时隐藏镜外人物方框及文字，保留镜内方框与血条。红点和机瞄保持普通显示。");
 internal void Tick(IReadOnlyList<Player> actors,Player? local){try{
  this.actors=actors;this.local=local;
  var optic=CameraManager.Instance?.OpticCameraManager;
  var target=Enabled.Value&&Plugin.Self.People.Value&&RaidClock.Active&&!DirectorStage.HudHidden&&local?.ProceduralWeaponAnimation?.IsAiming==true&&optic?.CurrentOpticSight!=null?optic.Camera:null;
  if(target==null||!target.isActiveAndEnabled){Detach();return;}
  if(camera==null||camera.Pointer!=target.Pointer){Detach();camera=target;}
  if(!Resources()){Detach();return;}
  if(renderCallback==null){renderCallback=DelegateSupport.ConvertDelegate<Camera.CameraCallback>(new Action<Camera>(BeforeRender));Camera.onPreRender+=renderCallback;}
  // Fallback still uses world vertices and the camera's render-time matrices.
  // Once callbacks arrive, avoid building twice during every rendered frame.
  if(lastRenderFrame<Time.frameCount-1)Build(target);
 }catch(Exception e){Failed(e);}}
 void BeforeRender(Camera target){
  if(camera==null||target==null||target.Pointer!=camera.Pointer)return;
  try{
   if(!Enabled.Value||!Plugin.Self.People.Value||!RaidClock.Active||DirectorStage.HudHidden||local?.ProceduralWeaponAnimation?.IsAiming!=true){commands?.Clear();active=false;return;}
   Build(target);lastRenderFrame=Time.frameCount;
   if(!renderReported){renderReported=true;Plugin.Self.Log.LogInfo("Optic ESP render synchronization active: native camera onPreRender, world vertices, render-time view/projection");}
  }catch(Exception e){Failed(e);}
 }
 void Build(Camera target){
  if(actors==null||local==null)return;
  commands!.Clear();vertices.Clear();colors.Clear();indices.Clear();
  // Match the texture's aspect ratio, including scopes that aren't square.
  float aspect=Math.Max(.01f,target.aspect),tx=1.5f/Math.Max(64,target.pixelWidth),ty=1.5f/Math.Max(64,target.pixelHeight);
  foreach(var actor in actors){
   if(vertices.Count>65000)break;
   if(actor==null||actor.HealthController?.IsAlive!=true)continue;
   var feet=actor.Position;if(Vector3.Distance(feet,local!.Position)>Plugin.Self.PeopleRange.Value)continue;
   var head=actor.PlayerBones?.Head?.Original?.position+Vector3.up*.15f??feet+Vector3.up*1.8f;
   var f=target.WorldToViewportPoint(feet);var h=target.WorldToViewportPoint(head);
   if(!ScopeBoxRules.Bounds(new(f.x,f.y,f.z),new(h.x,h.y,h.z),aspect,out var b))continue;
   depth=(f.z+h.z)*.5f;
   float height=b.Height,width=b.Width,x=b.X,y=b.Y;
   var color=BodyTint.Color(actor);
   Stripe(new(x,y,width,ty),color);Stripe(new(x,y+height-ty,width,ty),color);
   Stripe(new(x,y,tx,height),color);Stripe(new(x+width-tx,y,tx,height),color);
   if(Plugin.Self.ShowHealth.Value){
    if(!healthCache.TryGetValue(actor.Pointer,out var health)||Time.unscaledTime>=health.Expires){var value=actor.HealthController.GetBodyPartHealth(EBodyPart.Common,false);healthCache[actor.Pointer]=health=(value.Current,value.Maximum,Time.unscaledTime+.1f);}
    if(float.IsFinite(health.Current)&&float.IsFinite(health.Maximum)&&health.Maximum>0){
     float ratio=Math.Clamp(health.Current/health.Maximum,0,1),bar=Math.Clamp(width,.04f,.2f);
     Stripe(new((f.x+h.x-bar)*.5f,y+height+ty*3,bar,ty*3),new(0,0,0,.85f));
     Stripe(new((f.x+h.x-bar)*.5f,y+height+ty*3,bar*ratio,ty*2),ratio>.66f?Color.green:ratio>.33f?Color.yellow:Color.red);
    }
   }
  }
  if(vertices.Count>0){
   if(vertexBuffer==null||vertexBuffer.Length<vertices.Count){int capacity=Math.Max(1024,vertices.Count*2);vertexBuffer=new(capacity);colorBuffer=new(capacity);}
   if(indexBuffer==null||indexBuffer.Length<indices.Count)indexBuffer=new(Math.Max(1536,indices.Count*2));
   for(int i=0;i<vertices.Count;i++){vertexBuffer[i]=vertices[i];colorBuffer![i]=colors[i];}
   for(int i=0;i<indices.Count;i++)indexBuffer[i]=indices[i];
   mesh!.Clear();mesh.SetVertices(vertexBuffer,0,vertices.Count);mesh.SetColors(colorBuffer!,0,colors.Count);mesh.SetTriangles(indexBuffer,0,indices.Count,0,false,0);mesh.RecalculateBounds();
   // Use the same native render matrices and texture orientation as the actors.
   // Never freeze a LateUpdate camera matrix or force an orthographic overlay.
   commands.DrawMesh(mesh,Matrix4x4.identity,material!,0,0);
  }
  if(!active)Plugin.Self.Log.LogInfo($"Optic ESP active: camera={target.name}, texture={target.pixelWidth}x{target.pixelHeight}, fov={target.fieldOfView:F2}; native lens mapping, one batch");active=true;
 }
 void Failed(Exception e){Detach();if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+20;Plugin.Self.Log.LogWarning("Optic ESP: "+e);}}
 bool Resources(){
  if(material==null){var shader=Shader.Find("Hidden/Internal-Colored");if(shader==null||!shader.isSupported)return false;
   material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);material.SetInt("_Cull",(int)CullMode.Off);material.SetInt("_ZWrite",0);material.SetInt("_ZTest",(int)CompareFunction.Always);
  }
  if(mesh==null){mesh=new Mesh{hideFlags=HideFlags.HideAndDontSave};mesh.MarkDynamic();}
  if(commands==null){commands=new CommandBuffer{name="SPT Assistant optic ESP boxes"};camera!.AddCommandBuffer(CameraEvent.BeforeImageEffects,commands);}
  return true;
 }
 float depth;
 void Stripe(ScopeBoxRules.Stripe value,Color color){if(!ScopeBoxRules.Clip(value,out var r))return;int i=vertices.Count;
  vertices.Add(camera!.ViewportToWorldPoint(new(r.X,r.Y,depth)));vertices.Add(camera.ViewportToWorldPoint(new(r.X+r.Width,r.Y,depth)));vertices.Add(camera.ViewportToWorldPoint(new(r.X+r.Width,r.Y+r.Height,depth)));vertices.Add(camera.ViewportToWorldPoint(new(r.X,r.Y+r.Height,depth)));
  for(int j=0;j<4;j++)colors.Add(color);indices.Add(i);indices.Add(i+1);indices.Add(i+2);indices.Add(i);indices.Add(i+2);indices.Add(i+3);
 }
 void Detach(){active=false;lastRenderFrame=-10;healthCache.Clear();if(commands!=null){try{if(camera!=null)camera.RemoveCommandBuffer(CameraEvent.BeforeImageEffects,commands);}finally{commands.Dispose();commands=null;}}camera=null;}
 internal void Dispose(){if(renderCallback!=null){Camera.onPreRender-=renderCallback;renderCallback=null;}Detach();actors=null;local=null;if(mesh!=null)UnityEngine.Object.Destroy(mesh);if(material!=null)UnityEngine.Object.Destroy(material);mesh=null;material=null;vertexBuffer=null;colorBuffer=null;indexBuffer=null;}
}
