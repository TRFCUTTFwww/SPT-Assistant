using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using HarmonyLib;
using PlayerMap = Il2CppSystem.Collections.Generic.Dictionary<string, EFT.Player>;

namespace LocalSpt5Esp;

internal static class LocalRaidCleanupGuard
{
    internal static void Initialize(Plugin plugin)
    {
        var cleanup = AccessTools.DeclaredMethod(typeof(LocalGame), nameof(LocalGame.CleanUp), Type.EmptyTypes)
            ?? throw new MissingMethodException(typeof(LocalGame).FullName, nameof(LocalGame.CleanUp));
        plugin.Patches.Patch(cleanup, prefix: new HarmonyMethod(typeof(LocalRaidCleanupGuard), nameof(BeforeCleanup)));
        plugin.Log.LogInfo("Local raid cleanup: invalid and duplicate player registry entries are pruned before disposal.");
    }

    // Match the order in LocalGame.CleanUp so each live player is disposed by the native code once.
    static void BeforeCleanup(LocalGame __instance)
    {
        var players = __instance._players ??= new PlayerMap();
        var bots = __instance._bots ??= new PlayerMap();
        var pending = new HashSet<IntPtr>();
        Prune(players, pending);
        if (players.Pointer != bots.Pointer)
            Prune(bots, pending);
    }

    static void Prune(PlayerMap registry, HashSet<IntPtr> pending)
    {
        var stale = new List<string>();
        foreach (var entry in registry)
        {
            var player = entry.Value;
            if (player == null || player.gameObject == null || !pending.Add(player.Pointer))
                stale.Add(entry.Key);
        }
        foreach (string key in stale)
            registry.Remove(key);
    }

    internal static void Forget(GameWorld world, Player player)
    {
        var local = Singleton<AbstractGame>.Instance?.TryCast<LocalGame>();
        if (local == null || local.GameWorld?.Pointer != world.Pointer)
            return;
        RemoveFromRegistry(local._players, player.Pointer);
        RemoveFromRegistry(local._bots, player.Pointer);
    }

    static void RemoveFromRegistry(PlayerMap? registry, IntPtr pointer)
    {
        if (registry == null)
            return;
        var matches = new List<string>();
        foreach (var entry in registry)
            if (entry.Value?.Pointer == pointer)
                matches.Add(entry.Key);
        foreach (string key in matches)
            registry.Remove(key);
    }
}
