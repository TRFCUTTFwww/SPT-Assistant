using System;
using Comfort.Common;
using EFT;
using HarmonyLib;
namespace LocalSpt5Esp;
// A missing optional rain effect must not interrupt DestroyController between
// clearing the old animation rig and creating the next hands controller.
internal static class WeaponLifecycleGuard {
 static int nullCollections,removedEffects,lightRequests;
 static string recovery="not requested";
 internal static void Initialize(Plugin plugin){
  plugin.Patches.Patch(AccessTools.Method(typeof(RainCondensatorHelper),nameof(RainCondensatorHelper.SetEnabled)),prefix:new HarmonyMethod(typeof(WeaponLifecycleGuard),nameof(ValidRain)));
  plugin.Patches.Patch(AccessTools.Method(typeof(Player.FirearmController),nameof(Player.FirearmController.SetLightsState)),prefix:new HarmonyMethod(typeof(WeaponLifecycleGuard),nameof(TraceLightSwitch)),postfix:new HarmonyMethod(typeof(WeaponLifecycleGuard),nameof(LightSwitchFinished)));
 }
 static readonly Queue<object> lightTrace=new();
 static float traceUntil,nextTrace;
 static void TraceLightSwitch(Player.FirearmController __instance,Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<EFT.LightsState> __0,bool __1,bool __2){
  try{if(!LocalHands(__instance))return;lightRequests++;traceUntil=UnityEngine.Time.unscaledTime+5;nextTrace=0;
  RecordLight("request",new{force=__1,animated=__2,requested=__0?.Select(s=>new{s.Id,s.IsActive,s.LightMode}).ToArray(),state=LightAnimation(__instance)});
  }catch(Exception e){RecordLight("trace-error",e.Message);}
 }
 static void LightSwitchFinished(Player.FirearmController __instance,bool __result){try{if(LocalHands(__instance))RecordLight("result",new{accepted=__result,state=LightAnimation(__instance)});}catch(Exception e){RecordLight("trace-error",e.Message);}}
 static bool LocalHands(Player.FirearmController f)=>!f.Destroyed&&f._player?.IsYourPlayer==true&&f._player.HandsController?.Pointer==f.Pointer;
 static void RecordLight(string kind,object details){if(lightTrace.Count>=48)lightTrace.Dequeue();lightTrace.Enqueue(new{time=UnityEngine.Time.unscaledTime,kind,details});}
 static object LightAnimation(Player.FirearmController f){
  var animator=f.FirearmsAnimator?.Animator;
  return new{operation=f.CurrentOperation?.GetIl2CppType().FullName,lights=f.GetAllLightMods()?.Select(l=>new{id=l.Item.Id.ToString(),l.IsActive,l.SelectedMode}).ToArray(),animator=animator?.name,layers=animator==null?null:Enumerable.Range(0,animator.layerCount).Select(i=>{var state=animator.GetCurrentAnimatorStateInfo(i);return new{layer=animator.GetLayerName(i),name=animator.GetStateName(ref state),weight=animator.GetLayerWeight(i)};}).ToArray(),parameters=animator==null?null:Enumerable.Range(0,animator.parameterCount).Select(i=>{string name=animator.GetParameterName(i);var type=animator.GetParameterType(i);return new{name,type=type.ToString(),value=type==UnityEngine.AnimatorControllerParameterType.Float?(object)animator.GetFloat(name):type==UnityEngine.AnimatorControllerParameterType.Int?animator.GetInteger(name):type==UnityEngine.AnimatorControllerParameterType.Bool?animator.GetBool(name):null};}).ToArray()};
 }
 internal static void Tick(){if(UnityEngine.Time.unscaledTime>=traceUntil||UnityEngine.Time.unscaledTime<nextTrace)return;nextTrace=UnityEngine.Time.unscaledTime+.5f;try{var f=Singleton<GameWorld>.Instance?.MainPlayer?.HandsController?.TryCast<Player.FirearmController>();if(f!=null&&LocalHands(f))RecordLight("sample",LightAnimation(f));}catch(Exception e){RecordLight("error",e.Message);}}
 internal static object LightDiagnostics()=>new{lightRequests,trace=lightTrace.ToArray()};
 static bool ValidRain(ref Il2CppSystem.Collections.Generic.IEnumerable<RainCondensator> __0){
  if(__0==null){nullCollections++;return false;}
  var valid=new Il2CppSystem.Collections.Generic.List<RainCondensator>();bool changed=false;
  foreach(var effect in __0){if(effect==null){removedEffects++;changed=true;}else valid.Add(effect);}
  if(changed)__0=valid.Cast<Il2CppSystem.Collections.Generic.IEnumerable<RainCondensator>>();
  return true;
 }
 internal static object Diagnostics(){
  var p=Singleton<GameWorld>.Instance?.MainPlayer;var h=p?.HandsController;var f=h?.TryCast<Player.FirearmController>();var a=p?.ProceduralWeaponAnimation;
  int count=a?.ScopeAimTransforms?.Count??0,index=a?.AimIndex??-1;
  var scope=count>0&&index>=0&&index<count?a!.CurrentScope:null;
  return new{nullCollections,removedEffects,lightRequests,recovery,player=p!=null,process=p?.ProcessStatus.ToString(),scheduled=p?.ScheduledProcess?.GetIl2CppType().FullName,hands=h?.GetIl2CppType().FullName,destroyed=h?.Destroyed,item=h?.Item?.StringTemplateId,root=h?.ControllerGameObject!=null,operation=f?.CurrentOperation?.GetIl2CppType().FullName??h?.TryCast<Player.KnifeController>()?.CurrentOperation?.GetIl2CppType().FullName,prefab=f?._weaponPrefab!=null,ccv=f?.CCV!=null,rain=f?.Firearms?._rainCondensators?.Count,scopeCount=count,scope=scope!=null,scopeBone=scope?.Bone!=null,lights=f?.GetAllLightMods()?.Select(l=>new{id=l.Item.Id.ToString(),active=l.IsActive,mode=l.SelectedMode}).ToArray(),animators=h?.ControllerGameObject?.GetComponentsInChildren<UnityEngine.Animator>(true)?.Select(a=>new{name=a.name,enabled=a.enabled,speed=a.speed,layers=Enumerable.Range(0,a.layerCount).Select(i=>new{layer=a.GetLayerName(i),state=a.GetCurrentAnimatorStateInfo(i).fullPathHash,time=a.GetCurrentAnimatorStateInfo(i).normalizedTime,clips=a.GetCurrentAnimatorClipInfo(i).Select(c=>new{name=c.clip?.name,weight=c.weight}).ToArray()}).ToArray()}).ToArray()};
 }
 internal static object Recover(){
  var p=Singleton<GameWorld>.Instance?.MainPlayer??throw new InvalidOperationException("No local player");
  var firearm=p.HandsController?.TryCast<Player.FirearmController>();
  if(firearm?.Destroyed==true&&firearm._weaponPrefab==null&&firearm.CCV==null&&firearm.CurrentOperation?.TryCast<Player.FirearmController.Remove>()!=null&&p.ProcessStatus==Player.EProcessStatus.Scheduled&&p.ScheduledProcess==null){
   var item=firearm.Item??throw new InvalidOperationException("Destroyed controller has no weapon item");
   // This controller has already been disposed. Its interrupted removal left
   // only a stale reference and scheduling lock; never dispose it twice. Draw
   // the same owned inventory item through the native controller factory.
   p._handsController=null;p._processStatus=Player.EProcessStatus.None;
   recovery="redrawing "+item.StringTemplateId;
   p.Proceed(item,(Action<Comfort.Common.Result<IFirearmHandsController>>)(result=>{recovery=result.ToString();Plugin.Self.Log.LogInfo("Hands recovery native result: "+recovery);}),true);
  }else{p.FastForwardCurrentOperations();recovery="fast-forward requested";}
  return Diagnostics();
 }
 internal static object Check(){
  RainCondensatorHelper.SetEnabled(null!,false);
  var list=new Il2CppSystem.Collections.Generic.List<RainCondensator>();list.Add(null!);
  RainCondensatorHelper.SetEnabled(list.Cast<Il2CppSystem.Collections.Generic.IEnumerable<RainCondensator>>(),false);
  return new{nullCollectionPassed=true,nullElementPassed=true,nullCollections,removedEffects};
 }
}
