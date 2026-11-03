using System;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class ToyFireControl {
 internal static ConfigEntry<bool> Automatic=null!,CustomRate=null!;internal static ConfigEntry<int> Rpm=null!;
 static Weapon? gun;static WeaponTemplate? original,copy;static IFireModeComponentTemplate? modeTemplate;static Weapon.EFireMode originalMode;static IntPtr world;static float nextWarning;static bool appliedAuto;static int appliedRate=-1;
 internal static bool Active=>Automatic!=null&&(Automatic.Value||CustomRate.Value);
 internal static void Initialize(){const string section="50. 玩具枪工具";Automatic=FeatureConfig.Bind(section,"Toy automatic fire","玩具枪连发（按住开火）",false,"为当前手持玩具枪启用原生自动射击；松开开火键停止，使用原有装填弹药。");CustomRate=FeatureConfig.Bind(section,"Toy custom fire rate","玩具枪自定义射速",false,"只修改当前手持玩具枪；关闭后恢复原射速。");Rpm=FeatureConfig.Bind(section,"Toy rounds per minute","玩具枪射速（发／分钟）",600,"实际速度受游戏帧率与原生开火动画限制；不影响工具操作间隔。",new AcceptableValueRange<int>(60,3000));}
 static void Restore(){try{if(gun!=null&&copy!=null&&gun.Template?.Pointer==copy.Pointer){gun._Template_k__BackingField=original!;if(gun.FireMode is {} mode){mode._template=modeTemplate!;mode.SetFireMode(originalMode);}}}catch(Exception e){Plugin.Self.Log.LogWarning("Restore toy firing: "+e.Message);}finally{gun=null;original=copy=null;modeTemplate=null;appliedRate=-1;}}
 internal static void Reset(){Restore();world=IntPtr.Zero;}
 internal static void Tick(GameWorld? current,Player? player){
  try{
   if(current==null||player?.HealthController?.IsAlive!=true){Reset();return;}if(world!=current.Pointer){Reset();world=current.Pointer;}
   var controller=player.HandsController?.TryCast<Player.FirearmController>();var held=controller?.Item;
   if(!Active||held?.StringTemplateId!=ToyDeveloper.Toy){Restore();return;}
   if(gun?.Pointer!=held.Pointer||copy==null||held.Template.Pointer!=copy.Pointer){Restore();gun=held;original=held.Template;originalMode=held.SelectedFireMode;modeTemplate=held.FireMode?._template;copy=new WeaponTemplate(original.MemberwiseClone().Pointer);if(copy.Pointer==original.Pointer)throw new InvalidOperationException("玩具枪射击模板隔离失败。");held._Template_k__BackingField=copy;}
   int rate=CustomRate.Value?Rpm.Value:0;if(appliedRate!=rate||appliedAuto!=Automatic.Value){copy.bFirerate=rate>0?rate:original!.bFirerate;copy.SingleFireRate=rate>0?rate:original!.SingleFireRate;var modes=original!.weapFireType.ToArray();copy.weapFireType=Automatic.Value?new Il2CppStructArray<Weapon.EFireMode>(modes.Append(Weapon.EFireMode.fullauto).Distinct().ToArray()):original.weapFireType;appliedRate=rate;appliedAuto=Automatic.Value;}
   if(held.FireMode is {} fireMode){fireMode._template=copy.Cast<IFireModeComponentTemplate>();var desired=Automatic.Value?Weapon.EFireMode.fullauto:originalMode;if(fireMode.FireMode!=desired){fireMode.SetFireMode(desired);controller!.ChangeFireMode(desired);}}
  }catch(Exception e){Restore();if(Time.unscaledTime>=nextWarning){nextWarning=Time.unscaledTime+10;Plugin.Self.Log.LogWarning("Toy firing: "+e);}}
 }
}
