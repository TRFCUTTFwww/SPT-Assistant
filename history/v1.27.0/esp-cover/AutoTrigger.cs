using System;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class AutoTrigger {
 static ConfigEntry<bool> enabled=null!,allies=null!;static ConfigEntry<KeyCode> hold=null!;
 static ConfigEntry<float> delay=null!,interval=null!,range=null!;
 static readonly TriggerGate gate=new();static Player.FirearmController? pressed;static IntPtr world;static float nextError;
 internal static void Initialize(){
  const string section="44. 自动扳机";
  enabled=FeatureConfig.Bind(section,"Auto trigger","自动扳机",false,"准星实际指向允许的存活 AI 时自动扣动扳机，不转动视角；共用自瞄排除规则。");
  hold=FeatureConfig.Bind(section,"Trigger hold key","扳机按住键",KeyCode.None,"None 表示开启后持续检测；可设为侧键等按住触发。");
  allies=FeatureConfig.Bind(section,"Exclude allies","跳过友军",true);
  delay=FeatureConfig.Bind(section,"Trigger delay","瞄准停留时间（秒）",.08f,"准星连续停留在同一名 AI 上才开火。",new AcceptableValueRange<float>(0,2));
  interval=FeatureConfig.Bind(section,"Trigger interval","扳机间隔（秒）",.12f,"正常武器射速仍然有效。",new AcceptableValueRange<float>(.02f,3));
  range=FeatureConfig.Bind(section,"Trigger distance","扳机检测距离（米）",300f,"",new AcceptableValueRange<float>(1,2000));
 }
 static void Release(){var prior=pressed;pressed=null;if(prior!=null&&!Input.GetMouseButton(0))try{prior.SetTriggerPressed(false);}catch{}}
 internal static void Reset(){Release();gate.Reset();world=IntPtr.Zero;}
 static bool Ally(Player target,Player local){var bot=target.AIData?.BotOwner;if(bot==null)return false;var rule=AiActorControl.Relation(bot,local);if(rule.HasValue)return !rule.Value;var group=bot.BotsGroup;return group!=null&&(group.Pointer==AiTeamControl.Group(local)?.Pointer||group.Allies.ContainsKey(local));}
 internal static void Tick(GameWorld? current,Player? local,Camera? camera){
  Release();
  try{
   if(current==null||local?.IsYourPlayer!=true||local.HealthController?.IsAlive!=true||camera==null||!enabled.Value||InterfaceMenu.InputBlocked||Cursor.visible||Input.GetMouseButton(0)||hold.Value!=KeyCode.None&&!Input.GetKey(hold.Value)){gate.Ready("",Time.unscaledTime,0,1);return;}
   if(world!=current.Pointer){gate.Reset();world=current.Pointer;}
   var controller=local.HandsController?.TryCast<Player.FirearmController>();
   if(controller==null||controller.IsTriggerPressed||controller._isInventoryOpened||!controller.SuitableForHandInput||controller.IsInReloadOperation()||!controller.CanPressTrigger()){gate.Ready("",Time.unscaledTime,0,1);return;}
   var target=CrosshairProbe.Target(current,local,camera,range.Value,out _);
   if(target!=null&&(!AimTargetFilter.Allowed(target)||allies.Value&&Ally(target,local)))target=null;
   if(!gate.Ready(target?.ProfileId??"",Time.unscaledTime,delay.Value,interval.Value))return;
   controller.SetTriggerPressed(true);pressed=controller;
  }catch(Exception error){gate.Reset();if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+15;Plugin.Self.Log.LogWarning("Auto trigger: "+error.Message);}}
 }
}
