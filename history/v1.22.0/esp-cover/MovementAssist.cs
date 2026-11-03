using System;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace LocalSpt5Esp;
internal static class MovementAssist {
 static ConfigEntry<bool> UnlimitedLegs=null!,UnlimitedArms=null!,SpeedEnabled=null!;
 static ConfigEntry<float> SpeedMultiplier=null!;
 static float nextError;
 sealed class MoveState {internal ICharacterController Controller=null!;internal float Limit;}
 internal static void Initialize(Plugin plugin){
  UnlimitedLegs=CnConfig.Bind(plugin.Config,"20. Movement","Unlimited leg stamina",true);
  UnlimitedArms=CnConfig.Bind(plugin.Config,"20. Movement","Unlimited arm stamina",true);
  SpeedEnabled=CnConfig.Bind(plugin.Config,"20. Movement","Speed enabled",false);
  SpeedMultiplier=CnConfig.Bind(plugin.Config,"20. Movement","Speed multiplier",1.5f,new ConfigDescription("",new AcceptableValueRange<float>(1f,5f)));
  foreach(var type in new[]{typeof(SimpleCharacterController),typeof(PlayerCharacterController),typeof(ImpostorCharacterController),typeof(PlayerSpiritCharacterController)})try{
   var move=type.GetMethod("Move",new[]{typeof(Vector3),typeof(float)})??throw new MissingMethodException(type.Name,"Move");
   plugin.Patches.Patch(move,prefix:new HarmonyMethod(typeof(MovementAssist),nameof(BeforeMove)),finalizer:new HarmonyMethod(typeof(MovementAssist),nameof(FinishMove)));
   plugin.Log.LogInfo("Local movement multiplier hooked: "+type.Name);
  }catch(Exception error){plugin.Log.LogError("Movement multiplier hook unavailable: "+error);}
 }
 static void BeforeMove(Il2CppObjectBase __instance,ref Vector3 __0,out MoveState? __state){
  __state=null;
  try{
   if(!SpeedEnabled.Value||SpeedMultiplier.Value<=1)return;
   var local=Singleton<GameWorld>.Instance?.MainPlayer;
   var controller=local?.CharacterController;
   if(local==null||!local.IsYourPlayer||local.HealthController==null||!local.HealthController.IsAlive||controller==null||controller.Pointer!=__instance.Pointer)return;
   float factor=SpeedMultiplier.Value;if(!float.IsFinite(factor)||factor<1)return;
   if(!float.IsFinite(__0.x*factor)||!float.IsFinite(__0.z*factor)||!float.IsFinite(controller.SpeedLimit*factor))return;
   __state=new MoveState{Controller=controller,Limit=controller.SpeedLimit};
   if(float.IsFinite(__state.Limit)&&__state.Limit>0)controller.SpeedLimit=__state.Limit*factor;
   __0.x*=factor;__0.z*=factor;
  }catch(Exception error){Warn(error);}
 }
 static void FinishMove(MoveState? __state){if(__state!=null)try{__state.Controller.SpeedLimit=__state.Limit;}catch(Exception error){Warn(error);}}
 internal static void Tick(Player? player){
  if(player==null||!player.IsYourPlayer||player.HealthController==null||!player.HealthController.IsAlive)return;
  try{var physical=player.Physical;if(physical==null)return;if(UnlimitedLegs.Value)Fill(physical.Stamina);if(UnlimitedArms.Value)Fill(physical.HandsStamina);}catch(Exception error){Warn(error);}
 }
 static void Fill(Stamina? gauge){
  if(gauge==null)return;float maximum=gauge.TotalCapacity?.Value??0;
  if(!float.IsFinite(maximum)||maximum<=0)return;
  if(gauge.Current<maximum-0.01f)gauge.Current=maximum;if(gauge.Overuse>0)gauge.Overuse=0;
 }
 internal static void Reset(){}
 static void Warn(Exception error){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+20;Plugin.Self.Error(error);}
}
