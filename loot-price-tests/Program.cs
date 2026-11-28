using System.Text.Json;
using LocalSpt5Esp;

const string item = "59faff1d86f7746c51718c9c", other = "5bc9b355d4351e6d1509862a";
string home = Path.Combine(Path.GetTempPath(), "esp-price-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(home);
int checks = 0;
long revision = DateTime.UtcNow.Ticks;
void Write(string file, string json)
{
    string path = Path.Combine(home, file);
    File.WriteAllText(path, json);
    File.SetLastWriteTimeUtc(path, new DateTime(revision += TimeSpan.TicksPerSecond, DateTimeKind.Utc));
}
void Settings(string mode, Dictionary<string, double>? overrides = null) =>
    Write("settings.json", JsonSerializer.Serialize(new { Mode = mode, Overrides = overrides ?? new() }));
void Feed(string file, double price) => Write(file, JsonSerializer.Serialize(new Dictionary<string, double> { [item] = price }));
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}
try
{
    var source = new FleaPriceSnapshot(home);
    Check(FleaPriceSnapshot.Resolve(source.Read(), item, 100) == 100, "No network cache: local fallback");
    Settings("pve");
    Feed("prices-pve.json", 200);
    var first = source.Read();
    Check(FleaPriceSnapshot.Resolve(first, item, 100) == 200, "Online replaces stale local value");
    Check(FleaPriceSnapshot.Resolve(first, other, 50) == 50, "Partial feed: per-item local fallback");
    Check(ReferenceEquals(first, source.Read()), "Unchanged files avoid repeated invalidation");
    Feed("prices-pve.json", 300);
    Check(source.Read()[item] == 300 && first[item] == 200, "New sync replaces immutable published snapshot");
    Write("prices-pve.json", "{");
    Check(source.Read()[item] == 300, "Broken feed retains last good prices");
    Write("prices-pve.json", "{}");
    Check(source.Read()[item] == 300, "Empty feed retains last good prices");
    File.Delete(Path.Combine(home, "prices-pve.json"));
    Check(source.Read()[item] == 300, "Unavailable cache retains current mode's memory snapshot");
    Settings("pvp");
    Check(FleaPriceSnapshot.Resolve(source.Read(), item, 100) == 100, "PVP must not inherit PVE prices");
    Feed("prices-regular.json", 400);
    Check(source.Read()[item] == 400, "PVP uses regular feed");
    Settings("pve");
    Check(source.Read()[item] == 300, "Switch back offline restores mode-specific cached prices");
    Feed("prices-pve.json", 500);
    Check(source.Read()[item] == 500, "Recovery resumes synced prices");
    Settings("pve", new() { [item] = 600 });
    Check(source.Read()[item] == 600, "Manual flea price takes precedence");
    Settings("pve");
    Check(source.Read()[item] == 500, "Removing override restores online price");
    Settings("local");
    Check(FleaPriceSnapshot.Resolve(source.Read(), item, 100) == 100, "Local mode drops online overlay");
    Settings("local", new() { [item] = 700 });
    Check(source.Read()[item] == 700, "Local mode retains explicit manual price");
    Write("settings.json", "{");
    Check(source.Read()[item] == 700, "Broken settings preserve last good state");
    Settings("pve");
    Write("prices-pve.json", JsonSerializer.Serialize(new Dictionary<string, double> { [item] = -1, [other] = 800, ["invalid"] = 900 }));
    var valid = source.Read();
    Check(valid.Count == 1 && valid[other] == 800 && FleaPriceSnapshot.Resolve(valid, item, 100) == 100,
        "Invalid IDs and nonpositive feed values fall back locally");
    Check(new FleaPriceSnapshot(home).Read()[other] == 800, "Offline restart uses server's persisted cache");
    Console.WriteLine($"PASS: {checks} ESP price sync/fallback checks");
}
finally
{
    Directory.Delete(home, recursive: true);
}

// Optional read-only smoke check of the installed server files.
if (args.Length == 1)
{
    var installed = new FleaPriceSnapshot(args[0]);
    var prices = installed.Read();
    if (prices.Count == 0) throw new Exception("Installed price cache not available");
    Console.WriteLine($"Installed cache: mode={installed.Mode}, prices={prices.Count}, sample={prices.GetValueOrDefault(item)}");
}
