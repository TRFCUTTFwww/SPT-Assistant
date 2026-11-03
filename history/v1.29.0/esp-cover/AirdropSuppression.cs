using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using EFT;
using EFT.Airdrop;
using EFT.SynchronizableObjects;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class AirdropControl {
 static ConfigEntry<bool> disabled=null!;static ConfigEntry<KeyCode> disabledKey=null!;
 static readonly Dictionary<IntPtr,(GameObject Object,bool Active)> hiddenPlanes=new();static float nextPlaneScan;
 internal static bool Disabled=>disabled!=null&&disabled.Value;
 static bool AllowAirdrop()=>!Disabled;
 static void InitializeSuppression(Plugin plugin){
  disabled=FeatureConfig.Bind("38. 空投控制","Disable airdrops","关闭空投",false,"禁止后续随机、信号弹和手动空投；隐藏并暂停在途飞机，落地箱保留。关闭开关后恢复。");
  disabledKey=FeatureConfig.Bind("38. 空投控制","Disable airdrops key","关闭空投切换键",KeyCode.None);
  foreach(var type in new[]{typeof(ServerAirdropManager),typeof(ServerPlane)})plugin.Patches.Patch(type.GetMethod("ManualUpdate",new[]{typeof(float)}),prefix:new HarmonyMethod(typeof(AirdropControl),nameof(AllowAirdrop)));
  plugin.Patches.Patch(typeof(ServerAirdropManager).GetMethod("FlareSuccessEventHandler"),prefix:new HarmonyMethod(typeof(AirdropControl),nameof(AllowAirdrop)));plugin.Patches.Patch(typeof(GameWorld).GetMethod("InitAirdrop"),prefix:new HarmonyMethod(typeof(AirdropControl),nameof(AllowAirdrop)));
 }
 static void RestorePlanes(){foreach(var entry in hiddenPlanes.Values)try{if(entry.Object!=null)entry.Object.SetActive(entry.Active);}catch{}hiddenPlanes.Clear();nextPlaneScan=0;}
 static void TickSuppression(){
  if(!InterfaceMenu.InputBlocked&&disabledKey.Value!=KeyCode.None&&Input.GetKeyDown(disabledKey.Value))disabled.Value=!disabled.Value;
  if(!Disabled){RestorePlanes();return;}if(Time.unscaledTime<nextPlaneScan)return;nextPlaneScan=Time.unscaledTime+1;
  foreach(var plane in UnityEngine.Object.FindObjectsOfType<AirplaneSynchronizableObject>())if(plane!=null){var obj=plane.gameObject;if(!hiddenPlanes.ContainsKey(obj.Pointer))hiddenPlanes[obj.Pointer]=(obj,obj.activeSelf);obj.SetActive(false);}
 }
}
