using System;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class SurvivalAssist {
 static ConfigEntry<bool> water=null!,food=null!,weight=null!;static ConfigEntry<KeyCode> healKey=null!;
 static PhysicalBase? previous;static bool encumberWasDisabled;static float nextTick,nextError;
 internal static string Notice="";
 internal static void Initialize(Plugin p){water=FeatureConfig.Bind("29. 生存便利","Lock hydration","锁定水分为满值",false);food=FeatureConfig.Bind("29. 生存便利","Lock energy","锁定能量为满值",false);weight=FeatureConfig.Bind("29. 生存便利","Ignore weight","忽略负重对移动和体力的影响",false);healKey=FeatureConfig.Bind("29. 生存便利","Heal key","一键治疗快捷键（Ctrl + 按键）",KeyCode.H);try{p.Patches.Patch(typeof(PhysicalBase).GetMethod("UpdateInternalParameters",new[]{typeof(float)}),prefix:new HarmonyMethod(typeof(SurvivalAssist),nameof(BeforeWeight)));}catch(Exception e){p.Log.LogError("Local weight hook failed: "+e);}}
 static void BeforeWeight(PhysicalBase __instance,ref float __0){if(!weight.Value||!RaidClock.Active)return;var local=Singleton<GameWorld>.Instance?.MainPlayer?.Physical;if(local!=null&&local.Pointer==__instance.Pointer)__0=0;}
 internal static void Heal(){try{var player=Singleton<GameWorld>.Instance?.MainPlayer;if(!RaidClock.Active||player?.ActiveHealthController==null||!player.HealthController.IsAlive)throw new ArgumentException("需要进入战局且角色存活。");var health=player.ActiveHealthController;foreach(var part in new[]{EBodyPart.Head,EBodyPart.Chest,EBodyPart.Stomach,EBodyPart.LeftArm,EBodyPart.RightArm,EBodyPart.LeftLeg,EBodyPart.RightLeg}){health.RemoveNegativeEffects(part);health.FullRestoreBodyPart(part);}health.RemoveNegativeEffects(EBodyPart.Common);health.RestoreFullHealth();health.ChangeHydration(health.Hydration.Maximum-health.Hydration.Current);health.ChangeEnergy(health.Energy.Maximum-health.Energy.Current);health.RemoveIntoxication();Notice="已恢复生命值、水分和能量，并清除负面状态。";}catch(Exception e){Notice=e.Message;}}
 internal static void Tick(Player? player){if(!RaidClock.Active||player==null){Reset();return;}if(Hotkeys.Pressed(healKey.Value))Heal();if(Time.unscaledTime<nextTick)return;nextTick=Time.unscaledTime+.25f;try{var h=player.ActiveHealthController;if(h!=null){if(water.Value)h.ChangeHydration(h.Hydration.Maximum-h.Hydration.Current);if(food.Value)h.ChangeEnergy(h.Energy.Maximum-h.Energy.Current);}var physical=player.Physical;if(weight.Value&&physical!=null){if(previous==null||previous.Pointer!=physical.Pointer){Reset();previous=physical;encumberWasDisabled=physical.EncumberDisabled;}physical.EncumberDisabled=true;physical.UpdateInternalParameters(0);}else Reset();}catch(Exception e){if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+20;Plugin.Self.Error(e);}}}
 internal static void Reset(){if(previous!=null)try{previous.EncumberDisabled=encumberWasDisabled;previous.MarkWeightAsDirty();previous.OnWeightUpdated();}catch{}previous=null;}
 internal static void DrawActions(Rect rect){if(InterfaceMenu.Button(new Rect(rect.x,rect.y,180,34),"一键治疗 · Ctrl+"+healKey.Value))Heal();InterfaceMenu.Paragraph(new Rect(rect.x+195,rect.y,Math.Max(50,rect.width-195),48),Notice,null,12);}
}
