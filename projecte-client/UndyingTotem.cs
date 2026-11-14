using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;
namespace ProjectE.Client;
public sealed partial class ProjectModule {
 static readonly EBodyPart[] bodyParts=[EBodyPart.Head,EBodyPart.Chest,EBodyPart.Stomach,EBodyPart.LeftArm,EBodyPart.RightArm,EBodyPart.LeftLeg,EBodyPart.RightLeg];
 readonly HashSet<string> consumedTotems=[];
 bool savingLife,emptyHands;float totemStart=-100,regenUntil,fireUntil,nextRegen;IntPtr savedPlayer;
 void InitializeTotem(Harmony harmony){
  harmony.Patch(AccessTools.Method(typeof(ActiveHealthController),nameof(ActiveHealthController.Kill),[typeof(EDamageType)]),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(TotemBeforeKill)){priority=Priority.Last});
  harmony.Patch(AccessTools.Method(typeof(ActiveHealthController),nameof(ActiveHealthController.ApplyDamage),[typeof(EBodyPart),typeof(float),typeof(DamageInfo)]),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(TotemFire)){priority=Priority.Last});
 }
 static bool TotemBeforeKill(ActiveHealthController __instance,EDamageType __0){
  var a=active;var p=RaidPlayer();if(a?.context?.IsActive!=true||!a.totemEnabled.Value||a.savingLife||p==null||__instance.Player?.Pointer!=p.Pointer||__0.ToString() is "Existence" or "Undefined")return true;
  var item=p.HandsController?.GetItem();if(item==null||!MatterRules.CanSave(true,p.IsYourPlayer,item.StringTemplateId==Gadgets.Totem,a.consumedTotems.Contains(item.Id.ToString()),__instance.IsAlive))return true;
  try{
   a.savingLife=true;
   var controller=p.InventoryController;if(controller==null||item.CurrentAddress==null)return true;
   var removal=ItemManipulator.Remove(item,controller,false);if(!removal.Succeeded)return true;
   a.consumedTotems.Add(item.Id.ToString());
   removal.Value.RaiseEvents(controller.Cast<IItemOwner>(),CommandStatus.Succeed);
   // Restore before allowing the death method to notify listeners or create a corpse.
   __instance.RemoveNegativeEffects(EBodyPart.Common);
   foreach(var part in bodyParts){__instance.RemoveNegativeEffects(part);var hp=__instance.GetBodyPartHealth(part,false);float before=hp.Current;if(before<=0)__instance.FullRestoreBodyPart(part);hp=__instance.GetBodyPartHealth(part,false);float target=Math.Max(before,MatterRules.RevivalHealth(hp.Maximum));__instance.ChangeHealth(part,target-hp.Current,new DamageInfo());}
   a.savedPlayer=p.Pointer;a.totemStart=Time.unscaledTime;a.regenUntil=Time.unscaledTime+45;a.fireUntil=Time.unscaledTime+40;a.nextRegen=Time.unscaledTime+1;a.emptyHands=true;
   try{a.resources!.Play("totem",a.volume.Value);a.Say("不死图腾已消耗 · 再生 45 秒 · 火焰抵抗 40 秒");}catch(Exception e){a.context.Log(Local.ModHost.LogLevel.Warning,"Totem presentation: "+e.Message);}
   return false;
  }catch(Exception e){a.context.Log(Local.ModHost.LogLevel.Error,"Totem rescue failed: "+e);return true;}
  finally{a.savingLife=false;}
 }
 static void TotemFire(ActiveHealthController __instance,ref float __1,DamageInfo __2){
  var a=active;if(a?.context?.IsActive!=true||__2==null||Time.unscaledTime>=a.fireUntil||__instance.Player?.Pointer!=a.savedPlayer)return;
  if(__2.DamageType.ToString() is "Flame" or "Fire")__1=0;
 }
 void TickTotem(Player? p){
  if(p==null||p.Pointer!=savedPlayer||p.HealthController?.IsAlive!=true){regenUntil=fireUntil=0;emptyHands=false;return;}
  if(emptyHands){emptyHands=false;try{p.Proceed(true,(Action<Result<IEmptyHandsController>>)(_=>{}),false);}catch(Exception e){context!.Log(Local.ModHost.LogLevel.Warning,"Totem hands: "+e.Message);}}
  if(Time.unscaledTime>=regenUntil||Time.unscaledTime<nextRegen)return;nextRegen=Time.unscaledTime+1;
  foreach(var part in bodyParts){var hp=p.ActiveHealthController.GetBodyPartHealth(part,false);if(hp.Current>0&&hp.Current<hp.Maximum)p.ActiveHealthController.ChangeHealth(part,Math.Min(2,hp.Maximum-hp.Current),new DamageInfo());}
 }
 void DrawTotem(){
  float age=Time.unscaledTime-totemStart;if(age<0||age>2.1f||RaidPlayer()?.Pointer!=savedPlayer)return;
  var old=GUI.matrix;var color=GUI.color;int depth=GUI.depth;GUI.depth=-9000;GUI.matrix=Matrix4x4.identity;
  try{
   float t=Math.Clamp(age/2.1f,0,1),fade=Math.Clamp((1-t)*5,0,1);Vector2 center=new(Screen.width*.5f,Screen.height*.48f);
   GUI.color=Color.white;McGui.Fill(new(0,0,Screen.width,Screen.height),new(.15f,.65f,.2f,.16f*(1-t)));
   // Deterministic green/gold particles avoid changing Unity's gameplay RNG.
   for(int n=0;n<64;n++){float angle=n*2.399963f,radius=(30+(n%9)*12)*MathF.Sqrt(t)*2.5f;float x=center.x+Mathf.Cos(angle)*radius,y=center.y+Mathf.Sin(angle)*radius-age*40;float size=3+n%4;McGui.Fill(new(x,y,size,size),n%3==0?new(1,.85f,.25f,fade):new(.25f,1,.3f,fade));}
   float sizePx=Math.Min(Screen.height*.42f,320)*(Mathf.Sin(Math.Min(1,t*4)*Mathf.PI*.5f))*(1-.45f*t);
   float spin=Mathf.Sin(t*Mathf.PI*4)*(1-t)*28;GUIUtility.RotateAroundPivot(spin,center);GUI.color=new(1,1,1,fade);
   GUI.DrawTexture(new(center.x-sizePx/2,center.y-sizePx/2,sizePx,sizePx),resources!.Texture("totem"),ScaleMode.ScaleToFit);
  }finally{GUI.matrix=old;GUI.color=color;GUI.depth=depth;}
 }
}
