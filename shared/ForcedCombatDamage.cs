using System;
using EFT;
using EFT.Ballistics;
using HarmonyLib;

namespace LocalAiCombat;

// EFT's AI miss controller can discard an actual collision before armor/health
// processing solely because the bots have the same role or native squad.
internal static class ForcedCombatDamage {
    static Func<BotOwner,IPlayer,bool>? hostile;
    internal static void Install(Action<System.Reflection.MethodBase,HarmonyMethod> patch,Func<BotOwner,IPlayer,bool> rule) { hostile=rule; var m=typeof(BotEnemiesMissController).GetMethod("ShouldApplyDamage",new[]{typeof(Player),typeof(DamageInfo),typeof(EBodyPart)})??throw new MissingMethodException("BotEnemiesMissController.ShouldApplyDamage"); patch(m,new HarmonyMethod(typeof(ForcedCombatDamage),nameof(Before))); }
    internal static void Install(Harmony harmony,Func<BotOwner,IPlayer,bool> rule) {
        hostile=rule;
        var method=typeof(BotEnemiesMissController).GetMethod("ShouldApplyDamage",new[]{typeof(Player),typeof(DamageInfo),typeof(EBodyPart)})
            ??throw new MissingMethodException("BotEnemiesMissController.ShouldApplyDamage");
        harmony.Patch(method,prefix:new HarmonyMethod(typeof(ForcedCombatDamage),nameof(Before)));
    }
    internal static bool Before(BotEnemiesMissController __instance,Player __0,ref bool __result) {
        var owner=__instance._botEnemies?._owner;
        var actor=owner?.GetPlayer;
        if(actor==null||__0==null||actor.IsYourPlayer||__0.IsYourPlayer||!actor.IsAI||!__0.IsAI||actor.Pointer==__0.Pointer||__0.AIData?.BotOwner==null||hostile?.Invoke(owner!,__0)!=true)return true;
        // This only permits the real hit to continue. Native armor, damage
        // coefficients, health, and explicit invincibility still run normally.
        __result=true;
        return false;
    }
}
