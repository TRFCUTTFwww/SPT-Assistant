using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace LocalSpt5Esp;

internal static class MagazineAmmoCompatibility
{
    private enum ContainerKind { Magazine, Chamber, Cylinder }
    private static ConfigEntry<bool> enabled = null!, chambers = null!;
    private static readonly Dictionary<IntPtr, FilterOverride> containers = new();
    private static readonly Dictionary<IntPtr, FilterOverride> filters = new();
    private static readonly Dictionary<IntPtr, Weapon> weapons = new();
    private static float nextScan, nextWarning;
    private static int lastOptions = -1;
    private static int scopeFrame = -1;
    private static bool localScope;
    private static IntPtr stashRoot, equipmentRoot, menuEquipmentRoot, lastHeld;
    private static Inventory? menuInventory, playerInventory;
    private static Player? localPlayer;
    private static bool restoring;

    private sealed class FilterOverride(Item owner, StackSlot? stack, Slot? slot, ContainerKind kind,
        Il2CppReferenceArray<ItemFilter> original, Il2CppReferenceArray<ItemFilter> copy)
    {
        internal readonly Item Owner = owner;
        internal readonly ContainerKind Kind = kind;
        internal readonly Il2CppReferenceArray<ItemFilter> Original = original, Copy = copy;
        internal IntPtr Pointer => stack?.Pointer ?? slot!.Pointer;
        internal Il2CppReferenceArray<ItemFilter>? Current => stack != null ? stack.Filters : slot!.Filters;
        internal void Assign(Il2CppReferenceArray<ItemFilter> value)
        {
            if (stack != null) stack.Filters = value;
            else slot!._Filters_k__BackingField = value;
        }
    }

    internal static void Initialize(Plugin plugin)
    {
        enabled = FeatureConfig.Bind("28. 武器便利", "Allow incompatible magazine ammo", "弹匣允许混装口径", false,
            "允许在仓库和本地单人战局把不同口径的普通弹药装进自己的弹匣。");
        chambers = FeatureConfig.Bind("28. 武器便利", "Allow incompatible chamber ammo", "枪膛允许不同口径弹药", false,
            "允许自己的枪膛装入不同口径的普通弹药，并允许混装弹匣向枪膛供弹。");

        // IL2CPP inlines Slot/StackSlot/Magazine.CheckCompatibility and
        // Magazine.ApplyItemBase. Their real callers still reach this helper.
        var check = AccessTools.DeclaredMethod(typeof(ItemFilterExtension), nameof(ItemFilterExtension.CheckItem),
            new[] { typeof(Il2CppReferenceArray<ItemFilter>), typeof(Item), typeof(bool) })
            ?? throw new MissingMethodException(typeof(ItemFilterExtension).FullName, nameof(ItemFilterExtension.CheckItem));
        plugin.Patches.Patch(check, postfix: new HarmonyMethod(typeof(MagazineAmmoCompatibility), nameof(AfterFilterCheck)));
        // Native shot, reload, repair and draw operations call this method
        // before moving ammunition. Prepare its actual destination immediately.
        var feed = AccessTools.DeclaredMethod(typeof(Magazine), nameof(Magazine.IsAmmoCompatible),
            new[] { typeof(Il2CppReferenceArray<Slot>) })
            ?? throw new MissingMethodException(typeof(Magazine).FullName, nameof(Magazine.IsAmmoCompatible));
        plugin.Patches.Patch(feed, prefix: new HarmonyMethod(typeof(MagazineAmmoCompatibility), nameof(BeforeMagazineFeed)));
        foreach (var type in new[] { typeof(CompoundItem), typeof(Magazine), typeof(Weapon) })
        {
            var apply = AccessTools.DeclaredMethod(type, nameof(CompoundItem.Apply),
                new[] { typeof(ItemController), typeof(Item), typeof(int), typeof(bool) })
                ?? throw new MissingMethodException(type.FullName, nameof(CompoundItem.Apply));
            plugin.Patches.Patch(apply, prefix: new HarmonyMethod(typeof(MagazineAmmoCompatibility), nameof(BeforeApply)));
        }
        plugin.Log.LogInfo("Magazine and chamber compatibility hooks ready (native item filters and feeding entry)");
    }

    private static bool On(ContainerKind kind) => (kind switch
    {
        ContainerKind.Magazine => enabled?.Value == true,
        ContainerKind.Chamber => chambers?.Value == true,
        _ => enabled?.Value == true || chambers?.Value == true
    }) && LocalScope();

    private static bool LocalScope()
    {
        // Drag previews can ask about thousands of slots in one frame. Resolve
        // reflection, profile and raid state once, never once per candidate.
        int frame = Time.frameCount;
        if (scopeFrame == frame) return localScope;
        scopeFrame = frame;
        localScope = false;
        stashRoot = equipmentRoot = menuEquipmentRoot = IntPtr.Zero;
        menuInventory = playerInventory = null;
        localPlayer = null;
        if (ChatFikaSession.Role != 0) return false;
        var local = Singleton<GameWorld>.Instance?.MainPlayer;
        if (local?.IsYourPlayer == true)
        {
            // The hideout shooting range can use a separate equipment instance
            // from the menu profile. Its weapon must receive its own overrides.
            localPlayer = local;
            playerInventory = local.Profile?.Inventory;
            equipmentRoot = playerInventory?.Equipment?.Pointer ?? IntPtr.Zero;
        }
        if (!ItemSpawner.InRaid())
        {
            menuInventory = ItemUiContext.Instance?._profile?.Inventory;
            stashRoot = menuInventory?.Stash?.Pointer ?? IntPtr.Zero;
            menuEquipmentRoot = menuInventory?.Equipment?.Pointer ?? IntPtr.Zero;
        }
        return localScope = stashRoot != IntPtr.Zero || equipmentRoot != IntPtr.Zero || menuEquipmentRoot != IntPtr.Zero;
    }

    private static void AfterFilterCheck(Il2CppReferenceArray<ItemFilter> __0, Item __1, bool __2, ref bool __result)
    {
        // Exclusion-only queries have different semantics. Only override a
        // rejected normal compatibility query for a registered, private array.
        if (__result || __2 || __0 == null || !filters.TryGetValue(__0.Pointer, out var entry) || !On(entry.Kind) ||
            __1?.TryCast<Ammo>() is not { } ammo || ammo.IsUsed || ammo.TryCast<Rocket>() != null) return;
        try
        {
            if (entry.Current?.Pointer == __0.Pointer && Owns(entry.Owner)) __result = true;
        }
        catch (Exception error) { Warn(error); }
    }

    private static void BeforeApply(CompoundItem __instance, Item __1)
    {
        if (__1?.TryCast<Ammo>() == null && __1?.TryCast<Magazine>() == null) return;
        if (__instance.TryCast<Magazine>() == null && __instance.TryCast<Weapon>() == null) return;
        try { Prepare(__instance, null); }
        catch (Exception error) { Warn(error); }
    }

    private static void BeforeMagazineFeed(Il2CppReferenceArray<Slot> __0)
    {
        if (restoring || chambers?.Value != true || !LocalScope() || __0 == null || __0.Length == 0) return;
        try
        {
            var weapon = __0[0]?.ParentItem?.TryCast<Weapon>();
            if (weapon?.Chambers?.Pointer == __0.Pointer) Prepare(weapon, null);
        }
        catch (Exception error) { Warn(error); }
    }

    private static bool Owns(Item item)
    {
        if (!LocalScope()) return false;
        // Native addresses already describe the ancestry. Searching the entire
        // stash here made drag validation quadratic in inventory size.
        for (int depth = 0; item != null && depth < 64; depth++)
        {
            var pointer = item.Pointer;
            if (pointer == stashRoot || pointer == equipmentRoot || pointer == menuEquipmentRoot) return true;
            var parent = item.Parent?.Container?.ParentItem;
            if (parent?.Pointer == pointer) return false;
            item = parent!;
        }
        return false;
    }

    private static void Prepare(Item item, HashSet<IntPtr>? seen)
    {
        var magazine = item.TryCast<Magazine>();
        var weapon = item.TryCast<Weapon>();
        if (magazine == null && weapon == null) return;
        if ((!On(ContainerKind.Magazine) && !On(ContainerKind.Chamber)) || !Owns(item)) return;
        if (magazine != null)
        {
            if (On(ContainerKind.Magazine) && magazine.Cartridges is { } stack)
                Track(item, stack, null, ContainerKind.Magazine, seen);
            if (magazine.TryCast<CylinderMagazine>()?.Camoras is { } camoras)
                foreach (var slot in camoras)
                    if (slot != null) Track(item, null, slot, ContainerKind.Cylinder, seen);
        }
        if (weapon == null) return;
        weapons[weapon.Pointer] = weapon;
        if (On(ContainerKind.Chamber) && weapon.Chambers is { } slots)
            foreach (var slot in slots)
                if (slot != null) Track(item, null, slot, ContainerKind.Chamber, seen);
        if (weapon.GetCurrentMagazine() is { } current) Prepare(current, seen);
    }

    private static void Track(Item owner, StackSlot? stack, Slot? slot, ContainerKind kind, HashSet<IntPtr>? seen)
    {
        var pointer = stack?.Pointer ?? slot!.Pointer;
        seen?.Add(pointer);
        if (containers.TryGetValue(pointer, out var old))
        {
            if (old.Current?.Pointer == old.Copy.Pointer && old.Owner.Pointer == owner.Pointer && old.Kind == kind) return;
            Restore(old);
        }
        var original = stack != null ? stack.Filters : slot!.Filters;
        if (original == null) return;
        // Templates can share arrays. Give this exact container a private array
        // without changing its filter entries or any global item template.
        var copy = new Il2CppReferenceArray<ItemFilter>(original.Length);
        for (int i = 0; i < original.Length; i++) copy[i] = original[i];
        var entry = new FilterOverride(owner, stack, slot, kind, original, copy);
        entry.Assign(copy);
        containers[pointer] = entry;
        filters[copy.Pointer] = entry;
    }

    internal static void Tick()
    {
        int options = (enabled.Value ? 1 : 0) | (chambers.Value ? 2 : 0);
        if (options != 0 && !LocalScope()) options = 0;
        if (options == 0)
        {
            if (containers.Count != 0 || weapons.Count != 0) Reset();
            lastOptions = options;
            return;
        }
        try
        {
            // A stale CompatibleAmmo=false can prevent the operation from ever
            // reaching IsAmmoCompatible. Refresh the held instance between scans.
            var held = localPlayer?.HandsController?.TryCast<Player.FirearmController>()?.Item;
            if (held != null && Owns(held))
            {
                Prepare(held, null);
                Refresh(held);
                if (lastHeld != held.Pointer)
                    Plugin.Self.Log.LogInfo($"Ammo compatibility held weapon ready: chambers={held.Chambers?.Length ?? 0}, chamberOption={chambers.Value}, compatible={held.CompatibleAmmo}");
                lastHeld = held.Pointer;
            }
            else lastHeld = IntPtr.Zero;
            if (options == lastOptions && Time.unscaledTime < nextScan) return;
            lastOptions = options;
            nextScan = Time.unscaledTime + 1;
            var seen = new HashSet<IntPtr>();
            Scan(playerInventory, seen);
            if (menuInventory != null && menuInventory.Pointer != playerInventory?.Pointer) Scan(menuInventory, seen);
            foreach (var entry in containers.Values.ToArray())
                if (!seen.Contains(entry.Pointer) || !On(entry.Kind)) Restore(entry);
            // CompatibleAmmo is a native cached field, not a patchable getter.
            foreach (var weapon in weapons.Values.ToArray())
            {
                Refresh(weapon);
                if (!Owns(weapon)) weapons.Remove(weapon.Pointer);
            }
        }
        catch (Exception error) { Warn(error); }
    }

    private static void Scan(Inventory? inventory, HashSet<IntPtr> seen)
    {
        if (inventory?.AllRealPlayerItems is not { } items) return;
        foreach (var item in items)
            if (item != null && (item.TryCast<Magazine>() != null || item.TryCast<Weapon>() != null)) Prepare(item, seen);
    }

    private static void Restore(FilterOverride entry)
    {
        if (entry.Current?.Pointer == entry.Copy.Pointer) entry.Assign(entry.Original);
        containers.Remove(entry.Pointer);
        filters.Remove(entry.Copy.Pointer);
    }

    private static void Refresh(Weapon weapon)
    {
        var magazine = weapon.GetCurrentMagazine();
        weapon.CompatibleAmmo = magazine == null || magazine.IsAmmoCompatible(weapon.Chambers);
    }

    internal static void Reset()
    {
        // Reset runs before Harmony unpatching. Recomputing native caches must
        // not let the feeding prefix re-register the filters being restored.
        restoring = true;
        try
        {
            foreach (var entry in containers.Values.ToArray()) Restore(entry);
            foreach (var weapon in weapons.Values) Refresh(weapon);
            weapons.Clear();
        }
        finally { restoring = false; }
        nextScan = 0;
        lastOptions = -1;
        scopeFrame = -1;
        localScope = false;
        stashRoot = equipmentRoot = menuEquipmentRoot = lastHeld = IntPtr.Zero;
        menuInventory = playerInventory = null;
        localPlayer = null;
    }

    private static void Warn(Exception error)
    {
        if (Time.unscaledTime < nextWarning) return;
        nextWarning = Time.unscaledTime + 15;
        Plugin.Self.Log.LogWarning("Ammo compatibility update failed: " + error.Message);
    }
}
