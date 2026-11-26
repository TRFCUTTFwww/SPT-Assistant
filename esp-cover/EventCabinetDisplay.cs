using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class EventCabinetDisplay {
 sealed record Cabinet(Animator Animator,HashSet<string> Parameters);
 sealed record Marker(Animator Animator,Vector3 Position,string Text,Color Color,float Distance);
 static ConfigEntry<bool> enabled=null!;
 static readonly List<Cabinet> cabinets=new();
 static readonly List<Marker> markers=new();
 static IntPtr world;static bool wasEnabled;
 static float nextRead,nextScan,nextError,scanUntil;
 internal static void Initialize()=>enabled=FeatureConfig.Bind("6. Loot details","Halloween cabinet hints","万圣节柜子：钱包／手雷提示",true,"读取已触发的钱包、手雷或断手结果；开启前尚未随机，显示结果未定。");
 static void Clear(){cabinets.Clear();markers.Clear();world=IntPtr.Zero;nextRead=nextScan=0;scanUntil=Time.unscaledTime+20;}
 static string Clip(Animator animator){if(animator.IsInTransition(0)){foreach(var item in animator.GetNextAnimatorClipInfo(0))if(item.clip!=null&&item.weight>0)return item.clip.name;}foreach(var item in animator.GetCurrentAnimatorClipInfo(0))if(item.clip!=null&&item.weight>0)return item.clip.name;return "";}
 static void Warn(Exception error){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+20;Plugin.Self.Log.LogWarning("Halloween cabinet hints: "+error.Message);}
 static void Scan(){
  nextScan=Time.unscaledTime+2;
  foreach(var animator in UnityEngine.Object.FindObjectsOfType<Animator>(true)){
   if(animator==null||animator.name!="INTERACTIVE_Table_ShapeP_Randomize_Zombie_Arm")continue;
   bool known=false;foreach(var cabinet in cabinets)if(cabinet.Animator!=null&&cabinet.Animator.Pointer==animator.Pointer){known=true;break;}if(known)continue;
   try{var parameters=new HashSet<string>(StringComparer.Ordinal);foreach(var parameter in animator.parameters)if(parameter.type==AnimatorControllerParameterType.Bool)parameters.Add(parameter.name);cabinets.Add(new(animator,parameters));}catch(Exception error){Warn(error);}
  }
 }
 internal static void Tick(GameWorld? current,Player? local){
  if(current==null||local==null||!RaidClock.Active){if(world!=IntPtr.Zero)Clear();return;}
  if(world!=current.Pointer){Clear();world=current.Pointer;}
  if(!enabled.Value){markers.Clear();wasEnabled=false;return;}
  if(!wasEnabled){wasEnabled=true;scanUntil=Time.unscaledTime+20;nextScan=0;}
  if(Time.unscaledTime<nextRead)return;nextRead=Time.unscaledTime+.1f;
  // Retry initial scene setup, without indefinitely scanning every map.
  if(Time.unscaledTime<=scanUntil&&Time.unscaledTime>=nextScan&&cabinets.Count<6)try{Scan();}catch(Exception error){Warn(error);}
  markers.Clear();
  foreach(var cabinet in cabinets)try{
   var animator=cabinet.Animator;if(animator==null||!animator.gameObject.activeInHierarchy)continue;
   var position=animator.transform.position+Vector3.up*.7f;float distance=Vector3.Distance(position,local.Position);if(distance>Plugin.Self.LootRange.Value)continue;
   // Verified against the copied controller graph: 01 Wallet, 02 Fuck, 03 Grenade.
   bool Flag(string name)=>cabinet.Parameters.Contains(name)&&animator.GetBool(name);
   bool wallet=Flag("IsOpen_01"),hand=Flag("IsOpen_02"),grenade=Flag("IsOpen_03");
   var outcome=EventCabinetRules.Resolve(wallet,hand,grenade,wallet||hand||grenade?"":Clip(animator));
   string text="活动柜子 · 未开启，结果未定";Color color=new(1,.8f,.3f);
   if(outcome==CabinetOutcome.Grenade){text="手雷陷阱 · 远离";color=new(1,.12f,.08f);}
   else if(outcome==CabinetOutcome.Wallet){text="钱包 · 已触发";color=new(.3f,1,.45f);}
   else if(outcome==CabinetOutcome.Hand){text="断手";color=new(.8f,.8f,.8f);}
   markers.Add(new(animator,position,text,color,distance));
  }catch(Exception error){Warn(error);}
 }
 internal static void Draw(Camera camera){if(!enabled.Value||!RaidClock.Active||Event.current.type!=EventType.Repaint)return;foreach(var marker in markers){if(marker.Animator==null)continue;var point=OverlayProjection.ScreenPoint(camera,marker.Position);if(point.z<=.05f||point.x<0||point.x>Screen.width||point.y<0||point.y>Screen.height)continue;InterfaceMenu.Label(new Rect(point.x-140,Screen.height-point.y-28,300,28),marker.Text+" · "+marker.Distance.ToString("F0")+" 米",marker.Color,14);}}
}