using System;
using System.IO;
using Comfort.Common;
using EFT.Settings;
using EFT.Settings.Graphics;
using UnityEngine;
namespace LocalSpt5Esp;

// An explicit local repair request is consumed once; later player choices are
// left alone. Use the native settings group so disk and active cameras agree.
internal static class GraphicsRepair {
 static float next;static Il2CppSystem.Threading.Tasks.Task? saving;static bool failed;
 static string Request=>Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","runtime","repair-graphics.request");
 internal static void Tick(){
  if(failed||Time.unscaledTime<next)return;next=Time.unscaledTime+1;
  try{
   if(saving!=null){if(!saving.IsCompleted)return;if(saving.IsFaulted||saving.IsCanceled)throw new IOException("Native graphics settings save failed");File.Delete(Request);saving=null;Plugin.Self.Log.LogInfo("Graphics repair saved: native resolution, TAA High, FSR/DLSS off, noise/chromatic aberration off");return;}
   if(!File.Exists(Request))return;
   var provider=Singleton<SettingsManager>.Instance?.Graphics;var settings=provider?.Settings;if(settings==null)return;
   settings.DLSSMode.Value=EDLSSMode.Off;settings.FSR2Mode.Value=EFSR2Mode.Off;settings.FSR3Mode.Value=EFSR3Mode.Off;
   settings.SuperSampling.Value=ESamplingMode.Off;settings.AntiAliasing.Value=EAntialiasingMode.TAA_High;
   settings.Noise.Value=false;settings.ChromaticAberrations.Value=false;
   settings.AnisotropicFiltering.Value=AnisotropicFiltering.Enable;
   saving=provider!.Save();
  }catch(Exception e){failed=true;Plugin.Self.Log.LogError("Graphics repair failed (request retained): "+e);}
 }
}
