using System;
using Comfort.Common;
using EFT;
using HarmonyLib;
namespace LocalSpt5Esp;
// A missing optional rain effect must not interrupt DestroyController between
// clearing the old animation rig and creating the next hands controller.
internal static class WeaponLifecycleGuard {
 static int nullCollections,removedEffects;
 static string recovery="not requested";
 internal static void Initialize(Plugin plugin)=>plugin.Patches.Patch(AccessTools.Method(typeof(RainCondensatorHelper),nameof(RainCondensatorHelper.SetEnabled)),prefix:new HarmonyMethod(typeof(WeaponLifecycleGuard),nameof(ValidRain)));
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
  return new{nullCollections,removedEffects,recovery,player=p!=null,process=p?.ProcessStatus.ToString(),scheduled=p?.ScheduledProcess?.GetIl2CppType().FullName,hands=h?.GetIl2CppType().FullName,destroyed=h?.Destroyed,item=h?.Item?.StringTemplateId,root=h?.ControllerGameObject!=null,operation=f?.CurrentOperation?.GetIl2CppType().FullName??h?.TryCast<Player.KnifeController>()?.CurrentOperation?.GetIl2CppType().FullName,prefab=f?._weaponPrefab!=null,ccv=f?.CCV!=null,rain=f?.Firearms?._rainCondensators?.Count,scopeCount=count,scope=scope!=null,scopeBone=scope?.Bone!=null};
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
