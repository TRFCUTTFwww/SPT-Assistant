using System;
using System.IO;
using System.Text.Json;
using EFT;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
namespace LocalSpt5Esp;
internal static class InfectionActivationProbe {
 [ThreadStatic] static BotOwner? activating;
 static string home="";static bool installed;
 internal static void Install(string directory){if(installed)return;home=directory;var harmony=Plugin.Self.Patches;
  harmony.Patch(AccessTools.Method(typeof(BotOwner),"Activate"),prefix:new HarmonyMethod(typeof(InfectionActivationProbe),nameof(Begin)),finalizer:new HarmonyMethod(typeof(InfectionActivationProbe),nameof(End)));
  harmony.Patch(AccessTools.Method(typeof(AbstractLogger),"LogException",new[]{typeof(Il2CppSystem.Exception)}),prefix:new HarmonyMethod(typeof(InfectionActivationProbe),nameof(Exception)));
  harmony.Patch(AccessTools.Method(typeof(AbstractLogger),"LogError",new[]{typeof(string),typeof(Il2CppReferenceArray<Il2CppSystem.Object>)}),prefix:new HarmonyMethod(typeof(InfectionActivationProbe),nameof(Error)));installed=true;
 }
 static void Begin(BotOwner __instance,out BotOwner? __state){__state=activating;activating=__instance;}
 static void End(BotOwner? __state){activating=__state;}
 static void Exception(Il2CppSystem.Exception __0)=>Record(__0?.ToString()??"null exception");
 static void Error(string __0,Il2CppReferenceArray<Il2CppSystem.Object> __1){string message=__0;if(__1!=null)foreach(var arg in __1)message+="\n"+arg?.ToString();Record(message);}
 static void Record(string message){if(activating==null||!activating.Role.ToString().StartsWith("infected",StringComparison.OrdinalIgnoreCase))return;File.AppendAllText(Path.Combine(home,"infection-activation-errors.jsonl"),JsonSerializer.Serialize(new{utc=DateTime.UtcNow,role=activating.Role.ToString(),error=message})+Environment.NewLine);}
}
