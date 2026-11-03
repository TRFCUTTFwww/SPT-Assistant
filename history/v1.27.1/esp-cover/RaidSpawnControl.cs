using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using HarmonyLib;
using NativeTask=Il2CppSystem.Threading.Tasks.Task;
namespace LocalSpt5Esp;
internal static class RaidSpawnControl {
 static IntPtr game;static bool noAi;static readonly HashSet<IntPtr> manual=new();
 internal static bool NoAiActive=>noAi&&game!=IntPtr.Zero&&Singleton<AbstractGame>.Instance?.Pointer==game;
 internal static void Reset(){game=IntPtr.Zero;noAi=false;manual.Clear();}
 internal static void AllowManual(BotCreationData data)=>manual.Add(data.Pointer);
 internal static void ForgetManual(BotCreationData? data){if(data!=null)manual.Remove(data.Pointer);}
 internal static void Initialize(Plugin plugin){try{plugin.Patches.Patch(AccessTools.Method(typeof(LocalGame),"Create"),postfix:new HarmonyMethod(typeof(RaidSpawnControl),nameof(Created)));plugin.Patches.Patch(AccessTools.Method(typeof(LocalGame),"CleanUp"),postfix:new HarmonyMethod(typeof(RaidSpawnControl),nameof(Cleaned)));plugin.Patches.Patch(AccessTools.Method(typeof(BotSpawner),"ActivateBots"),prefix:new HarmonyMethod(typeof(RaidSpawnControl),nameof(Activate)));plugin.Patches.Patch(AccessTools.Method(typeof(BotSpawner),"SpawnBotBTR"),prefix:new HarmonyMethod(typeof(RaidSpawnControl),nameof(Btr)));plugin.Log.LogInfo("Next-raid no-AI guard ready: captured per game; explicit manual spawn allowed");}catch(Exception e){plugin.Log.LogError("Next-raid no-AI hooks failed: "+e);}}
 static void Created(LocalGame __result){Reset();if(__result==null)return;game=__result.Pointer;try{noAi=NextRaidLoot.Read().NoAi;Plugin.Self.Log.LogInfo("No-AI raid captured: "+noAi);}catch(Exception e){Plugin.Self.Log.LogWarning("No-AI raid settings: "+e.Message);}}
 static void Cleaned(LocalGame __instance){if(game==__instance.Pointer)Reset();}
 static bool Activate(BotCreationData __2,ref NativeTask __result){if(!NoAiActive||manual.Contains(__2.Pointer))return true;__result=NativeTask.CompletedTask;return false;}
 static bool Btr(ref NativeTask __result){if(!NoAiActive)return true;__result=NativeTask.CompletedTask;return false;}
}
