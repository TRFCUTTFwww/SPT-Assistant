using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using EFT;
using HarmonyLib;
using UnityEngine;
using NativeTask=Il2CppSystem.Threading.Tasks.Task;
namespace LocalSpt5Esp;
internal static class RaidSpawnControl {
 static IntPtr game,world;static bool noAi;static float nextSweep,nextWarning;static int blocked;
 static readonly HashSet<IntPtr> manual=new();static readonly HashSet<string> manualProfiles=new(StringComparer.Ordinal);
 static readonly Dictionary<IntPtr,float> pending=new();
 internal static bool NoAiActive=>noAi&&(game!=IntPtr.Zero||RaidClock.Active);
 internal static void Reset(){game=world=IntPtr.Zero;noAi=false;blocked=0;nextSweep=nextWarning=0;manual.Clear();manualProfiles.Clear();pending.Clear();}
 internal static void Configure(bool value){noAi=value;nextSweep=0;}
 internal static void AllowManual(BotCreationData data){manual.Add(data.Pointer);if(data.Profiles is {} profiles)for(int i=0;i<profiles.Count;i++)if(profiles[i]?.Id is string id&&id.Length>0)manualProfiles.Add(id);}
 internal static void ForgetManual(BotCreationData? data){if(data!=null)manual.Remove(data.Pointer);}
 static bool Block(bool explicitManual){bool value=AiCleanupRules.BlockSpawn(NoAiActive,SceneTemplates.Exclusive,explicitManual);if(value)blocked++;return value;}
 internal static void Initialize(Plugin plugin){
  try{Configure(NextRaidLoot.Read().NoAi);}catch(Exception e){plugin.Log.LogWarning("No-AI settings: "+e.Message);}
  void Hook(MethodInfo method,string prefix){try{plugin.Patches.Patch(method,prefix:new HarmonyMethod(typeof(RaidSpawnControl),prefix));}catch(Exception e){plugin.Log.LogError("No-AI hook "+method.DeclaringType?.Name+"."+method.Name+": "+e);}}
  try{plugin.Patches.Patch(AccessTools.Method(typeof(LocalGame),"Create"),postfix:new HarmonyMethod(typeof(RaidSpawnControl),nameof(Created)));plugin.Patches.Patch(AccessTools.Method(typeof(LocalGame),"CleanUp"),postfix:new HarmonyMethod(typeof(RaidSpawnControl),nameof(Cleaned)));}catch(Exception e){plugin.Log.LogError("No-AI raid lifecycle: "+e);}
  Hook(AccessTools.Method(typeof(BotSpawner),"ActivateBots"),nameof(Activate));Hook(AccessTools.Method(typeof(BotSpawner),"SpawnBotBTR"),nameof(Btr));
  // Concrete async implementations only: never hook IBotCreator or empty base stubs.
  foreach(var method in typeof(BotCreatorClient).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.DeclaredOnly).Where(m=>m.ReturnType==typeof(NativeTask)&&(m.Name is "SpawnBot" or "CreateBot" or "ActivateBot"))){
   var parameters=method.GetParameters();if(parameters.Length==0)continue;if(parameters[0].ParameterType==typeof(BotCreationData))Hook(method,nameof(CreatorData));else if(parameters[0].ParameterType==typeof(Profile))Hook(method,nameof(CreatorProfile));
  }
  plugin.Log.LogInfo("No-AI guard ready: concrete creator interception, persistent manual profile allowlist, world reconciliation");
 }
 static void Created(LocalGame __result){Reset();if(__result==null)return;game=__result.Pointer;try{Configure(NextRaidLoot.Read().NoAi);Plugin.Self.Log.LogInfo("No-AI raid captured: "+noAi);}catch(Exception e){Plugin.Self.Log.LogWarning("No-AI raid settings: "+e.Message);}}
 static void Cleaned(LocalGame __instance){if(game==__instance.Pointer)Reset();}
 static bool Activate(BotCreationData __2,ref NativeTask __result){if(!Block(__2!=null&&manual.Contains(__2.Pointer)))return true;__result=NativeTask.CompletedTask;return false;}
 static bool CreatorData(BotCreationData __0,ref NativeTask __result){if(!Block(__0!=null&&manual.Contains(__0.Pointer)))return true;__result=NativeTask.CompletedTask;return false;}
 static bool CreatorProfile(Profile __0,ref NativeTask __result){if(!Block(__0!=null&&manualProfiles.Contains(__0.Id)))return true;__result=NativeTask.CompletedTask;return false;}
 static bool Btr(ref NativeTask __result){if(!NoAiActive)return true;blocked++;__result=NativeTask.CompletedTask;return false;}
 internal static void Tick(GameWorld? current){
  if(current==null){if(world!=IntPtr.Zero){world=IntPtr.Zero;pending.Clear();manual.Clear();manualProfiles.Clear();}return;}
  if(world!=current.Pointer){world=current.Pointer;pending.Clear();nextSweep=0;try{Configure(NextRaidLoot.Read().NoAi);}catch{}Plugin.Self.Log.LogInfo($"No-AI world bound: {world}; enabled={noAi}");}
  if(!(NoAiActive||SceneTemplates.Exclusive)||Time.unscaledTime<nextSweep)return;nextSweep=Time.unscaledTime+1;
  try{
   var actors=AiRemoval.Gather(current);var candidates=new List<Player>();var seen=new HashSet<IntPtr>();
   foreach(var actor in actors){if(manualProfiles.Contains(actor.ProfileId)||actor.HealthController?.IsAlive!=true)continue;seen.Add(actor.Pointer);if(!pending.TryGetValue(actor.Pointer,out float ready)){pending[actor.Pointer]=Time.unscaledTime+.75f;continue;}if(Time.unscaledTime>=ready){candidates.Add(actor);pending[actor.Pointer]=Time.unscaledTime+5;}}
   foreach(var pointer in pending.Keys.Where(p=>!seen.Contains(p)).ToArray())pending.Remove(pointer);
   if(candidates.Count>0){AiRemoval.Delete(current,candidates,false);Plugin.Self.Log.LogInfo("No-AI reconciliation: "+AiRemoval.Status+" Manual profiles="+manualProfiles.Count+"; blocked="+blocked);}
  }catch(Exception e){if(Time.unscaledTime>=nextWarning){nextWarning=Time.unscaledTime+10;Plugin.Self.Log.LogWarning("No-AI reconciliation: "+e);}}
 }
}