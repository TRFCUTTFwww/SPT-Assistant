using System;
using BepInEx.Configuration;
using Comfort.Common;
using HarmonyLib;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class ToyFireControl {
 internal static ConfigEntry<bool> Automatic=null!,CustomRate=null!;internal static ConfigEntry<int> Rpm=null!;
 static Weapon? gun;static WeaponTemplate? original,copy;static IntPtr world;static float nextWarning;static int appliedRate=-1;
 static readonly ToyRepeatGate repeat=new();static Player.FirearmController? driven;static int drivenFrame=-1;[ThreadStatic]static bool driving;
 internal static bool Active=>Automatic!=null&&(Automatic.Value||CustomRate.Value);
 internal static void Initialize(Plugin plugin){const string section="50. 玩具枪工具";Automatic=FeatureConfig.Bind(section,"Toy automatic fire","玩具枪连发（按住开火）",false,"按住开火时重复扣动单发扳机；松开停止，保留原武器射击模式与装填流程。");CustomRate=FeatureConfig.Bind(section,"Toy custom fire rate","玩具枪自定义射速",false,"只修改当前手持玩具枪；关闭后恢复原射速。");Rpm=FeatureConfig.Bind(section,"Toy rounds per minute","玩具枪射速（发／分钟）",600,"实际速度受游戏帧率、装填及原生开火动画限制；不影响工具操作间隔。",new AcceptableValueRange<int>(60,3000));plugin.Patches.Patch(typeof(Player.FirearmController).GetMethod("ManualUpdate",new[]{typeof(float)}),prefix:new HarmonyMethod(typeof(ToyFireControl),nameof(BeforeWeaponUpdate)));plugin.Patches.Patch(typeof(Player.FirearmController).GetMethod("SetTriggerPressed",new[]{typeof(bool)}),prefix:new HarmonyMethod(typeof(ToyFireControl),nameof(BeforeTrigger)));}
 static bool Local(Player.FirearmController controller)=>controller._player?.IsYourPlayer==true&&Singleton<GameWorld>.Instance?.MainPlayer?.Pointer==controller._player.Pointer&&controller._player.HealthController?.IsAlive==true&&RaidClock.Active;
 [HarmonyPriority(Priority.High)] static void BeforeWeaponUpdate(Player.FirearmController __instance){if(Local(__instance))Drive(__instance);}
 [HarmonyPriority(Priority.First)] static void BeforeTrigger(Player.FirearmController __instance,ref bool __0){if(Local(__instance))MergeInput(__instance,ref __0);}
 static void Restore(){try{if(gun!=null&&copy!=null&&gun.Template?.Pointer==copy.Pointer)gun._Template_k__BackingField=original!;}catch(Exception e){Plugin.Self.Log.LogWarning("Restore toy firing: "+e.Message);}finally{gun=null;original=copy=null;appliedRate=-1;}}
 static bool InputAllowed=>!WorldEditor.Active&&!DirectorStage.FreeActive&&!InterfaceMenu.InputBlocked&&!Cursor.visible;
 static bool Handles(Player.FirearmController controller)=>Automatic.Value&&controller.Item?.StringTemplateId==ToyDeveloper.Toy&&InputAllowed;
 static void Release(){var previous=driven;driven=null;repeat.Reset();drivenFrame=-1;if(previous==null)return;try{driving=true;previous.SetTriggerPressed(false);}catch{}finally{driving=false;}}
 internal static bool MergeInput(Player.FirearmController controller,ref bool pressed){if(driving||!Handles(controller)||!Input.GetMouseButton(0)||controller._isInventoryOpened||controller.IsInReloadOperation())return false;pressed=driven?.Pointer==controller.Pointer&&repeat.IsDown(Time.frameCount);return true;}
 // Called only for the living local player's concrete firearm update. Keep native single-shot operations intact.
 internal static bool Drive(Player.FirearmController controller){
  if(!Handles(controller)||!Input.GetMouseButton(0)||controller._isInventoryOpened||controller.IsInReloadOperation()){Release();return false;}
  if(driven?.Pointer!=controller.Pointer){Release();driven=controller;}if(drivenFrame==Time.frameCount)return true;drivenFrame=Time.frameCount;
  try{int rpm=CustomRate.Value?Rpm.Value:Math.Max(60,controller.Item.Template.SingleFireRate);bool pressed=repeat.Step(true,Time.frameCount,Time.unscaledTime,rpm,controller.CanPressTrigger());driving=true;controller.SetTriggerPressed(pressed);return true;}
  catch(Exception e){Release();Automatic.Value=false;Plugin.Self.Log.LogWarning("Toy repeat disabled after trigger failure: "+e);FeatureToasts.Show("玩具枪连发已暂停，请检查武器装填状态。");return false;}finally{driving=false;}
 }
 internal static void Reset(){Release();Restore();world=IntPtr.Zero;}
 internal static void Tick(GameWorld? current,Player? player){
  try{
   if(current==null||player?.HealthController?.IsAlive!=true){Reset();return;}if(world!=current.Pointer){Reset();world=current.Pointer;}
   var controller=player.HandsController?.TryCast<Player.FirearmController>();var held=controller?.Item;
   if(!Active||held?.StringTemplateId!=ToyDeveloper.Toy){Release();Restore();return;}
   if(!Automatic.Value||!Input.GetMouseButton(0)||!InputAllowed)Release();
   if(!CustomRate.Value){Restore();return;}
   if(gun?.Pointer!=held.Pointer||copy==null||held.Template.Pointer!=copy.Pointer){Restore();gun=held;original=held.Template;copy=new WeaponTemplate(original.MemberwiseClone().Pointer);if(copy.Pointer==original.Pointer)throw new InvalidOperationException("玩具枪射击模板隔离失败。");held._Template_k__BackingField=copy;}
   int rate=Rpm.Value;if(appliedRate!=rate){copy.bFirerate=rate;copy.SingleFireRate=rate;appliedRate=rate;}
  }catch(Exception e){Restore();if(Time.unscaledTime>=nextWarning){nextWarning=Time.unscaledTime+10;Plugin.Self.Log.LogWarning("Toy firing: "+e);}}
 }
}
