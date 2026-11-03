using System;
using System.Linq;
using BepInEx.Configuration;
using EFT;
using EFT.Ballistics;
using EFT.HealthSystem;
using HarmonyLib;
using UnityEngine;
using NVector=System.Numerics.Vector3;
namespace LocalSpt5Esp;
internal static class IncomingDamage {
 static ConfigEntry<bool> enabled=null!,details=null!;
 static ConfigEntry<float> duration=null!,radius=null!;
 static ConfigEntry<int> maximum=null!,font=null!;
 static readonly IncomingDamageFeed feed=new();
 static IntPtr worldPointer;static bool ready;static float nextError;
 [ThreadStatic] static State? shot;
 [ThreadStatic] static State? damage;
 sealed class State {
  internal Player Player=null!;internal float Before;internal IncomingDamageFeed.Entry Entry=null!;
  internal State? Parent;
 }
 static NVector N(Vector3 p)=>new(p.x,p.y,p.z);
 static Vector3 U(NVector p)=>new(p.X,p.Y,p.Z);
 internal static void Initialize(Plugin plugin){
  const string section="35. 受击方向";
  enabled=FeatureConfig.Bind(section,"Enabled","启用受击方向提示",true);
  details=FeatureConfig.Bind(section,"Show details","显示来源与实际损血",true);
  duration=FeatureConfig.Bind(section,"Duration","提示持续时间（秒）",4f,"",new AcceptableValueRange<float>(1,12));
  radius=FeatureConfig.Bind(section,"Radius","方向提示距准星（像素）",105f,"",new AcceptableValueRange<float>(45,300));
  maximum=FeatureConfig.Bind(section,"Maximum indicators","最多保留命中提示",4,"",new AcceptableValueRange<int>(1,8));
  font=FeatureConfig.Bind(section,"Font size","受击明细字号",14,"",new AcceptableValueRange<int>(10,24));
  try{
   var apply=typeof(ActiveHealthController).GetMethods().Single(m=>m.DeclaringType==typeof(ActiveHealthController)&&m.Name=="ApplyDamage");
   var impact=typeof(Player).GetMethods().Single(m=>m.DeclaringType==typeof(Player)&&m.Name=="ApplyShot");
   plugin.Patches.Patch(apply,prefix:new HarmonyMethod(typeof(IncomingDamage),nameof(BeforeDamage)){priority=Priority.First},postfix:new HarmonyMethod(typeof(IncomingDamage),nameof(AfterDamage)),finalizer:new HarmonyMethod(typeof(IncomingDamage),nameof(FinishDamage)));
   plugin.Patches.Patch(impact,prefix:new HarmonyMethod(typeof(IncomingDamage),nameof(BeforeShot)){priority=Priority.First},postfix:new HarmonyMethod(typeof(IncomingDamage),nameof(AfterShot)),finalizer:new HarmonyMethod(typeof(IncomingDamage),nameof(FinishShot)));
   ready=true;plugin.Log.LogInfo("Incoming damage ready: local impacts + actual health loss + camera-relative direction");
  }catch(Exception e){plugin.Log.LogError("Incoming damage hooks failed: "+e);}
 }
 static bool Combat(EDamageType type)=>type==EDamageType.Bullet||type==EDamageType.Explosion||type==EDamageType.Melee||type==EDamageType.Sniper;
 static string Part(EBodyPart part)=>part switch {EBodyPart.Head=>"头部",EBodyPart.Chest=>"胸部",EBodyPart.Stomach=>"腹部",EBodyPart.LeftArm=>"左臂",EBodyPart.RightArm=>"右臂",EBodyPart.LeftLeg=>"左腿",EBodyPart.RightLeg=>"右腿",_=>"全身"};
 static bool Valid(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
 static State? Capture(Player player,DamageInfo info,EBodyPart part){
  if(!ready||!enabled.Value||!RaidClock.Active||player==null||!player.IsYourPlayer||info==null||player.HealthController==null)return null;
  var entry=new IncomingDamageFeed.Entry{Key=info.DamageType.ToString(),Name=Combat(info.DamageType)?"未知来源":info.DamageType.ToString(),Part=Part(part),Time=Time.unscaledTime,GodBlocked=Plugin.Self.God.Value};
  var attacker=info.Player?.iPlayer;
  if(Combat(info.DamageType)){
   if(attacker!=null&&attacker.ProfileId!=player.ProfileId&&Valid(attacker.Position)){
    entry.Key=attacker.ProfileId;entry.Name=attacker.Profile?.Nickname??"攻击者";entry.Origin=N(attacker.Position);entry.Distance=Vector3.Distance(attacker.Position,player.Position);
   }else if(Valid(info.MasterOrigin)&&info.MasterOrigin.sqrMagnitude>.01f&&Vector3.Distance(info.MasterOrigin,player.Position)>1){entry.Origin=N(info.MasterOrigin);entry.Name="命中来源";}
   else if(Valid(info.Direction)&&info.Direction.sqrMagnitude>.001f)entry.Direction=N(-info.Direction.normalized);
  }
  return new State{Player=player,Before=player.HealthController.GetBodyPartHealth(EBodyPart.Common,false).Current,Entry=entry};
 }
 static void Complete(State? state){if(state==null)return;try{
  var health=state.Player.HealthController;if(health==null)return;
  state.Entry.Damage=Math.Max(0,state.Before-health.GetBodyPartHealth(EBodyPart.Common,false).Current);
  if(state.Entry.Damage<=.001f&&!state.Entry.Origin.HasValue&&!state.Entry.Direction.HasValue)return;
  feed.Add(state.Entry,maximum.Value);
 }catch(Exception e){Warn(e);}}
 static void BeforeShot(Player __instance,DamageInfo __0,EBodyPart __1,out State? __state){__state=null;try{if(shot!=null)return;__state=Capture(__instance,__0,__1);if(__state!=null){__state.Parent=shot;shot=__state;}}catch(Exception e){Warn(e);}}
 static void AfterShot(State? __state)=>Complete(__state);
 static void FinishShot(State? __state){if(__state!=null&&ReferenceEquals(shot,__state))shot=__state.Parent;}
 static void BeforeDamage(ActiveHealthController __instance,EBodyPart __0,DamageInfo __2,out State? __state){__state=null;try{
  if(shot!=null||damage!=null||__2==null||__2.OverDamageFrom.HasValue)return;
  __state=Capture(__instance.Player,__2,__0);if(__state!=null){__state.Parent=damage;damage=__state;}
 }catch(Exception e){Warn(e);}}
 static void AfterDamage(State? __state)=>Complete(__state);
 static void FinishDamage(State? __state){if(__state!=null&&ReferenceEquals(damage,__state))damage=__state.Parent;}
 internal static void Reset(){feed.Clear();shot=damage=null;worldPointer=IntPtr.Zero;}
 internal static void Tick(GameWorld? world){var pointer=world?.Pointer??IntPtr.Zero;if(pointer!=worldPointer){Reset();worldPointer=pointer;}if(!enabled.Value)feed.Clear();feed.Prune(Time.unscaledTime,duration.Value,maximum.Value);}
 internal static void Preview(){
  if(!RaidClock.Active)return;enabled.Value=true;var camera=Camera.main;if(camera==null)return;
  foreach(float angle in new[]{-65f,135f}){var direction=Quaternion.AngleAxis(angle,Vector3.up)*Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;
   feed.Add(new IncomingDamageFeed.Entry{Key="preview"+angle,Name="方向预览",Part="胸部",Damage=angle<0?24:0,Direction=N(direction),Preview=true,Time=Time.unscaledTime},maximum.Value);}
 }
 internal static void Draw(Camera camera,Player player){
  if(!ready||!enabled.Value||feed.Entries.Count==0)return;
  var center=new Vector2(Screen.width*.5f,Screen.height*.5f);
  foreach(var entry in feed.Entries){float age=Time.unscaledTime-entry.Time,alpha=Math.Clamp((duration.Value-age)/.7f,0,1);if(alpha<=0)continue;
   var direction=entry.Origin.HasValue?U(entry.Origin.Value)-player.Position:entry.Direction.HasValue?U(entry.Direction.Value):Vector3.zero;
   float angle=DamageDirection.Angle(N(direction),N(camera.transform.forward));if(!float.IsFinite(angle))continue;
   var color=entry.Preview?new Color(.35f,.8f,1,alpha):entry.Damage>.001f?new Color(1,.22f,.19f,alpha):new Color(1,.72f,.26f,alpha);
   Vector2 Point(float degrees,float r){float a=degrees*Mathf.Deg2Rad;return center+new Vector2(Mathf.Sin(a),-Mathf.Cos(a))*r;}
   for(int i=0;i<12;i++)RaidLoot.Line(Point(angle-14+i*28f/12,radius.Value),Point(angle-14+(i+1)*28f/12,radius.Value),color,4);
   RaidLoot.Line(Point(angle-5,radius.Value-12),Point(angle,radius.Value-22),color,3);RaidLoot.Line(Point(angle+5,radius.Value-12),Point(angle,radius.Value-22),color,3);
  }
  if(!details.Value)return;float row=font.Value+12;var rect=InterfaceMenu.Place("incoming",new Rect(12,Screen.height*.65f,460,feed.Entries.Count*row));int n=0;
  for(int i=feed.Entries.Count-1;i>=0;i--){var entry=feed.Entries[i];float alpha=Math.Clamp((duration.Value-(Time.unscaledTime-entry.Time))/.7f,0,1);var r=new Rect(rect.x,rect.y+n++*row,rect.width,row-3);InterfaceMenu.Panel(r,alpha);
   var name=entry.Name.Replace('\n',' ').Replace('\r',' ');if(name.Length>14)name=name[..14]+"…";
   string loss=entry.Damage>.001f?$"损血 {entry.Damage:F1}":entry.GodBlocked?"无敌开启 · 未损血":"未损血／护甲挡下";
   InterfaceMenu.Label(new Rect(r.x+10,r.y,r.width-20,r.height),(entry.Preview?"[预览] ":"受击 · ")+name+(entry.Distance.HasValue?$" [{entry.Distance:F0}米]":"")+" · "+entry.Part+" · "+loss,new Color(1,.62f,.47f,alpha),font.Value);
  }
 }
 static void Warn(Exception e){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+15;Plugin.Self.Error(e);}
}
