using System;
using EFT;
using HarmonyLib;
using Il2CppSystems.Effects;

namespace LocalSpt5Esp;

internal static class BleedingDecalGuard
{
    internal static void Initialize(Plugin plugin)
    {
        var update = AccessTools.DeclaredMethod(typeof(EffectsCommutator), "UpdatePlayersBleedings", Type.EmptyTypes)
            ?? throw new MissingMethodException(typeof(EffectsCommutator).FullName, "UpdatePlayersBleedings");
        plugin.Patches.Patch(update, prefix: new HarmonyMethod(typeof(BleedingDecalGuard), nameof(RemoveStaleDecals)));
        plugin.Log.LogInfo("Bleeding decals: stale player transform guard ready.");
    }

    static void RemoveStaleDecals(EffectsCommutator __instance)
    {
        var tracked = __instance._playerBleedingDecalTimes;
        if (tracked == null)
            return;

        for (int i = tracked.Count - 1; i >= 0; i--)
        {
            // BifacialTransform reads the cached position, an imitation delegate, or the original Transform.
            var transform = tracked[i].Key?.Transform;
            if (transform == null || (!transform.AccumulatePositionAndRotation
                && (transform.UseImitation ? transform._positionImitator == null : transform.Original == null)))
                tracked.RemoveAt(i);
        }
    }
}
