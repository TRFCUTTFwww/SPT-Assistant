using System;
using System.IO;
using BepInEx.Configuration;
using Comfort.Common;
using EFT.Settings;
using EFT.Settings.Graphics;
using UnityEngine;
namespace LocalSpt5Esp;

// Apply the clear-image profile through the native settings provider so the
// active renderer and saved settings agree. Leave unrelated quality untouched.
internal static class GraphicsRepair {
 internal static ConfigEntry<bool> KeepClear=null!;
 internal static void Initialize()=>KeepClear=FeatureConfig.Bind("1. ESP","Keep clear graphics","保持清晰画面",true,"保持原生渲染、TAA 高，关闭 FSR/DLSS、噪点和色差。使用其他抗锯齿设置前可关闭。");
 static float next;static Il2CppSystem.Threading.Tasks.Task? saving;static int failures;
 static string Request=>Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","runtime","repair-graphics.request");
 internal static void Tick(){
  if(Time.unscaledTime<next)return;next=Time.unscaledTime+1;
  try{
   if(saving!=null){
    if(!saving.IsCompleted)return;
    if(saving.IsFaulted||saving.IsCanceled)throw new IOException("Native graphics settings save failed");
    File.Delete(Request);saving=null;failures=0;
    Plugin.Self.Log.LogInfo("Graphics repair saved: native rendering, TAA High, FSR/DLSS off, noise/chromatic aberration off");return;
   }
   bool requested=File.Exists(Request);if(!requested&&!KeepClear.Value)return;
   var provider=Singleton<SettingsManager>.Instance?.Graphics;var settings=provider?.Settings;if(settings==null)return;
   bool changed=settings.DLSSMode.Value!=EDLSSMode.Off||settings.FSR2Mode.Value!=EFSR2Mode.Off||settings.FSR3Mode.Value!=EFSR3Mode.Off||settings.SuperSampling.Value!=ESamplingMode.Off||settings.AntiAliasing.Value!=EAntialiasingMode.TAA_High||settings.Noise.Value||settings.ChromaticAberrations.Value||settings.AnisotropicFiltering.Value!=AnisotropicFiltering.Enable;
   if(!changed&&!requested)return;
   settings.DLSSMode.Value=EDLSSMode.Off;settings.FSR2Mode.Value=EFSR2Mode.Off;settings.FSR3Mode.Value=EFSR3Mode.Off;
   settings.SuperSampling.Value=ESamplingMode.Off;settings.AntiAliasing.Value=EAntialiasingMode.TAA_High;
   settings.Noise.Value=false;settings.ChromaticAberrations.Value=false;
   settings.AnisotropicFiltering.Value=AnisotropicFiltering.Enable;
   saving=provider!.Save();
  }catch(Exception e){saving=null;next=Time.unscaledTime+Math.Min(60,5*++failures);Plugin.Self.Log.LogError("Graphics repair failed (will retry): "+e);}
 }
}