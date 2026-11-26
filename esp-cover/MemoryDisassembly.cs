using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Iced.Intel;

namespace LocalSpt5Esp;

internal readonly record struct DisasmRow(
    ulong Address,
    byte[] Bytes,
    string HexBytes,
    string ModuleOffset,
    string Text,
    ulong BranchTarget,
    bool IsPatched,
    string? InvertLabel);

internal static class MemoryDisassembly
{
    const int MaxInstructionBytes = 15;
    internal const int MaxContextInstructions = 512;

    internal static string[] Decode(string text)
    {
        if (text.Length > 1024) return [];
        string hex = string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
        if (hex.Length is 0 or > 512 || hex.Length % 2 != 0 || !hex.All(Uri.IsHexDigit))
            return [];

        return DecodeBytes(Convert.FromHexString(hex), 0, false, out _);
    }

    internal static bool TryParseAddress(string text, out ulong address)
    {
        return MemoryEditorAccess.TryResolveSimpleAddress(text, out address);
    }

    internal static string[] ReadCode(ulong address, out ulong next)
    {
        next = 0;
        if (!MemoryEditorAccess.Query(address, out var region) || !region.ReadableCode) return [];
        // Include lookahead for the final variable-length x64 instruction on this page of results.
        const int capacity = 256 + 14;
        int length = (int)Math.Min((ulong)capacity, region.End - address);
        if (length < capacity && MemoryEditorAccess.Query(region.End, out var adjacent) && adjacent.ReadableCode)
            length += (int)Math.Min((ulong)(capacity - length), adjacent.Size);
        var bytes = new byte[length];
        if (!MemoryEditorAccess.Read(address, bytes, length)) return [];
        return DecodeBytes(bytes, address, true, out next);
    }

    internal static DisasmRow[] ReadInstructions(ulong address, int maxCount, out ulong next)
    {
        next = 0;
        if (maxCount <= 0 || !MemoryEditorAccess.Query(address, out var region) || !region.ReadableCode)
            return [];
        maxCount = Math.Min(maxCount, MaxContextInstructions * 2 + 1);
        int capacity = maxCount * MaxInstructionBytes + (MaxInstructionBytes - 1);
        int length = (int)Math.Min((ulong)capacity, region.End - address);
        while (length < capacity && MemoryEditorAccess.Query(address + (ulong)length, out var adjacent) && adjacent.ReadableCode)
            length += (int)Math.Min((ulong)(capacity - length), adjacent.End - address - (ulong)length);
        var bytes = new byte[length];
        if (!MemoryEditorAccess.Read(address, bytes, length)) return [];

        var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
        decoder.IP = address;
        var formatter = new IntelFormatter();
        var output = new StringOutput();
        var rows = new List<DisasmRow>(Math.Min(maxCount, length));
        int limitBytes = Math.Max(1, length - (MaxInstructionBytes - 1));
        while (rows.Count < maxCount && (int)(decoder.IP - address) < length)
        {
            ulong ip = decoder.IP;
            int start = (int)(ip - address);
            if (start >= limitBytes && rows.Count > 0 && length == capacity) break;
            decoder.Decode(out var insn);
            if (insn.Length <= 0 || start + insn.Length > length) break;
            formatter.Format(in insn, output);
            string text = output.ToStringAndReset();
            var slice = bytes.AsSpan(start, insn.Length).ToArray();
            string hex = BitConverter.ToString(slice).Replace("-", " ");
            ulong branch = ExtractBranchTarget(in insn);
            bool patched = MemoryCodePatcher.IsPatched(ip, insn.Length);
            string? invertLabel = TryGetInvertCode(insn.Code, out _, out string label) ? label : null;
            rows.Add(new(ip, slice, hex, MemoryEditorAccess.FormatModuleAddress(ip), text, branch, patched, invertLabel));
        }
        if (MemoryEditorAccess.Query(decoder.IP, out var nextRegion) && nextRegion.ReadableCode)
            next = decoder.IP;
        return rows.ToArray();
    }

    internal static ulong FindLeadingContext(ulong address, int maxCount, out int count)
    {
        count = 0;
        if (address == 0 || maxCount <= 0 ||
            !MemoryEditorAccess.Query(address - 1, out var region) || !region.ReadableCode)
            return address;

        maxCount = Math.Min(maxCount, MaxContextInstructions);
        ulong limit = (ulong)(maxCount * MaxInstructionBytes);
        ulong minimum = address > limit ? address - limit : 0;
        ulong start = Math.Max(region.Base, minimum);
        while (start > minimum && MemoryEditorAccess.Query(start - 1, out var adjacent) && adjacent.ReadableCode)
            start = Math.Max(adjacent.Base, minimum);

        var bytes = new byte[(int)(address - start)];
        if (!MemoryEditorAccess.Read(start, bytes, bytes.Length)) return address;

        // Build only valid instruction chains that end exactly at the highlighted address.
        var reader = new ByteArrayCodeReader(bytes);
        var decoder = Decoder.Create(64, reader);
        var lengths = new int[bytes.Length + 1];
        var successors = new int[bytes.Length];
        int best = bytes.Length;
        for (int i = bytes.Length - 1; i >= 0; i--)
        {
            reader.Position = i;
            decoder.IP = start + (ulong)i;
            decoder.Decode(out var instruction);
            int end = reader.Position;
            if (instruction.IsInvalid || (end != bytes.Length && lengths[end] == 0)) continue;
            successors[i] = end;
            lengths[i] = lengths[end] + 1;
            if (lengths[i] >= lengths[best]) best = i;
        }

        while (lengths[best] > maxCount) best = successors[best];
        count = lengths[best];
        return start + (ulong)best;
    }

    internal static DisasmRow[] ReadInstructionContext(ulong address, int before, int after, out int hitIndex, out ulong next)
    {
        after = Math.Clamp(after, 0, MaxContextInstructions);
        ulong start = FindLeadingContext(address, before, out int count);
        var rows = ReadInstructions(start, count + 1 + after, out next);
        if (count < rows.Length && rows[count].Address == address)
        {
            hitIndex = count;
            return rows;
        }
        rows = start == address ? [] : ReadInstructions(address, 1 + after, out next);
        hitIndex = rows.Length == 0 ? -1 : 0;
        if (hitIndex < 0) next = 0;
        return rows;
    }

    internal static string FormatInstructionContext(DisasmRow[] rows, int hitIndex)
    {
        if (rows.Length == 0 || hitIndex < 0 || hitIndex >= rows.Length) return "";
        var sb = new System.Text.StringBuilder(rows.Length * 96);
        sb.AppendLine($"Before: {hitIndex}  After: {rows.Length - hitIndex - 1}");
        sb.AppendLine("    Address             Module+offset                    Bytes                                         Instruction");
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            sb.AppendLine($"{(i == hitIndex ? ">>" : "  ")}  0x{row.Address:X16}  {row.ModuleOffset,-31}  {row.HexBytes,-44}  {row.Text}");
        }
        return sb.ToString();
    }

    internal static string WriteInstructionContext(string folder, DisasmRow[] rows, int hitIndex)
    {
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        string path = Path.Combine(folder, $"disassembly-{rows[hitIndex].Address:X}-{stamp}.txt");
        string temporary = path + ".tmp";
        bool created = false;
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                using var writer = new StreamWriter(file, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                writer.Write(FormatInstructionContext(rows, hitIndex));
            }
            File.Move(temporary, path);
            created = false;
            return path;
        }
        finally
        {
            if (created) File.Delete(temporary);
        }
    }

    internal static bool TryDecodeSingle(ulong address, out Instruction instruction, out byte[] rawBytes, out string formatted)
    {
        instruction = default;
        rawBytes = [];
        formatted = "";
        if (!MemoryEditorAccess.Query(address, out var region) || !region.ReadableCode) return false;
        int len = (int)Math.Min(15UL, region.End - address);
        if (len <= 0) return false;
        var buf = new byte[len];
        if (!MemoryEditorAccess.Read(address, buf, len)) return false;
        var decoder = Decoder.Create(64, new ByteArrayCodeReader(buf));
        decoder.IP = address;
        decoder.Decode(out instruction);
        if (instruction.IsInvalid || instruction.Length <= 0 || instruction.Length > len) return false;
        rawBytes = buf.AsSpan(0, instruction.Length).ToArray();
        var formatter = new IntelFormatter();
        var output = new StringOutput();
        formatter.Format(in instruction, output);
        formatted = output.ToStringAndReset();
        return true;
    }

    internal static string FormatBytes(byte[] bytes, ulong origin)
    {
        if (bytes.Length == 0) return "";
        var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
        decoder.IP = origin;
        var formatter = new IntelFormatter();
        var output = new StringOutput();
        var parts = new List<string>();
        while ((int)(decoder.IP - origin) < bytes.Length)
        {
            decoder.Decode(out var insn);
            if (insn.Length <= 0) break;
            formatter.Format(in insn, output);
            parts.Add(output.ToStringAndReset());
        }
        return string.Join("; ", parts);
    }

    /// <summary>
    /// Because x64 data breakpoints (DR0-DR3) are trap-class exceptions, Context.Rip
    /// points to the instruction immediately after the writing/accessing instruction.
    /// Walks back up to 64 bytes to find the instruction whose exact end equals trapRip.
    /// </summary>
    internal static ulong FindPrecedingInstruction(ulong trapRip, out string formatted)
    {
        formatted = "";
        if (trapRip == 0 || !MemoryEditorAccess.Query(trapRip, out var region) || !region.ReadableCode)
            return trapRip;

        ulong windowStart = trapRip > region.Base + 64 ? trapRip - 64 : region.Base;
        int backLen = (int)(trapRip - windowStart);
        int aheadLen = (int)Math.Min(15UL, region.End - trapRip);
        if (backLen <= 0)
        {
            TryDecodeSingle(trapRip, out _, out _, out formatted);
            return trapRip;
        }

        var buffer = new byte[backLen + aheadLen];
        if (!MemoryEditorAccess.Read(windowStart, buffer, buffer.Length))
            return trapRip;

        int bestScore = -1;
        Instruction bestInsn = default;
        for (int start = 0; start < backLen; start++)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(buffer, start, backLen - start));
            decoder.IP = windowStart + (ulong)start;
            int validCount = 0;
            bool ok = true;
            Instruction last = default;
            while (decoder.IP < trapRip)
            {
                decoder.Decode(out last);
                if (last.IsInvalid || last.Length <= 0 || decoder.IP > trapRip)
                {
                    ok = false;
                    break;
                }
                validCount++;
            }
            if (ok && decoder.IP == trapRip && validCount > 0)
            {
                // Prefer chains with more valid instructions AND where the final instruction touches memory
                int score = validCount * 10 + (TouchesMemory(in last) ? 100 : 0) + (last.Length >= 2 ? 2 : 0);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestInsn = last;
                }
            }
        }

        if (bestScore >= 0)
        {
            var formatter = new IntelFormatter();
            var output = new StringOutput();
            formatter.Format(in bestInsn, output);
            formatted = output.ToStringAndReset();
            return bestInsn.IP;
        }

        TryDecodeSingle(trapRip, out _, out _, out formatted);
        return trapRip;
    }

    static bool TouchesMemory(in Instruction insn)
    {
        for (int i = 0; i < insn.OpCount; i++)
        {
            var kind = insn.GetOpKind(i);
            if (kind is OpKind.Memory or OpKind.MemorySegSI or OpKind.MemorySegESI or OpKind.MemorySegRSI
                or OpKind.MemorySegDI or OpKind.MemorySegEDI or OpKind.MemorySegRDI
                or OpKind.MemoryESDI or OpKind.MemoryESEDI or OpKind.MemoryESRDI)
                return true;
        }
        return false;
    }

    internal static ulong ExtractBranchTarget(in Instruction insn)
    {
        return insn.FlowControl switch
        {
            FlowControl.UnconditionalBranch or FlowControl.ConditionalBranch or FlowControl.Call
                when insn.NearBranchTarget != 0 => insn.NearBranchTarget,
            _ => 0
        };
    }

    internal static bool TryGetInvertCode(Code code, out Code inverted, out string label)
    {
        switch (code)
        {
            // Scalar float/double SSE & AVX arithmetic (sub <-> add)
            case Code.Subss_xmm_xmmm32: inverted = Code.Addss_xmm_xmmm32; label = "subss → addss"; return true;
            case Code.Addss_xmm_xmmm32: inverted = Code.Subss_xmm_xmmm32; label = "addss → subss"; return true;
            case Code.Subsd_xmm_xmmm64: inverted = Code.Addsd_xmm_xmmm64; label = "subsd → addsd"; return true;
            case Code.Addsd_xmm_xmmm64: inverted = Code.Subsd_xmm_xmmm64; label = "addsd → subsd"; return true;
            case Code.VEX_Vsubss_xmm_xmm_xmmm32: inverted = Code.VEX_Vaddss_xmm_xmm_xmmm32; label = "vsubss → vaddss"; return true;
            case Code.VEX_Vaddss_xmm_xmm_xmmm32: inverted = Code.VEX_Vsubss_xmm_xmm_xmmm32; label = "vaddss → vsubss"; return true;
            case Code.VEX_Vsubsd_xmm_xmm_xmmm64: inverted = Code.VEX_Vaddsd_xmm_xmm_xmmm64; label = "vsubsd → vaddsd"; return true;
            case Code.VEX_Vaddsd_xmm_xmm_xmmm64: inverted = Code.VEX_Vsubsd_xmm_xmm_xmmm64; label = "vaddsd → vsubsd"; return true;
            case Code.Subps_xmm_xmmm128: inverted = Code.Addps_xmm_xmmm128; label = "subps → addps"; return true;
            case Code.Addps_xmm_xmmm128: inverted = Code.Subps_xmm_xmmm128; label = "addps → subps"; return true;

            // Integer sub <-> add
            case Code.Sub_rm32_r32: inverted = Code.Add_rm32_r32; label = "sub → add"; return true;
            case Code.Add_rm32_r32: inverted = Code.Sub_rm32_r32; label = "add → sub"; return true;
            case Code.Sub_r32_rm32: inverted = Code.Add_r32_rm32; label = "sub → add"; return true;
            case Code.Add_r32_rm32: inverted = Code.Sub_r32_rm32; label = "add → sub"; return true;
            case Code.Sub_rm32_imm8: inverted = Code.Add_rm32_imm8; label = "sub → add"; return true;
            case Code.Add_rm32_imm8: inverted = Code.Sub_rm32_imm8; label = "add → sub"; return true;
            case Code.Sub_rm32_imm32: inverted = Code.Add_rm32_imm32; label = "sub → add"; return true;
            case Code.Add_rm32_imm32: inverted = Code.Sub_rm32_imm32; label = "add → sub"; return true;
            case Code.Sub_rm64_r64: inverted = Code.Add_rm64_r64; label = "sub → add"; return true;
            case Code.Add_rm64_r64: inverted = Code.Sub_rm64_r64; label = "add → sub"; return true;
            case Code.Sub_r64_rm64: inverted = Code.Add_r64_rm64; label = "sub → add"; return true;
            case Code.Add_r64_rm64: inverted = Code.Sub_r64_rm64; label = "add → sub"; return true;
            case Code.Sub_rm64_imm8: inverted = Code.Add_rm64_imm8; label = "sub → add"; return true;
            case Code.Add_rm64_imm8: inverted = Code.Sub_rm64_imm8; label = "add → sub"; return true;
            case Code.Sub_rm64_imm32: inverted = Code.Add_rm64_imm32; label = "sub → add"; return true;
            case Code.Add_rm64_imm32: inverted = Code.Sub_rm64_imm32; label = "add → sub"; return true;

            // dec <-> inc
            case Code.Dec_rm32: inverted = Code.Inc_rm32; label = "dec → inc"; return true;
            case Code.Inc_rm32: inverted = Code.Dec_rm32; label = "inc → dec"; return true;
            case Code.Dec_rm64: inverted = Code.Inc_rm64; label = "dec → inc"; return true;
            case Code.Inc_rm64: inverted = Code.Dec_rm64; label = "inc → dec"; return true;

            // Conditional branches
            case Code.Je_rel8_64: inverted = Code.Jne_rel8_64; label = "je → jne"; return true;
            case Code.Jne_rel8_64: inverted = Code.Je_rel8_64; label = "jne → je"; return true;
            case Code.Je_rel32_64: inverted = Code.Jne_rel32_64; label = "je → jne"; return true;
            case Code.Jne_rel32_64: inverted = Code.Je_rel32_64; label = "jne → je"; return true;
            case Code.Jl_rel8_64: inverted = Code.Jge_rel8_64; label = "jl → jge"; return true;
            case Code.Jge_rel8_64: inverted = Code.Jl_rel8_64; label = "jge → jl"; return true;
            case Code.Jl_rel32_64: inverted = Code.Jge_rel32_64; label = "jl → jge"; return true;
            case Code.Jge_rel32_64: inverted = Code.Jl_rel32_64; label = "jge → jl"; return true;
            case Code.Jle_rel8_64: inverted = Code.Jg_rel8_64; label = "jle → jg"; return true;
            case Code.Jg_rel8_64: inverted = Code.Jle_rel8_64; label = "jg → jle"; return true;
            case Code.Jle_rel32_64: inverted = Code.Jg_rel32_64; label = "jle → jg"; return true;
            case Code.Jg_rel32_64: inverted = Code.Jle_rel32_64; label = "jg → jle"; return true;
            case Code.Jb_rel8_64: inverted = Code.Jae_rel8_64; label = "jb → jae"; return true;
            case Code.Jae_rel8_64: inverted = Code.Jb_rel8_64; label = "jae → jb"; return true;
            case Code.Jb_rel32_64: inverted = Code.Jae_rel32_64; label = "jb → jae"; return true;
            case Code.Jae_rel32_64: inverted = Code.Jb_rel32_64; label = "jae → jb"; return true;
            case Code.Jbe_rel8_64: inverted = Code.Ja_rel8_64; label = "jbe → ja"; return true;
            case Code.Ja_rel8_64: inverted = Code.Jbe_rel8_64; label = "ja → jbe"; return true;
            case Code.Jbe_rel32_64: inverted = Code.Ja_rel32_64; label = "jbe → ja"; return true;
            case Code.Ja_rel32_64: inverted = Code.Jbe_rel32_64; label = "ja → jbe"; return true;
            case Code.Js_rel8_64: inverted = Code.Jns_rel8_64; label = "js → jns"; return true;
            case Code.Jns_rel8_64: inverted = Code.Js_rel8_64; label = "jns → js"; return true;
            case Code.Js_rel32_64: inverted = Code.Jns_rel32_64; label = "js → jns"; return true;
            case Code.Jns_rel32_64: inverted = Code.Js_rel32_64; label = "jns → js"; return true;

            default:
                inverted = default;
                label = "";
                return false;
        }
    }

    static string[] DecodeBytes(byte[] bytes, ulong origin, bool live, out ulong next)
    {
        next = 0;
        var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
        decoder.IP = origin;
        var formatter = new IntelFormatter();
        var output = new StringOutput();
        var rows = new List<string>();
        while (decoder.IP - origin < (ulong)Math.Min(bytes.Length, 256))
        {
            ulong ip = decoder.IP;
            int start = (int)(ip - origin);
            decoder.Decode(out var instruction);
            formatter.Format(in instruction, output);
            string code = Convert.ToHexString(bytes, start, instruction.Length);
            string location = live ? $"0x{ip:X}" : $"+{start:X4}";
            rows.Add($"{location}     {code}     {output.ToStringAndReset()}");
        }
        if (live && MemoryEditorAccess.Query(decoder.IP, out var region) && region.ReadableCode)
            next = decoder.IP;
        return rows.ToArray();
    }
}
