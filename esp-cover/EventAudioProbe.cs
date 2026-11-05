using System;
using System.IO;
using System.Text.Json;
using Audio.SpatialSystem;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class EventAudioProbe {
 static bool installed;static float nextPoll;static Il2CppSystem.Threading.Tasks.Task? task;
 internal static void Install(){if(installed)return;Plugin.Self.Patches!.Patch(AccessTools.Method(typeof(SpatialAudioSystem),"Initialize"),postfix:new HarmonyMethod(typeof(EventAudioProbe),nameof(Capture)));installed=true;}
 static void Capture(Il2CppSystem.Threading.Tasks.Task __result)=>task=__result;
 internal static void Tick(){if(!installed||Time.unscaledTime<nextPoll)return;nextPoll=Time.unscaledTime+1;try{
  var system=UnityEngine.Object.FindObjectOfType<SpatialAudioSystem>(true);if(system==null)return;var loader=system._dataLoader;
  File.WriteAllText(Path.Combine(BepInEx.Paths.PluginPath,"HalloweenEventPort","audio-load-status.json"),JsonSerializer.Serialize(new{system=system!=null,initialized=system?.Initialized,path=loader?._fullPath,exists=loader!=null&&File.Exists(loader._fullPath),task=task?.Status.ToString(),error=task?.Exception?.ToString(),dataTask=loader?._dataLoadedTcs?.Task?.Status.ToString(),dataError=loader?._dataLoadedTcs?.Task?.Exception?.ToString(),factoryRegistered=Application.CanStreamedLevelBeLoaded("Assets/Content/Locations/Factory_event/Factory_Event_Scripts.unity"),utc=DateTime.UtcNow},new JsonSerializerOptions{WriteIndented=true}));
 }catch(Exception error){File.WriteAllText(Path.Combine(BepInEx.Paths.PluginPath,"HalloweenEventPort","audio-probe-error.txt"),error.ToString());}}
}
