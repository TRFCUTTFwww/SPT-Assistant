using System;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace LocalSpt5Esp;

internal static class BulletWallPenetration
{
    static ConfigEntry<bool> enabled = null!;
    static int coverLayers;

    internal static void Initialize(Plugin plugin)
    {
        enabled = FeatureConfig.Bind("8. Aim assist", "Bullet wall penetration", "子弹穿墙", false);
        coverLayers = LayerMask.GetMask("Terrain", "HighPolyCollider", "LowPolyCollider", "DoorLowPolyCollider",
            "TransparentCollider", "Foliage", "LevelBorder");
        var signature = new[]
        {
            typeof(Vector3), typeof(Vector3), typeof(LayerMask), typeof(LayerMask),
            typeof(Il2CppSystem.Func<RaycastHit, bool>)
        };
        var checkCollision = AccessTools.DeclaredMethod(typeof(Shot), "CheckCollision", signature)
            ?? throw new MissingMethodException(typeof(Shot).FullName, "CheckCollision");
        plugin.Patches.Patch(checkCollision,
            prefix: new HarmonyMethod(typeof(BulletWallPenetration), nameof(BeforeCollisionCheck)));
    }

    static void BeforeCollisionCheck(Shot __instance, ref LayerMask __2, ref LayerMask __3)
    {
        if (!enabled.Value || coverLayers == 0)
            return;
        var player = Singleton<GameWorld>.Instance?.MainPlayer;
        if (player?.IsYourPlayer != true || __instance.PlayerRaidId != player.RaidId)
            return;
        var weapon = __instance.Weapon;
        if (weapon?.TryCast<Weapon>() == null || ToyDeveloper.Uses(weapon)
            || __instance.Ammo?.TryCast<Ammo>()?.AmmoTemplate is not { HasGrenaderComponent: false }
            || Singleton<AbstractGame>.Instance?.TryCast<LocalGame>() == null)
            return;

        // Retain the native hit and armor layers and let the game resolve penetration on the target.
        __2.value &= ~coverLayers;
        __3.value &= ~coverLayers;
    }
}
