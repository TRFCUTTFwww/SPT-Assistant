using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using Path = System.IO.Path;

namespace LocalGearMail;

internal sealed record FleaRules(
    [property: JsonPropertyName("unbanAll")] bool UnbanAll = false,
    [property: JsonPropertyName("ignoreLevelLocks")] bool IgnoreLevelLocks = false,
    [property: JsonPropertyName("autoPriceMode")] int AutoPriceMode = 0,
    [property: JsonPropertyName("autoPricePercent")] int AutoPricePercent = 100,
    [property: JsonPropertyName("showTraderCompare")] bool ShowTraderCompare = true,
    [property: JsonPropertyName("purchasesFir")] bool PurchasesFir = false,
    [property: JsonPropertyName("fullCondition")] bool FullCondition = false,
    [property: JsonPropertyName("roubleOnly")] bool RoubleOnly = false,
    [property: JsonPropertyName("noBarter")] bool NoBarter = false)
{
    internal FleaRules Normalize() => this with
    {
        AutoPriceMode = Math.Clamp(AutoPriceMode, 0, 3)
    };
}

internal sealed class FleaPriceBook
{
    internal const string Local = "local", Pvp = "pvp", Pve = "pve";
    private const string FeedRoot = "https://raw.githubusercontent.com/DrakiaXYZ/SPT-LiveFleaPriceDB/main/";
    private const string SettingsFile = "settings.json", LegacySnapshotFile = "settings-snapshot.json";
    private const string PresetsFile = "settings-presets.json";
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions FlexibleJson = new() { PropertyNameCaseInsensitive = true };

    internal sealed record Feed(DateTimeOffset Updated, Dictionary<MongoId, int> Prices);
    internal sealed record State(
        string Mode,
        Dictionary<MongoId, int> Manual,
        Dictionary<MongoId, bool> Listings,
        FleaRules Rules,
        Feed? Online)
    {
        internal bool TryGet(MongoId id, out int price)
        {
            if (Manual.TryGetValue(id, out price))
                return true;
            return Online != null && Online.Prices.TryGetValue(id, out price);
        }
    }

    private sealed record SavedSettings(
        string Mode,
        Dictionary<string, int> Overrides,
        Dictionary<string, bool>? Listings = null,
        FleaRules? Rules = null);

    private readonly string home;
    private readonly HttpClient client;
    private readonly object gate = new();
    private State state;
    private Task? refresh;
    private DateTimeOffset retryAfter;
    private string? failedMode;

    internal State Current => Volatile.Read(ref state);

    internal FleaPriceBook(string home, HttpClient client)
    {
        this.home = home;
        this.client = client;
        var file = Path.Combine(home, SettingsFile);
        state = FromSettings(File.Exists(file) ? ReadSettings(file) : new(Pvp, []));
    }

    internal void SetMode(string mode)
    {
        if (!ValidMode(mode))
            throw new ArgumentException("无效价格来源。");
        lock (gate)
        {
            var current = state;
            if (current.Mode == mode)
                return;
            var next = current with { Mode = mode, Online = ReadFeed(mode) };
            Save(next);
            retryAfter = default;
            failedMode = null;
        }
    }

    internal void SetManual(MongoId id, int? price)
    {
        lock (gate)
        {
            var manual = new Dictionary<MongoId, int>(state.Manual);
            if (price.HasValue)
                manual[id] = price.Value;
            else
                manual.Remove(id);
            Save(state with { Manual = manual });
        }
    }

    internal void SetListing(MongoId id, bool? allowed)
    {
        lock (gate)
        {
            var listings = new Dictionary<MongoId, bool>(state.Listings);
            if (allowed.HasValue)
                listings[id] = allowed.Value;
            else
                listings.Remove(id);
            Save(state with { Listings = listings });
        }
    }

    internal void SetRules(FleaRules rules)
    {
        var normalized = (rules ?? new FleaRules()).Normalize();
        lock (gate)
            Save(state with { Rules = normalized });
    }

    internal int ApplyBatch(IReadOnlyList<MongoId> ids, string op, double multiplier, bool? listingAllowed, Func<MongoId, double> nativeFallback)
    {
        if (ids.Count == 0)
            throw new ArgumentException("当前筛选列表没有可操作的物品。");
        lock (gate)
        {
            var current = state;
            switch (op)
            {
                case "scalePrice":
                {
                    if (!double.IsFinite(multiplier))
                        throw new ArgumentException("请输入有限的价格倍率。");
                    var manual = new Dictionary<MongoId, int>(current.Manual);
                    int updated = 0;
                    foreach (var id in ids)
                    {
                        double basePrice = current.TryGet(id, out int active) ? active : nativeFallback(id);
                        double scaled = Math.Round(basePrice * multiplier, MidpointRounding.AwayFromZero);
                        if (!double.IsFinite(scaled) || scaled < int.MinValue || scaled > int.MaxValue) throw new ArgumentException("计算后的价格超出 Int32 存储范围。");
                        manual[id] = (int)scaled;
                        updated++;
                    }
                    Save(current with { Manual = manual });
                    return updated;
                }
                case "resetPrice":
                {
                    var manual = new Dictionary<MongoId, int>(current.Manual);
                    int removed = 0;
                    foreach (var id in ids)
                        if (manual.Remove(id))
                            removed++;
                    Save(current with { Manual = manual });
                    return removed;
                }
                case "setListing":
                {
                    if (!listingAllowed.HasValue)
                        throw new ArgumentException("出售权限无效。");
                    var listings = new Dictionary<MongoId, bool>(current.Listings);
                    foreach (var id in ids)
                        listings[id] = listingAllowed.Value;
                    Save(current with { Listings = listings });
                    return ids.Count;
                }
                case "resetListing":
                {
                    var listings = new Dictionary<MongoId, bool>(current.Listings);
                    int removed = 0;
                    foreach (var id in ids)
                        if (listings.Remove(id))
                            removed++;
                    Save(current with { Listings = listings });
                    return removed;
                }
                default:
                    throw new ArgumentException("未知批量操作。");
            }
        }
    }

    internal static string ValidatePresetName(string? name)
    {
        name = name?.Trim() ?? "";
        if (name.Length is < 1 or > 80 || name.Any(char.IsControl))
            throw new ArgumentException("请输入 1～80 字的方案名称。");
        return name;
    }

    internal void SavePreset(string? name)
    {
        string valid = ValidatePresetName(name);
        lock (gate)
        {
            var presets = ReadPresets();
            presets[valid] = CaptureSettings(state);
            Write(PresetsFile, JsonSerializer.Serialize(presets));
        }
    }

    internal void DeletePreset(string? name)
    {
        lock (gate)
        {
            if (string.IsNullOrEmpty(name))
            {
                var legacy = Path.Combine(home, LegacySnapshotFile);
                if (!File.Exists(legacy))
                    throw new ArgumentException("找不到该跳蚤方案。");
                File.Delete(legacy);
                return;
            }
            var presets = ReadPresets();
            if (!presets.Remove(name.Trim()))
                throw new ArgumentException("找不到该跳蚤方案。");
            Write(PresetsFile, JsonSerializer.Serialize(presets));
        }
    }

    internal void RenamePreset(string? oldName, string? newName)
    {
        string source = ValidatePresetName(oldName);
        string target = ValidatePresetName(newName);
        lock (gate)
        {
            var presets = ReadPresets();
            var existingKey = presets.Keys.FirstOrDefault(key => string.Equals(key, source, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException("找不到要重命名的跳蚤方案。");
            if (!string.Equals(existingKey, target, StringComparison.OrdinalIgnoreCase) && presets.ContainsKey(target))
                throw new ArgumentException("同名跳蚤方案已存在。");
            var saved = presets[existingKey];
            presets.Remove(existingKey);
            presets[target] = saved;
            Write(PresetsFile, JsonSerializer.Serialize(presets));
        }
    }

    internal string ExportPreset(string? name)
    {
        lock (gate)
        {
            if (name == "*")
                return JsonSerializer.Serialize(CaptureSettings(state), IndentedJson);
            SavedSettings saved;
            if (string.IsNullOrEmpty(name))
            {
                var file = Path.Combine(home, LegacySnapshotFile);
                if (!File.Exists(file))
                    throw new ArgumentException("找不到该跳蚤方案。");
                saved = ReadSettings(file);
            }
            else
            {
                if (!ReadPresets().TryGetValue(name.Trim(), out var preset) || preset == null)
                    throw new ArgumentException("找不到该跳蚤方案。");
                saved = preset;
            }
            return JsonSerializer.Serialize(saved, IndentedJson);
        }
    }

    internal string ImportPreset(string? name, string? rawJson)
    {
        string validName = ValidatePresetName(name);
        if (string.IsNullOrWhiteSpace(rawJson))
            throw new ArgumentException("导入内容为空。");
        SavedSettings parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<SavedSettings>(rawJson, FlexibleJson)
                ?? throw new InvalidDataException();
            if (!ValidMode(parsed.Mode) || parsed.Overrides == null)
                throw new InvalidDataException();
        }
        catch (Exception error) when (error is JsonException or InvalidDataException)
        {
            throw new ArgumentException("跳蚤方案 JSON 格式无效。", error);
        }

        var sanitized = CaptureSettings(FromSettings(parsed));
        lock (gate)
        {
            var presets = ReadPresets();
            presets[validName] = sanitized;
            Write(PresetsFile, JsonSerializer.Serialize(presets));
        }
        return validName;
    }

    internal void LoadPreset(string? name)
    {
        lock (gate)
        {
            SavedSettings saved;
            if (string.IsNullOrEmpty(name))
            {
                var file = Path.Combine(home, LegacySnapshotFile);
                if (!File.Exists(file))
                    throw new ArgumentException("找不到该跳蚤方案。");
                try
                {
                    saved = ReadSettings(file);
                }
                catch (Exception error) when (error is IOException or JsonException or InvalidDataException)
                {
                    throw new ArgumentException("无法读取该跳蚤方案。", error);
                }
            }
            else
            {
                if (!ReadPresets().TryGetValue(name, out var preset))
                    throw new ArgumentException("找不到该跳蚤方案。");
                if (preset == null || !ValidMode(preset.Mode) || preset.Overrides == null)
                    throw new ArgumentException("无法读取该跳蚤方案。");
                saved = preset;
            }
            bool changingMode = state.Mode != saved.Mode;
            Save(FromSettings(saved));
            if (changingMode)
            {
                retryAfter = default;
                failedMode = null;
            }
        }
    }

    internal (State Current, bool Refreshing, bool Failed) Status()
    {
        lock (gate)
            return (state, state.Mode != Local && refresh?.IsCompleted == false, state.Mode == failedMode);
    }

    internal (string[]? Names, bool HasLegacy) PresetStatus()
    {
        lock (gate)
        {
            bool hasLegacy = File.Exists(Path.Combine(home, LegacySnapshotFile));
            try
            {
                return (ReadPresets().Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray(), hasLegacy);
            }
            catch (ArgumentException)
            {
                return (null, hasLegacy);
            }
        }
    }

    internal Task? Schedule(bool force = false)
    {
        lock (gate)
        {
            var mode = state.Mode;
            var now = DateTimeOffset.UtcNow;
            if (mode == Local)
                return null;
            if (refresh?.IsCompleted == false)
                return refresh;
            if (now < retryAfter && !force)
                return null;
            if (!force && state.Online is { } feed && now - feed.Updated < TimeSpan.FromHours(1))
                return null;
            retryAfter = now.AddMinutes(1);
            return refresh = Task.Run(() => Fetch(mode));
        }
    }

    private async Task Fetch(string mode)
    {
        try
        {
            using var response = await client.GetAsync(FeedRoot + FeedName(mode));
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var prices = ParseFeed(json);
            var updated = DateTimeOffset.UtcNow;
            lock (gate)
            {
                Write(FeedName(mode), json);
                if (state.Mode == mode)
                {
                    Volatile.Write(ref state, state with { Online = new(updated, prices) });
                    failedMode = null;
                }
            }
        }
        catch (Exception error) when (error is HttpRequestException or IOException or UnauthorizedAccessException
            or JsonException or OperationCanceledException)
        {
            lock (gate)
                if (state.Mode == mode)
                    failedMode = mode;
        }
    }

    private void Save(State next)
    {
        Write(SettingsFile, JsonSerializer.Serialize(CaptureSettings(next)));
        Volatile.Write(ref state, next);
    }

    private static SavedSettings CaptureSettings(State source) =>
        new(source.Mode,
            source.Manual.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            source.Listings.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            source.Rules.Normalize());

    private Dictionary<string, SavedSettings> ReadPresets()
    {
        var file = Path.Combine(home, PresetsFile);
        try
        {
            var saved = JsonSerializer.Deserialize<Dictionary<string, SavedSettings>>(File.ReadAllText(file), FlexibleJson);
            return new(saved ?? throw new JsonException(), StringComparer.OrdinalIgnoreCase);
        }
        catch (FileNotFoundException)
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
        catch (DirectoryNotFoundException)
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            throw new ArgumentException("无法读取跳蚤方案列表。", error);
        }
    }

    private void Write(string name, string contents)
    {
        Directory.CreateDirectory(home);
        string target = Path.Combine(home, name);
        string staging = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(staging, contents);
            File.Move(staging, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(staging))
                File.Delete(staging);
        }
    }

    private static SavedSettings ReadSettings(string file)
    {
        var saved = JsonSerializer.Deserialize<SavedSettings>(File.ReadAllText(file), FlexibleJson);
        if (saved == null || !ValidMode(saved.Mode) || saved.Overrides == null)
            throw new InvalidDataException("跳蚤价格设置无效。");
        return saved;
    }

    private State FromSettings(SavedSettings saved)
    {
        var listings = (saved.Listings ?? []).Where(pair => MongoId.IsValidMongoId(pair.Key))
            .ToDictionary(pair => new MongoId(pair.Key), pair => pair.Value);
        var rules = (saved.Rules ?? new FleaRules()).Normalize();
        return new(saved.Mode, ValidPrices(saved.Overrides), listings, rules, ReadFeed(saved.Mode));
    }

    private Feed? ReadFeed(string mode)
    {
        if (mode == Local)
            return null;
        var file = Path.Combine(home, FeedName(mode));
        try
        {
            return new(File.GetLastWriteTimeUtc(file), ParseFeed(File.ReadAllText(file)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static Dictionary<MongoId, int> ParseFeed(string json)
    {
        var raw = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
        var prices = raw == null ? [] : ValidPrices(raw);
        if (prices.Count == 0)
            throw new JsonException("跳蚤价格表为空。");
        return prices;
    }

    private static Dictionary<MongoId, int> ValidPrices(Dictionary<string, int> raw)
    {
        var prices = new Dictionary<MongoId, int>(raw.Count);
        foreach (var (id, price) in raw)
            if (MongoId.IsValidMongoId(id))
                prices[new MongoId(id)] = price;
        return prices;
    }

    private static bool ValidMode(string mode) => mode is Local or Pvp or Pve;
    private static string FeedName(string mode) => mode == Pve ? "prices-pve.json" : "prices-regular.json";
}
