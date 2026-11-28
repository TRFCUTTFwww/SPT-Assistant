using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;

namespace LocalSpt5Esp;

internal static class GrenadeArc {
 static ConfigEntry<bool> enabled=null!,landing=null!;
 static ConfigEntry<float> horizon=null!,width=null!;
 static readonly List<Vector3> points=new(192);
 static readonly Il2CppStructArray<RaycastHit> hits=new(32);
 static GameObject? arcNode;
 static LineRenderer? arcLine;
 static Material? arcMaterial;
 static bool ready,collided;
 static float nextError,flight;
 static Vector3 endpoint,origin;
 static string kind="";
 [ThreadStatic] static bool probing,captured;
 [ThreadStatic] static IntPtr probingController;
 [ThreadStatic] static Vector3 sampledPosition,sampledDirection;
 [ThreadStatic] static float sampledForceScalar;
 [ThreadStatic] static bool sampledWithVelocity;

 internal static void Initialize(Plugin plugin){
  const string section="54. 投掷物轨迹";
  enabled=FeatureConfig.Bind(section,"Grenade trajectory preview","显示投掷物抛物线",true,"手持投掷物时显示至首次碰撞点的抛物线轨迹。");
  landing=FeatureConfig.Bind(section,"Grenade predicted impact","投掷物首次碰撞点与距离",true);
  horizon=FeatureConfig.Bind(section,"Grenade preview seconds","轨迹预览时长（秒）",6f,"",new AcceptableValueRange<float>(1,8));
  width=FeatureConfig.Bind(section,"Grenade preview line width","投掷轨迹线宽",2.2f,"",new AcceptableValueRange<float>(1,5));
  try{
   plugin.Patches.Patch(AccessTools.Method(typeof(Player.BaseGrenadeHandsController),"ThrowGrenadeInner"),
    prefix:new HarmonyMethod(typeof(GrenadeArc),nameof(BeforeInner)){priority=Priority.First});
   ready=true;
   plugin.Log.LogInfo("Grenade arc ready: LateUpdate 3D LineRenderer + stable camera-aligned aim trajectory");
  }catch(Exception error){
   plugin.Log.LogError("Grenade trajectory parameters unavailable: "+error);
  }
 }

 static bool BeforeInner(Player.BaseGrenadeHandsController __instance,Il2CppSystem.Nullable<Vector3> __0,float __2,Vector3 __3,float __4,bool __6){
  if(!probing||__instance.Pointer!=probingController)return true;
  sampledPosition=__0.HasValue?__0.Value:__instance.FindThrowPosition();
  sampledDirection=__3;
  sampledForceScalar=__2*__4;
  sampledWithVelocity=__6;
  captured=true;
  return false;
 }

 static bool EnsureLineRenderer(){
  if(arcLine!=null)return true;
  var shader=Shader.Find("Hidden/Internal-Colored");
  if(shader==null||!shader.isSupported)return false;
  arcMaterial??=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
  arcMaterial.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);
  arcMaterial.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
  arcMaterial.SetInt("_Cull",(int)CullMode.Off);
  arcMaterial.SetInt("_ZWrite",0);
  arcMaterial.SetInt("_ZTest",(int)CompareFunction.LessEqual);
  arcNode=new GameObject("SPT Assistant grenade trajectory"){hideFlags=HideFlags.HideAndDontSave};
  arcLine=arcNode.AddComponent<LineRenderer>();
  arcLine.enabled=false;
  arcLine.sharedMaterial=arcMaterial;
  arcLine.useWorldSpace=true;
  arcLine.numCapVertices=3;
  arcLine.numCornerVertices=2;
  arcLine.shadowCastingMode=ShadowCastingMode.Off;
  arcLine.receiveShadows=false;
  arcLine.lightProbeUsage=LightProbeUsage.Off;
  arcLine.reflectionProbeUsage=ReflectionProbeUsage.Off;
  arcLine.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
  return true;
 }

 static void HideLine(){
  if(arcLine!=null)arcLine.enabled=false;
 }

 internal static void Tick(GameWorld? world){
  if(!ready||!enabled.Value||!RaidClock.Active||DirectorStage.HudHidden||DirectorStage.FreeActive
   ||world?.MainPlayer is not {} local||local.HealthController?.IsAlive!=true||InterfaceMenu.Open){
   Reset();return;
  }
  points.Clear();
  try{
   var controller=local.HandsController?.TryCast<Player.BaseGrenadeHandsController>();
   var camera=OverlayProjection.MainCamera;
   if(controller?.Item==null||controller.GrenadePrefab==null||camera==null){HideLine();return;}
   var grenadeHands=controller.TryCast<Player.GrenadeHandsController>();
   bool low=grenadeHands?.WaitingForLowThrow==true;

   var rngState=UnityEngine.Random.state;
   captured=false;
   probingController=controller.Pointer;
   try{
    probing=true;
    controller.Throw(0,low);
   }finally{
    probing=false;
    probingController=IntPtr.Zero;
    UnityEngine.Random.state=rngState;
   }

   float mass=Player.BaseGrenadeHandsController.MASS;
   if(!float.IsFinite(mass)||mass<=0)mass=.6f;

   float forceMag=captured&&float.IsFinite(sampledForceScalar)&&sampledForceScalar>0
    ?sampledForceScalar
    :(EFTHardSettings.Instance!=null?EFTHardSettings.Instance.GrenadeForce:11f)*(low?.66f:1f);

   // During idle, draw, walking or holding the pin cocked behind the shoulder, the hand bone
   // (_dropTransform) has not rotated into its release-keyframe pose yet, and non-elite throws
   // inject per-call Random.insideUnitSphere scatter. Align preview direction with the active
   // camera look vector (matching EFT's release angle) so the arc stays rock-steady and accurate.
   var camTransform=camera.transform;
   Vector3 camForward=camTransform.forward;
   Vector3 aimDir=(camForward+camTransform.up*(low?.025f:.065f)).normalized;
   Vector3 startPos=captured&&Finite(sampledPosition)?sampledPosition:camTransform.position;
   Vector3 camOrigin=camTransform.position;
   if(Vector3.Dot(startPos-camOrigin,camForward)<.08f||(startPos-camOrigin).sqrMagnitude>1.6f){
    Vector3 desired=camOrigin+camTransform.right*.13f-camTransform.up*.06f+camForward*.22f;
    if(Player.BaseGrenadeHandsController.CheckHandsToBodyObstacles(local,desired,out _,out var safePoint)&&Finite(safePoint))
     startPos=safePoint;
    else startPos=camOrigin+camForward*.15f;
   }

   Vector3 impulse=aimDir*forceMag;
   if(!captured||sampledWithVelocity)impulse+=local.Velocity;
   Vector3 velocity=impulse/mass;
   if(!Finite(startPos)||!Finite(velocity)){HideLine();return;}

   float drag=0f;
   if(controller.GrenadePrefab.GrenadeItself!=null){
    var body=controller.GrenadePrefab.GrenadeItself.GetComponent<Rigidbody>()
     ??controller.GrenadePrefab.GrenadeItself.GetComponentInParent<Rigidbody>();
    if(body!=null&&float.IsFinite(body.drag)&&body.drag>0)drag=body.drag;
   }

   origin=startPos;
   Vector3 position=origin;
   points.Add(position);
   collided=false;
   flight=0;
   kind=low?"低抛":"高抛";
   int mask=EFTHardSettings.Instance!=null?(int)EFTHardSettings.Instance.GunshotMask:Physics.DefaultRaycastLayers;
   float step=.025f,maxTime=horizon.Value;
   for(int i=0;i<320&&flight<maxTime;i++){
    float dt=Math.Min(step,maxTime-flight);
    var nextVelocity=(velocity+Physics.gravity*dt)/(1f+drag*dt);
    var next=position+nextVelocity*dt;
    var delta=next-position;
    float distance=delta.magnitude;
    if(distance>.0001f){
     int count=Physics.SphereCastNonAlloc(position,.04f,delta/distance,hits,distance,mask,QueryTriggerInteraction.Ignore);
     float nearest=distance+1f;
     Vector3 hitPoint=next;
     for(int n=0;n<count;n++){
      var hit=hits[n];
      if(hit.collider==null||CrosshairProbe.LocalCollider(world,local,hit.collider)
       ||hit.collider.transform.IsChildOf(controller.GrenadePrefab.transform))continue;
      if(hit.distance<nearest){nearest=hit.distance;hitPoint=hit.point;}
     }
     if(nearest<=distance){
      flight+=dt*Math.Clamp(nearest/distance,0f,1f);
      position=hitPoint;
      points.Add(position);
      collided=true;
      break;
     }
    }
    position=next;
    velocity=nextVelocity;
    flight+=dt;
    points.Add(position);
   }
   endpoint=position;

   if(points.Count>=2&&EnsureLineRenderer()){
    float worldWidth=Math.Clamp(width.Value*.0065f,.005f,.035f);
    arcLine!.startWidth=worldWidth;
    arcLine.endWidth=worldWidth*1.25f;
    var c0=InterfaceMenu.Accent;c0.a=.92f;
    var c1=new Color(1f,.76f,.3f,.95f);
    arcLine.startColor=c0;
    arcLine.endColor=c1;
    arcLine.positionCount=points.Count;
    for(int i=0;i<points.Count;i++)arcLine.SetPosition(i,points[i]);
    arcLine.enabled=true;
   }else HideLine();
  }catch(Exception error){
   points.Clear();
   HideLine();
   if(Time.unscaledTime>=nextError){
    nextError=Time.unscaledTime+10;
    Plugin.Self.Log.LogWarning("Grenade arc: "+error.GetBaseException().Message);
   }
  }
 }

 static bool Finite(Vector3 value)=>float.IsFinite(value.x)&&float.IsFinite(value.y)&&float.IsFinite(value.z);

 internal static void Reset(){
  points.Clear();
  HideLine();
  probing=false;
  captured=false;
  probingController=IntPtr.Zero;
  collided=false;
 }

 internal static void Dispose(){
  Reset();
  if(arcNode!=null)UnityEngine.Object.Destroy(arcNode);
  if(arcMaterial!=null)UnityEngine.Object.Destroy(arcMaterial);
  arcNode=null;arcLine=null;arcMaterial=null;
 }

 internal static void Draw(Camera? camera){
  if(!enabled.Value||camera==null||points.Count<2||Event.current.type!=EventType.Repaint)return;
  if(arcLine==null||!arcLine.enabled){
   for(int i=1;i<points.Count;i++){
    var a=OverlayProjection.ScreenPoint(camera,points[i-1]);
    var b=OverlayProjection.ScreenPoint(camera,points[i]);
    if(a.z<=.05f||b.z<=.05f||Math.Abs(a.x)>Screen.width*3||Math.Abs(b.x)>Screen.width*3||Math.Abs(a.y)>Screen.height*3||Math.Abs(b.y)>Screen.height*3)continue;
    float t=i/(float)points.Count;
    var color=Color.Lerp(InterfaceMenu.Accent,new Color(1,.76f,.3f),t);color.a=.92f;
    RaidLoot.Line(new(a.x,Screen.height-a.y),new(b.x,Screen.height-b.y),color,width.Value);
   }
  }
  if(!landing.Value)return;
  var end=OverlayProjection.ScreenPoint(camera,endpoint);
  if(end.z<=0||end.x<0||end.x>Screen.width||end.y<0||end.y>Screen.height)return;
  var center=new Vector2(end.x,Screen.height-end.y);
  var tint=collided?new Color(1,.76f,.3f):InterfaceMenu.Accent;
  for(int i=0;i<24;i++){
   float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;
   RaidLoot.Line(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*8,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*8,tint,1.7f);
  }
  InterfaceMenu.Label(new(center.x+14,center.y-18,350,36),$"{kind} · {(collided?"首次碰撞":"预览末端")} · {Vector3.Distance(origin,endpoint):F1}米 · {flight:F1}秒",tint,13);
 }
}
