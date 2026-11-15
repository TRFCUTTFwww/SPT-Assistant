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
 readonly TotemRescueGate totemGate=new();
 int totemRescues,totemBlockedDamage,totemBlockedSpread,totemBlockedKills,totemBlockedHealthChanges,totemBlockedDestruction;
 bool totemProtectionWasActive;float nextTotemStatus;
 bool totemFollowup;
 bool emptyHands;float totemStart=-100,regenUntil,fireUntil,nextRegen;IntPtr savedPlayer;
 // Zero is not a defined EDamageType. The native enum converter emits numeric
 // zero, which the server's string DamageStats.Type rejects during settlement.
 static DamageInfo HealingDamage()=>new(){DamageType=EDamageType.Medicine};
 void InitializeTotem(Harmony harmony){
  harmony.Patch(AccessTools.Method(typeof(ActiveHealthController),nameof(ActiveHealthController.Kill),[typeof(EDamageType)]),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(TotemBeforeKill)){priority=Priority.Last});
  harmony.Patch(AccessTools.Method(typeof(ActiveHealthController),nameof(ActiveHealthController.ApplyDamage),[typeof(EBodyPart),typeof(float),typeof(DamageInfo)]),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(TotemDamage)){priority=Priority.Last},postfix:new HarmonyMethod(typeof(ProjectModule),nameof(TotemDamageFinished)));
  harmony.Patch(AccessTools.Method(typeof(ActiveHealthController),nameof(ActiveHealthController.SpreadOverDamage)),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(TotemSpread)));
  harmony.Patch(AccessTools.Method(typeof(ActiveHealthController),nameof(ActiveHealthController.ChangeHealth),[typeof(EBodyPart),typeof(float),typeof(DamageInfo)]),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(TotemHealthChange)));
  harmony.Patch(AccessTools.Method(typeof(ActiveHealthController),nameof(ActiveHealthController.DestroyBodyPart),[typeof(EBodyPart),typeof(EDamageType)]),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(TotemDestroyPart)));
  InitializeTotemHotbar(harmony);
 }
 static bool TotemBeforeKill(ActiveHealthController __instance,EDamageType __0){
  var a=active;var p=RaidPlayer();if(a?.context?.IsActive!=true||p==null||__instance.Player?.Pointer!=p.Pointer||__0.ToString() is "Existence" or "Undefined")return true;
  if(a.totemGate.Blocks(__instance.Pointer.ToInt64(),Time.frameCount,Time.unscaledTime)){a.totemBlockedKills++;return false;}
  if(!a.totemEnabled.Value)return true;
  var item=CarriedInventory.Items(p).FirstOrDefault(i=>i.StringTemplateId==Gadgets.Totem&&!a.consumedTotems.Contains(i.Id.ToString()));
  if(item==null||!MatterRules.CanSave(true,p.IsYourPlayer,true,false,__instance.IsAlive))return true;
  try{
   a.totemGate.Begin(__instance.Pointer.ToInt64());
   var controller=p.InventoryController;if(controller==null||item.CurrentAddress==null)return true;
   bool held=p.HandsController?.GetItem()?.Id.ToString()==item.Id.ToString();
   var removal=ItemManipulator.Remove(item,controller,false);if(!removal.Succeeded)return true;
   a.consumedTotems.Add(item.Id.ToString());
   removal.Value.RaiseEvents(controller.Cast<IItemOwner>(),CommandStatus.Succeed);
   // Restore before allowing the death method to notify listeners or create a corpse.
   __instance.RemoveNegativeEffects(EBodyPart.Common);
   foreach(var part in bodyParts){__instance.RemoveNegativeEffects(part);var hp=__instance.GetBodyPartHealth(part,false);float before=hp.Current;if(before<=0)__instance.FullRestoreBodyPart(part);hp=__instance.GetBodyPartHealth(part,false);float target=Math.Max(before,MatterRules.RevivalHealth(hp.Maximum));__instance.ChangeHealth(part,target-hp.Current,HealingDamage());}
   if(!__instance.IsAlive||__instance.GetBodyPartHealth(EBodyPart.Head,false).Current<=0||__instance.GetBodyPartHealth(EBodyPart.Chest,false).Current<=0)throw new InvalidOperationException("Native health restoration did not preserve the living critical parts");
   double protection=TotemRescueGate.Duration(a.totemProtectionSeconds.Value);
   a.totemGate.Commit(Time.frameCount,Time.unscaledTime,protection);a.totemRescues++;a.totemProtectionWasActive=protection>0;a.nextTotemStatus=Time.unscaledTime+1;
   a.savedPlayer=p.Pointer;a.totemStart=Time.unscaledTime;a.regenUntil=Time.unscaledTime+45;a.fireUntil=Time.unscaledTime+40;a.nextRegen=Time.unscaledTime+1;a.emptyHands=held;a.totemFollowup=true;
   a.WriteTotemStatus(p,"rescued");
   try{a.resources!.Play("totem",a.volume.Value);a.Say($"不死图腾已消耗 · 免伤 {protection:0.##} 秒 · 再生 45 秒 · 火焰抵抗 40 秒");}catch(Exception e){a.context.Log(Local.ModHost.LogLevel.Warning,"Totem presentation: "+e.Message);}
   return false;
  }catch(Exception e){a.totemGate.Clear();a.context.Log(Local.ModHost.LogLevel.Error,"Totem rescue failed: "+e);a.WriteTotemStatus(p,"failed",e.Message);return true;}
  finally{a.totemGate.End();}
 }
 static bool TotemDamage(ActiveHealthController __instance,ref float __1,DamageInfo __2,ref float __result,out int __state){
  var a=active;__state=-1;if(a?.context?.IsActive!=true||__instance.Player?.IsYourPlayer!=true)return true;
  __state=a.totemRescues;
  if(a.totemGate.Blocks(__instance.Pointer.ToInt64(),Time.frameCount,Time.unscaledTime)){a.totemBlockedDamage++;__result=0;return false;}
  if(__2!=null&&Time.unscaledTime<a.fireUntil&&__instance.Player?.Pointer==a.savedPlayer&&__2.DamageType.ToString() is "Flame" or "Fire")__1=0;
  return true;
 }
 static bool TotemSpread(ActiveHealthController __instance){var a=active;if(a?.context?.IsActive!=true||!a.totemGate.Blocks(__instance.Pointer.ToInt64(),Time.frameCount,Time.unscaledTime))return true;a.totemBlockedSpread++;return false;}
 static bool TotemHealthChange(ActiveHealthController __instance,float __1){
  var a=active;if(a?.context?.IsActive!=true||!a.totemGate.BlocksHealthChange(__instance.Pointer.ToInt64(),Time.frameCount,Time.unscaledTime,__1))return true;
  // Bleeding, poison and other effects can bypass ApplyDamage and adjust HP
  // directly. Preserve healing while rejecting negative health deltas.
  a.totemBlockedHealthChanges++;return false;
 }
 static bool TotemDestroyPart(ActiveHealthController __instance){var a=active;if(a?.context?.IsActive!=true||!a.totemGate.Blocks(__instance.Pointer.ToInt64(),Time.frameCount,Time.unscaledTime))return true;a.totemBlockedDestruction++;return false;}
 static void TotemDamageFinished(ActiveHealthController __instance,int __state){
  var a=active;if(a?.context?.IsActive!=true||__state<0||a.totemRescues==__state||__instance.Player?.Pointer!=a.savedPlayer)return;
  // Native ApplyDamage can append bleeding/pain after Kill was intercepted.
  __instance.RemoveNegativeEffects(EBodyPart.Common);foreach(var part in bodyParts)__instance.RemoveNegativeEffects(part);
  if(__instance.Player is {} player)a.WriteTotemStatus(player,"damage-finished");
 }
 void WriteTotemStatus(Player p,string stage,string? error=null){
  try{var health=p.ActiveHealthController;File.WriteAllText(Path.Combine(context!.DataDirectory,"totem-status.json"),System.Text.Json.JsonSerializer.Serialize(new{utc=DateTime.UtcNow,stage,error,version=Version,process=Environment.ProcessId,totemRescues,totemBlockedDamage,totemBlockedSpread,totemBlockedKills,totemBlockedHealthChanges,totemBlockedDestruction,configuredProtectionSeconds=totemProtectionSeconds.Value,protectionRemaining=totemGate.Remaining(Time.unscaledTime),alive=health?.IsAlive,body=bodyParts.Select(part=>new{part=part.ToString(),current=health?.GetBodyPartHealth(part,false).Current,maximum=health?.GetBodyPartHealth(part,false).Maximum}),consumed=consumedTotems.Count}));}catch(Exception e){context?.Log(Local.ModHost.LogLevel.Warning,"Totem status: "+e.Message);}
 }
 void TickTotem(Player? p){
  if(p==null||p.Pointer!=savedPlayer||p.HealthController?.IsAlive!=true){regenUntil=fireUntil=0;emptyHands=false;totemGate.Clear();totemFollowup=false;totemProtectionWasActive=false;return;}
  if(totemGate.Timed(Time.unscaledTime)){if(Time.unscaledTime>=nextTotemStatus){nextTotemStatus=Time.unscaledTime+1;WriteTotemStatus(p,"protected");}}
  else if(totemProtectionWasActive){totemProtectionWasActive=false;WriteTotemStatus(p,"protection-ended");}
  if(totemFollowup){totemFollowup=false;WriteTotemStatus(p,"next-tick");}
  if(emptyHands){emptyHands=false;try{p.Proceed(true,(Action<Result<IEmptyHandsController>>)(_=>{}),false);}catch(Exception e){context!.Log(Local.ModHost.LogLevel.Warning,"Totem hands: "+e.Message);}}
  if(Time.unscaledTime>=regenUntil||Time.unscaledTime<nextRegen)return;nextRegen=Time.unscaledTime+1;
  foreach(var part in bodyParts){var hp=p.ActiveHealthController.GetBodyPartHealth(part,false);if(hp.Current>0&&hp.Current<hp.Maximum)p.ActiveHealthController.ChangeHealth(part,Math.Min(2,hp.Maximum-hp.Current),HealingDamage());}
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
