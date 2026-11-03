using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using EFT;
using EFT.Airdrop;
using UnityEngine;
using AirdropParameters=JsonType.LocationSettings.Location.AirdropParameters;
namespace LocalSpt5Esp;
internal static partial class AirdropControl {
 static ConfigEntry<bool> disabled=null!;static ConfigEntry<KeyCode> disabledKey=null!;
 static readonly Dictionary<IntPtr,(AirdropParameters Parameters,float Chance)> originalChances=new();
 static readonly Dictionary<IntPtr,(ServerAirdropManager Manager,bool Enabled)> originalManagers=new();
 internal static bool Disabled=>disabled!=null&&disabled.Value;
 static void InitializeSuppression(Plugin plugin){
  disabled=FeatureConfig.Bind("38. 空投控制","Disable airdrops","关闭空投",false,"将本图随机投放概率设为 0，停用本局空投管理器；关闭此开关后恢复原值。已在途飞机和落地箱不强行移除。");
  disabledKey=FeatureConfig.Bind("38. 空投控制","Disable airdrops key","关闭空投切换键",KeyCode.None);
  plugin.Log.LogInfo("Airdrop switch ready: reversible runtime probability=0 and native manager flag; no airdrop detours");
 }
 static void RestorePlanes(){foreach(var entry in originalChances.Values)try{entry.Parameters.PlaneAirdropChance=entry.Chance;}catch{}originalChances.Clear();foreach(var entry in originalManagers.Values)try{entry.Manager._airdropsEnabled=entry.Enabled;}catch{}originalManagers.Clear();}
 static void Zero(AirdropParameters? parameters){if(parameters==null)return;if(!originalChances.ContainsKey(parameters.Pointer))originalChances[parameters.Pointer]=(parameters,parameters.PlaneAirdropChance);parameters.PlaneAirdropChance=0;}
 static void TickSuppression(){
  if(!InterfaceMenu.InputBlocked&&disabledKey.Value!=KeyCode.None&&Input.GetKeyDown(disabledKey.Value))disabled.Value=!disabled.Value;
  if(!Disabled){RestorePlanes();return;}var manager=Manager();if(manager==null)return;
  if(!originalManagers.ContainsKey(manager.Pointer))originalManagers[manager.Pointer]=(manager,manager._airdropsEnabled);
  Zero(manager.CachedAirdropParameters);var settings=manager.AirdropParameters;if(settings!=null)foreach(var parameter in settings)Zero(parameter);
  manager._airdropsEnabled=false;
 }
}
