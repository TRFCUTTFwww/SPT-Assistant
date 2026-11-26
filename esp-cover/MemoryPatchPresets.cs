using System;
using System.Collections.Generic;
using System.IO;
using static LocalSpt5Esp.MemoryEditorAccess;

namespace LocalSpt5Esp;

internal sealed record MemoryPatchPreset(
    string Name,
    string GameVersion,
    string Module,
    ulong FallbackOffset,
    string AobPattern,
    string Mode,
    string CodeInput,
    int CaveOrder)
{
    internal bool Valid =>
        Name is { Length: > 0 and <= 128 } &&
        GameVersion is { Length: > 0 and <= 128 } &&
        Module is { Length: <= 260 } &&
        Module.IndexOfAny(['/', '\\', ':']) < 0 &&
        FallbackOffset < AddressLimit &&
        AobPattern is { Length: > 0 and <= 1024 } &&
        Mode is "Raw" or "CodeCave" &&
        CodeInput is { Length: > 0 and <= 4096 } &&
        CaveOrder is >= 0 and <= 2;
}

internal sealed record MemoryTableEntryPreset(
    string Description,
    string GameVersion,
    string Module,
    ulong Offset,
    long[] Offsets,
    MemoryValueType Type,
    string Value,
    bool Locked,
    string Hotkey);

internal static class MemoryPatchPresets
{
    internal const int Limit = 128;

    internal static List<MemoryPatchPreset> Load(string path)
    {
        if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024)
            throw new InvalidDataException("补丁脚本文件过大。");
        var list = PresetStorage.Load<MemoryPatchPreset>(path);
        if (list.Count > Limit || list.Exists(p => p is not { Valid: true }))
            throw new InvalidDataException("补丁脚本文件包含无效记录。");
        return list;
    }

    internal static List<MemoryPatchPreset> Save(string path, MemoryPatchPreset preset)
    {
        if (!preset.Valid) throw new InvalidDataException("补丁脚本无效。");
        var list = Load(path);
        int idx = list.FindIndex(p => p.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) list[idx] = preset;
        else if (list.Count < Limit) list.Add(preset);
        else throw new InvalidOperationException($"补丁脚本已满（{Limit} 条）。");
        PresetStorage.Save(path, list);
        return list;
    }

    internal static List<MemoryPatchPreset> Remove(string path, string name)
    {
        var list = Load(path);
        if (list.RemoveAll(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) != 0)
            PresetStorage.Save(path, list);
        return list;
    }

    internal static bool TryLocateTarget(MemoryPatchPreset preset, string gameVersion, out ulong address, out string error)
    {
        address = 0;
        error = "";
        if (!TryParseAob(preset.AobPattern, out byte[] pattern, out byte[] mask))
        {
            error = "补丁特征码格式无效。";
            return false;
        }

        if (!string.IsNullOrEmpty(preset.Module) && TryGetModuleRange(preset.Module, out ulong modBase, out ulong modEnd))
        {
            if (preset.GameVersion == gameVersion && preset.FallbackOffset > 0 &&
                preset.FallbackOffset < modEnd - modBase)
            {
                ulong candidate = modBase + preset.FallbackOffset;
                if (MatchesAobAt(candidate, pattern, mask))
                {
                    address = candidate;
                    return true;
                }
            }
            return ScanAobInRange(modBase, modEnd, pattern, mask, out address, out error);
        }

        return ScanAobInRange(0x10000, AddressLimit, pattern, mask, out address, out error);
    }

    internal static bool TryApplyPreset(MemoryPatchPreset preset, string gameVersion, out CodePatch? patch, out string error)
    {
        patch = null;
        if (!TryLocateTarget(preset, gameVersion, out ulong target, out error))
            return false;
        return preset.Mode == "CodeCave"
            ? MemoryCodePatcher.InjectCodeCave(target, preset.CodeInput, preset.CaveOrder == 0, preset.CaveOrder == 1, out patch, out error)
            : MemoryCodePatcher.ApplyHexOrAssembly(target, preset.CodeInput, out patch, out error);
    }

    static bool MatchesAobAt(ulong address, byte[] pattern, byte[] mask)
    {
        if (!Query(address, out var region) || !region.ReadableCode || !region.Contains(address, pattern.Length))
            return false;
        var buf = new byte[pattern.Length];
        if (!Read(address, buf, buf.Length)) return false;
        for (int i = 0; i < pattern.Length; i++)
            if ((buf[i] & mask[i]) != pattern[i]) return false;
        return true;
    }

    static bool ScanAobInRange(ulong start, ulong end, byte[] pattern, byte[] mask, out ulong match, out string error)
    {
        match = 0;
        error = "";
        int patLen = pattern.Length;
        ulong cursor = start;
        var buffer = new byte[256 * 1024];
        ulong found = 0;
        int hitCount = 0;

        while (cursor < end && Query(cursor, out var region))
        {
            if (region.Base >= end) break;
            ulong regEnd = Math.Min(region.End, end);
            if (!region.ReadableCode || regEnd <= cursor)
            {
                cursor = region.End > cursor ? region.End : cursor + 0x1000;
                continue;
            }
            ulong pos = cursor;
            while (pos < regEnd)
            {
                int chunk = (int)Math.Min((ulong)buffer.Length, regEnd - pos);
                if (chunk < patLen) break;
                if (Read(pos, buffer, chunk))
                {
                    int limit = chunk - patLen;
                    for (int i = 0; i <= limit; i++)
                    {
                        bool ok = true;
                        for (int j = 0; j < patLen; j++)
                        {
                            if ((buffer[i + j] & mask[j]) != pattern[j]) { ok = false; break; }
                        }
                        if (!ok) continue;
                        found = pos + (ulong)i;
                        hitCount++;
                        if (hitCount > 1)
                        {
                            error = "特征码匹配到多个位置，请加长特征码或更新模块偏移。";
                            return false;
                        }
                    }
                }
                int step = Math.Max(1, chunk - patLen + 1);
                pos += (ulong)step;
                if (chunk < buffer.Length) break;
            }
            cursor = region.End > cursor ? region.End : cursor + 0x1000;
        }

        if (hitCount == 1)
        {
            match = found;
            return true;
        }
        error = "未找到匹配的特征码位置。";
        return false;
    }
}
