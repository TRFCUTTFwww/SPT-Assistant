using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using static LocalSpt5Esp.MemoryEditorAccess;

namespace LocalSpt5Esp;

internal readonly record struct PointerChainMatch(
    ulong BaseAddress,
    string BaseModuleOffset,
    long[] Offsets,
    ulong ResolvedAddress)
{
    internal string Expression => FormatPointerExpression(BaseModuleOffset, Offsets);
}

internal readonly record struct StructFieldRow(
    int Offset,
    ulong Address,
    string HexBytes,
    int Int32Value,
    uint UInt32Value,
    float FloatValue,
    ulong UInt64Value,
    ulong PointerTarget,
    string PointerLabel,
    string AsciiPreview,
    bool Changed);

internal static class MemoryPointerScanner
{
    internal static List<PointerChainMatch> Scan(ulong targetAddress, int maxDepth, ulong maxOffset, int maxResults = 200)
    {
        var results = new List<PointerChainMatch>();
        if (targetAddress == 0 || targetAddress >= AddressLimit || maxDepth < 1) return results;
        maxDepth = Math.Clamp(maxDepth, 1, 3);
        maxOffset = Math.Clamp(maxOffset, 0x10, 0x2000);
        maxResults = Math.Clamp(maxResults, 10, 500);

        // Collect readable data regions once
        var regions = CollectReadableDataRegions();
        if (regions.Count == 0) return results;

        var seenBases = new HashSet<(ulong Base, int Depth)>();
        var frontier = new List<(ulong Addr, long[] Offsets)> { (targetAddress, []) };

        for (int depth = 1; depth <= maxDepth && frontier.Count > 0 && results.Count < maxResults; depth++)
        {
            var nextFrontier = new List<(ulong Addr, long[] Offsets)>();
            var targets = new ulong[frontier.Count];
            for (int i = 0; i < frontier.Count; i++) targets[i] = frontier[i].Addr;

            var buffer = new byte[256 * 1024];
            foreach (var region in regions)
            {
                if (results.Count >= maxResults) break;
                ulong pos = region.Base;
                while (pos < region.End && results.Count < maxResults)
                {
                    int len = (int)Math.Min((ulong)buffer.Length, region.End - pos);
                    int alignedLen = len & ~7;
                    if (alignedLen >= 8 && Read(pos, buffer, alignedLen))
                    {
                        for (int off = 0; off < alignedLen && results.Count < maxResults; off += 8)
                        {
                            ulong ptr = BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(off, 8));
                            if (ptr < 0x10000 || ptr >= AddressLimit) continue;

                            for (int t = 0; t < targets.Length; t++)
                            {
                                ulong target = targets[t];
                                if (ptr > target || target - ptr > maxOffset) continue;

                                long delta = (long)(target - ptr);
                                ulong holder = pos + (ulong)off;
                                var prevOffsets = frontier[t].Offsets;
                                var chain = new long[prevOffsets.Length + 1];
                                chain[0] = delta;
                                Array.Copy(prevOffsets, 0, chain, 1, prevOffsets.Length);

                                if (TryResolveModule(holder, out string mod, out ulong modOff))
                                {
                                    if (seenBases.Add((holder, depth)))
                                    {
                                        results.Add(new(holder, $"{mod}+0x{modOff:X}", chain, targetAddress));
                                        if (results.Count >= maxResults) break;
                                    }
                                }
                                else if (depth < maxDepth && nextFrontier.Count < 1500 && seenBases.Add((holder, depth)))
                                {
                                    nextFrontier.Add((holder, chain));
                                }
                                break;
                            }
                        }
                    }
                    pos += (ulong)len;
                }
            }

            frontier = nextFrontier;
        }

        return results;
    }

    static List<Region> CollectReadableDataRegions()
    {
        var regions = new List<Region>(256);
        ulong cursor = 0x10000;
        while (cursor < AddressLimit && Query(cursor, out var region))
        {
            if (region.Readable && !region.ReadableCode && region.Size <= 64UL * 1024 * 1024)
                regions.Add(region);
            ulong next = region.End;
            if (next <= cursor) break;
            cursor = next;
        }
        return regions;
    }

    internal static StructFieldRow[] ReadStruct(ulong baseAddress, int size, int stride, byte[]? previousBytes, out byte[] currentBytes)
    {
        currentBytes = [];
        stride = stride == 8 ? 8 : 4;
        size = Math.Clamp(size, stride, 4096);
        if (!ReadBytes(baseAddress, size, out currentBytes) || currentBytes.Length < stride)
            return [];

        int count = currentBytes.Length / stride;
        var rows = new StructFieldRow[count];
        for (int i = 0; i < count; i++)
        {
            int off = i * stride;
            var span = currentBytes.AsSpan(off);
            ulong addr = baseAddress + (ulong)off;
            bool changed = previousBytes != null && off + stride <= previousBytes.Length &&
                !span[..stride].SequenceEqual(previousBytes.AsSpan(off, stride));

            int i32 = BinaryPrimitives.ReadInt32LittleEndian(span);
            uint u32 = BinaryPrimitives.ReadUInt32LittleEndian(span);
            float f32 = BitConverter.UInt32BitsToSingle(u32);
            ulong u64 = span.Length >= 8 ? BinaryPrimitives.ReadUInt64LittleEndian(span) : u32;

            ulong ptrTarget = 0;
            string ptrLabel = "";
            if (stride == 8 && u64 > 0x10000 && u64 < AddressLimit && Query(u64, out var pr) && pr.Readable)
            {
                ptrTarget = u64;
                ptrLabel = FormatModuleAddress(u64);
            }

            var hex = new StringBuilder(stride * 3);
            var ascii = new StringBuilder(stride);
            for (int b = 0; b < stride; b++)
            {
                byte v = span[b];
                if (b > 0) hex.Append(' ');
                hex.Append(v.ToString("X2"));
                ascii.Append(v is >= 32 and <= 126 ? (char)v : '.');
            }

            rows[i] = new(off, addr, hex.ToString(), i32, u32, f32, u64, ptrTarget, ptrLabel, ascii.ToString(), changed);
        }
        return rows;
    }
}
