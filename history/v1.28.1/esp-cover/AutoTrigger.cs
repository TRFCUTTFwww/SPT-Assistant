using System;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.CameraControl;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class AutoTrigger {
 static ConfigEntry<bool> enabled=null!,allies=null!;static ConfigEntry<KeyCode> hold=null!;
 static ConfigEntry<float> delay=null!,interval=null!,range=null!;
 static readonly TriggerGate gate=new();static readonly TriggerPulse pulse=new();
 static Player.FirearmController? owned;static IntPtr world,drivenController;static int drivenFrame=-1,manualFrame=-1;
 [ThreadStatic] static bool driving;
 static float nextError,lastRequest;static string status="已关闭",requestedWeapon="";
 static long checks,candidates,requests,confirmed;static int lastShot=-1;static string lastShotWeapon="";
 internal static string Diagnostics=>$"trigger checks={checks}, candidates={candidates}, requests={requests}, shots={confirmed}, state={status}; probe: {CrosshairProbe.Diagnostic}";
 internal static bool LauncherRequested(Player.FirearmController controller)=>InputAllowed&&owned?.Pointer==controller.Pointer&&pulse.IsDown(Time.frameCount);
 internal static void Initialize(Plugin plugin){
  const string section="44. 自动扳机";
  enabled=FeatureConfig.Bind(section,"Auto trigger","自动扳机",false,"准星实际指向允许的存活 AI 时自动扣动扳机，不转动视角；共用自瞄排除规则。");
  hold=FeatureConfig.Bind(section,"Trigger hold key","扳机按住键",KeyCode.None,"None 表示开启后持续检测；可设为侧键等按住触发。");
  allies=FeatureConfig.Bind(section,"Exclude allies","跳过友军",true);
  delay=FeatureConfig.Bind(section,"Trigger delay","瞄准停留时间（秒）",.08f,"准星连续停留在同一名 AI 上才开火。",new AcceptableValueRange<float>(0,2));
  interval=FeatureConfig.Bind(section,"Trigger interval","扳机间隔（秒）",.12f,"正常武器射速仍然有效。",new AcceptableValueRange<float>(.02f,3));
  range=FeatureConfig.Bind(section,"Trigger distance","扳机检测距离（米）",300f,"",new AcceptableValueRange<float>(1,2000));
  plugin.Patches.Patch(typeof(Player.FirearmController).GetMethod("ManualUpdate",new[]{typeof(float)}),prefix:new HarmonyMethod(typeof(AutoTrigger),nameof(BeforeWeaponUpdate)));
  plugin.Patches.Patch(typeof(Player.FirearmController).GetMethod("SetTriggerPressed",new[]{typeof(bool)}),prefix:new HarmonyMethod(typeof(AutoTrigger),nameof(MergeInput)));
  plugin.Log.LogInfo("Automatic trigger ready: native weapon-update driver and input merge; one full weapon frame pressed, one released");
 }
 static bool Manual=>Input.GetMouseButton(0)||manualFrame==Time.frameCount;
 static bool InputAllowed=>enabled.Value&&!InterfaceMenu.InputBlocked&&!Cursor.visible&&!Manual&&(hold.Value==KeyCode.None||Input.GetKey(hold.Value));
 static bool Local(Player.FirearmController controller)=>controller._player?.IsYourPlayer==true&&Singleton<GameWorld>.Instance?.MainPlayer?.Pointer==controller._player.Pointer&&controller._player.HealthController?.IsAlive==true&&RaidClock.Active;
 static void ReleaseOwned(){var previous=owned;owned=null;if(previous==null||Manual)return;try{driving=true;previous.SetTriggerPressed(false);}catch{}finally{driving=false;}}
 static void Yield(string reason){status=reason;pulse.Reset();gate.Ready("",Time.unscaledTime,0,1);ReleaseOwned();}
 internal static void Reset(){ReleaseOwned();pulse.Reset();gate.Reset();world=drivenController=IntPtr.Zero;drivenFrame=manualFrame=-1;requestedWeapon="";}
 static bool Ally(Player target,Player local){var bot=target.AIData?.BotOwner;if(bot==null)return false;var rule=AiActorControl.Relation(bot,local);if(rule.HasValue)return !rule.Value;if(AiActorControl.Ignored(bot,local))return true;var group=bot.BotsGroup;return group!=null&&(group.Pointer==AiTeamControl.Group(local)?.Pointer||group.Allies.ContainsKey(local));}
 [HarmonyPriority(Priority.First)]
 static void MergeInput(Player.FirearmController __instance,ref bool __0){
  if(driving||!Local(__instance))return;
  if(__0){if(Input.GetMouseButton(0)||!pulse.IsDown(Time.frameCount)){manualFrame=Time.frameCount;if(owned?.Pointer==__instance.Pointer)owned=null;pulse.Reset();}return;}
  if(InputAllowed&&owned?.Pointer==__instance.Pointer&&pulse.IsDown(Time.frameCount)&&!__instance._isInventoryOpened&&!__instance.IsInReloadOperation())__0=true;
 }
 [HarmonyPriority(Priority.First)]
 static void BeforeWeaponUpdate(Player.FirearmController __instance){
  if(__instance._player is {} actor)EntertainmentControl.RestoreForLogic(actor);
  if(!Local(__instance))return;
  if(drivenFrame==Time.frameCount&&drivenController==__instance.Pointer)return;
  drivenFrame=Time.frameCount;drivenController=__instance.Pointer;
  try{
   var current=Singleton<GameWorld>.Instance!;var local=current.MainPlayer;var camera=CameraManager.Instance?.Camera??Camera.main;
   if(world!=current.Pointer){Reset();world=current.Pointer;drivenFrame=Time.frameCount;drivenController=__instance.Pointer;}
   if(!InputAllowed){Yield(!enabled.Value?"已关闭":Manual?"手动开火优先":"等待恢复游戏操作");return;}
   if(camera==null){Yield("相机尚未就绪");return;}
   if(__instance._isInventoryOpened||__instance.IsInReloadOperation()){Yield("换弹／背包操作中");return;}
   checks++;var hit=CrosshairProbe.Read(current,local,camera,range.Value);var target=hit.Actor;
   if(target==null){Yield(hit.State==CrosshairState.Blocked?"准星前有遮挡":hit.State==CrosshairState.Overflow?"准星碰撞过多，暂不触发":"等待准星指向 AI");return;}
   candidates++;if(!AimTargetFilter.Allowed(target)){Yield("目标已被排除规则过滤");return;}
   if(allies.Value&&Ally(target,local)){Yield("已跳过友军");return;}
   bool canAcquire=pulse.CanAcquire(Time.frameCount);
   if(canAcquire&&gate.Ready(target.ProfileId,Time.unscaledTime,delay.Value,interval.Value)){
    if(!__instance.CanPressTrigger()){Yield("武器尚未允许开火");return;}
    pulse.Press(Time.frameCount);requests++;lastRequest=Time.unscaledTime;requestedWeapon=__instance.Item?.Id??"";
   }
   bool press=pulse.IsDown(Time.frameCount);status=(press?"正在触发 · ":"瞄准等待／扳机间隔 · ")+(target.Profile?.Nickname??"AI");
   if(press){owned=__instance;try{driving=true;__instance.SetTriggerPressed(true);}finally{driving=false;}}
   else ReleaseOwned();
  }catch(Exception error){Yield("触发暂不可用");if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+15;Plugin.Self.Log.LogWarning("Auto trigger: "+error);}}
 }
 internal static void Tick(GameWorld? current,Player? local,Camera? camera){
  if(current==null||local?.HealthController?.IsAlive!=true){if(world!=IntPtr.Zero)Reset();status=enabled.Value?"进入战局后可用":"已关闭";return;}
  if(!InputAllowed)Yield(!enabled.Value?"已关闭":Manual?"手动开火优先":"等待恢复游戏操作");
 }
 internal static void OnPlayerShot(Shot shot){
  if(shot==null||shot.Parent!=null||shot.Weapon==null||Time.unscaledTime-lastRequest>.3f||shot.Weapon.Id!=requestedWeapon)return;
  var player=Singleton<GameWorld>.Instance?.MainPlayer;if(player==null||shot.PlayerRaidId!=player.RaidId)return;
  string weapon=shot.Weapon.Id;if(lastShot==shot.FireIndex&&lastShotWeapon==weapon)return;lastShot=shot.FireIndex;lastShotWeapon=weapon;confirmed++;
 }
 internal static void DrawStatus(Rect rect)=>InterfaceMenu.Paragraph(rect,"自动扳机："+status,InterfaceMenu.Accent,13);
}
