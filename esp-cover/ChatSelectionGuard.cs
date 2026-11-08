using System;
using EFT;
using ChatShared;
using EFT.UI.Chat;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class ChatSelectionGuard {
 static readonly ChatSelectionGate switching=new();
 static long prevented,selections;
 internal static void Initialize(Plugin plugin){
  plugin.Patches.Patch(AccessTools.Method(typeof(SocialNetwork),"SetDialogueSelected"),prefix:new HarmonyMethod(typeof(ChatSelectionGuard),nameof(Before)));
  plugin.Log.LogInfo("Chat selection guard ready: commit selected dialogue before native view callbacks");
 }
 static bool Before(SocialNetwork __instance,UpdatableChatDialogue __0,bool __1){
  if(!__1)return true;
  selections++;
  if(!switching.Select(__instance.Pointer,__instance.SelectedDialogue,__0,
   next=>{__instance.ReplyToMessage=null;__instance.SelectedDialogue=next;},
   (item,selected)=>item.SelectDialogue(selected),
   next=>__instance.OnDialogueSelectedGlobal?.Invoke(next)))prevented++;
  return false;
 }
 internal static object Diagnostics(){
  var rows=new List<object>();
  foreach(var view in UnityEngine.Object.FindObjectsOfType<DialogueView>()){
   var network=view._social;if(network==null)continue;
   rows.Add(new{dialog=view._dialogue?._id,current=network.SelectedDialogue?._id,selected=view._selected,toggle=view._dialogueToggle?.isOn});
   if(rows.Count>=40)break;
  }
  return new{selections,prevented,switching=switching.Count,rows};
 }
}
