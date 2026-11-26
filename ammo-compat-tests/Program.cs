using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LocalSpt5Esp;
using UnityEngine;

MagazineAmmoCompatibility.Initialize(Plugin.Self);
if (args.Contains("--feed-only"))
{
    FeedChecks.Run();
    return;
}
if (args.Contains("--drag-performance-only"))
{
    DragPerformanceChecks.Run();
    return;
}
var magazineOption = FeatureConfig.Entries["Allow incompatible magazine ammo"];
var chamberOption = FeatureConfig.Entries["Allow incompatible chamber ammo"];
int checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
    Console.WriteLine("PASS " + message);
}
Il2CppReferenceArray<T> Array<T>(params T[] values)
{
    var array = new Il2CppReferenceArray<T>(values.Length);
    for (int i = 0; i < values.Length; i++) array[i] = values[i];
    return array;
}
var original = Array(new ItemFilter());
var ammo = new Ammo();
var magazine = new Magazine { Cartridges = new() { Filters = original }, TopAmmo = ammo };
var chamber = new Slot { _Filters_k__BackingField = original };
var weapon = new Weapon { Chambers = Array(chamber), Magazine = magazine };
weapon.Children.Add(magazine);
var profile = new Profile();
profile.Inventory.Stash.Children.Add(weapon);
ItemUiContext.Instance = new() { _profile = profile };
void Tick() { Time.unscaledTime += 2; Time.frameCount++; MagazineAmmoCompatibility.Tick(); }
bool Accept(Il2CppReferenceArray<ItemFilter> filters, Item? item = null, bool excluded = false) =>
    ItemFilterExtension.CheckItem(filters, item ?? ammo, excluded);

Check(Plugin.Self.Patches.Patches.Count == 5 &&
    Plugin.Self.Patches.Patches.Any(p => p.Target.DeclaringType == typeof(ItemFilterExtension) && p.Target.Name == "CheckItem") &&
    Plugin.Self.Patches.Patches.Any(p => p.Target.DeclaringType == typeof(Magazine) && p.Target.Name == "IsAmmoCompatible"),
    "hooks use the shared native helper, real feeding entry and three real apply methods");
Tick();
Check(!Accept(magazine.Cartridges!.Filters) && ReferenceEquals(magazine.Cartridges.Filters, original),
    "disabled options leave the original filter array and native rejection intact");
magazineOption.Value = true;
Tick();
var privateMagazine = magazine.Cartridges.Filters;
Check(privateMagazine.Pointer != original.Pointer && ReferenceEquals(privateMagazine[0], original[0]),
    "owned magazine gets a private array without modifying template filter entries");
Check(Accept(privateMagazine), "inlined magazine calls reaching the shared filter accept incompatible ordinary ammo");
Check(!Accept(original), "another container sharing the template array keeps its native rejection");
Check(Accept(privateMagazine, new Ammo { Template = "native" }), "native-compatible ammunition remains compatible");
Check(!Accept(privateMagazine, new Ammo { IsUsed = true }) && !Accept(privateMagazine, new Rocket()) &&
    !Accept(privateMagazine, new Item()), "spent rounds, rockets and non-ammo items cannot gain compatibility");
Check(!Accept(privateMagazine, excluded: true), "exclusion-only queries retain their native result");
Check(!Accept(chamber.Filters) && !weapon.CompatibleAmmo, "magazine option alone does not unlock the chamber or cached feeding flag");
chamberOption.Value = true;
MagazineAmmoCompatibility.Tick();
Check(Accept(chamber.Filters) && weapon.CompatibleAmmo, "enabling chambers takes effect immediately and refreshes the native feeding cache");
magazineOption.Value = false;
MagazineAmmoCompatibility.Tick();
Check(ReferenceEquals(magazine.Cartridges.Filters, original) && !Accept(original) && Accept(chamber.Filters),
    "magazine and chamber settings can be disabled independently");
chamberOption.Value = false;
MagazineAmmoCompatibility.Tick();
Check(ReferenceEquals(chamber.Filters, original) && !weapon.CompatibleAmmo,
    "disabling both restores original filters and recomputes the incompatible feeding cache");

magazineOption.Value = true;
Tick();
var removedArray = magazine.Cartridges.Filters;
weapon.Children.Remove(magazine);
Check(!Accept(removedArray), "losing ownership rejects an override before the periodic scan");
weapon.Magazine = null;
Tick();
Check(ReferenceEquals(magazine.Cartridges.Filters, original), "the next scan restores a removed magazine");
profile.Inventory.Equipment.Children.Add(magazine);
Tick();
var localArray = magazine.Cartridges.Filters;
ChatFikaSession.Role = 1;
Time.frameCount++;
Check(!Accept(localArray), "joining Fika rejects overrides immediately");
MagazineAmmoCompatibility.Tick();
Check(ReferenceEquals(magazine.Cartridges.Filters, original), "Fika transition restores the original container array");
ChatFikaSession.Role = 0;
ItemSpawner.InRaidValue = true;
Singleton<GameWorld>.Instance = new() { MainPlayer = new() { Profile = profile } };
chamberOption.Value = true;
Tick();
Check(Accept(magazine.Cartridges.Filters) && !Accept(chamber.Filters), "solo raids permit carried equipment but exclude stash weapons");
Singleton<GameWorld>.Instance.MainPlayer!.IsYourPlayer = false;
Time.frameCount++;
Check(!Accept(magazine.Cartridges.Filters), "a non-local player cannot use registered arrays");
Singleton<GameWorld>.Instance.MainPlayer.IsYourPlayer = true;
Time.frameCount++;
var impostor = new Magazine { Id = magazine.Id, Cartridges = new() { Filters = original } };
var prefix = Plugin.Self.Patches.Patches.Single(p => p.Target.DeclaringType == typeof(CompoundItem)).Prefix!;
prefix.Method.Invoke(null, [impostor, ammo]);
Check(ReferenceEquals(impostor.Cartridges!.Filters, original), "matching IDs with different native pointers do not pass ownership checks");

var external = Array(new ItemFilter { Allowed = "external" });
magazine.Cartridges.Filters = external;
MagazineAmmoCompatibility.Reset();
Check(ReferenceEquals(magazine.Cartridges.Filters, external), "unloading preserves a newer filter assignment from another mod");
ItemSpawner.InRaidValue = false;
Singleton<GameWorld>.Instance = null;
Tick();
Check(Accept(magazine.Cartridges.Filters), "re-enabling tracks the newer original filter array");
MagazineAmmoCompatibility.Reset();
Check(ReferenceEquals(magazine.Cartridges.Filters, external) && ReferenceEquals(chamber.Filters, original),
    "unload restores every tracked container to its own original array");

var fresh = new Magazine { Cartridges = new() { Filters = original } };
profile.Inventory.Stash.Children.Add(fresh);
prefix.Method.Invoke(null, [fresh, ammo]);
Check(Accept(fresh.Cartridges!.Filters), "the real apply prefix prepares newly acquired magazines before the next scan");
var camora = new Slot { _Filters_k__BackingField = original };
var cylinder = new CylinderMagazine { Camoras = Array(camora) };
profile.Inventory.Stash.Children.Add(cylinder);
magazineOption.Value = false;
chamberOption.Value = true;
Tick();
Check(Accept(camora.Filters), "the chamber option also handles cylinder magazine chambers");
MagazineAmmoCompatibility.Reset();
Check(ReferenceEquals(camora.Filters, original), "cylinder chamber arrays are restored on unload");
Check(Plugin.Self.Log.Warnings.Count == 0, "all regression scenarios complete without swallowed exceptions");
checks += FeedChecks.Run();
checks += DragPerformanceChecks.Run();
Console.WriteLine($"PASS {checks} ammo compatibility regression checks");
