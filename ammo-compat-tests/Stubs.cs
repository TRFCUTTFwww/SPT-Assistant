using System.Collections;
using System.Reflection;
using EFT.InventoryLogic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

// Minimal interop doubles. The production class is compiled unchanged.
namespace TestRuntime
{
    public static class Counters
    {
        public static int InventoryEnumerations, RootSearches, RoleReads, ParentReads;
        public static void Reset() => InventoryEnumerations = RootSearches = RoleReads = ParentReads = 0;
    }
    public class Native
    {
        private static long next;
        public IntPtr Pointer { get; } = (IntPtr)Interlocked.Increment(ref next);
        public T? TryCast<T>() where T : class => this as T;
    }
}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
    public class Il2CppReferenceArray<T>(long length) : TestRuntime.Native, IEnumerable<T>
    {
        private readonly T[] values = new T[length];
        public int Length => values.Length;
        public T this[int i] { get => values[i]; set => values[i] = value; }
        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)values).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
namespace BepInEx.Configuration
{
    public class ConfigEntry<T>(T value) { public T Value { get; set; } = value; }
}
namespace Comfort.Common { public static class Singleton<T> { public static T? Instance; } }
namespace UnityEngine { public static class Time { public static float unscaledTime; public static int frameCount; } }
namespace HarmonyLib
{
    public static class AccessTools
    {
        public static MethodInfo? DeclaredMethod(Type type, string name, Type[] args) =>
            type.GetMethod(name, BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic, args);
    }
    public class HarmonyMethod(Type type, string name)
    {
        public readonly MethodInfo Method = type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;
    }
    public class Harmony
    {
        public readonly List<(MethodInfo Target, HarmonyMethod? Prefix, HarmonyMethod? Postfix)> Patches = [];
        public void Patch(MethodInfo target, HarmonyMethod? prefix = null, HarmonyMethod? postfix = null) =>
            Patches.Add((target, prefix, postfix));
    }
}
namespace EFT
{
    public class Profile { public Inventory Inventory = new(); }
    public class Player
    {
        public bool IsYourPlayer = true;
        public Profile Profile = new();
        public TestRuntime.Native? HandsController;
        public class FirearmController : TestRuntime.Native { public Weapon? Item; }
    }
    public class GameWorld { public Player? MainPlayer; }
}
namespace EFT.UI { public class ItemUiContext { public static ItemUiContext? Instance; public EFT.Profile? _profile; } }
namespace EFT.InventoryLogic
{
    public class Item : TestRuntime.Native
    {
        public string Id = Guid.NewGuid().ToString();
        public string Template = "incompatible";
        internal ItemAddress? Address;
        public ItemAddress? Parent { get { TestRuntime.Counters.ParentReads++; return Address; } }
    }
    public class ItemAddress(Item parent) { public ParentContainer Container = new(parent); }
    public class ParentContainer(Item parent) { public Item ParentItem = parent; }
    public class ItemChildren(Item parent) : System.Collections.ObjectModel.Collection<Item>
    {
        protected override void InsertItem(int index, Item item)
        {
            base.InsertItem(index, item);
            item.Address = new(parent);
        }
        protected override void RemoveItem(int index)
        {
            this[index].Address = null;
            base.RemoveItem(index);
        }
        protected override void SetItem(int index, Item item)
        {
            this[index].Address = null;
            base.SetItem(index, item);
            item.Address = new(parent);
        }
        protected override void ClearItems()
        {
            foreach (var item in this) item.Address = null;
            base.ClearItems();
        }
    }
    public class Ammo : Item { public bool IsUsed; }
    public class Rocket : Ammo { }
    public class ItemFilter { public string Allowed = "native"; }
    public class ItemController { }
    public class CompoundItem : Item
    {
        public readonly ItemChildren Children;
        public CompoundItem() => Children = new(this);
        public IEnumerable<Item> Descendants => Children.SelectMany(item => item is CompoundItem root
            ? new[] { item }.Concat(root.Descendants) : new[] { item });
        public bool TryFindItem(string id, out Item? item) { TestRuntime.Counters.RootSearches++; item = Descendants.FirstOrDefault(i => i.Id == id); return item != null; }
        public virtual void Apply(ItemController controller, Item item, int count, bool simulate) { }
    }
    public class Inventory : TestRuntime.Native
    {
        public CompoundItem Stash = new(), Equipment = new();
        public IEnumerable<Item> AllRealPlayerItems { get { TestRuntime.Counters.InventoryEnumerations++; return Stash.Descendants.Concat(Equipment.Descendants); } }
    }
    public class StackSlot : TestRuntime.Native { public Il2CppReferenceArray<ItemFilter> Filters = new(1); }
    public class Slot : TestRuntime.Native
    {
        public Item? ParentItem;
        public Il2CppReferenceArray<ItemFilter> _Filters_k__BackingField = new(1);
        public Il2CppReferenceArray<ItemFilter> Filters => _Filters_k__BackingField;
    }
    public class Magazine : CompoundItem
    {
        public StackSlot? Cartridges = new();
        public Ammo? TopAmmo;
        public override void Apply(ItemController controller, Item item, int count, bool simulate) { }
        public bool IsAmmoCompatible(Il2CppReferenceArray<Slot> slots)
        {
            LocalSpt5Esp.Plugin.Self.Patches.Patches.SingleOrDefault(p => p.Target.Name == nameof(IsAmmoCompatible))
                .Prefix?.Method.Invoke(null, [slots]);
            return TopAmmo == null || slots.Length == 0 || slots.Any(slot => ItemFilterExtension.CheckItem(slot.Filters, TopAmmo, false));
        }
    }
    public class CylinderMagazine : Magazine { public Il2CppReferenceArray<Slot> Camoras = new(0); }
    public class Weapon : CompoundItem
    {
        public Il2CppReferenceArray<Slot> Chambers = new(0);
        public Magazine? Magazine;
        public bool CompatibleAmmo;
        public Magazine? GetCurrentMagazine() => Magazine;
        public override void Apply(ItemController controller, Item item, int count, bool simulate) { }
    }
    public static class ItemFilterExtension
    {
        public static bool CheckItem(Il2CppReferenceArray<ItemFilter> filters, Item item, bool exclusionOnly)
        {
            bool native = filters.Any(filter => filter?.Allowed == item.Template);
            object[] args = [filters, item, exclusionOnly, native];
            LocalSpt5Esp.Plugin.Self.Patches.Patches.Single(p => p.Target.Name == nameof(CheckItem))
                .Postfix!.Method.Invoke(null, args);
            return (bool)args[3];
        }
    }
}
namespace LocalSpt5Esp
{
    public static class ChatFikaSession
    {
        private static int role;
        public static int Role { get { TestRuntime.Counters.RoleReads++; return role; } set => role = value; }
    }
    public static class ItemSpawner { public static bool InRaidValue; public static bool InRaid() => InRaidValue; }
    public static class FeatureConfig
    {
        public static readonly Dictionary<string, BepInEx.Configuration.ConfigEntry<bool>> Entries = [];
        public static BepInEx.Configuration.ConfigEntry<bool> Bind(string section, string key, string label, bool value, string description)
        {
            var entry = new BepInEx.Configuration.ConfigEntry<bool>(value);
            Entries.Add(key, entry);
            return entry;
        }
    }
    public class Log
    {
        public readonly List<string> Warnings = [];
        public void LogInfo(string text) { }
        public void LogWarning(string text) => Warnings.Add(text);
    }
    public class Plugin
    {
        public static Plugin Self = new();
        public HarmonyLib.Harmony Patches = new();
        public Log Log = new();
    }
}
