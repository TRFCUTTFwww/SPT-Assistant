using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using RaidLimits = Il2CppSystem.Collections.Generic.Dictionary<string, int>;

namespace LocalSpt5Esp;

internal static class Smuggler
{
    internal static void Initialize(Plugin plugin)
    {
        plugin.Patches.Patch(AccessTools.DeclaredMethod(typeof(InventoryController), nameof(InventoryController.CheckInRaidItemsRestrictions)),
            prefix: new HarmonyMethod(typeof(Smuggler), nameof(UseEmptyCarryLimits)),
            finalizer: new HarmonyMethod(typeof(Smuggler), nameof(RestoreCarryLimits)));
        plugin.Patches.Patch(AccessTools.DeclaredMethod(typeof(Player.PlayerInventoryController), nameof(Player.PlayerInventoryController.HasDiscardLimit)),
            prefix: new HarmonyMethod(typeof(Smuggler), nameof(CheckDiscardLimit)));
        plugin.Patches.Patch(AccessTools.DeclaredMethod(typeof(Player.PlayerInventoryController), nameof(Player.PlayerInventoryController.GetItemsOverDiscardLimit)),
            prefix: new HarmonyMethod(typeof(Smuggler), nameof(KeepDiscardedItems)));
        plugin.Log.LogInfo("Smuggler hooks ready: carry limits, discard checks and native over-limit destruction list");
    }

    private static bool LocalController(Player.PlayerInventoryController controller) => Enabled && ItemSpawner.InRaid() &&
        Singleton<GameWorld>.Instance?.MainPlayer?.InventoryController is { } local && local.Pointer == controller.Pointer;

    private static bool Enabled => MiscPanel.Options.Smuggler && ChatFikaSession.Role == 0;

    private static void UseEmptyCarryLimits(InventoryController __instance, out RaidLimits? __state)
    {
        __state = null;
        if (!Enabled || __instance._restrictionsInRaid is not { Count: > 0 } original)
            return;
        __instance._restrictionsInRaid = new RaidLimits();
        __state = original;
    }

    private static void RestoreCarryLimits(InventoryController __instance, RaidLimits? __state)
    {
        if (__state != null)
            __instance._restrictionsInRaid = __state;
    }

    // Throw/DestroyOverLimit enumerates this independently of HasDiscardLimit.
    // Bypass the whole list, including restricted children inside a dropped bag,
    // before native enumeration can consume quotas or mark items for destruction.
    private static bool KeepDiscardedItems(Player.PlayerInventoryController __instance,
        ref Il2CppSystem.Collections.Generic.IEnumerable<ItemsCount> __result)
    {
        if (!LocalController(__instance)) return true;
        __result = new Il2CppSystem.Collections.Generic.List<ItemsCount>();
        return false;
    }

    private static bool CheckDiscardLimit(Player.PlayerInventoryController __instance, Item __0, ref int __1, ref bool __result)
    {
        if (__0 == null || __0.SpawnedInSession || !LocalController(__instance))
            return true;
        __1 = 0;
        __result = false;
        return false;
    }
}
