using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace LocalSpt5Esp;

internal sealed record ScopeStickerProfile
{
    public string Name { get; init; } = "";
    public string Image { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public float Width { get; init; } = 32;
    public float X { get; init; } = 8;
    public float Y { get; init; }
    public float Opacity { get; init; } = .7f;
    public float Rotation { get; init; }
    public bool AutoContrast { get; init; }
    public bool HitFlash { get; init; }
    public string HitColor { get; init; } = "#FF4040";
    public float HitSeconds { get; init; } = .18f;
    public int ColorMode { get; init; }
    public float ColorSpeed { get; init; } = 1f;
    public string ColorPrimary { get; init; } = "#00F0FF";
    public string ColorSecondary { get; init; } = "#FF38B8";
    public bool ColorUseMask { get; init; }

    internal ScopeStickerProfile Normalized() => this with
    {
        Image = ScopeStickerStore.ValidImageId(Image) ? Image : "",
        Name = (Name ?? "")[..Math.Min(Name?.Length ?? 0, 200)],
        Width = float.IsFinite(Width) ? Math.Clamp(Width, 1, 150) : 32,
        X = float.IsFinite(X) ? Math.Clamp(X, -150, 150) : 8,
        Y = float.IsFinite(Y) ? Math.Clamp(Y, -150, 150) : 0,
        Opacity = float.IsFinite(Opacity) ? Math.Clamp(Opacity, 0, 1) : .7f,
        Rotation = float.IsFinite(Rotation) ? ((Rotation % 360 + 540) % 360) - 180 : 0,
        HitColor = ScopeStickerRules.TryHex(HitColor, out var color) ? color.Hex : "#FF4040",
        HitSeconds = float.IsFinite(HitSeconds) ? Math.Clamp(HitSeconds, .05f, 1) : .18f,
        ColorMode = ColorMode is >= 0 and <= 4 ? ColorMode : 0,
        ColorSpeed = float.IsFinite(ColorSpeed) && ColorSpeed > 0 ? Math.Clamp(ColorSpeed, .01f, 50f) : 1f,
        ColorPrimary = ScopeStickerRules.TryHex(ColorPrimary, out var primary) ? primary.Hex : "#00F0FF",
        ColorSecondary = ScopeStickerRules.TryHex(ColorSecondary, out var secondary) ? secondary.Hex : "#FF38B8"
    };
}

internal sealed class ScopeStickerStore
{
    internal sealed class Document
    {
        public int Version { get; set; } = 1;
        public Dictionary<string, ScopeStickerProfile> Profiles { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, Dictionary<string, ScopeStickerProfile>> Variants { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Images { get; set; } = new(StringComparer.Ordinal);
    }
    private static readonly JsonSerializerOptions json = new() { WriteIndented = true };
    private Document document;
    private readonly string root, path;
    internal const int MaximumImageBytes = 16 * 1024 * 1024;
    internal IReadOnlyDictionary<string, ScopeStickerProfile> Profiles => document.Profiles;
    internal IReadOnlyDictionary<string, string> Images => document.Images;
    internal string DirectoryPath => root;

    internal ScopeStickerStore(string directory)
    {
        root = Path.GetFullPath(directory);
        path = Path.Combine(root, "profiles.json");
        document = File.Exists(path) ? JsonSerializer.Deserialize<Document>(File.ReadAllText(path), json)
            ?? throw new InvalidDataException("Invalid scope sticker profiles") : new();
        if (document.Version != 1 || document.Profiles == null || document.Images == null || document.Variants == null)
            throw new InvalidDataException("Unsupported scope sticker profiles");
        document.Profiles = document.Profiles.Where(p => !string.IsNullOrEmpty(p.Key) && p.Value != null)
            .ToDictionary(p => p.Key, p => p.Value.Normalized(), StringComparer.Ordinal);
        document.Images = document.Images.Where(p => ValidImageId(p.Key))
            .ToDictionary(p => p.Key, p => Path.GetFileName(p.Value), StringComparer.Ordinal);
        document.Variants = document.Variants.Where(p => !string.IsNullOrWhiteSpace(p.Key) && p.Value != null)
            .ToDictionary(p => p.Key, p => p.Value.Where(v => ValidImageId(v.Key) && v.Value != null)
                .ToDictionary(v => v.Key, v => (v.Value with { Image = v.Key }).Normalized(), StringComparer.Ordinal), StringComparer.Ordinal);
    }

    internal static string Key(string template, int scopeIndex) =>
        string.IsNullOrWhiteSpace(template) || scopeIndex < 0 ? "" : template + ":" + scopeIndex.ToString(CultureInfo.InvariantCulture);

    internal string CycleImage(string image, int direction)
    {
        var images = document.Images.Keys.ToArray();
        if (images.Length == 0) return "";
        int index = Array.IndexOf(images, image), step = Math.Sign(direction);
        if (step == 0) return index >= 0 ? image : "";
        if (index < 0) return step > 0 ? images[0] : images[^1];
        return images[(index + step + images.Length) % images.Length];
    }
    internal ScopeStickerProfile Get(string key, ScopeStickerProfile defaults) => document.Profiles.GetValueOrDefault(key) ?? defaults with { };

    internal ScopeStickerProfile ForImage(string key, string image, ScopeStickerProfile defaults)
    {
        if (!ValidImageId(image)) throw new ArgumentException("Invalid sticker image ID");
        if (document.Variants.TryGetValue(key, out var variants) && variants.TryGetValue(image, out var saved)) return saved;
        if (document.Profiles.TryGetValue(key, out var active) && active.Image == image) return active;
        return (defaults with { Image = image }).Normalized();
    }

    internal void Save(string key, ScopeStickerProfile value)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Missing optic identity");
        var next = Copy();
        value = value.Normalized();
        next.Profiles[key] = value;
        var variants = document.Variants.TryGetValue(key, out var previous)
            ? new Dictionary<string, ScopeStickerProfile>(previous, StringComparer.Ordinal) : new(StringComparer.Ordinal);
        // The active selection and only this scope/image pair are committed together.
        if (ValidImageId(value.Image)) variants[value.Image] = value;
        next.Variants[key] = variants;
        Write(next);
    }

    internal void ApplyColorToAll(int mode, string primaryHex, string secondaryHex, float speed, bool useMask)
    {
        var next = Copy();
        foreach (var key in next.Profiles.Keys.ToArray())
        {
            var p = next.Profiles[key];
            next.Profiles[key] = (p with
            {
                ColorMode = mode,
                ColorPrimary = primaryHex,
                ColorSecondary = secondaryHex,
                ColorSpeed = speed,
                ColorUseMask = useMask
            }).Normalized();
        }
        foreach (var scopeKey in next.Variants.Keys.ToArray())
        {
            var dict = new Dictionary<string, ScopeStickerProfile>(next.Variants[scopeKey], StringComparer.Ordinal);
            foreach (var imgKey in dict.Keys.ToArray())
            {
                var v = dict[imgKey];
                dict[imgKey] = (v with
                {
                    ColorMode = mode,
                    ColorPrimary = primaryHex,
                    ColorSecondary = secondaryHex,
                    ColorSpeed = speed,
                    ColorUseMask = useMask
                }).Normalized();
            }
            next.Variants[scopeKey] = dict;
        }
        Write(next);
    }

    internal bool DeleteProfile(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        if (!document.Profiles.ContainsKey(key) && !document.Variants.ContainsKey(key)) return false;
        var next = Copy();
        next.Profiles.Remove(key);
        next.Variants.Remove(key);
        Write(next);
        return true;
    }

    internal int DeleteUnusedImages()
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var profile in document.Profiles.Values) if (ValidImageId(profile.Image)) used.Add(profile.Image);
        foreach (var variants in document.Variants.Values)
            foreach (string image in variants.Keys) if (ValidImageId(image)) used.Add(image);
        var unused = document.Images.Keys.Where(id => !used.Contains(id)).ToArray();
        if (unused.Length == 0) return 0;
        var next = Copy();
        foreach (string id in unused) next.Images.Remove(id);
        Write(next);
        foreach (string id in unused)
        {
            try { string file = ImagePath(id); if (File.Exists(file)) File.Delete(file); } catch { }
        }
        return unused.Length;
    }

    internal static bool ValidImageId(string? id) => id is { Length: 68 } &&
        (id.EndsWith(".png", StringComparison.Ordinal) || id.EndsWith(".jpg", StringComparison.Ordinal)) &&
        id.AsSpan(0, 64).ToArray().All(char.IsAsciiHexDigit);

    internal string ImagePath(string id)
    {
        if (!ValidImageId(id)) throw new ArgumentException("Invalid sticker image ID");
        return Path.Combine(root, "images", id);
    }

    internal string Import(byte[] bytes, string name)
    {
        if (!ScopeStickerImage.TrySize(bytes, out _, out _, out string extension))
            throw new InvalidDataException("请选择不超过 4096 × 4096、16 MB 的 PNG 或 JPG 图片。");
        string id = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() + extension;
        string imagePath = ImagePath(id);
        Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
        if (!File.Exists(imagePath)) AtomicWrite(imagePath, bytes);
        var next = Copy();
        next.Images[id] = Path.GetFileName(name);
        Write(next);
        return id;
    }

    private Document Copy() => new()
    {
        Profiles = new(document.Profiles, StringComparer.Ordinal), Images = new(document.Images, StringComparer.Ordinal),
        Variants = new(document.Variants, StringComparer.Ordinal)
    };

    private void Write(Document next)
    {
        AtomicWrite(path, System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(next, json)));
        document = next;
    }

    private static void AtomicWrite(string target, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(true); }
            File.Move(temporary, target, true);
        }
        finally { File.Delete(temporary); }
    }
}

internal static class ScopeStickerImage
{
    // Inspect dimensions before Unity allocates a decoded texture. PNG and
    // JPEG use network byte order; JPEG dimensions live in a SOF segment.
    internal static bool TrySize(ReadOnlySpan<byte> bytes, out int width, out int height, out string extension)
    {
        width = height = 0; extension = "";
        if (bytes.Length < 24 || bytes.Length > ScopeStickerStore.MaximumImageBytes) return false;
        if (bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) &&
            bytes.Length >= 33 && bytes.Slice(8, 4).SequenceEqual(new byte[] { 0, 0, 0, 13 }) &&
            bytes.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(16, 4));
            height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(20, 4));
            extension = ".png";
        }
        else if (bytes[0] == 255 && bytes[1] == 216)
        {
            for (int at = 2; at < bytes.Length - 3;)
            {
                if (bytes[at++] != 255) return false;
                while (at < bytes.Length && bytes[at] == 255) at++;
                if (at >= bytes.Length) return false;
                int marker = bytes[at++];
                if (marker is 217 or 218) return false;
                if (marker is 1 or >= 208 and <= 215) continue;
                if (at + 2 > bytes.Length) return false;
                int size = bytes[at] * 256 + bytes[at + 1];
                if (size < 2 || size > bytes.Length - at) return false;
                if (marker is >= 192 and <= 207 && marker is not (196 or 200 or 204))
                {
                    if (size < 8) return false;
                    height = bytes[at + 3] * 256 + bytes[at + 4];
                    width = bytes[at + 5] * 256 + bytes[at + 6];
                    extension = ".jpg"; break;
                }
                at += size;
            }
        }
        return extension.Length != 0 && width is > 0 and <= 4096 && height is > 0 and <= 4096;
    }
}
