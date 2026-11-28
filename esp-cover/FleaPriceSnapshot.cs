using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace LocalSpt5Esp;

// Reads the same atomically published files used by FleaPriceBook. No second web feed.
// Run Read on one background worker; published dictionaries are never mutated.
internal sealed class FleaPriceSnapshot(string home)
{
    private sealed record Settings(string Mode, Dictionary<string, double> Overrides);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly Dictionary<string, (long Stamp, Dictionary<string, double> Prices)> feeds = new();
    private Settings settings = new("pvp", new());
    private long settingsStamp;
    private Dictionary<string, double>? lastFeed;
    private Settings? lastSettings;
    private Dictionary<string, double> current = new(StringComparer.Ordinal);

    internal string Mode => settings.Mode;

    internal Dictionary<string, double> Read()
    {
        try
        {
            string path = Path.Combine(home, "settings.json");
            long stamp = Stamp(path);
            if (stamp != 0 && stamp != settingsStamp)
            {
                var next = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), JsonOptions);
                if (next == null || next.Mode is not ("pvp" or "pve" or "local") || next.Overrides == null)
                    throw new JsonException("Invalid flea price settings.");
                settings = next with { Overrides = ValidPrices(next.Overrides) };
                settingsStamp = stamp;
            }
        }
        catch (Exception error) when (Recoverable(error)) { /* Keep the last valid settings. */ }

        Dictionary<string, double>? feed = null;
        if (settings.Mode != "local")
        {
            feeds.TryGetValue(settings.Mode, out var cached);
            feed = cached.Prices;
            try
            {
                string path = Path.Combine(home, settings.Mode == "pve" ? "prices-pve.json" : "prices-regular.json");
                long stamp = Stamp(path);
                if (stamp != 0 && stamp != cached.Stamp)
                {
                    var parsed = JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(path));
                    var valid = ValidPrices(parsed ?? throw new JsonException("Missing flea prices."));
                    if (valid.Count == 0) throw new JsonException("Empty flea prices.");
                    feeds[settings.Mode] = (stamp, valid);
                    feed = valid;
                }
            }
            catch (Exception error) when (Recoverable(error)) { /* Offline or damaged file: retain this mode's last feed. */ }
        }

        if (!ReferenceEquals(lastSettings, settings) || !ReferenceEquals(lastFeed, feed))
        {
            var next = feed == null ? new Dictionary<string, double>(StringComparer.Ordinal) : new(feed, StringComparer.Ordinal);
            foreach (var pair in settings.Overrides) next[pair.Key] = pair.Value;
            current = next;
            lastSettings = settings;
            lastFeed = feed;
        }
        return current;
    }

    internal static double Resolve(IReadOnlyDictionary<string, double> live, string id, double local) =>
        live.TryGetValue(id, out var price) ? price : local;

    private static long Stamp(string path) => File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0;
    private static bool Recoverable(Exception error) => error is IOException or UnauthorizedAccessException or JsonException;
    private static Dictionary<string, double> ValidPrices(Dictionary<string, double> raw)
    {
        var valid = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var pair in raw)
            if (pair.Key.Length == 24 && IsHex(pair.Key) && double.IsFinite(pair.Value) && pair.Value > 0)
                valid[pair.Key] = pair.Value;
        return valid;
    }
    private static bool IsHex(string value)
    {
        foreach (char c in value) if (!Uri.IsHexDigit(c)) return false;
        return true;
    }
}
