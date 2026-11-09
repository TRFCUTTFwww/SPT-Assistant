using System;
using EFT;
using ChatShared;
using EFT.UI.Chat;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using Comfort.Common;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
namespace LocalSpt5Esp;
internal static class ChatSelectionGuard {
 static readonly ChatSelectionGate switching=new();
 static long prevented,selections,pages,paginationStops;
 internal static void Initialize(Plugin plugin){
  plugin.Patches.Patch(AccessTools.Method(typeof(SocialNetwork),"SetDialogueSelected"),prefix:new HarmonyMethod(typeof(ChatSelectionGuard),nameof(Before)));
  plugin.Patches.Patch(AccessTools.Method(typeof(SocialNetwork),"UpdateDialogueList",new[]{typeof(Result<Il2CppReferenceArray<ChatRoomInformation>>)}),prefix:new HarmonyMethod(typeof(ChatSelectionGuard),nameof(BeforePage)),postfix:new HarmonyMethod(typeof(ChatSelectionGuard),nameof(AfterPage)));
  plugin.Log.LogInfo("Chat selection and pagination guards ready: commit selection before callbacks, stop unpaged/repeated dialogue responses");
 }
 static bool Before(SocialNetwork __instance,UpdatableChatDialogue __0,bool __1){
  if(!__1)return true;
  selections++;
  if(!switching.Select(__instance.Pointer,__instance.SelectedDialogue,__0,
   next=>{__instance.ReplyToMessage=null;__instance.SelectedDialogue=next;},
   (item,selected)=>item.SelectDialogue(selected),
   next=>__instance.OnDialogueSelectedGlobal?.Invoke(next),
   (left,right)=>left.Pointer==right.Pointer))prevented++;
  return false;
 }
 static void BeforePage(SocialNetwork __instance,Result<Il2CppReferenceArray<ChatRoomInformation>> __0,out bool __state){
  __state=false;if(!__0.Succeed||__0.Value==null)return;
  var items=__0.Value;int size=SocialNetwork.MAXIMUM_DIALOGUES_RECEIVE_COUNT;bool hasNew=false;
  if(items.Length==size)foreach(var item in items){if(item!=null&&!__instance.TryGetDialog(item.Id,out var existing)){hasNew=true;break;}}
  __state=ChatPaginationRules.Complete(items.Length,size,hasNew);pages++;
 }
 static void AfterPage(SocialNetwork __instance,bool __state){
  if(!__state)return;
  __instance.FullyRead=true;paginationStops++;
  if(paginationStops<=3)Plugin.Self.Log.LogInfo("Chat dialogue pagination completed: backend returned an unpaged, short or repeated page");
 }
 internal static object Diagnostics(){
  var current=Singleton<TarkovApplication>.Instance?.GetClientBackEndSession()?.SocialNetwork;
  var rows=new List<object>();
  foreach(var view in UnityEngine.Object.FindObjectsOfType<DialogueView>()){
   var network=view._social;if(network==null)continue;
   rows.Add(new{dialog=view._dialogue?._id,current=network.SelectedDialogue?._id,selected=view._selected,toggle=view._dialogueToggle?.isOn});
   if(rows.Count>=40)break;
  }
  return new{selections,prevented,pages,paginationStops,switching=switching.Count,fullyRead=current?.FullyRead,updating=current?.MessageListUpdating,dialogues=current==null?0:current.PinnedDialogues.Count+current.UnpinnedDialogues.Count,rows};
 }
}
