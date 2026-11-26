using System.Diagnostics;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LocalSpt5Esp;
using TestRuntime;
using UnityEngine;

internal static class DragPerformanceChecks
{
    private delegate void FilterHook(Il2CppReferenceArray<ItemFilter> filters, Item item, bool excluded, ref bool result);
    private readonly record struct Sample(int Searches, int Enumerations, int Roles, int Parents, long Allocated, double Ms);

    internal static int Run()
    {
        MagazineAmmoCompatibility.Reset();
        ChatFikaSession.Role = 0;
        ItemSpawner.InRaidValue = false;
        FeatureConfig.Entries["Allow incompatible magazine ammo"].Value = true;
        FeatureConfig.Entries["Allow incompatible chamber ammo"].Value = false;
        var profile = new Profile();
        ItemUiContext.Instance = new() { _profile = profile };
        var bag = new CompoundItem();
        var magazine = new Magazine();
        magazine.Cartridges!.Filters[0] = new ItemFilter();
        bag.Children.Add(magazine);
        profile.Inventory.Stash.Children.Add(bag);
        var ammo = new Ammo();
        var patches = Plugin.Self.Patches.Patches;
        var hook = patches.Single(p => p.Target.Name == "CheckItem").Postfix!.Method.CreateDelegate<FilterHook>();
        Time.frameCount++;
        Time.unscaledTime += 2;
        MagazineAmmoCompatibility.Tick();
        var filters = magazine.Cartridges.Filters;
        const int queries = 20_000;
        int checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
            checks++;
            Console.WriteLine("PASS " + name);
        }
        bool Accept()
        {
            bool result = false;
            hook(filters, ammo, false, ref result);
            return result;
        }
        Sample Measure()
        {
            // Warm the direct delegate, excluding the test harness's reflection
            // and fake native filter implementation from the hot-path sample.
            if (!Accept()) throw new Exception("Benchmark magazine was not registered");
            Counters.Reset();
            Time.frameCount++;
            long allocated = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
            bool accepted = true;
            for (int i = 0; i < queries; i++) accepted &= Accept();
            double ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            if (!accepted) throw new Exception("Benchmark lost compatibility");
            return new(Counters.RootSearches, Counters.InventoryEnumerations, Counters.RoleReads, Counters.ParentReads, bytes, ms);
        }
        var small = Measure();
        Console.WriteLine($"INFO small-stash counters: searches={small.Searches}, enumerations={small.Enumerations}, roles={small.Roles}, parentReads={small.Parents}");
        Check(small.Searches == 0 && small.Enumerations == 0 && small.Roles <= 1,
            "20,000 drag checks perform no inventory searches/enumerations and at most one role query per frame");

        for (int i = 0; i < 10_000; i++) profile.Inventory.Stash.Children.Add(new Item());
        var large = Measure();
        Console.WriteLine($"INFO large-stash counters: searches={large.Searches}, enumerations={large.Enumerations}, roles={large.Roles}, parentReads={large.Parents}");
        Check(large.Searches == 0 && large.Enumerations == 0 && large.Roles <= 1 && large.Parents == small.Parents,
            "adding 10,000 stash items does not increase drag validation work");
        Check(small.Allocated == 0 && large.Allocated == 0,
            "the direct compatibility hook allocates no managed memory with interop doubles");
        Console.WriteLine($"INFO {queries:N0} direct checks: small stash={small.Ms:F2}ms, large stash={large.Ms:F2}ms; actual game FPS requires manual verification");

        var before = patches.Single(p => p.Target.DeclaringType == typeof(CompoundItem)).Prefix!.Method
            .CreateDelegate<Action<CompoundItem, Item>>();
        Counters.Reset();
        Time.frameCount++;
        for (int i = 0; i < queries; i++) before(bag, ammo);
        Check(Counters.RootSearches == 0 && Counters.InventoryEnumerations == 0 && Counters.RoleReads == 0 && Counters.ParentReads == 0,
            "ordinary container drag previews skip ownership and compatibility preparation entirely");

        Counters.Reset();
        Time.frameCount++;
        Time.unscaledTime += 2;
        MagazineAmmoCompatibility.Tick();
        Check(Counters.InventoryEnumerations == 1 && Counters.RootSearches == 0 && Counters.RoleReads <= 1,
            "periodic discovery enumerates the inventory once without nested inventory searches");

        profile.Inventory.Stash.Children.Remove(bag);
        Check(!Accept(), "moving an ancestor container revokes ownership within the same frame");
        bag.Address = new(bag);
        Counters.Reset();
        Check(!Accept() && Counters.ParentReads <= 64, "malformed parent cycles terminate without scanning the inventory");
        MagazineAmmoCompatibility.Reset();
        return checks;
    }
}
