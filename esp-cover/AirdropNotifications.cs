using System;
using EFT;
using EFT.Airdrop;
using EFT.SynchronizableObjects;
using HarmonyLib;
using UnityEngine;

namespace LocalSpt5Esp;

internal static partial class AirdropControl
{
    static float nextAirdropNoticeScan;

    static bool WantsAirdropNotices =>
        RaidNotifications.Wants(RaidEventKind.PlaneArrived) || RaidNotifications.Wants(RaidEventKind.AirdropReleased);

    static void InitializeAirdropNotifications(Plugin plugin)
    {
        var method = AccessTools.DeclaredMethod(typeof(SynchronizableObjectLogicProcessor), "InitSyncObject",
            new[] { typeof(SynchronizableObject), typeof(Vector3), typeof(Vector3), typeof(int) });
        if (method == null)
        {
            plugin.Log.LogWarning("Native airdrop activation hook unavailable; registry observation remains active.");
            return;
        }
        try
        {
            plugin.Patches.Patch(method,
                postfix: new HarmonyMethod(typeof(AirdropControl), nameof(AfterAirdropActivation)));
        }
        catch (Exception error)
        {
            plugin.Log.LogWarning("Native airdrop activation hook: " + error.Message);
        }
    }

    static void AfterAirdropActivation(SynchronizableObject __0)
    {
        if (!WantsAirdropNotices || !RaidClock.Active)
            return;
        try
        {
            EnsureInformation();
            ObserveAirdropNotice(__0);
        }
        catch (Exception error)
        {
            Plugin.Self.Log.LogWarning("Native airdrop activation: " + error.Message);
        }
    }

    static void TickAirdropNotifications(GameWorld current)
    {
        if (!WantsAirdropNotices || Time.unscaledTime < nextAirdropNoticeScan)
            return;
        nextAirdropNoticeScan = Time.unscaledTime + .5f;
        var sync = current.SynchronizableObjectLogicProcessor?.GetSynchronizableObjects();
        if (sync != null)
            foreach (var item in sync)
                ObserveAirdropNotice(item);
    }

    static void ObserveAirdropNotice(SynchronizableObject? item)
    {
        if (item == null || !item.IsActive || !item.IsInited)
            return;

        if (RaidNotifications.Wants(RaidEventKind.PlaneArrived) && item.TryCast<AirplaneSynchronizableObject>() is { } airplane)
        {
            var server = airplane.Logic?.TryCast<ServerPlane>() ?? airplane.Logic?.TryCast<ClientAirPlane>()?.OfflineServerLogic;
            if (server == null || server._flyToAirdrop)
                PublishPlaneNotice(airplane);
            return;
        }

        if (!RaidNotifications.Wants(RaidEventKind.AirdropReleased) || item.TryCast<AirdropSynchronizableObject>() is not { } drop)
            return;
        var client = drop.Logic?.TryCast<ClientAirDrop>();
        var dropServer = drop.Logic?.TryCast<ServerAirDrop>() ?? client?.OfflineServerLogic;
        var stage = dropServer?._fallingStage ?? client?._fallingStage;
        if (stage is EAirdropFallingStage.Fall or EAirdropFallingStage.ParachuteStartOpen or EAirdropFallingStage.ParachuteEndOpen)
            PublishDropNotice(drop);
    }

    static void PublishPlaneNotice(AirplaneSynchronizableObject plane)
    {
        if (plane.IsActive && plane.IsInited && plane.gameObject.activeInHierarchy)
            RaidNotifications.Publish(RaidEventKind.PlaneArrived, identity: Key(plane));
    }

    static void PublishDropNotice(AirdropSynchronizableObject drop)
    {
        Track(drop);
        var key = Key(drop);
        ledger.Released(key, Elapsed);
        RaidNotifications.Publish(RaidEventKind.AirdropReleased, identity: key);
    }
}
