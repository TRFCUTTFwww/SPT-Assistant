using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LocalSpt5Esp;
using TestRuntime;
using UnityEngine;

internal static class FeedChecks
{
    internal static int Run()
    {
        MagazineAmmoCompatibility.Reset();
        ChatFikaSession.Role = 0;
        ItemSpawner.InRaidValue = false;
        var magazineOption = FeatureConfig.Entries["Allow incompatible magazine ammo"];
        var chamberOption = FeatureConfig.Entries["Allow incompatible chamber ammo"];
        magazineOption.Value = chamberOption.Value = true;
        var menu = new Profile();
        var live = new Profile();
        var player = new Player { Profile = live };
        ItemUiContext.Instance = new() { _profile = menu };
        Singleton<GameWorld>.Instance = new() { MainPlayer = player };
        int checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            checks++;
            Console.WriteLine("PASS " + message);
        }
        (Weapon Weapon, Slot Chamber, Magazine Magazine, Il2CppReferenceArray<ItemFilter> Original) Gun(CompoundItem root)
        {
            var original = new Il2CppReferenceArray<ItemFilter>(1);
            original[0] = new ItemFilter();
            var chamber = new Slot { _Filters_k__BackingField = original };
            var slots = new Il2CppReferenceArray<Slot>(1);
            slots[0] = chamber;
            var magazine = new Magazine { TopAmmo = new Ammo(), Cartridges = new() { Filters = original } };
            var weapon = new Weapon { Chambers = slots, Magazine = magazine };
            chamber.ParentItem = weapon;
            weapon.Children.Add(magazine);
            root.Children.Add(weapon);
            return (weapon, chamber, magazine, original);
        }
        void Tick(bool scan = true)
        {
            Time.frameCount++;
            if (scan) Time.unscaledTime += 2;
            MagazineAmmoCompatibility.Tick();
        }

        var hideout = Gun(live.Inventory.Equipment);
        var stash = Gun(menu.Inventory.Stash);
        var menuEquipped = Gun(menu.Inventory.Equipment);
        Tick();
        Check(hideout.Magazine.IsAmmoCompatible(hideout.Weapon.Chambers) && hideout.Weapon.CompatibleAmmo,
            "hideout player equipment receives chamber compatibility even with a separate menu profile");
        Check(stash.Weapon.CompatibleAmmo && menuEquipped.Weapon.CompatibleAmmo,
            "supporting live hideout equipment preserves both menu inventory roots");

        var fresh = Gun(live.Inventory.Equipment);
        Check(fresh.Magazine.IsAmmoCompatible(fresh.Weapon.Chambers) && fresh.Chamber.Filters.Pointer != fresh.Original.Pointer,
            "the native feeding entry prepares a new weapon before any periodic scan");
        var held = Gun(live.Inventory.Equipment);
        player.HandsController = new Player.FirearmController { Item = held.Weapon };
        Counters.Reset();
        Tick(scan: false);
        Check(held.Weapon.CompatibleAmmo && Counters.InventoryEnumerations == 0 && Counters.RootSearches == 0,
            "held-weapon feeding cache refreshes between scans without enumerating inventory");

        chamberOption.Value = false;
        Tick(scan: false);
        Check(!held.Magazine.IsAmmoCompatible(held.Weapon.Chambers) && !held.Weapon.CompatibleAmmo &&
            ReferenceEquals(held.Chamber.Filters, held.Original), "disabling chambers restores live equipment and its native feeding rejection");
        chamberOption.Value = true;
        Tick();
        live.Inventory.Equipment.Children.Remove(fresh.Weapon);
        Check(!fresh.Magazine.IsAmmoCompatible(fresh.Weapon.Chambers),
            "a registered feed target loses compatibility immediately when ownership changes");

        var foreign = Gun(new CompoundItem());
        var otherSlots = new Il2CppReferenceArray<Slot>(1);
        otherSlots[0] = held.Chamber;
        MagazineAmmoCompatibility.Reset();
        Check(!foreign.Magazine.IsAmmoCompatible(foreign.Weapon.Chambers) &&
            !held.Magazine.IsAmmoCompatible(otherSlots) && ReferenceEquals(held.Chamber.Filters, held.Original),
            "feeding preparation rejects foreign weapons and arrays other than the weapon's real chambers");
        Check(!held.Weapon.CompatibleAmmo && ReferenceEquals(hideout.Chamber.Filters, hideout.Original),
            "unload recomputes native feeding state without re-registering restored arrays");

        Tick();
        held.Magazine.TopAmmo = new Ammo { IsUsed = true };
        Check(!held.Magazine.IsAmmoCompatible(held.Weapon.Chambers), "feeding cannot unlock spent ammunition");
        held.Magazine.TopAmmo = new Rocket();
        Check(!held.Magazine.IsAmmoCompatible(held.Weapon.Chambers), "feeding cannot unlock rockets");
        held.Magazine.TopAmmo = new Ammo();
        ItemSpawner.InRaidValue = true;
        Tick();
        Check(held.Weapon.CompatibleAmmo && !stash.Magazine.IsAmmoCompatible(stash.Weapon.Chambers) &&
            !menuEquipped.Magazine.IsAmmoCompatible(menuEquipped.Weapon.Chambers),
            "solo raids accept the live player's equipment and revoke both menu roots");
        ChatFikaSession.Role = 1;
        Time.frameCount++;
        Check(!held.Magazine.IsAmmoCompatible(held.Weapon.Chambers), "Fika blocks native feeding overrides before the next scan");
        Tick();
        Check(!held.Weapon.CompatibleAmmo && ReferenceEquals(held.Chamber.Filters, held.Original),
            "Fika transition restores the live held weapon and feeding cache");
        ChatFikaSession.Role = 0;
        player.IsYourPlayer = false;
        Tick();
        Check(!held.Magazine.IsAmmoCompatible(held.Weapon.Chambers), "non-local live players cannot prepare feeding slots");

        player.IsYourPlayer = true;
        player.Profile = menu;
        ItemSpawner.InRaidValue = false;
        Counters.Reset();
        Tick();
        Check(Counters.InventoryEnumerations == 1 && Counters.RootSearches == 0,
            "a shared live/menu inventory is enumerated once per discovery scan");
        Check(Plugin.Self.Log.Warnings.Count == 0, "feeding regressions complete without swallowed exceptions");

        MagazineAmmoCompatibility.Reset();
        ItemSpawner.InRaidValue = false;
        Singleton<GameWorld>.Instance = null;
        return checks;
    }
}
