using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;

namespace LocalSpt5Esp;

internal sealed class CodePatch
{
    internal int Id;
    internal ulong Address;
    internal string ModuleOffset = "";
    internal string Label = "";
    internal byte[] OriginalBytes = [];
    internal byte[] PatchedBytes = [];
    internal string OriginalDisasm = "";
    internal string PatchedDisasm = "";
    internal ulong CaveAddress;
    internal bool Enabled;
}

/// <summary>
/// Live x64 code patcher, NOP/invert helper, lightweight x64+SSE assembler,
/// BlockEncoder-based code cave injector, and patch manager.
/// </summary>
internal static class MemoryCodePatcher
{
    const uint MemCommitReserve = 0x3000, MemRelease = 0x8000, PageExecuteReadWrite = 0x40;
    static readonly object Gate = new();
    static readonly List<CodePatch> Patches = [];
    static int nextId = 1;

    internal static CodePatch[] GetPatches()
    {
        lock (Gate) return Patches.ToArray();
    }

    internal static bool IsPatched(ulong address, int length)
    {
        lock (Gate)
        {
            foreach (var p in Patches)
                if (p.Enabled && MemoryEditorAccess.Overlaps(address, Math.Max(1, length), p.Address, p.OriginalBytes.Length))
                    return true;
            return false;
        }
    }

    internal static bool ApplyNop(ulong address, int length, out CodePatch? patch, out string error)
    {
        patch = null;
        error = "";
        if (length <= 0)
        {
            if (!MemoryDisassembly.TryDecodeSingle(address, out var insn, out _, out _))
            {
                error = "无法解码目标地址的指令。";
                return false;
            }
            length = insn.Length;
        }
        var nops = Enumerable.Repeat((byte)0x90, length).ToArray();
        return ApplyRawPatch(address, nops, $"NOP ({length}B)", 0, out patch, out error);
    }

    internal static bool ApplyInvert(ulong address, out CodePatch? patch, out string error)
    {
        patch = null;
        error = "";
        if (!MemoryDisassembly.TryDecodeSingle(address, out var insn, out var origBytes, out _))
        {
            error = "无法解码目标地址的指令。";
            return false;
        }
        if (!MemoryDisassembly.TryGetInvertCode(insn.Code, out var inverted, out string label))
        {
            error = "该指令不支持一键反转（可改用汇编或机器码编辑）。";
            return false;
        }

        insn.Code = inverted;
        var writer = new MemoryStream();
        var encoder = Encoder.Create(64, new StreamCodeWriter(writer));
        if (!encoder.TryEncode(in insn, address, out _, out string? encodeError))
        {
            error = "反转指令重新编码失败：" + encodeError;
            return false;
        }
        var encoded = writer.ToArray();
        if (encoded.Length > origBytes.Length)
        {
            error = $"反转后的指令更长（{encoded.Length} > {origBytes.Length} 字节），请改用代码洞注入。";
            return false;
        }
        var finalBytes = PadWithNops(encoded, origBytes.Length);
        return ApplyRawPatch(address, finalBytes, label, 0, out patch, out error);
    }

    internal static bool ApplyHexOrAssembly(ulong address, string input, out CodePatch? patch, out string error)
    {
        patch = null;
        error = "";
        if (!TryParseCodeInput(input, address, out var assembled, out error))
            return false;

        if (!CollectCoveredInstructions(address, assembled.Length, out _, out int coveredBytes))
        {
            error = "无法读取目标处的完整指令边界。";
            return false;
        }
        var finalBytes = PadWithNops(assembled, coveredBytes);
        string summary = MemoryDisassembly.FormatBytes(assembled, address);
        return ApplyRawPatch(address, finalBytes, summary.Length > 48 ? summary[..48] + "…" : summary, 0, out patch, out error);
    }

    internal static bool InjectCodeCave(ulong targetAddress, string input, bool runOriginalBefore, bool runOriginalAfter,
        out CodePatch? patch, out string error)
    {
        patch = null;
        error = "";
        if (!MemoryEditorAccess.Query(targetAddress, out var region) || !region.ReadableCode)
        {
            error = "目标地址不在可读可执行代码页。";
            return false;
        }

        ulong cave = AllocateNearbyCave(targetAddress, out bool useRel32);
        if (cave == 0)
        {
            error = "无法分配可执行代码洞内存。";
            return false;
        }

        try
        {
            int jumpSize = useRel32 ? 5 : 14;
            if (!CollectCoveredInstructions(targetAddress, jumpSize, out var stolen, out int stolenLen))
            {
                MemoryEditorAccess.VirtualFree(cave, 0, MemRelease);
                error = $"无法在目标处提取至少 {jumpSize} 字节的完整指令。";
                return false;
            }

            if (!TryParseCodeInput(input, cave, out var injectedBytes, out error))
            {
                MemoryEditorAccess.VirtualFree(cave, 0, MemRelease);
                return false;
            }

            using var caveStream = new MemoryStream();
            ulong cursor = cave;

            if (runOriginalBefore && stolen.Count > 0)
            {
                if (!RelocateBlock(stolen, cursor, out var relocBefore, out error))
                {
                    MemoryEditorAccess.VirtualFree(cave, 0, MemRelease);
                    return false;
                }
                caveStream.Write(relocBefore);
                cursor += (ulong)relocBefore.Length;
            }

            // Re-assemble injected code at its exact runtime cave IP if it's text assembly
            if (!TryParseCodeInput(input, cursor, out injectedBytes, out error))
            {
                MemoryEditorAccess.VirtualFree(cave, 0, MemRelease);
                return false;
            }
            caveStream.Write(injectedBytes);
            cursor += (ulong)injectedBytes.Length;

            if (runOriginalAfter && stolen.Count > 0)
            {
                if (!RelocateBlock(stolen, cursor, out var relocAfter, out error))
                {
                    MemoryEditorAccess.VirtualFree(cave, 0, MemRelease);
                    return false;
                }
                caveStream.Write(relocAfter);
                cursor += (ulong)relocAfter.Length;
            }

            // Emit jump back to targetAddress + stolenLen
            ulong returnIp = targetAddress + (ulong)stolenLen;
            byte[] returnJump = BuildAbsoluteJump(returnIp);
            caveStream.Write(returnJump);

            var caveBytes = caveStream.ToArray();
            if (caveBytes.Length > 4096)
            {
                MemoryEditorAccess.VirtualFree(cave, 0, MemRelease);
                error = "代码洞体积超过 4096 字节上限。";
                return false;
            }

            Marshal.Copy(caveBytes, 0, (IntPtr)(long)cave, caveBytes.Length);
            MemoryEditorAccess.FlushInstructionCache(MemoryEditorAccess.CurrentProcess, cave, (nuint)caveBytes.Length);

            byte[] entryJump = useRel32 ? BuildRel32Jump(targetAddress, cave) : BuildAbsoluteJump(cave);
            byte[] finalPatch = PadWithNops(entryJump, stolenLen);
            string label = $"代码洞 → 0x{cave:X}";
            if (!ApplyRawPatch(targetAddress, finalPatch, label, cave, out patch, out error))
            {
                MemoryEditorAccess.VirtualFree(cave, 0, MemRelease);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            MemoryEditorAccess.VirtualFree(cave, 0, MemRelease);
            error = "代码洞注入失败：" + ex.Message;
            return false;
        }
    }

    internal static bool SetEnabled(int patchId, bool enabled, out string error)
    {
        error = "";
        lock (Gate)
        {
            var patch = Patches.Find(p => p.Id == patchId);
            if (patch == null) { error = "未找到该补丁。"; return false; }
            if (patch.Enabled == enabled) return true;
            byte[] bytes = enabled ? patch.PatchedBytes : patch.OriginalBytes;
            if (!MemoryEditorAccess.WriteCode(patch.Address, bytes, out _))
            {
                error = "写入代码页失败。";
                return false;
            }
            patch.Enabled = enabled;
            return true;
        }
    }

    internal static bool RemovePatch(int patchId, out string error)
    {
        error = "";
        lock (Gate)
        {
            var patch = Patches.Find(p => p.Id == patchId);
            if (patch == null) { error = "未找到该补丁。"; return false; }
            if (patch.Enabled && !MemoryEditorAccess.WriteCode(patch.Address, patch.OriginalBytes, out _))
            {
                error = "还原原字节失败。";
                return false;
            }
            patch.Enabled = false;
            if (patch.CaveAddress != 0)
            {
                MemoryEditorAccess.VirtualFree(patch.CaveAddress, 0, MemRelease);
                patch.CaveAddress = 0;
            }
            Patches.Remove(patch);
            return true;
        }
    }

    internal static void RestoreAll()
    {
        lock (Gate)
        {
            for (int i = Patches.Count - 1; i >= 0; i--)
            {
                var p = Patches[i];
                if (p.Enabled)
                    MemoryEditorAccess.WriteCode(p.Address, p.OriginalBytes, out _);
                p.Enabled = false;
                if (p.CaveAddress != 0)
                {
                    MemoryEditorAccess.VirtualFree(p.CaveAddress, 0, MemRelease);
                    p.CaveAddress = 0;
                }
            }
            Patches.Clear();
        }
    }

    static bool ApplyRawPatch(ulong address, byte[] patchedBytes, string label, ulong caveAddress, out CodePatch? patch, out string error)
    {
        patch = null;
        error = "";
        lock (Gate)
        {
            // Prevent overlapping active patches at the same range unless replacing an existing disabled/active one cleanly
            foreach (var existing in Patches)
            {
                if (MemoryEditorAccess.Overlaps(address, patchedBytes.Length, existing.Address, existing.OriginalBytes.Length))
                {
                    error = $"该地址范围已有补丁 #{existing.Id}（0x{existing.Address:X}），请先在“代码补丁”中还原后再修改。";
                    return false;
                }
            }
            if (!MemoryEditorAccess.WriteCode(address, patchedBytes, out var original))
            {
                error = "写入目标代码地址失败，请检查地址和页面权限。";
                return false;
            }
            patch = new CodePatch
            {
                Id = nextId++,
                Address = address,
                ModuleOffset = MemoryEditorAccess.FormatModuleAddress(address),
                Label = label,
                OriginalBytes = original,
                PatchedBytes = patchedBytes.ToArray(),
                OriginalDisasm = MemoryDisassembly.FormatBytes(original, address),
                PatchedDisasm = MemoryDisassembly.FormatBytes(patchedBytes, address),
                CaveAddress = caveAddress,
                Enabled = true
            };
            Patches.Add(patch);
            return true;
        }
    }

    static bool CollectCoveredInstructions(ulong address, int minimumLength, out List<Instruction> instructions, out int totalBytes)
    {
        instructions = [];
        totalBytes = 0;
        if (!MemoryEditorAccess.Query(address, out var region) || !region.ReadableCode) return false;
        int readLen = (int)Math.Min((ulong)Math.Max(minimumLength + 16, 48), region.End - address);
        if (readLen < minimumLength) return false;
        var buf = new byte[readLen];
        if (!MemoryEditorAccess.Read(address, buf, readLen)) return false;

        var decoder = Decoder.Create(64, new ByteArrayCodeReader(buf));
        decoder.IP = address;
        while (totalBytes < minimumLength && totalBytes < readLen)
        {
            decoder.Decode(out var insn);
            if (insn.IsInvalid || insn.Length <= 0 || totalBytes + insn.Length > readLen) return false;
            instructions.Add(insn);
            totalBytes += insn.Length;
        }
        return totalBytes >= minimumLength;
    }

    static bool RelocateBlock(List<Instruction> instructions, ulong targetIp, out byte[] bytes, out string error)
    {
        bytes = [];
        error = "";
        var stream = new MemoryStream();
        var block = new InstructionBlock(new StreamCodeWriter(stream), instructions, targetIp);
        if (!BlockEncoder.TryEncode(64, block, out string? msg, out _))
        {
            error = "指令重定位失败：" + msg;
            return false;
        }
        bytes = stream.ToArray();
        return true;
    }

    static ulong AllocateNearbyCave(ulong target, out bool isRel32)
    {
        // Search within +/- 1.8 GB in 64KB granularity so a 5-byte E9 rel32 jump reaches it
        const ulong maxDelta = 0x70000000;
        const ulong step = 0x10000;
        ulong minAddr = target > maxDelta ? (target - maxDelta) & ~(step - 1) : step;
        ulong maxAddr = Math.Min(MemoryEditorAccess.AddressLimit - step, (target + maxDelta) & ~(step - 1));

        for (ulong probe = (target + step) & ~(step - 1); probe < maxAddr; probe += step)
        {
            if (MemoryEditorAccess.Query(probe, out var info) && info.State == 0x10000) // MEM_FREE
            {
                ulong candidate = (Math.Max(probe, info.Base) + step - 1) & ~(step - 1);
                if (candidate < maxAddr)
                {
                    ulong allocated = MemoryEditorAccess.VirtualAlloc(candidate, 4096, MemCommitReserve, PageExecuteReadWrite);
                    if (allocated != 0 && FitsRel32(target, allocated))
                    {
                        isRel32 = true;
                        return allocated;
                    }
                    if (allocated != 0) MemoryEditorAccess.VirtualFree(allocated, 0, MemRelease);
                }
                probe = info.End > probe ? (info.End & ~(step - 1)) : probe;
            }
        }
        for (ulong probe = (target - step) & ~(step - 1); probe > minAddr; probe -= step)
        {
            if (MemoryEditorAccess.Query(probe, out var info) && info.State == 0x10000)
            {
                ulong candidate = (Math.Min(probe, info.End - step)) & ~(step - 1);
                if (candidate >= info.Base && candidate > minAddr)
                {
                    ulong allocated = MemoryEditorAccess.VirtualAlloc(candidate, 4096, MemCommitReserve, PageExecuteReadWrite);
                    if (allocated != 0 && FitsRel32(target, allocated))
                    {
                        isRel32 = true;
                        return allocated;
                    }
                    if (allocated != 0) MemoryEditorAccess.VirtualFree(allocated, 0, MemRelease);
                }
                if (info.Base <= step) break;
                probe = info.Base;
            }
        }

        // Fallback: allocate anywhere and use 14-byte absolute jump
        isRel32 = false;
        return MemoryEditorAccess.VirtualAlloc(0, 4096, MemCommitReserve, PageExecuteReadWrite);
    }

    static bool FitsRel32(ulong from, ulong to)
    {
        long delta = (long)to - (long)(from + 5);
        return delta >= int.MinValue && delta <= int.MaxValue;
    }

    static byte[] BuildRel32Jump(ulong from, ulong to)
    {
        var bytes = new byte[5];
        bytes[0] = 0xE9;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(1), (int)((long)to - (long)(from + 5)));
        return bytes;
    }

    static byte[] BuildAbsoluteJump(ulong target)
    {
        // FF 25 00 00 00 00 <8-byte absolute target>
        var bytes = new byte[14];
        bytes[0] = 0xFF;
        bytes[1] = 0x25;
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(6), target);
        return bytes;
    }

    static byte[] PadWithNops(byte[] bytes, int targetLength)
    {
        if (bytes.Length >= targetLength) return bytes;
        var padded = new byte[targetLength];
        Array.Copy(bytes, padded, bytes.Length);
        for (int i = bytes.Length; i < targetLength; i++) padded[i] = 0x90;
        return padded;
    }

    /// <summary>
    /// Accepts either raw hex bytes (e.g. "90 90" or "F3 0F 58 C1") OR x64 assembly
    /// statements separated by ';' or newlines (e.g. "addss xmm0, xmm1; nop").
    /// </summary>
    internal static bool TryParseCodeInput(string input, ulong rip, out byte[] bytes, out string error)
    {
        bytes = [];
        error = "";
        if (string.IsNullOrWhiteSpace(input))
        {
            error = "请输入十六进制机器码或 x64 汇编指令。";
            return false;
        }
        string trimmed = input.Trim();
        if (TryParseRawHex(trimmed, out bytes))
            return true;
        return TryAssembleText(trimmed, rip, out bytes, out error);
    }

    static bool TryParseRawHex(string text, out byte[] bytes)
    {
        bytes = [];
        var tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return false;
        var list = new List<byte>(tokens.Length);
        foreach (string t in tokens)
        {
            if (t.Length == 2 && byte.TryParse(t, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte b))
                list.Add(b);
            else
                return false;
        }
        bytes = list.ToArray();
        return bytes.Length > 0;
    }

    internal static bool TryAssembleText(string text, ulong rip, out byte[] bytes, out string error)
    {
        bytes = [];
        error = "";
        var rawStatements = text.Split([';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (rawStatements.Length == 0)
        {
            error = "未检测到汇编语句。";
            return false;
        }

        var asm = new Assembler(64);
        var labels = new Dictionary<string, Label>(StringComparer.OrdinalIgnoreCase);
        var cleaned = new List<string>(rawStatements.Length);

        foreach (string rawStmt in rawStatements)
        {
            string stmt = rawStmt;
            int comment = stmt.IndexOf("//", StringComparison.Ordinal);
            if (comment >= 0) stmt = stmt[..comment].Trim();
            if (stmt.Length == 0) continue;

            while (true)
            {
                int colon = stmt.IndexOf(':');
                if (colon <= 0) break;
                string candidate = stmt[..colon].Trim();
                if (!IsValidLabelName(candidate)) break;
                if (!labels.ContainsKey(candidate))
                    labels[candidate] = asm.CreateLabel(candidate);
                cleaned.Add(candidate + ":");
                stmt = stmt[(colon + 1)..].Trim();
                if (stmt.Length == 0) break;
            }
            if (stmt.Length > 0) cleaned.Add(stmt);
        }

        if (cleaned.Count == 0)
        {
            error = "未检测到汇编语句。";
            return false;
        }

        foreach (string stmt in cleaned)
        {
            if (stmt.EndsWith(':'))
            {
                var lbl = labels[stmt[..^1]];
                asm.Label(ref lbl);
                continue;
            }

            int sp = stmt.IndexOfAny([' ', '\t']);
            string mnem = (sp < 0 ? stmt : stmt[..sp]).ToLowerInvariant();
            string argsText = sp < 0 ? "" : stmt[(sp + 1)..].Trim();
            string[] args = argsText.Length == 0 ? [] : argsText.Split(',', StringSplitOptions.TrimEntries);

            if (args.Length == 1 && labels.TryGetValue(args[0], out var targetLabel))
            {
                if (TryEmitBranchToLabel(asm, mnem, targetLabel)) continue;
                error = $"不支持跳转到标签的指令：{mnem}";
                return false;
            }

            if (EmitInstruction(asm, mnem, args, out error)) continue;
            if (!MemoryTextAssembler.TryEmit(asm, mnem, args, out string? extendedError))
            {
                error = extendedError ?? error;
                return false;
            }
            error = "";
        }

        try
        {
            using var stream = new MemoryStream();
            asm.Assemble(new StreamCodeWriter(stream), rip);
            bytes = stream.ToArray();
            if (bytes.Length == 0) { error = "汇编结果为空。"; return false; }
            return true;
        }
        catch (Exception ex)
        {
            error = "汇编编码失败：" + ex.Message;
            return false;
        }
    }

    static bool IsValidLabelName(string name)
    {
        if (name.Length == 0 || (!char.IsLetter(name[0]) && name[0] != '_')) return false;
        for (int i = 1; i < name.Length; i++)
            if (!char.IsLetterOrDigit(name[i]) && name[i] != '_') return false;
        return true;
    }

    static bool TryEmitBranchToLabel(Assembler asm, string mnem, Label label)
    {
        switch (mnem)
        {
            case "jmp": asm.jmp(label); return true;
            case "call": asm.call(label); return true;
            case "je": case "jz": asm.je(label); return true;
            case "jne": case "jnz": asm.jne(label); return true;
            case "jl": case "jnge": asm.jl(label); return true;
            case "jge": case "jnl": asm.jge(label); return true;
            case "jle": case "jng": asm.jle(label); return true;
            case "jg": case "jnle": asm.jg(label); return true;
            case "jb": case "jnae": case "jc": asm.jb(label); return true;
            case "jae": case "jnb": case "jnc": asm.jae(label); return true;
            case "jbe": case "jna": asm.jbe(label); return true;
            case "ja": case "jnbe": asm.ja(label); return true;
            case "js": asm.js(label); return true;
            case "jns": asm.jns(label); return true;
            case "jo": asm.jo(label); return true;
            case "jno": asm.jno(label); return true;
            case "jp": case "jpe": asm.jp(label); return true;
            case "jnp": case "jpo": asm.jnp(label); return true;
            default: return false;
        }
    }

    internal static bool GenerateAobSignature(ulong address, int minBytes, out string aob, out string module, out ulong offset)
    {
        aob = "";
        module = "";
        offset = 0;
        if (!CollectCoveredInstructions(address, Math.Clamp(minBytes, 6, 48), out var insns, out int totalBytes))
            return false;
        var buf = new byte[totalBytes];
        if (!MemoryEditorAccess.Read(address, buf, totalBytes))
            return false;

        MemoryEditorAccess.TryResolveModule(address, out module, out offset);
        var decoder = Decoder.Create(64, new ByteArrayCodeReader(buf));
        decoder.IP = address;
        var tokens = new List<string>(totalBytes);
        int cursor = 0;
        foreach (var insn in insns)
        {
            var offsets = decoder.GetConstantOffsets(in insn);
            int dispStart = offsets.HasDisplacement ? offsets.DisplacementOffset : -1;
            int dispEnd = offsets.HasDisplacement ? dispStart + offsets.DisplacementSize : -1;
            int immStart = offsets.HasImmediate ? offsets.ImmediateOffset : -1;
            int immEnd = offsets.HasImmediate ? immStart + offsets.ImmediateSize : -1;
            bool wildcardBranch = insn.FlowControl is FlowControl.Call or FlowControl.ConditionalBranch or FlowControl.UnconditionalBranch;
            bool wildcardRip = insn.IsIPRelativeMemoryOperand;

            for (int i = 0; i < insn.Length && cursor + i < totalBytes; i++)
            {
                bool isWild = (wildcardRip && i >= dispStart && i < dispEnd) ||
                              (wildcardBranch && ((i >= dispStart && i < dispEnd) || (i >= immStart && i < immEnd)));
                tokens.Add(isWild ? "??" : buf[cursor + i].ToString("X2", CultureInfo.InvariantCulture));
            }
            cursor += insn.Length;
        }
        aob = string.Join(" ", tokens);
        return tokens.Count > 0;
    }

    static bool EmitInstruction(Assembler asm, string mnem, string[] args, out string error)
    {
        error = "";
        try
        {
            switch (mnem)
            {
                case "nop" when args.Length == 0: asm.nop(); return true;
                case "ret" when args.Length == 0: asm.ret(); return true;
                case "int3" when args.Length == 0: asm.int3(); return true;
                case "db":
                {
                    var tokens = string.Join(" ", args).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length == 0) { error = "db 需要至少一个十六进制字节。"; return false; }
                    var raw = new byte[tokens.Length];
                    for (int i = 0; i < tokens.Length; i++)
                    {
                        string t = tokens[i].StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? tokens[i][2..] : tokens[i];
                        if (!byte.TryParse(t, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out raw[i]))
                        { error = "无效的 db 字节：" + tokens[i]; return false; }
                    }
                    asm.db(raw);
                    return true;
                }
                case "inc":
                case "dec":
                case "neg":
                case "push":
                case "pop":
                    if (args.Length != 1) { error = $"{mnem} 需要 1 个操作数。"; return false; }
                    return EmitUnary(asm, mnem, args[0], out error);
                case "jmp":
                case "call":
                    if (args.Length != 1) { error = $"{mnem} 需要 1 个目标地址或寄存器。"; return false; }
                    return EmitBranch(asm, mnem, args[0], out error);
                case "mov":
                case "add":
                case "sub":
                case "xor":
                case "and":
                case "or":
                case "cmp":
                case "test":
                case "lea":
                    if (args.Length != 2) { error = $"{mnem} 需要 2 个操作数。"; return false; }
                    return EmitBinaryGpr(asm, mnem, args[0], args[1], out error);
                case "movss":
                case "addss":
                case "subss":
                case "mulss":
                case "divss":
                case "ucomiss":
                case "xorps":
                case "movsd":
                case "addsd":
                case "subsd":
                case "mulsd":
                case "divsd":
                case "ucomisd":
                case "xorpd":
                    if (args.Length != 2) { error = $"{mnem} 需要 2 个操作数。"; return false; }
                    return EmitSse(asm, mnem, args[0], args[1], out error);
                case "vzeroupper":
                    if (args.Length != 0) { error = "vzeroupper 不接受操作数。"; return false; }
                    asm.vzeroupper();
                    return true;
                case "vaddss": case "vsubss": case "vmulss": case "vdivss":
                case "vaddsd": case "vsubsd": case "vmulsd": case "vdivsd":
                case "vaddps": case "vsubps": case "vmulps": case "vdivps": case "vxorps":
                case "vaddpd": case "vsubpd": case "vmulpd": case "vdivpd": case "vxorpd":
                    return EmitVectorArithmetic(asm, mnem, args, out error);
                default:
                    error = $"不支持的指令：{mnem}（可直接输入十六进制机器码）。";
                    return false;
            }
        }
        catch (Exception ex)
        {
            error = $"指令 {mnem} 组装失败：{ex.Message}";
            return false;
        }
    }

    static bool EmitUnary(Assembler asm, string mnem, string op, out string error)
    {
        error = "";
        if (TryReg64(op, out var r64))
        {
            switch (mnem)
            {
                case "inc": asm.inc(r64); break;
                case "dec": asm.dec(r64); break;
                case "neg": asm.neg(r64); break;
                case "push": asm.push(r64); break;
                case "pop": asm.pop(r64); break;
            }
            return true;
        }
        if (TryReg32(op, out var r32) && mnem is "inc" or "dec" or "neg")
        {
            switch (mnem)
            {
                case "inc": asm.inc(r32); break;
                case "dec": asm.dec(r32); break;
                case "neg": asm.neg(r32); break;
            }
            return true;
        }
        if (TryMemory(op, out var mem, out int memSize))
        {
            var m = memSize == 8 ? __qword_ptr[mem] : __dword_ptr[mem];
            switch (mnem)
            {
                case "inc": asm.inc(m); break;
                case "dec": asm.dec(m); break;
                case "neg": asm.neg(m); break;
                case "push": asm.push(__qword_ptr[mem]); break;
                case "pop": asm.pop(__qword_ptr[mem]); break;
            }
            return true;
        }
        error = $"无法识别 {mnem} 的操作数：{op}";
        return false;
    }

    static bool EmitBranch(Assembler asm, string mnem, string op, out string error)
    {
        error = "";
        if (TryReg64(op, out var r64))
        {
            if (mnem == "jmp") asm.jmp(r64); else asm.call(r64);
            return true;
        }
        if (TryImm64(op, out ulong addr))
        {
            if (mnem == "jmp") asm.jmp(addr); else asm.call(addr);
            return true;
        }
        error = $"无法识别 {mnem} 的目标：{op}";
        return false;
    }

    static bool EmitBinaryGpr(Assembler asm, string mnem, string dst, string src, out string error)
    {
        error = "";
        if (mnem == "lea")
        {
            if (TryReg64(dst, out var lr64) && TryMemory(src, out var lmem, out _)) { asm.lea(lr64, __[lmem]); return true; }
            if (TryReg32(dst, out var lr32) && TryMemory(src, out var lmem2, out _)) { asm.lea(lr32, __[lmem2]); return true; }
            error = "lea 需要 reg, [mem] 操作数。";
            return false;
        }

        // r64, r64
        if (TryReg64(dst, out var d64) && TryReg64(src, out var s64))
        {
            switch (mnem)
            {
                case "mov": asm.mov(d64, s64); break;
                case "add": asm.add(d64, s64); break;
                case "sub": asm.sub(d64, s64); break;
                case "xor": asm.xor(d64, s64); break;
                case "and": asm.and(d64, s64); break;
                case "or": asm.or(d64, s64); break;
                case "cmp": asm.cmp(d64, s64); break;
                case "test": asm.test(d64, s64); break;
            }
            return true;
        }
        // r32, r32
        if (TryReg32(dst, out var d32) && TryReg32(src, out var s32))
        {
            switch (mnem)
            {
                case "mov": asm.mov(d32, s32); break;
                case "add": asm.add(d32, s32); break;
                case "sub": asm.sub(d32, s32); break;
                case "xor": asm.xor(d32, s32); break;
                case "and": asm.and(d32, s32); break;
                case "or": asm.or(d32, s32); break;
                case "cmp": asm.cmp(d32, s32); break;
                case "test": asm.test(d32, s32); break;
            }
            return true;
        }
        // r64, imm
        if (TryReg64(dst, out var dr64) && TryImm64(src, out ulong imm64))
        {
            if (mnem == "mov") { asm.mov(dr64, imm64); return true; }
            int imm32 = unchecked((int)imm64);
            switch (mnem)
            {
                case "add": asm.add(dr64, imm32); break;
                case "sub": asm.sub(dr64, imm32); break;
                case "xor": asm.xor(dr64, imm32); break;
                case "and": asm.and(dr64, imm32); break;
                case "or": asm.or(dr64, imm32); break;
                case "cmp": asm.cmp(dr64, imm32); break;
                case "test": asm.test(dr64, imm32); break;
            }
            return true;
        }
        // r32, imm
        if (TryReg32(dst, out var dr32) && TryImm32(src, out int imm))
        {
            switch (mnem)
            {
                case "mov": asm.mov(dr32, imm); break;
                case "add": asm.add(dr32, imm); break;
                case "sub": asm.sub(dr32, imm); break;
                case "xor": asm.xor(dr32, imm); break;
                case "and": asm.and(dr32, imm); break;
                case "or": asm.or(dr32, imm); break;
                case "cmp": asm.cmp(dr32, imm); break;
                case "test": asm.test(dr32, imm); break;
            }
            return true;
        }
        // r64, [mem]
        if (TryReg64(dst, out var mr64) && TryMemory(src, out var smem64, out _))
        {
            var m = __qword_ptr[smem64];
            switch (mnem)
            {
                case "mov": asm.mov(mr64, m); break;
                case "add": asm.add(mr64, m); break;
                case "sub": asm.sub(mr64, m); break;
                case "xor": asm.xor(mr64, m); break;
                case "and": asm.and(mr64, m); break;
                case "or": asm.or(mr64, m); break;
                case "cmp": asm.cmp(mr64, m); break;
                case "test": asm.test(m, mr64); break;
            }
            return true;
        }
        // r32, [mem]
        if (TryReg32(dst, out var mr32) && TryMemory(src, out var smem32, out _))
        {
            var m = __dword_ptr[smem32];
            switch (mnem)
            {
                case "mov": asm.mov(mr32, m); break;
                case "add": asm.add(mr32, m); break;
                case "sub": asm.sub(mr32, m); break;
                case "xor": asm.xor(mr32, m); break;
                case "and": asm.and(mr32, m); break;
                case "or": asm.or(mr32, m); break;
                case "cmp": asm.cmp(mr32, m); break;
                case "test": asm.test(m, mr32); break;
            }
            return true;
        }
        // [mem], r64
        if (TryMemory(dst, out var dmem64, out _) && TryReg64(src, out var sr64))
        {
            var m = __qword_ptr[dmem64];
            switch (mnem)
            {
                case "mov": asm.mov(m, sr64); break;
                case "add": asm.add(m, sr64); break;
                case "sub": asm.sub(m, sr64); break;
                case "xor": asm.xor(m, sr64); break;
                case "and": asm.and(m, sr64); break;
                case "or": asm.or(m, sr64); break;
                case "cmp": asm.cmp(m, sr64); break;
                case "test": asm.test(m, sr64); break;
            }
            return true;
        }
        // [mem], r32
        if (TryMemory(dst, out var dmem32, out _) && TryReg32(src, out var sr32))
        {
            var m = __dword_ptr[dmem32];
            switch (mnem)
            {
                case "mov": asm.mov(m, sr32); break;
                case "add": asm.add(m, sr32); break;
                case "sub": asm.sub(m, sr32); break;
                case "xor": asm.xor(m, sr32); break;
                case "and": asm.and(m, sr32); break;
                case "or": asm.or(m, sr32); break;
                case "cmp": asm.cmp(m, sr32); break;
                case "test": asm.test(m, sr32); break;
            }
            return true;
        }
        // [mem], imm32
        if (TryMemory(dst, out var dmemImm, out int memSz) && TryImm32(src, out int mImm))
        {
            if (memSz == 1)
            {
                var m = __byte_ptr[dmemImm];
                byte b = unchecked((byte)mImm);
                switch (mnem)
                {
                    case "mov": asm.mov(m, b); break;
                    case "add": asm.add(m, b); break;
                    case "sub": asm.sub(m, b); break;
                    case "xor": asm.xor(m, b); break;
                    case "and": asm.and(m, b); break;
                    case "or": asm.or(m, b); break;
                    case "cmp": asm.cmp(m, b); break;
                    case "test": asm.test(m, b); break;
                }
                return true;
            }
            var md = memSz == 8 ? __qword_ptr[dmemImm] : __dword_ptr[dmemImm];
            switch (mnem)
            {
                case "mov": asm.mov(md, mImm); break;
                case "add": asm.add(md, mImm); break;
                case "sub": asm.sub(md, mImm); break;
                case "xor": asm.xor(md, mImm); break;
                case "and": asm.and(md, mImm); break;
                case "or": asm.or(md, mImm); break;
                case "cmp": asm.cmp(md, mImm); break;
                case "test": asm.test(md, mImm); break;
            }
            return true;
        }

        error = $"不支持的操作数组合：{mnem} {dst}, {src}";
        return false;
    }

    static bool EmitSse(Assembler asm, string mnem, string dst, string src, out string error)
    {
        error = "";
        // xmm, xmm
        if (TryXmm(dst, out var dx) && TryXmm(src, out var sx))
        {
            switch (mnem)
            {
                case "movss": asm.movss(dx, sx); break;
                case "addss": asm.addss(dx, sx); break;
                case "subss": asm.subss(dx, sx); break;
                case "mulss": asm.mulss(dx, sx); break;
                case "divss": asm.divss(dx, sx); break;
                case "ucomiss": asm.ucomiss(dx, sx); break;
                case "xorps": asm.xorps(dx, sx); break;
                case "movsd": asm.movsd(dx, sx); break;
                case "addsd": asm.addsd(dx, sx); break;
                case "subsd": asm.subsd(dx, sx); break;
                case "mulsd": asm.mulsd(dx, sx); break;
                case "divsd": asm.divsd(dx, sx); break;
                case "ucomisd": asm.ucomisd(dx, sx); break;
                case "xorpd": asm.xorpd(dx, sx); break;
            }
            return true;
        }
        // xmm, [mem]
        if (TryXmm(dst, out var dxm) && TryMemory(src, out var smem, out _))
        {
            var m = mnem.EndsWith("sd") || mnem == "xorpd" ? __qword_ptr[smem] : __dword_ptr[smem];
            switch (mnem)
            {
                case "movss": asm.movss(dxm, m); break;
                case "addss": asm.addss(dxm, m); break;
                case "subss": asm.subss(dxm, m); break;
                case "mulss": asm.mulss(dxm, m); break;
                case "divss": asm.divss(dxm, m); break;
                case "ucomiss": asm.ucomiss(dxm, m); break;
                case "xorps": asm.xorps(dxm, __xmmword_ptr[smem]); break;
                case "movsd": asm.movsd(dxm, m); break;
                case "addsd": asm.addsd(dxm, m); break;
                case "subsd": asm.subsd(dxm, m); break;
                case "mulsd": asm.mulsd(dxm, m); break;
                case "divsd": asm.divsd(dxm, m); break;
                case "ucomisd": asm.ucomisd(dxm, m); break;
                case "xorpd": asm.xorpd(dxm, __xmmword_ptr[smem]); break;
            }
            return true;
        }
        // [mem], xmm (movss / movsd)
        if (TryMemory(dst, out var dmem, out _) && TryXmm(src, out var sxm))
        {
            if (mnem == "movss") { asm.movss(__dword_ptr[dmem], sxm); return true; }
            if (mnem == "movsd") { asm.movsd(__qword_ptr[dmem], sxm); return true; }
        }
        error = $"不支持的 SSE 操作数组合：{mnem} {dst}, {src}";
        return false;
    }

    static bool EmitVectorArithmetic(Assembler asm, string mnem, string[] args, out string error)
    {
        error = $"不支持的 AVX 操作数组合：{mnem} {string.Join(", ", args)}";
        if (args.Length != 3 || !TryVectorRegister(args[0], out var dst, out int width) ||
            !TryVectorRegister(args[1], out var src1, out int srcWidth) || srcWidth != width)
            return false;

        var (xmm, ymm, zmm, scalarSize) = mnem switch
        {
            "vaddss" => (Code.VEX_Vaddss_xmm_xmm_xmmm32, Code.INVALID, Code.INVALID, 4),
            "vsubss" => (Code.VEX_Vsubss_xmm_xmm_xmmm32, Code.INVALID, Code.INVALID, 4),
            "vmulss" => (Code.VEX_Vmulss_xmm_xmm_xmmm32, Code.INVALID, Code.INVALID, 4),
            "vdivss" => (Code.VEX_Vdivss_xmm_xmm_xmmm32, Code.INVALID, Code.INVALID, 4),
            "vaddsd" => (Code.VEX_Vaddsd_xmm_xmm_xmmm64, Code.INVALID, Code.INVALID, 8),
            "vsubsd" => (Code.VEX_Vsubsd_xmm_xmm_xmmm64, Code.INVALID, Code.INVALID, 8),
            "vmulsd" => (Code.VEX_Vmulsd_xmm_xmm_xmmm64, Code.INVALID, Code.INVALID, 8),
            "vdivsd" => (Code.VEX_Vdivsd_xmm_xmm_xmmm64, Code.INVALID, Code.INVALID, 8),
            "vaddps" => (Code.VEX_Vaddps_xmm_xmm_xmmm128, Code.VEX_Vaddps_ymm_ymm_ymmm256, Code.EVEX_Vaddps_zmm_k1z_zmm_zmmm512b32_er, 0),
            "vsubps" => (Code.VEX_Vsubps_xmm_xmm_xmmm128, Code.VEX_Vsubps_ymm_ymm_ymmm256, Code.EVEX_Vsubps_zmm_k1z_zmm_zmmm512b32_er, 0),
            "vmulps" => (Code.VEX_Vmulps_xmm_xmm_xmmm128, Code.VEX_Vmulps_ymm_ymm_ymmm256, Code.EVEX_Vmulps_zmm_k1z_zmm_zmmm512b32_er, 0),
            "vdivps" => (Code.VEX_Vdivps_xmm_xmm_xmmm128, Code.VEX_Vdivps_ymm_ymm_ymmm256, Code.EVEX_Vdivps_zmm_k1z_zmm_zmmm512b32_er, 0),
            "vxorps" => (Code.VEX_Vxorps_xmm_xmm_xmmm128, Code.VEX_Vxorps_ymm_ymm_ymmm256, Code.EVEX_Vxorps_zmm_k1z_zmm_zmmm512b32, 0),
            "vaddpd" => (Code.VEX_Vaddpd_xmm_xmm_xmmm128, Code.VEX_Vaddpd_ymm_ymm_ymmm256, Code.EVEX_Vaddpd_zmm_k1z_zmm_zmmm512b64_er, 0),
            "vsubpd" => (Code.VEX_Vsubpd_xmm_xmm_xmmm128, Code.VEX_Vsubpd_ymm_ymm_ymmm256, Code.EVEX_Vsubpd_zmm_k1z_zmm_zmmm512b64_er, 0),
            "vmulpd" => (Code.VEX_Vmulpd_xmm_xmm_xmmm128, Code.VEX_Vmulpd_ymm_ymm_ymmm256, Code.EVEX_Vmulpd_zmm_k1z_zmm_zmmm512b64_er, 0),
            "vdivpd" => (Code.VEX_Vdivpd_xmm_xmm_xmmm128, Code.VEX_Vdivpd_ymm_ymm_ymmm256, Code.EVEX_Vdivpd_zmm_k1z_zmm_zmmm512b64_er, 0),
            "vxorpd" => (Code.VEX_Vxorpd_xmm_xmm_xmmm128, Code.VEX_Vxorpd_ymm_ymm_ymmm256, Code.EVEX_Vxorpd_zmm_k1z_zmm_zmmm512b64, 0),
            _ => (Code.INVALID, Code.INVALID, Code.INVALID, 0)
        };
        Code code = width switch { 16 => xmm, 32 => ymm, _ => zmm };
        if (code == Code.INVALID) return false;

        if (TryVectorRegister(args[2], out var src2, out int thirdWidth) && thirdWidth == width)
            asm.AddInstruction(Instruction.Create(code, dst, src1, src2));
        else if (TryVectorMemory(args[2], scalarSize == 0 ? width : scalarSize, out var memory))
            asm.AddInstruction(Instruction.Create(code, dst, src1, memory));
        else
            return false;

        error = "";
        return true;
    }

    static bool TryVectorRegister(string text, out Register register, out int width)
    {
        register = Register.None;
        width = 0;
        if (text.Length is < 4 or > 5 ||
            !int.TryParse(text.AsSpan(3), NumberStyles.None, CultureInfo.InvariantCulture, out int index))
            return false;
        var (first, bytes, count) = text[..3].ToLowerInvariant() switch
        {
            "xmm" => (Register.XMM0, 16, 16),
            "ymm" => (Register.YMM0, 32, 16),
            "zmm" => (Register.ZMM0, 64, 32),
            _ => (Register.None, 0, 0)
        };
        if (index >= count) return false;
        register = first + index;
        width = bytes;
        return true;
    }

    static bool TryVectorMemory(string text, int size, out MemoryOperand memory)
    {
        memory = default;
        // Legacy operands assume a dword for unqualified memory. Vector operands infer their width.
        if (!TryMemory(text, out var operand, out int sizeHint, defaultSizeHint: 0) ||
            (sizeHint != 0 && sizeHint != size))
            return false;
        memory = operand.ToMemoryOperand(64);
        return true;
    }

    static bool TryReg64(string text, out AssemblerRegister64 reg)
    {
        switch (text.Trim().ToLowerInvariant())
        {
            case "rax": reg = rax; return true;
            case "rcx": reg = rcx; return true;
            case "rdx": reg = rdx; return true;
            case "rbx": reg = rbx; return true;
            case "rsp": reg = rsp; return true;
            case "rbp": reg = rbp; return true;
            case "rsi": reg = rsi; return true;
            case "rdi": reg = rdi; return true;
            case "r8": reg = r8; return true;
            case "r9": reg = r9; return true;
            case "r10": reg = r10; return true;
            case "r11": reg = r11; return true;
            case "r12": reg = r12; return true;
            case "r13": reg = r13; return true;
            case "r14": reg = r14; return true;
            case "r15": reg = r15; return true;
            default: reg = default; return false;
        }
    }

    static bool TryReg32(string text, out AssemblerRegister32 reg)
    {
        switch (text.Trim().ToLowerInvariant())
        {
            case "eax": reg = eax; return true;
            case "ecx": reg = ecx; return true;
            case "edx": reg = edx; return true;
            case "ebx": reg = ebx; return true;
            case "esp": reg = esp; return true;
            case "ebp": reg = ebp; return true;
            case "esi": reg = esi; return true;
            case "edi": reg = edi; return true;
            case "r8d": reg = r8d; return true;
            case "r9d": reg = r9d; return true;
            case "r10d": reg = r10d; return true;
            case "r11d": reg = r11d; return true;
            case "r12d": reg = r12d; return true;
            case "r13d": reg = r13d; return true;
            case "r14d": reg = r14d; return true;
            case "r15d": reg = r15d; return true;
            default: reg = default; return false;
        }
    }

    static bool TryXmm(string text, out AssemblerRegisterXMM reg)
    {
        switch (text.Trim().ToLowerInvariant())
        {
            case "xmm0": reg = xmm0; return true;
            case "xmm1": reg = xmm1; return true;
            case "xmm2": reg = xmm2; return true;
            case "xmm3": reg = xmm3; return true;
            case "xmm4": reg = xmm4; return true;
            case "xmm5": reg = xmm5; return true;
            case "xmm6": reg = xmm6; return true;
            case "xmm7": reg = xmm7; return true;
            case "xmm8": reg = xmm8; return true;
            case "xmm9": reg = xmm9; return true;
            case "xmm10": reg = xmm10; return true;
            case "xmm11": reg = xmm11; return true;
            case "xmm12": reg = xmm12; return true;
            case "xmm13": reg = xmm13; return true;
            case "xmm14": reg = xmm14; return true;
            case "xmm15": reg = xmm15; return true;
            default: reg = default; return false;
        }
    }

    static bool TryMemory(string text, out AssemblerMemoryOperand mem, out int sizeHint, int defaultSizeHint = 4)
    {
        mem = default;
        sizeHint = defaultSizeHint;
        string s = text.Trim();
        string lower = s.ToLowerInvariant();
        if (lower.StartsWith("byte ptr")) { sizeHint = 1; s = s[8..].Trim(); }
        else if (lower.StartsWith("word ptr")) { sizeHint = 2; s = s[8..].Trim(); }
        else if (lower.StartsWith("dword ptr")) { sizeHint = 4; s = s[9..].Trim(); }
        else if (lower.StartsWith("qword ptr")) { sizeHint = 8; s = s[9..].Trim(); }
        else if (defaultSizeHint == 0 && lower.StartsWith("xmmword ptr")) { sizeHint = 16; s = s[11..].Trim(); }
        else if (defaultSizeHint == 0 && lower.StartsWith("ymmword ptr")) { sizeHint = 32; s = s[11..].Trim(); }
        else if (defaultSizeHint == 0 && lower.StartsWith("zmmword ptr")) { sizeHint = 64; s = s[11..].Trim(); }
        if (!s.StartsWith('[') || !s.EndsWith(']')) return false;

        string inner = s[1..^1].Trim();
        if (inner.Length == 0) return false;

        // Parse terms separated by + / -: baseReg, optional indexReg*scale, optional displacement
        AssemblerRegister64? baseReg = null;
        AssemblerRegister64? indexReg = null;
        int scale = 1;
        long disp = 0;

        int i = 0;
        while (i < inner.Length)
        {
            int sign = 1;
            if (inner[i] == '+') { sign = 1; i++; }
            else if (inner[i] == '-') { sign = -1; i++; }
            while (i < inner.Length && char.IsWhiteSpace(inner[i])) i++;
            int start = i;
            while (i < inner.Length && inner[i] != '+' && inner[i] != '-') i++;
            string term = inner[start..i].Trim();
            if (term.Length == 0) return false;

            int star = term.IndexOf('*');
            if (star > 0)
            {
                if (sign < 0 || indexReg != null) return false;
                string rPart = term[..star].Trim(), sPart = term[(star + 1)..].Trim();
                if (!TryReg64(rPart, out var ir) || !int.TryParse(sPart, out int sc) || sc is not (1 or 2 or 4 or 8))
                    return false;
                indexReg = ir;
                scale = sc;
            }
            else if (TryReg64(term, out var r))
            {
                if (sign < 0) return false;
                if (baseReg == null) baseReg = r;
                else if (indexReg == null) { indexReg = r; scale = 1; }
                else return false;
            }
            else if (TryImm64(term, out ulong mag))
            {
                disp += sign * (long)mag;
            }
            else
            {
                return false;
            }
        }

        if (baseReg is {} b && indexReg is {} idx)
            mem = b + idx * scale + disp;
        else if (baseReg is {} bOnly)
            mem = bOnly + disp;
        else if (indexReg is {} idxOnly)
            mem = idxOnly * scale + disp;
        else
            return false;
        return true;
    }

    static bool TryImm64(string text, out ulong value)
    {
        string s = text.Trim();
        bool neg = s.StartsWith('-');
        if (neg) s = s[1..].Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (!ulong.TryParse(s.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value))
                return false;
            if (neg) value = unchecked((ulong)-(long)value);
            return true;
        }
        if (s.EndsWith('h') || s.EndsWith('H'))
        {
            if (!ulong.TryParse(s.AsSpan(0, s.Length - 1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value))
                return false;
            if (neg) value = unchecked((ulong)-(long)value);
            return true;
        }
        if (long.TryParse(neg ? "-" + s : s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long signed))
        {
            value = unchecked((ulong)signed);
            return true;
        }
        return ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    static bool TryImm32(string text, out int value)
    {
        value = 0;
        if (!TryImm64(text, out ulong raw)) return false;
        value = unchecked((int)raw);
        return true;
    }
}
