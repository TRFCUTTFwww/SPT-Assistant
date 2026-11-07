using System;
using EFT;
using HarmonyLib;
namespace LocalSpt5Esp;
internal static class ChatSelectionGuard {
 static readonly ChatSelectionGate switching=new();
 static long prevented;
 internal static void Initialize(Plugin plugin){
  plugin.Patches.Patch(AccessTools.Method(typeof(SocialNetwork),"SetDialogueSelected"),prefix:new HarmonyMethod(typeof(ChatSelectionGuard),nameof(Before)),finalizer:new HarmonyMethod(typeof(ChatSelectionGuard),nameof(After)));
  plugin.Log.LogInfo("Chat selection guard ready: preserve the requested conversation during native list rebuilds");
 }
 static bool Before(SocialNetwork __instance,bool __1,out bool __state){
  __state=false;
  if(!__1)return true;
  // The native method emits OnDialogueSelectedGlobal before assigning
  // SelectedDialogue. Rebuilt rows can reselect the old dialogue in that event.
  if(!switching.Enter(__instance.Pointer)){prevented++;return false;}
  __state=true;return true;
 }
 static void After(SocialNetwork __instance,bool __state){if(__state)switching.Exit(__instance.Pointer);}
 internal static object Diagnostics()=>new{prevented,switching=switching.Count};
}
