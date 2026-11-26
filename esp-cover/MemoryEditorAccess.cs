using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace LocalSpt5Esp;

internal enum MemoryValueType { Byte, Int16, Int32, Int64, Float, Double }

internal enum MemoryScanMode { Exact, Rounded, Unknown, Changed, Unchanged, Increased, Decreased, GreaterThan, LessThan, Between }

internal static class MemoryEditorAccess
{
    const uint Private = 0x20000, Commit = 0x1000;
    const uint ReadOnly = 0x02, ReadWrite = 0x04, WriteCopy = 0x08;
    const uint ExecuteRead = 0x20, ExecuteReadWrite = 0x40, ExecuteWriteCopy = 0x80;
    const uint GuardFlag = 0x100;
    const uint ModuleFromAddress = 0x00000004, ModuleUnchangedRefcount = 0x00000002;
    internal const ulong AddressLimit = 0x0000800000000000;
    internal static readonly IntPtr CurrentProcess = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Region
    {
        internal ulong Base, Allocation;
        internal uint AllocationProtection;
        internal ushort PartitionId;
        internal ulong Size;
        internal uint State, Protection, Type;

        internal ulong End => Base + Size;
        internal bool ReadableCode => State == Commit && (Protection & GuardFlag) == 0 &&
            (Protection & 0xff) is ExecuteRead or ExecuteReadWrite or ExecuteWriteCopy;
        internal bool Readable => State == Commit && (Protection & GuardFlag) == 0 &&
            (Protection & 0xff) is ReadOnly or ReadWrite or WriteCopy or ExecuteRead or ExecuteReadWrite or ExecuteWriteCopy;
        internal bool Writable => State == Commit && (Protection & GuardFlag) == 0 &&
            (Protection & 0xff) is ReadWrite or WriteCopy or ExecuteReadWrite or ExecuteWriteCopy;
        internal bool Editable => Type == Private && State == Commit &&
            Protection is ReadWrite or WriteCopy && (AllocationProtection & 0xf0) == 0;
        internal bool Contains(ulong address, int length) =>
            length > 0 && address >= Base && address < End && (ulong)length <= End - address;
    }

    [DllImport("kernel32.dll")]
    static extern nuint VirtualQuery(ulong address, out Region info, nuint size);

    [DllImport("kernel32.dll")]
    static extern bool ReadProcessMemory(IntPtr process, ulong address, [Out] byte[] bytes, nuint length, out nuint read);

    [DllImport("kernel32.dll")]
    static extern bool WriteProcessMemory(IntPtr process, ulong address, byte[] bytes, nuint length, out nuint written);

    [DllImport("kernel32.dll")]
    internal static extern bool VirtualProtect(ulong address, nuint size, uint newProtect, out uint oldProtect);

    [DllImport("kernel32.dll")]
    internal static extern ulong VirtualAlloc(ulong address, nuint size, uint allocationType, uint protect);

    [DllImport("kernel32.dll")]
    internal static extern bool VirtualFree(ulong address, nuint size, uint freeType);

    [DllImport("kernel32.dll")]
    internal static extern bool FlushInstructionCache(IntPtr process, ulong baseAddress, nuint size);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool GetModuleHandleExW(uint flags, ulong address, out IntPtr module);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr GetModuleHandleW(string moduleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern int GetModuleFileNameW(IntPtr module, [Out] char[] filename, int size);

    internal static bool Query(ulong address, out Region region)
    {
        region = default;
        return OperatingSystem.IsWindows() && Environment.Is64BitProcess && address < AddressLimit &&
            VirtualQuery(address, out region, (nuint)Marshal.SizeOf<Region>()) != 0 &&
            region.End > address;
    }

    internal static bool Read(ulong address, byte[] buffer, int size) =>
        size > 0 && size <= buffer.Length &&
        ReadProcessMemory(CurrentProcess, address, buffer, (nuint)size, out var read) && read == (nuint)size;

    internal static bool ReadBytes(ulong address, int size, out byte[] bytes)
    {
        bytes = [];
        if (size <= 0 || size > 64 * 1024 || !Query(address, out var region) || !region.Readable)
            return false;
        int actual = (int)Math.Min((ulong)size, region.End - address);
        if (actual <= 0) return false;
        var buffer = new byte[actual];
        if (!Read(address, buffer, actual)) return false;
        bytes = buffer;
        return true;
    }

    internal static bool ReadHexWindow(ulong target, int before, int size, out ulong origin, out byte[] bytes)
    {
        origin = 0;
        bytes = [];
        if (size <= 0 || size > 64 * 1024 || before < 0 || before >= size ||
            !Query(target, out var region) || !region.Readable)
            return false;

        ulong aligned = target & ~15UL;
        ulong minimum = aligned >= (ulong)before ? aligned - (ulong)before : 0;
        ulong start = Math.Max(region.Base, minimum);
        while (start > minimum && Query(start - 1, out var earlier) && earlier.Readable)
            start = Math.Max(earlier.Base, minimum);

        int length = (int)Math.Min((ulong)size, region.End - start);
        while (length < size && Query(start + (ulong)length, out var later) && later.Readable)
            length += (int)Math.Min((ulong)(size - length), later.End - start - (ulong)length);
        if (target - start >= (ulong)length) return false;
        var buffer = new byte[length];
        if (!Read(start, buffer, length)) return false;
        origin = start;
        bytes = buffer;
        return true;
    }

    internal static bool Write(ulong address, ulong allocation, byte[] buffer, int size, ulong value)
    {
        if (buffer.Length < sizeof(ulong) || size > sizeof(ulong) ||
            !Query(address, out var region) || !region.Writable ||
            (allocation != 0 && allocation != region.Allocation) || !region.Contains(address, size))
            return false;
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        return WriteProcessMemory(CurrentProcess, address, buffer, (nuint)size, out var written) && written == (nuint)size;
    }

    internal static bool WriteRaw(ulong address, byte[] bytes)
    {
        if (bytes.Length == 0 || !Query(address, out var region) || !region.Writable || !region.Contains(address, bytes.Length))
            return false;
        return WriteProcessMemory(CurrentProcess, address, bytes, (nuint)bytes.Length, out var written) &&
            written == (nuint)bytes.Length;
    }

    internal static bool WriteCode(ulong address, byte[] bytes, out byte[] original)
    {
        original = [];
        if (bytes.Length == 0 || bytes.Length > 4096 || address == 0 || address >= AddressLimit)
            return false;
        if (!Query(address, out var region) || !region.ReadableCode)
            return false;
        var saved = new byte[bytes.Length];
        if (!Read(address, saved, saved.Length))
            return false;
        if (!VirtualProtect(address, (nuint)bytes.Length, ExecuteReadWrite, out uint oldProtect))
            return false;
        try
        {
            if (!WriteProcessMemory(CurrentProcess, address, bytes, (nuint)bytes.Length, out var written) ||
                written != (nuint)bytes.Length)
                return false;
            FlushInstructionCache(CurrentProcess, address, (nuint)bytes.Length);
            original = saved;
            return true;
        }
        finally
        {
            VirtualProtect(address, (nuint)bytes.Length, oldProtect, out _);
        }
    }

    internal static string FormatModuleAddress(ulong address)
    {
        if (TryResolveModule(address, out string name, out ulong offset))
            return $"{name}+0x{offset:X}";
        return $"0x{address:X}";
    }

    internal static bool TryResolveModule(ulong address, out string moduleName, out ulong offset)
    {
        moduleName = "";
        offset = 0;
        if (!OperatingSystem.IsWindows() || address == 0 || address >= AddressLimit) return false;
        if (!GetModuleHandleExW(ModuleFromAddress | ModuleUnchangedRefcount, address, out IntPtr module) || module == IntPtr.Zero)
            return false;
        ulong baseAddr = (ulong)module.ToInt64();
        if (address < baseAddr) return false;
        var chars = new char[260];
        int len = GetModuleFileNameW(module, chars, chars.Length);
        if (len <= 0) return false;
        moduleName = Path.GetFileName(new string(chars, 0, len));
        offset = address - baseAddr;
        return !string.IsNullOrEmpty(moduleName);
    }

    internal static bool TryGetModuleBase(string moduleName, out ulong baseAddress)
    {
        baseAddress = 0;
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(moduleName)) return false;
        IntPtr handle = GetModuleHandleW(moduleName.Trim());
        if (handle == IntPtr.Zero) return false;
        baseAddress = (ulong)handle.ToInt64();
        return baseAddress > 0 && baseAddress < AddressLimit;
    }

    internal static bool TryGetModuleRange(string moduleName, out ulong baseAddress, out ulong endAddress)
    {
        endAddress = 0;
        if (!TryGetModuleBase(moduleName, out baseAddress)) return false;
        ulong cursor = baseAddress;
        while (Query(cursor, out var region) && region.Allocation == baseAddress)
        {
            endAddress = region.End;
            if (region.End <= cursor) break;
            cursor = region.End;
        }
        return endAddress > baseAddress;
    }

    /// <summary>
    /// Parses a hex address, "Module.dll+0xOffset", or pointer chain "[Base+0x10]+0x20+0x8".
    /// </summary>
    internal static bool TryParsePointerExpression(string text, out ulong baseAddress, out long[] offsets, out string canonical)
    {
        baseAddress = 0;
        offsets = [];
        canonical = "";
        if (string.IsNullOrWhiteSpace(text)) return false;
        string s = text.Trim();

        // Pointer chain syntax: [expr]+off1+off2... or nested [[base]+off1]+off2
        if (s.StartsWith('['))
        {
            var offsetList = new List<long>();
            int depth = 0, firstClose = -1;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '[') depth++;
                else if (s[i] == ']')
                {
                    depth--;
                    if (depth == 0) { firstClose = i; break; }
                }
            }
            if (firstClose < 2) return false;

            // Flatten nested brackets: [[base+a]+b]+c -> base+a, offsets [b, c]
            string inner = s[1..firstClose].Trim();
            string tail = s[(firstClose + 1)..].Trim();
            if (inner.StartsWith('['))
            {
                if (!TryParsePointerExpression(inner, out baseAddress, out var innerOffsets, out _))
                    return false;
                offsetList.AddRange(innerOffsets);
            }
            else
            {
                if (!TryResolveSimpleAddress(inner, out baseAddress))
                    return false;
            }
            if (tail.Length == 0)
            {
                offsetList.Add(0);
            }
            else if (!TryParseOffsetSequence(tail, offsetList))
            {
                return false;
            }
            offsets = offsetList.ToArray();
            canonical = FormatPointerExpression(baseAddress, offsets);
            return true;
        }

        if (!TryResolveSimpleAddress(s, out baseAddress)) return false;
        canonical = FormatModuleAddress(baseAddress);
        return true;
    }

    static bool TryParseOffsetSequence(string tail, List<long> target)
    {
        int i = 0;
        while (i < tail.Length)
        {
            while (i < tail.Length && char.IsWhiteSpace(tail[i])) i++;
            if (i >= tail.Length) break;
            int sign = 1;
            if (tail[i] == '+') { sign = 1; i++; }
            else if (tail[i] == '-') { sign = -1; i++; }
            else return false;
            while (i < tail.Length && char.IsWhiteSpace(tail[i])) i++;
            int start = i;
            while (i < tail.Length && tail[i] != '+' && tail[i] != '-' && !char.IsWhiteSpace(tail[i])) i++;
            string token = tail[start..i];
            if (!TryParseHexOffset(token, out ulong mag)) return false;
            target.Add(sign > 0 ? (long)mag : -(long)mag);
        }
        return target.Count > 0;
    }

    internal static bool TryResolveSimpleAddress(string text, out ulong address)
    {
        address = 0;
        string s = text.Trim();
        if (s.Length == 0) return false;

        int opIndex = s.LastIndexOfAny(['+', '-']);
        if (opIndex > 0 && TryParseHexOffset(s[(opIndex + 1)..].Trim(), out ulong delta))
        {
            string left = s[..opIndex].Trim();
            if (TryParseHex(left, out ulong leftAddr) || TryGetModuleBase(left, out leftAddr))
            {
                if (leftAddr >= AddressLimit) return false;
                if (s[opIndex] == '+')
                {
                    if (delta >= AddressLimit - leftAddr) return false;
                    address = leftAddr + delta;
                }
                else
                {
                    if (delta >= leftAddr) return false;
                    address = leftAddr - delta;
                }
                return address != 0;
            }
        }

        if (TryParseHex(s, out address))
            return address > 0 && address < AddressLimit;
        return TryGetModuleBase(s, out address);
    }

    internal static bool TryResolvePointerChain(ulong baseAddress, long[] offsets, out ulong resolved)
    {
        resolved = baseAddress;
        if (offsets.Length == 0) return resolved > 0 && resolved < AddressLimit;
        var ptrBuf = new byte[8];
        ulong current = baseAddress;
        for (int i = 0; i < offsets.Length; i++)
        {
            if (current == 0 || current >= AddressLimit || !Query(current, out var region) || !region.Readable)
                return false;
            if (!Read(current, ptrBuf, 8)) return false;
            ulong deref = BinaryPrimitives.ReadUInt64LittleEndian(ptrBuf);
            if (deref == 0 || deref >= AddressLimit) return false;
            long next = (long)deref + offsets[i];
            if (next <= 0 || (ulong)next >= AddressLimit) return false;
            current = (ulong)next;
        }
        resolved = current;
        return true;
    }

    internal static string FormatPointerExpression(ulong baseAddress, long[] offsets)
    {
        if (offsets.Length == 0) return $"0x{baseAddress:X}";
        return FormatPointerExpression(FormatModuleAddress(baseAddress), offsets);
    }

    internal static string FormatPointerExpression(string baseLabel, long[] offsets)
    {
        if (offsets.Length == 0) return baseLabel;
        var sb = new StringBuilder();
        sb.Append('[').Append(baseLabel).Append(']');
        for (int i = 0; i < offsets.Length; i++)
        {
            long off = offsets[i];
            if (off >= 0) sb.Append("+0x").Append(off.ToString("X", CultureInfo.InvariantCulture));
            else sb.Append("-0x").Append((-off).ToString("X", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    static bool TryParseHex(string token, out ulong value)
    {
        var span = token.AsSpan().Trim();
        if (span.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) span = span[2..];
        return ulong.TryParse(span, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);
    }

    static bool TryParseHexOffset(string token, out ulong value)
    {
        var span = token.AsSpan().Trim();
        if (span.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) span = span[2..];
        return ulong.TryParse(span, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);
    }

    internal static bool TryParseAob(string text, out byte[] pattern, out byte[] mask)
    {
        pattern = [];
        mask = [];
        if (string.IsNullOrWhiteSpace(text) || text.Length > 1024) return false;
        var tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0 || tokens.Length > 128) return false;
        var pat = new byte[tokens.Length];
        var msk = new byte[tokens.Length];
        bool anyConcrete = false;
        for (int i = 0; i < tokens.Length; i++)
        {
            string t = tokens[i];
            if (t is "?" or "??" or "*")
            {
                pat[i] = 0;
                msk[i] = 0;
            }
            else if (t.Length == 2 && byte.TryParse(t, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte b))
            {
                pat[i] = b;
                msk[i] = 0xFF;
                anyConcrete = true;
            }
            else
            {
                return false;
            }
        }
        if (!anyConcrete) return false;
        pattern = pat;
        mask = msk;
        return true;
    }

    internal static bool TryParseRoundedBounds(string text, MemoryValueType type, out double low, out double high)
    {
        low = high = 0;
        if (type is not (MemoryValueType.Float or MemoryValueType.Double)) return false;
        string trimmed = text.Trim();
        if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double center) || !double.IsFinite(center))
            return false;
        int dot = trimmed.IndexOf('.');
        int decimals = 0;
        if (dot >= 0)
        {
            int end = trimmed.IndexOfAny(['e', 'E'], dot + 1);
            int fracLen = (end >= 0 ? end : trimmed.Length) - dot - 1;
            decimals = Math.Clamp(fracLen, 0, 6);
        }
        double halfStep = 0.5 * Math.Pow(10, -decimals);
        low = center - halfStep;
        high = center + halfStep;
        return true;
    }

    internal static double ToDouble(ulong raw, MemoryValueType type) => type switch
    {
        MemoryValueType.Byte => (byte)raw,
        MemoryValueType.Int16 => (short)raw,
        MemoryValueType.Int32 => (int)raw,
        MemoryValueType.Int64 => (long)raw,
        MemoryValueType.Float => BitConverter.UInt32BitsToSingle((uint)raw),
        _ => BitConverter.UInt64BitsToDouble(raw)
    };

    internal static bool MatchesFilter(ulong currentRaw, ulong previousRaw, MemoryValueType type,
        MemoryScanMode mode, ulong targetRaw, ulong targetMaxRaw, double lowBound, double highBound)
    {
        ulong cur = Normalize(currentRaw, type);
        ulong prev = Normalize(previousRaw, type);
        switch (mode)
        {
            case MemoryScanMode.Exact:
                return cur == targetRaw;
            case MemoryScanMode.Rounded:
            {
                double val = ToDouble(cur, type);
                return double.IsFinite(val) && val >= lowBound && val < highBound;
            }
            case MemoryScanMode.Changed:
                return cur != prev;
            case MemoryScanMode.Unchanged:
                return cur == prev;
            case MemoryScanMode.Increased:
                return Compare(cur, prev, type) > 0;
            case MemoryScanMode.Decreased:
                return Compare(cur, prev, type) < 0;
            case MemoryScanMode.GreaterThan:
                return Compare(cur, targetRaw, type) > 0;
            case MemoryScanMode.LessThan:
                return Compare(cur, targetRaw, type) < 0;
            case MemoryScanMode.Between:
                return Compare(cur, targetRaw, type) >= 0 && Compare(cur, targetMaxRaw, type) <= 0;
            default:
                return true;
        }
    }

    internal static int Size(MemoryValueType type) => type switch
    {
        MemoryValueType.Byte => 1,
        MemoryValueType.Int16 => 2,
        MemoryValueType.Int32 or MemoryValueType.Float => 4,
        _ => 8
    };

    internal static ulong Decode(ReadOnlySpan<byte> bytes, int size) => size switch
    {
        1 => bytes[0],
        2 => BinaryPrimitives.ReadUInt16LittleEndian(bytes),
        4 => BinaryPrimitives.ReadUInt32LittleEndian(bytes),
        _ => BinaryPrimitives.ReadUInt64LittleEndian(bytes)
    };

    internal static string Format(ulong value, MemoryValueType type, bool hex = false, bool unsigned = false)
    {
        if (hex && type is not (MemoryValueType.Float or MemoryValueType.Double))
            return type switch
            {
                MemoryValueType.Byte => $"0x{(byte)value:X2}",
                MemoryValueType.Int16 => $"0x{(ushort)value:X4}",
                MemoryValueType.Int32 => $"0x{(uint)value:X8}",
                _ => $"0x{value:X16}"
            };
        if (unsigned && type is MemoryValueType.Int16 or MemoryValueType.Int32 or MemoryValueType.Int64)
            return type switch
            {
                MemoryValueType.Int16 => ((ushort)value).ToString(CultureInfo.InvariantCulture),
                MemoryValueType.Int32 => ((uint)value).ToString(CultureInfo.InvariantCulture),
                _ => value.ToString(CultureInfo.InvariantCulture)
            };
        return type switch
        {
            MemoryValueType.Byte => ((byte)value).ToString(CultureInfo.InvariantCulture),
            MemoryValueType.Int16 => ((short)value).ToString(CultureInfo.InvariantCulture),
            MemoryValueType.Int32 => ((int)value).ToString(CultureInfo.InvariantCulture),
            MemoryValueType.Int64 => ((long)value).ToString(CultureInfo.InvariantCulture),
            MemoryValueType.Float => BitConverter.UInt32BitsToSingle((uint)value).ToString("R", CultureInfo.InvariantCulture),
            _ => BitConverter.UInt64BitsToDouble(value).ToString("R", CultureInfo.InvariantCulture)
        };
    }

    internal static bool TryParse(string text, MemoryValueType type, out ulong value)
    {
        var culture = CultureInfo.InvariantCulture;
        value = 0;
        string trimmed = text.Trim();
        bool isHex = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        if (isHex && type is not (MemoryValueType.Float or MemoryValueType.Double))
        {
            if (!ulong.TryParse(trimmed.AsSpan(2), NumberStyles.AllowHexSpecifier, culture, out ulong parsed))
                return false;
            switch (type)
            {
                case MemoryValueType.Byte when parsed <= byte.MaxValue: value = parsed; return true;
                case MemoryValueType.Int16 when parsed <= ushort.MaxValue: value = parsed; return true;
                case MemoryValueType.Int32 when parsed <= uint.MaxValue: value = parsed; return true;
                case MemoryValueType.Int64: value = parsed; return true;
                default: return false;
            }
        }
        switch (type)
        {
            case MemoryValueType.Byte when byte.TryParse(trimmed, NumberStyles.Integer, culture, out var b):
                value = b;
                return true;
            case MemoryValueType.Int16 when short.TryParse(trimmed, NumberStyles.Integer, culture, out var s):
                value = (ushort)s;
                return true;
            case MemoryValueType.Int32 when int.TryParse(trimmed, NumberStyles.Integer, culture, out var i):
                value = (uint)i;
                return true;
            case MemoryValueType.Int64 when long.TryParse(trimmed, NumberStyles.Integer, culture, out var l):
                value = (ulong)l;
                return true;
            case MemoryValueType.Float when float.TryParse(trimmed, NumberStyles.Float, culture, out var f) && float.IsFinite(f):
                value = f == 0 ? 0 : BitConverter.SingleToUInt32Bits(f);
                return true;
            case MemoryValueType.Double when double.TryParse(trimmed, NumberStyles.Float, culture, out var d) && double.IsFinite(d):
                value = d == 0 ? 0 : BitConverter.DoubleToUInt64Bits(d);
                return true;
            default:
                return false;
        }
    }

    internal static ulong Normalize(ulong value, MemoryValueType type) =>
        (type == MemoryValueType.Float && value == 0x80000000 ||
         type == MemoryValueType.Double && value == 0x8000000000000000) ? 0 : value;

    internal static bool Overlaps(ulong address, int length, ulong other, int otherLength) =>
        address < other + (ulong)otherLength && other < address + (ulong)length;

    internal static int Compare(ulong current, ulong previous, MemoryValueType type) => type switch
    {
        MemoryValueType.Int16 => ((short)current).CompareTo((short)previous),
        MemoryValueType.Int32 => ((int)current).CompareTo((int)previous),
        MemoryValueType.Int64 => ((long)current).CompareTo((long)previous),
        MemoryValueType.Float => CompareFinite(BitConverter.UInt32BitsToSingle((uint)current),
            BitConverter.UInt32BitsToSingle((uint)previous)),
        MemoryValueType.Double => CompareFinite(BitConverter.UInt64BitsToDouble(current),
            BitConverter.UInt64BitsToDouble(previous)),
        _ => current.CompareTo(previous)
    };

    static int CompareFinite(double current, double previous) =>
        double.IsFinite(current) && double.IsFinite(previous) ? current.CompareTo(previous) : 0;
}
