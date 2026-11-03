using System;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.HealthSystem;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class HitDisplay {
 static ConfigEntry<bool> Enabled=null!;
 static ConfigEntry<float> Duration=null!;
 static ConfigEntry<int> Lines=null!,Font=null!;
 static readonly HitFeed feed=new();
 [ThreadStatic] static HitState? active;
 [ThreadStatic] static ShotState? shot;
 static IntPtr worldPointer;
 static float nextError;
 static bool ready;
 static GUIStyle? style;
 sealed class HitState {
  internal IntPtr Controller;
  internal string Victim="",Name="",Part="";
  internal float Before;
  internal bool Killed;
  internal HitState? Parent;
 }
 sealed class ShotState {
  internal string Victim="",Name="",Part="";
  internal bool Reported,Killed;
  internal ShotState? Parent;
 }
 internal static void Initialize(Plugin p){
  Enabled=CnConfig.Bind(p.Config,"11. Hit display","Enabled",true,"Show your confirmed health damage, hit body part and attributed kills. Armor durability damage is excluded.");
  Duration=CnConfig.Bind(p.Config,"11. Hit display","Duration",4f,new ConfigDescription("Seconds to retain a hit or kill message",new AcceptableValueRange<float>(1,15)));
  Lines=CnConfig.Bind(p.Config,"11. Hit display","Maximum lines",6,new ConfigDescription("Newest messages retained",new AcceptableValueRange<int>(1,12)));
  Font=CnConfig.Bind(p.Config,"11. Hit display","Font size",18,new ConfigDescription("Hit display text size",new AcceptableValueRange<int>(12,28)));
  try{
   var damage=typeof(ActiveHealthController).GetMethods().Single(m=>m.DeclaringType==typeof(ActiveHealthController)&&m.Name=="ApplyDamage");
   var death=typeof(Player).GetMethods().Single(m=>m.DeclaringType==typeof(Player)&&m.Name=="OnBeenKilledByAggressor");
   var impact=typeof(Player).GetMethods().Single(m=>m.DeclaringType==typeof(Player)&&m.Name=="ApplyShot");
   p.Patches.Patch(damage,prefix:new HarmonyMethod(typeof(HitDisplay),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(HitDisplay),nameof(AfterDamage)),finalizer:new HarmonyMethod(typeof(HitDisplay),nameof(FinishDamage)));
   p.Patches.Patch(death,postfix:new HarmonyMethod(typeof(HitDisplay),nameof(AfterDeath)));
   p.Patches.Patch(impact,prefix:new HarmonyMethod(typeof(HitDisplay),nameof(BeforeShot)),postfix:new HarmonyMethod(typeof(HitDisplay),nameof(AfterShot)),finalizer:new HarmonyMethod(typeof(HitDisplay),nameof(FinishShot)));
   ready=true;p.Log.LogInfo("Hit display ready: health damage + attributed death + armor-blocked impact hooks");
  }catch(Exception e){ready=false;p.Log.LogError("Hit display unavailable: "+e);}
 }
 static bool IsLocal(IPlayer? attacker){
  var world=Singleton<GameWorld>.Instance;var player=world!=null?world.MainPlayer:null;
  return player!=null&&player.IsYourPlayer&&attacker!=null&&attacker.ProfileId==player.ProfileId;
 }
 static string Part(EBodyPart part)=>part switch{
  EBodyPart.Head=>"头部",EBodyPart.Chest=>"胸部",EBodyPart.Stomach=>"腹部",
  EBodyPart.LeftArm=>"左臂",EBodyPart.RightArm=>"右臂",EBodyPart.LeftLeg=>"左腿",EBodyPart.RightLeg=>"右腿",_=>"全身"
 };
 static void BeforeDamage(ActiveHealthController __instance,EBodyPart __0,DamageInfo __2,out HitState? __state){
  __state=null;
  try{
   if(!ready||!Enabled.Value||__2==null||!IsLocal(__2.Player?.iPlayer))return;
   var victim=__instance.Player;if(victim==null||victim.IsYourPlayer||!__instance.IsAlive)return;
   for(var parent=active;parent!=null;parent=parent.Parent)if(parent.Controller==__instance.Pointer)return;
   __state=new HitState{Controller=__instance.Pointer,Victim=victim.ProfileId,Name=victim.Profile?.Nickname??"目标",Part=Part(__0),Before=__instance.GetBodyPartHealth(EBodyPart.Common,false).Current,Parent=active};
   active=__state;
  }catch(Exception e){Warn(e);}
 }
 static void AfterDamage(ActiveHealthController __instance,HitState? __state){
  if(__state==null)return;
  try{
   float damage=Math.Max(0,__state.Before-__instance.GetBodyPartHealth(EBodyPart.Common,false).Current);
   bool killed=__state.Killed||!__instance.IsAlive;
   feed.Prune(Time.unscaledTime,Duration.Value,Lines.Value);
   feed.Add(__state.Victim,__state.Name,__state.Part,damage,killed,Time.unscaledTime,Lines.Value);
   for(var context=shot;context!=null;context=context.Parent)if(context.Victim==__state.Victim){context.Reported=true;break;}
  }catch(Exception e){Warn(e);}
 }
 static void FinishDamage(HitState? __state){if(__state!=null&&ReferenceEquals(active,__state))active=__state.Parent;}
 static void BeforeShot(Player __instance,DamageInfo __0,EBodyPart __1,out ShotState? __state){
  __state=null;
  try{
   if(!ready||!Enabled.Value||__0==null||!IsLocal(__0.Player?.iPlayer)||__instance.IsYourPlayer||__instance.HealthController==null||!__instance.HealthController.IsAlive)return;
   __state=new ShotState{Victim=__instance.ProfileId,Name=__instance.Profile?.Nickname??"目标",Part=Part(__1),Parent=shot};shot=__state;
  }catch(Exception e){Warn(e);}
 }
 static void AfterShot(Player __instance,ShotState? __state){
  if(__state==null||__state.Reported)return;
  try{
   bool killed=__state.Killed||(__instance.HealthController!=null&&!__instance.HealthController.IsAlive);
   feed.Prune(Time.unscaledTime,Duration.Value,Lines.Value);
   feed.Add(__state.Victim,__state.Name,__state.Part,0,killed,Time.unscaledTime,Lines.Value);
  }catch(Exception e){Warn(e);}
 }
 static void FinishShot(ShotState? __state){if(__state!=null&&ReferenceEquals(shot,__state))shot=__state.Parent;}
 static void AfterDeath(Player __instance,IPlayer __0,EBodyPart __2){
  try{
   if(!ready||!Enabled.Value||__instance.IsYourPlayer||!IsLocal(__0))return;
   if(__instance.HealthController==null||__instance.HealthController.IsAlive)return;
   for(var state=active;state!=null;state=state.Parent)if(state.Victim==__instance.ProfileId){state.Killed=true;return;}
   for(var context=shot;context!=null;context=context.Parent)if(context.Victim==__instance.ProfileId&&!context.Reported){context.Killed=true;return;}
   feed.Kill(__instance.ProfileId,__instance.Profile?.Nickname??"目标",Part(__2),Time.unscaledTime,Duration.Value,Lines.Value);
  }catch(Exception e){Warn(e);}
 }
 internal static void Reset(){feed.Clear();worldPointer=IntPtr.Zero;active=null;shot=null;}
 internal static void Tick(GameWorld? world){
  var pointer=world!=null?world.Pointer:IntPtr.Zero;
  if(worldPointer!=pointer){feed.Clear();worldPointer=pointer;}
  if(!Enabled.Value)feed.Clear();
  feed.Prune(Time.unscaledTime,Duration.Value,Lines.Value);
 }
 internal static void Draw(){
  if(!ready||!Enabled.Value||feed.Entries.Count==0)return;
  if(style==null){style=new GUIStyle(GUI.skin.label);style.alignment=TextAnchor.MiddleLeft;style.richText=false;style.wordWrap=false;}
  style.fontSize=Font.Value;
  float row=Font.Value+12,width=Math.Min(660,Screen.width-24),x=Screen.width-width-12,y=Math.Max(70,Screen.height*0.26f);
  var oldColor=GUI.color;
  try{
   for(int i=feed.Entries.Count-1,visible=0;i>=0;i--,visible++){
    var hit=feed.Entries[i];float age=Time.unscaledTime-hit.Time;
    if(age>=Duration.Value)continue;
    float alpha=Math.Clamp((Duration.Value-age)/0.6f,0,1);
    var rect=new Rect(x,y+visible*row,width,row-3);
    if(rect.yMax>Screen.height-40)break;
    GUI.color=new Color(0,0,0,0.75f*alpha);GUI.DrawTexture(rect,Texture2D.whiteTexture);
    GUI.color=hit.Killed?new Color(1,0.3f,0.2f,alpha):new Color(1,0.9f,0.4f,alpha);
    string damage=hit.Damage.HasValue?$"{hit.Damage.Value:F1} 伤害":"伤害未记录";
    string name=hit.Name.Replace('\n',' ').Replace('\r',' ');
    if(name.Length>12)name=name[..12]+"…";
    string text=$"{(hit.Killed?"击杀":"命中")} · {name} · {hit.Part} · {damage}";
    GUI.Label(new Rect(rect.x+9,rect.y,rect.width-18,rect.height),text,style);
   }
  }finally{GUI.color=oldColor;}
 }
 static void Warn(Exception e){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+10;Plugin.Self.Error(e);}
}
