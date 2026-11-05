using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class GrenadeArc {
 static ConfigEntry<bool> enabled=null!,landing=null!;static ConfigEntry<float> horizon=null!,width=null!;
 static readonly List<Vector3> points=new(128);static readonly Il2CppStructArray<RaycastHit> hits=new(24);static bool ready,collided;static float nextRead,nextError,flight;static Vector3 endpoint,origin;static string kind="";
 [ThreadStatic] static bool probing,captured;[ThreadStatic] static IntPtr probingController;[ThreadStatic] static Vector3 sampledPosition,sampledImpulse;
 internal static void Initialize(Plugin plugin){const string section="54. 投掷物轨迹";enabled=FeatureConfig.Bind(section,"Grenade trajectory preview","投掷物抛物线",true,"手持投掷物时显示原生投掷参数对应的初始飞行轨迹。截止首次碰撞；不预测后续弹跳、滚动或动态障碍物的未来移动。");landing=FeatureConfig.Bind(section,"Grenade predicted impact","投掷物首次碰撞点与距离",true);horizon=FeatureConfig.Bind(section,"Grenade preview seconds","轨迹预览时长（秒）",6f,"",new AcceptableValueRange<float>(1,8));width=FeatureConfig.Bind(section,"Grenade preview line width","投掷轨迹线宽",2.2f,"",new AcceptableValueRange<float>(1,5));
  try{plugin.Patches.Patch(AccessTools.Method(typeof(Player.BaseGrenadeHandsController),"ThrowGrenadeInner"),prefix:new HarmonyMethod(typeof(GrenadeArc),nameof(BeforeInner)){priority=Priority.First});ready=true;plugin.Log.LogInfo("Grenade arc ready: scoped native parameter dry run, impulse/mass conversion and first-impact sphere casts");}catch(Exception error){plugin.Log.LogError("Grenade trajectory parameters unavailable: "+error);}
 }
 static bool BeforeInner(Player.BaseGrenadeHandsController __instance,Il2CppSystem.Nullable<Vector3> __0,float __2,Vector3 __3,float __4,bool __6){if(!probing||__instance.Pointer!=probingController)return true;sampledPosition=__0.HasValue?__0.Value:__instance.FindThrowPosition();sampledImpulse=__3*(__2*__4);if(__6)sampledImpulse+=Singleton<GameWorld>.Instance?.MainPlayer?.Velocity??Vector3.zero;captured=true;return false;}
 internal static void Tick(GameWorld? world){if(!ready||!enabled.Value||!RaidClock.Active||world?.MainPlayer is not {} local||local.HealthController?.IsAlive!=true||InterfaceMenu.Open){Reset();return;}if(Time.unscaledTime<nextRead)return;nextRead=Time.unscaledTime+.075f;points.Clear();try{
   var controller=local.HandsController?.TryCast<Player.BaseGrenadeHandsController>();if(controller?.Item==null||controller.GrenadePrefab?.GrenadeItself==null)return;bool low=controller.TryCast<Player.GrenadeHandsController>()?.WaitingForLowThrow==true;
   var state=UnityEngine.Random.state;captured=false;probingController=controller.Pointer;try{probing=true;controller.Throw(0,low);}finally{probing=false;probingController=IntPtr.Zero;UnityEngine.Random.state=state;}if(!captured)return;
   float mass=Player.BaseGrenadeHandsController.MASS;if(!float.IsFinite(mass)||mass<=0)return;var body=controller.GrenadePrefab.GrenadeItself.GetComponent<Rigidbody>()??controller.GrenadePrefab.GrenadeItself.GetComponentInParent<Rigidbody>();float drag=body?.drag??0;var velocity=sampledImpulse/mass;if(!Finite(sampledPosition)||!Finite(velocity))return;
   origin=sampledPosition;Vector3 position=origin;points.Add(position);collided=false;flight=0;kind=low?"低抛":"高抛";float step=Math.Max(.03f,Time.fixedDeltaTime),maxTime=horizon.Value;
   for(int i=0;i<256&&flight<maxTime;i++){float dt=Math.Min(step,maxTime-flight);var nextVelocity=(velocity+Physics.gravity*dt)/(1+Math.Max(0,drag)*dt);var next=position+nextVelocity*dt;var delta=next-position;float distance=delta.magnitude;if(distance>.0001f){int count=Physics.SphereCastNonAlloc(position,.045f,delta/distance,hits,distance,Physics.AllLayers,QueryTriggerInteraction.Ignore);float nearest=distance+1;Vector3 hitPoint=next;for(int n=0;n<count;n++){var hit=hits[n];if(hit.collider==null||CrosshairProbe.LocalCollider(world,local,hit.collider)||hit.collider.transform.IsChildOf(controller.GrenadePrefab.transform))continue;if(hit.distance<nearest){nearest=hit.distance;hitPoint=hit.point;}}if(nearest<=distance){flight+=dt*Math.Clamp(nearest/distance,0,1);position=hitPoint;points.Add(position);collided=true;break;}}position=next;velocity=nextVelocity;flight+=dt;points.Add(position);
   }endpoint=position;
  }catch(Exception error){points.Clear();if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+10;Plugin.Self.Log.LogWarning("Grenade arc: "+error.GetBaseException().Message);}}}
 static bool Finite(Vector3 value)=>float.IsFinite(value.x)&&float.IsFinite(value.y)&&float.IsFinite(value.z);
 internal static void Reset(){points.Clear();probing=false;captured=false;probingController=IntPtr.Zero;collided=false;}
 internal static void Draw(Camera? camera){if(camera==null||points.Count<2||Event.current.type!=EventType.Repaint)return;for(int i=1;i<points.Count;i++){var a=camera.WorldToScreenPoint(points[i-1]);var b=camera.WorldToScreenPoint(points[i]);if(a.z<=.03f||b.z<=.03f||Math.Abs(a.x)>Screen.width*4||Math.Abs(b.x)>Screen.width*4||Math.Abs(a.y)>Screen.height*4||Math.Abs(b.y)>Screen.height*4)continue;float t=i/(float)points.Count;var color=Color.Lerp(InterfaceMenu.Accent,new Color(1,.76f,.3f),t);color.a=.92f;RaidLoot.Line(new(a.x,Screen.height-a.y),new(b.x,Screen.height-b.y),color,width.Value);}
  if(!landing.Value)return;var end=camera.WorldToScreenPoint(endpoint);if(end.z<=0||end.x<0||end.x>Screen.width||end.y<0||end.y>Screen.height)return;var center=new Vector2(end.x,Screen.height-end.y);var tint=collided?new Color(1,.76f,.3f):InterfaceMenu.Accent;for(int i=0;i<24;i++){float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;RaidLoot.Line(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*8,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*8,tint,1.7f);}InterfaceMenu.Label(new(center.x+14,center.y-18,350,36),$"{kind} · {(collided?"首次碰撞":"预览末端")} · {Vector3.Distance(origin,endpoint):F1}米 · {flight:F1}秒",tint,13);
 }
}
