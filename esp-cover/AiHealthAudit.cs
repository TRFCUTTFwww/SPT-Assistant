using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.Ballistics;
using EFT.HealthSystem;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
// Read-only, rate-limited evidence for AI hits. Distinguishes protection, native
// damage suppression and armor hits without inventing damage or changing health.
internal static class AiHealthAudit {
 internal sealed record Snapshot(string Key,string Attacker,string Victim,float Before,float Requested);
 static readonly Dictionary<string,float> next=new();
 internal static void Initialize(Plugin plugin){next.Clear();var damage=typeof(ActiveHealthController).GetMethods().Single(m=>m.DeclaringType==typeof(ActiveHealthController)&&m.Name=="ApplyDamage");plugin.Patches.Patch(damage,prefix:new HarmonyMethod(typeof(AiHealthAudit),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(AiHealthAudit),nameof(AfterDamage)));plugin.Patches.Patch(typeof(Player).GetMethods().Single(m=>m.DeclaringType==typeof(Player)&&m.Name=="ApplyShot"),prefix:new HarmonyMethod(typeof(AiHealthAudit),nameof(BeforeShot)),postfix:new HarmonyMethod(typeof(AiHealthAudit),nameof(AfterShot)));}
 static Snapshot? Capture(Player? victim,DamageInfo? info,string stage,float requested){var attacker=info?.Player?.iPlayer;if(victim==null||attacker==null||victim.IsYourPlayer||attacker.IsYourPlayer||victim.AIData?.BotOwner==null||attacker.AIData?.BotOwner==null)return null;string key=stage+":"+attacker.ProfileId+":"+victim.ProfileId;if(next.GetValueOrDefault(key)>Time.unscaledTime)return null;if(next.Count>256)next.Clear();next[key]=Time.unscaledTime+3;var health=victim.ActiveHealthController;return health==null?null:new(key,attacker.ProfileId,victim.ProfileId,health.GetBodyPartHealth(EBodyPart.Common,false).Current,requested);}
 [HarmonyPriority(Priority.First)]static void BeforeDamage(ActiveHealthController __instance,float __1,DamageInfo __2,out Snapshot? __state){__state=null;try{__state=Capture(__instance.Player,__2,"health",__1);}catch{}}
 static void AfterDamage(ActiveHealthController __instance,Snapshot? __state,bool __runOriginal){Report(__instance,__state,__runOriginal);}
 [HarmonyPriority(Priority.First)]static void BeforeShot(Player __instance,DamageInfo __0,out Snapshot? __state){__state=null;try{__state=Capture(__instance,__0,"impact",__0?.Damage??0);}catch{}}
 static void AfterShot(Player __instance,Snapshot? __state,bool __runOriginal){try{Report(__instance.ActiveHealthController,__state,__runOriginal);}catch{}}
 static void Report(ActiveHealthController? health,Snapshot? state,bool original){if(state==null||health==null)return;try{float lost=Math.Max(0,state.Before-health.GetBodyPartHealth(EBodyPart.Common,false).Current);Plugin.Self.Log.LogInfo($"AI health audit: stage={state.Key.Split(':')[0]}; attacker={state.Attacker}; victim={state.Victim}; requested={state.Requested:F2}; lost={lost:F2}; original={original}; assistantProtection={Plugin.DamageBlocked(health)}; damageCoeff={health.DamageCoeff}; multiplier={health.DamageMultiplier}; companion={CompanionSquad.Owns(health.Player)}");}catch{}}
}
