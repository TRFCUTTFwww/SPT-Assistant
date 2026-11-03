using System;
using BepInEx.Configuration;
using BSG.CameraEffects;
using EFT;
using EFT.CameraControl;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class NightVisionAssist {
 internal static ConfigEntry<bool> Enabled=null!;
 static ConfigEntry<KeyCode> ToggleKey=null!;
 static NightVision? forced;
 static bool originalOn;
 static float nextError;
 internal static void Initialize(Plugin p){
  Enabled=CnConfig.Bind(p.Config,"16. Night vision","Enabled",false);
  ToggleKey=CnConfig.Bind(p.Config,"16. Night vision","Night vision toggle",KeyCode.N);
  if(ToggleKey.Value==p.IgnoreAIKey.Value)ToggleKey.Value=KeyCode.N;
 }
 internal static void Keys(){if(Hotkeys.Pressed(ToggleKey.Value))Enabled.Value=!Enabled.Value;}
 internal static string Status=>"夜视："+CnConfig.OnOff(Enabled.Value);
 internal static void Tick(Player? player){
  if(!Enabled.Value||player==null||!player.IsYourPlayer){Reset();return;}
  try{
   NightVision? current=null;
   try{current=CameraManager.Instance?.NightVision;}catch{}
   if(current==null){var camera=Camera.main;if(camera!=null)current=camera.GetComponent<NightVision>()??camera.GetComponentInParent<NightVision>();}
   if(current==null){Reset();return;}
   if(forced!=null&&forced.Pointer!=current.Pointer)Reset();
   if(forced==null){forced=current;originalOn=current.On;}
   if(!current.On)current.On=true;
  }catch(Exception e){
   if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+10;Plugin.Self.Error(e);}
  }
 }
 internal static void Reset(){
  try{if(forced!=null&&forced.On!=originalOn)forced.On=originalOn;}catch{}
  forced=null;originalOn=false;
 }
}
