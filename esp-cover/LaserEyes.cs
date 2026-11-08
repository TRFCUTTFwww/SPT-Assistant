using System;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using UnityEngine;
using UnityEngine.Rendering;
namespace LocalSpt5Esp;

internal static class LaserEyes {
 static ConfigEntry<bool> enabled=null!,continuous=null!;
 static ConfigEntry<KeyCode> holdKey=null!;
 static ConfigEntry<float> range=null!;
 static GameObject? root;static Material? material;
 static readonly LineRenderer?[] beams=new LineRenderer?[4];
 static readonly Vector3[] ends=new Vector3[2];
 static readonly bool[] visible=new bool[2];
 static IntPtr world;static float nextTrace,nextError;static Vector3 lastDirection,lastPosition;static bool firing;
 internal static void Initialize(){
  const string section="61. 镭射眼";
  enabled=FeatureConfig.Bind(section,"Laser eyes","镭射眼（命中秒杀）",false,"双眼发出红色激光，命中 AI 后立即致死；墙体会挡住射线，保留 AI 无敌保护。无需持枪或消耗弹药。");
  continuous=FeatureConfig.Bind(section,"Laser eyes continuous","镭射眼持续发射",false,"开启后持续发射，无需按住快捷键；菜单、背包、自由镜头和切出游戏时暂时停射。");
  holdKey=FeatureConfig.Bind(section,"Laser eyes fire key","镭射眼按住键",KeyCode.F10,"开启功能后按住发射，松开停止。None 表示不发射；菜单、背包和自由镜头中不触发。");
  range=FeatureConfig.Bind(section,"Laser eyes reach","镭射眼最大距离（米）",300f,"",new AcceptableValueRange<float>(1,1000));
 }
 internal static void Tick(){
  try{
   var current=Singleton<GameWorld>.Instance;var player=current?.MainPlayer;
   bool held=holdKey.Value!=KeyCode.None&&Input.GetKey(holdKey.Value);
   // Ctrl+F10 belongs to exit control; holding plain F10 fires the default beam.
   if(holdKey.Value==KeyCode.F10&&(Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl)))held=false;
   if(!LaserEyeRules.CanFire(enabled.Value,continuous.Value||held,RaidClock.Active,player?.IsYourPlayer==true&&player.HealthController?.IsAlive==true,InterfaceMenu.InputBlocked,DirectorStage.FreeActive,Cursor.visible,Application.isFocused)){Hide();return;}
   if(current==null||player==null){Hide();return;}
   var camera=OverlayProjection.MainCamera;if(camera==null){Hide();return;}
   if(world!=current.Pointer){Hide();world=current.Pointer;}
   float limit=range.Value;if(!float.IsFinite(limit)||limit<=0){Hide();return;}limit=Math.Min(limit,1000);
   var forward=camera.transform.forward;var first=player.PointOfView==EPointOfView.FirstPerson;
   var rotation=Quaternion.Euler(0,player.Rotation.x,0);
   var right=first?camera.transform.right:rotation*Vector3.right;
   var head=player.PlayerBones?.Head?.Original;
   var center=first?camera.transform.position:(head!=null?head.position:player.CameraPosition?.position??player.Position+Vector3.up*1.6f)+Vector3.up*.035f+rotation*Vector3.forward*.075f;
   if(first)center-=camera.transform.up*.025f;
   bool trace=!firing||Time.unscaledTime>=nextTrace||Vector3.Angle(forward,lastDirection)>.15f||Vector3.Distance(center,lastPosition)>.01f;
   if(trace){
    nextTrace=Time.unscaledTime+.05f;lastDirection=forward;lastPosition=center;
    var aimRay=camera.ViewportPointToRay(new Vector3(.5f,.5f));
    var sight=CrosshairProbe.Read(current,player,aimRay,limit);
    if(sight.State==CrosshairState.Overflow){Hide();return;}
    var aim=sight.State==CrosshairState.Empty?aimRay.GetPoint(limit):sight.Point;
    // Resolve both contacts before damage changes the alive roster or corpse colliders.
    var left=Trace(current,player,center-right*.032f,aim,forward,limit,0);
    var rightHit=Trace(current,player,center+right*.032f,aim,forward,limit,1);
    if(left.Actor!=null)Kill(player,left);
    if(rightHit.Actor!=null&&rightHit.Actor.Pointer!=left.Actor?.Pointer)Kill(player,rightHit);
   }
   if(!Resources()){Hide();return;}
   firing=true;
   for(int eye=0;eye<2;eye++){
    var origin=center+right*(eye==0?-.032f:.032f);var delta=ends[eye]-origin;
    // Start visible first-person geometry just beyond the near plane, along the actual eye ray.
    var start=first&&delta.magnitude>camera.nearClipPlane+.03f?origin+delta.normalized*(camera.nearClipPlane+.03f):origin;
    for(int layer=0;layer<2;layer++){var beam=beams[eye*2+layer]!;beam.enabled=visible[eye];if(!visible[eye])continue;beam.SetPosition(0,start);beam.SetPosition(1,ends[eye]);}
   }
  }catch(Exception error){Hide();if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+15;Plugin.Self.Log.LogWarning("Laser eyes: "+error);}}
 }
 static CrosshairContact Trace(GameWorld current,Player player,Vector3 origin,Vector3 aim,Vector3 forward,float limit,int eye){
  if(!LaserEyeRules.Beam(N(origin),N(aim),N(forward),limit,out var direction,out float length)){visible[eye]=false;return default;}
  var ray=new Ray(origin,U(direction));var hit=CrosshairProbe.Read(current,player,ray,length);
  visible[eye]=hit.State!=CrosshairState.Overflow;
  ends[eye]=hit.State==CrosshairState.Empty?ray.GetPoint(length):hit.Point;
  return hit;
 }
 static void Kill(Player player,CrosshairContact hit){
  var target=hit.Actor;
  if(hit.State!=CrosshairState.Target||target==null||target.IsYourPlayer||target.Pointer==player.Pointer||!(target.IsAI||target.AIData?.BotOwner!=null)||target.ActiveHealthController is not {} health||!health.IsAlive||Plugin.DamageBlocked(health))return;
  float total=health.GetBodyPartHealth(EBodyPart.Common,false).Current;
  if(!float.IsFinite(total)||total<=0)return;
  float lethal=Math.Max(10000,Math.Min(total,1000000)*10);
  var info=new DamageInfo{DamageType=EDamageType.Bullet,Damage=lethal,Player=new PlayerBridge(player).Cast<IObserverToPlayerBridge>(),MasterOrigin=player.Position,HitPoint=hit.Point,Direction=(hit.Point-player.Position).normalized,SourceId="local-laser-eyes"};
  target.LastAggressor=player;
  health.ApplyDamage(EBodyPart.Head,lethal,info);
  // Native death creates the normal corpse. Do not delete actors or manually invoke death callbacks.
  if(health.IsAlive&&!Plugin.DamageBlocked(health))health.Kill(EDamageType.Bullet);
 }
 static System.Numerics.Vector3 N(Vector3 v)=>new(v.x,v.y,v.z);
 static Vector3 U(System.Numerics.Vector3 v)=>new(v.X,v.Y,v.Z);
 static bool Resources(){
  if(root!=null&&beams[3]!=null)return true;
  var shader=Shader.Find("Hidden/Internal-Colored");if(shader==null||!shader.isSupported)return false;
  material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);material.SetInt("_Cull",(int)CullMode.Off);material.SetInt("_ZWrite",0);material.SetInt("_ZTest",(int)CompareFunction.LessEqual);
  root=new GameObject("SPT Assistant laser eyes"){hideFlags=HideFlags.HideAndDontSave};
  for(int i=0;i<beams.Length;i++){
   var node=new GameObject(i%2==0?"Red beam":"Hot core"){hideFlags=HideFlags.HideAndDontSave};node.transform.SetParent(root.transform,false);
   var beam=node.AddComponent<LineRenderer>();beams[i]=beam;beam.enabled=false;beam.sharedMaterial=material;beam.useWorldSpace=true;beam.positionCount=2;
   beam.startWidth=beam.endWidth=i%2==0?.035f:.009f;beam.startColor=beam.endColor=i%2==0?new Color(1,.01f,.015f,.7f):new Color(1,.65f,.55f,1);
   beam.shadowCastingMode=ShadowCastingMode.Off;beam.receiveShadows=false;beam.lightProbeUsage=LightProbeUsage.Off;beam.reflectionProbeUsage=ReflectionProbeUsage.Off;beam.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
  }
  return true;
 }
 static void Hide(){firing=false;nextTrace=0;for(int i=0;i<beams.Length;i++)if(beams[i]!=null)beams[i]!.enabled=false;visible[0]=visible[1]=false;}
 internal static void Reset(){Hide();world=IntPtr.Zero;if(root!=null)UnityEngine.Object.Destroy(root);if(material!=null)UnityEngine.Object.Destroy(material);root=null;material=null;Array.Clear(beams);}
}
