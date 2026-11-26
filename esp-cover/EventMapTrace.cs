using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Text.Json;
using HarmonyLib;
using EFT;
using UnityEngine;
using Runner=Il2CppSystem.Runtime.CompilerServices.AsyncMethodBuilderCore.MoveNextRunner;
#if EFT_48558
using MapLoadState=EFT.TarkovApplication._LoadMapAndData_d__236;
#else
using MapLoadState=EFT.TarkovApplication._LoadMapAndData_d__237;
#endif
namespace LocalSpt5Esp;
internal static class EventMapTrace {
 static bool installed;static float next;static readonly List<MapLoadState> states=new();
 static string Home=>Path.Combine(BepInEx.Paths.PluginPath,"HalloweenEventPort");
 internal static void Install(){if(installed)return;
  var h=Plugin.Self.Patches!;
  h.Patch(AccessTools.Method(typeof(Runner),"Run"),prefix:new HarmonyMethod(typeof(EventMapTrace),nameof(Run)));
  var execute=Array.Find(typeof(LoadScenesFromPresetOperation).GetMethods(),m=>m.Name=="Execute"&&m.GetParameters()[0].ParameterType==typeof(ScenePresetLoadConfig));
  h.Patch(execute,prefix:new HarmonyMethod(typeof(EventMapTrace),nameof(Preset)));
  var assembly=Array.Find(AppDomain.CurrentDomain.GetAssemblies(),a=>a.GetName().Name=="Local.HalloweenEventPort.Client")!;
  var build=assembly.GetType("HalloweenEventPort.RaidSceneBridge",true)!.GetMethod("BuildPreset",BindingFlags.Static|BindingFlags.NonPublic)!;
  h.Patch(build,prefix:new HarmonyMethod(typeof(EventMapTrace),nameof(BeforeBuild)),postfix:new HarmonyMethod(typeof(EventMapTrace),nameof(AfterBuild)));installed=true;
 }
 static void BeforeBuild(MethodBase __originalMethod){var presets=(System.Collections.IDictionary)__originalMethod.DeclaringType!.GetField("presets",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;var remove=new List<object>();foreach(System.Collections.DictionaryEntry entry in presets)if((ScenesPreset)entry.Value! == null)remove.Add(entry.Key);foreach(var key in remove)presets.Remove(key);}
 static void AfterBuild(ScenesPreset __result){if(__result!=null)__result.hideFlags=HideFlags.HideAndDontSave;}
 static void Preset(ScenePresetLoadConfig __0){try{File.AppendAllText(Path.Combine(Home,"preset-calls.txt"),DateTime.UtcNow.ToString("O")+" "+__0?.key?.path+"\n"+Il2CppSystem.Environment.StackTrace+"\n");}catch{}}
 static void Run(Runner __instance){try{var state=__instance.m_stateMachine?.TryCast<MapLoadState>();if(state!=null&&!states.Exists(s=>s.Pointer==state.Pointer)){states.Add(state);if(states.Count>8)states.RemoveAt(0);}}catch{}}
 internal static void Tick(){if(!installed||Time.unscaledTime<next)return;next=Time.unscaledTime+1;try{
  var output=new List<object>();foreach(var s in states){var op=s._loadSceneOperation_5__7;var task=s.__u__1.m_task;output.Add(new{pointer=s.Pointer.ToInt64(),state=s.__1__state,task=task?.Status.ToString(),error=task?.Exception?.ToString(),operation=op==null?null:new{op.Completed,op.Succeed,op.Error},world=s._gameWorld_5__3!=null});}
  File.WriteAllText(Path.Combine(Home,"map-continuation-status.json"),JsonSerializer.Serialize(new{states=output,utc=DateTime.UtcNow},new JsonSerializerOptions{WriteIndented=true}));
 }catch(Exception error){File.WriteAllText(Path.Combine(Home,"map-continuation-error.txt"),error.ToString());}}
}
