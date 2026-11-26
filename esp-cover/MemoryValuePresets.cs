using System;
using System.Collections.Generic;
using System.IO;
using static LocalSpt5Esp.MemoryEditorAccess;

namespace LocalSpt5Esp;

internal sealed record MemoryValuePreset(string GameVersion, string Module, ulong Offset, long[] Offsets,
    MemoryValueType Type, string Value, string Description)
{
    internal string Location => FormatPointerExpression($"{Module}+0x{Offset:X}", Offsets);

    internal bool SameTarget(MemoryValuePreset other) =>
        GameVersion == other.GameVersion && Module.Equals(other.Module, StringComparison.OrdinalIgnoreCase) &&
        Offset == other.Offset && Type == other.Type && Offsets.AsSpan().SequenceEqual(other.Offsets);

    internal bool TryResolveBase(string gameVersion, out ulong address)
    {
        address = 0;
        if (gameVersion != GameVersion || !TryGetModuleBase(Module, out ulong moduleBase) ||
            Offset >= AddressLimit - moduleBase)
            return false;
        ulong candidate = moduleBase + Offset;
        if (!TryResolveModule(candidate, out string module, out ulong offset) ||
            !module.Equals(Module, StringComparison.OrdinalIgnoreCase) || offset != Offset)
            return false;
        address = candidate;
        return true;
    }

    internal static bool TryCreate(string gameVersion, ulong baseAddress, long[] offsets, MemoryValueType type,
        string input, string description, out MemoryValuePreset? preset)
    {
        preset = null;
        if (!TryResolveModule(baseAddress, out string module, out ulong offset) ||
            !TryParse(input, type, out ulong value))
            return false;
        var candidate = new MemoryValuePreset(gameVersion, module, offset, offsets, type, Format(value, type), description);
        if (!candidate.Valid) return false;
        preset = candidate;
        return true;
    }

    internal bool Valid =>
        GameVersion is { Length: > 0 and <= 128 } && Module is { Length: > 0 and <= 260 } &&
        Module.IndexOfAny(['/', '\\', ':']) < 0 && Offset < AddressLimit &&
        Offsets is { Length: <= 32 } && Enum.IsDefined(Type) && Description is { Length: <= 256 } &&
        Value is { Length: > 0 and <= 64 } && TryParse(Value, Type, out _);
}

internal static class MemoryValuePresets
{
    internal const int Limit = 128;

    internal static List<MemoryValuePreset> Load(string path)
    {
        if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024)
            throw new InvalidDataException("调试预设文件过大。");
        var presets = PresetStorage.Load<MemoryValuePreset>(path);
        if (presets.Count > Limit || presets.Exists(preset => preset is not { Valid: true }))
            throw new InvalidDataException("调试预设文件包含无效记录。");
        return presets;
    }

    internal static List<MemoryValuePreset> Save(string path, MemoryValuePreset preset)
    {
        if (!preset.Valid) throw new InvalidDataException("调试预设无效。");
        var presets = Load(path);
        int existing = presets.FindIndex(preset.SameTarget);
        if (existing >= 0) presets[existing] = preset;
        else if (presets.Count < Limit) presets.Add(preset);
        else throw new InvalidOperationException($"调试预设已满（{Limit} 条）。");
        PresetStorage.Save(path, presets);
        return presets;
    }

    internal static List<MemoryValuePreset> Remove(string path, MemoryValuePreset preset)
    {
        var presets = Load(path);
        if (presets.RemoveAll(preset.SameTarget) != 0)
            PresetStorage.Save(path, presets);
        return presets;
    }
}
