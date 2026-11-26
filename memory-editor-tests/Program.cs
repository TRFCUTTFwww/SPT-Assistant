using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Iced.Intel;
using LocalSpt5Esp;
using static Iced.Intel.AssemblerRegisters;
using static LocalSpt5Esp.MemoryEditorAccess;

if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess)
    throw new PlatformNotSupportedException("This test requires 64-bit Windows.");

Assert(Marshal.SizeOf<Region>() == 48, "VirtualQuery region size");
Assert(Marshal.OffsetOf<Region>(nameof(Region.Size)).ToInt64() == 24, "VirtualQuery region size offset");

int pageSize = Environment.SystemPageSize;
var decoded = MemoryDisassembly.Decode("48 89 D8 C3");
Assert(decoded.Length == 2 && decoded[0].Contains("mov") && decoded[1].StartsWith("+0003") &&
    decoded[1].Contains("ret"), "x64 instruction boundaries");
Assert(MemoryDisassembly.Decode("48").Length == 1, "truncated instruction consumes its bytes");
foreach (string invalid in new[] { "", "9", "GG", new string('0', 514) })
    Assert(MemoryDisassembly.Decode(invalid).Length == 0, "invalid disassembly input");

// Round-trip AVX encodings without executing instructions on the test CPU.
foreach (var (input, code, dst, src1, src2, memorySize) in new[]
{
    ("VADDSS xmm0, xmm1, xmm2", Code.VEX_Vaddss_xmm_xmm_xmmm32, Register.XMM0, Register.XMM1, Register.XMM2, 0),
    ("vsubsd xmm3, xmm4, qword ptr [rcx+rdx*2-0x10]", Code.VEX_Vsubsd_xmm_xmm_xmmm64, Register.XMM3, Register.XMM4, Register.None, 8),
    ("vxorps xmm3, xmm4, xmm5", Code.VEX_Vxorps_xmm_xmm_xmmm128, Register.XMM3, Register.XMM4, Register.XMM5, 0),
    ("vmulps ymm8, ymm1, ymmword ptr [rcx+rdx*2-0x10]", Code.VEX_Vmulps_ymm_ymm_ymmm256, Register.YMM8, Register.YMM1, Register.None, 32),
    ("vdivpd zmm31, zmm16, zmmword ptr [rcx+rdx*2-0x10]", Code.EVEX_Vdivpd_zmm_k1z_zmm_zmmm512b64_er, Register.ZMM31, Register.ZMM16, Register.None, 64),
    ("vaddps zmm4, zmm5, zmm6", Code.EVEX_Vaddps_zmm_k1z_zmm_zmmm512b32_er, Register.ZMM4, Register.ZMM5, Register.ZMM6, 0)
})
{
    Assert(MemoryCodePatcher.TryParseCodeInput(input + "; vzeroupper", 0x1000, out byte[] encoded, out string error),
        "assemble " + input + ": " + error);
    var decoder = Decoder.Create(64, new ByteArrayCodeReader(encoded));
    var instruction = decoder.Decode();
    Assert(instruction.Code == code && instruction.Op0Register == dst && instruction.Op1Register == src1,
        "AVX opcode and registers: " + input);
    if (memorySize != 0)
        Assert(instruction.Op2Kind == OpKind.Memory && instruction.MemorySize.GetSize() == memorySize &&
            instruction.MemoryBase == Register.RCX && instruction.MemoryIndex == Register.RDX &&
            instruction.MemoryIndexScale == 2 && instruction.MemoryDisplacement64 == unchecked((ulong)-16),
            "AVX memory operand: " + input);
    else
        Assert(instruction.Op2Register == src2, "AVX source register: " + input);
    Assert(decoder.Decode().Code == Code.VEX_Vzeroupper && decoder.IP == (ulong)encoded.Length,
        "vzeroupper and complete output");
}
foreach (string invalid in new[]
{
    "vaddss xmm0, xmm1", "vaddss ymm0, ymm1, ymm2", "vaddps ymm0, xmm1, ymm2",
    "vaddps ymm0, ymm1, xmm2", "vaddps ymm0, ymm1, xmmword ptr [rcx]",
    "vaddsd xmm0, xmm1, dword ptr [rcx]", "vaddps zmm32, zmm1, zmm2", "vzeroupper ymm0"
})
    Assert(!MemoryCodePatcher.TryParseCodeInput(invalid, 0x1000, out byte[] encoded, out string error) &&
        encoded.Length == 0 && error.Length > 0, "reject invalid AVX input: " + invalid);

foreach (var (input, mnemonic, memorySize) in new[]
{
    ("vmovdqu xmm2, xmm5", Mnemonic.Vmovdqu, 0),
    ("vmovdqu xmm1, xmmword ptr [rax+4]", Mnemonic.Vmovdqu, 16),
    ("vmovdqu ymmword ptr [rax+rdx*2+20h], ymm8", Mnemonic.Vmovdqu, 32),
    ("vmovdqu ymm12, [eax+ecx*4]", Mnemonic.Vmovdqu, 32),
    ("vmovdqu64 zmm20{k2}{z}, zmmword ptr [rax]", Mnemonic.Vmovdqu64, 64),
    ("movzx r11d, byte ptr gs:[rax+1]", Mnemonic.Movzx, 1),
    ("vaddps zmm2, zmm3, dword ptr [rax]{1to16}", Mnemonic.Vaddps, 4),
    ("vpshufd ymm2, ymm4, 0xFF", Mnemonic.Vpshufd, 0),
    ("popcnt rax, rcx", Mnemonic.Popcnt, 0),
    ("ret 16", Mnemonic.Ret, 0),
    ("cpuid", Mnemonic.Cpuid, 0)
})
{
    Assert(MemoryCodePatcher.TryParseCodeInput(input, 0x1000, out byte[] encoded, out string error),
        "assemble using Iced forms: " + input + ": " + error);
    var decoder = Decoder.Create(64, new ByteArrayCodeReader(encoded));
    var instruction = decoder.Decode();
    Assert(!instruction.IsInvalid && instruction.Mnemonic == mnemonic && decoder.IP == (ulong)encoded.Length,
        "round-trip Iced form: " + input);
    if (memorySize > 0) Assert(instruction.MemorySize.GetSize() == memorySize, "memory width: " + input);
    if (mnemonic == Mnemonic.Vmovdqu64)
        Assert(instruction.OpMask == Register.K2 && instruction.ZeroingMasking, "EVEX mask and zeroing");
    if (mnemonic == Mnemonic.Ret)
        Assert(instruction.Immediate16 == 16, "ret preserves its stack adjustment");
}
foreach (string invalid in new[] { "vmovdqu ymm1, xmm4", "vmovdqu xmm1, ymmword ptr [rax]", "vpshufd ymm2, ymm4, 256" })
    Assert(!MemoryCodePatcher.TryParseCodeInput(invalid, 0x1000, out _, out _), "reject invalid Iced form: " + invalid);

const uint reserveAndCommit = 0x3000, release = 0x8000;
const uint readOnly = 2, readWrite = 4, executeReadWrite = 0x40;
ulong address = VirtualAlloc(0, (nuint)(2 * pageSize), reserveAndCommit, readWrite);
Assert(address != 0, "test allocation");
try
{
    ulong secondPage = address + (ulong)pageSize;
    Assert(VirtualProtect(secondPage, (nuint)pageSize, readOnly, out _), "test read-only page");
    Assert(Query(address, out var writable) && writable.Editable && writable.Allocation == address &&
        writable.Base == address && writable.Size == (ulong)pageSize && writable.End == secondPage, "writable page");
    Assert(Query(secondPage, out var other) && !other.Editable && other.Allocation == address, "read-only page");
    Assert(ReadHexWindow(secondPage + 3, 128, 256, out ulong window, out byte[] bytesAround) &&
        window == secondPage - 128 && bytesAround.Length == 256, "hex jump includes bytes on both sides of a readable boundary");
    Assert(ReadHexWindow(address + 3, 128, 256, out window, out bytesAround) && window <= address &&
        address + 3 - window < (ulong)bytesAround.Length, "hex window keeps the selected address at a region boundary");

    var buffer = new byte[8];
    foreach (var (type, text) in new[]
    {
        (MemoryValueType.Byte, "255"), (MemoryValueType.Int16, "-32768"),
        (MemoryValueType.Int32, "-2147483648"), (MemoryValueType.Int64, "-9223372036854775808"),
        (MemoryValueType.Float, "-1.25"), (MemoryValueType.Double, "3.141592653589793")
    })
    {
        Assert(TryParse(text, type, out var value), $"parse {type}");
        int size = Size(type);
        Assert(Write(secondPage - (ulong)size, address, buffer, size, value), $"write {type}");
        Array.Clear(buffer);
        Assert(Read(secondPage - (ulong)size, buffer, size), $"read {type}");
        Assert(Decode(buffer, size) == value && Format(value, type) == text, $"round trip {type}");
    }

    Assert(!Write(secondPage - 3, address, buffer, 4, 8), "reject crossing a region boundary");
    Assert(!Write(secondPage, address, buffer, 4, 8), "reject writing to read-only memory");
    Assert(!Write(address, secondPage, buffer, 4, 8), "reject different allocation");
    Assert(!Read(0, buffer, 4) && !Read(address, buffer, 9), "reject invalid reads");
    Assert(!Write(address, address, buffer, 0, 8) && !Write(address, address, buffer, 9, 8), "reject invalid writes");
    Assert(Overlaps(address, 8, address + 7, 1) && !Overlaps(address, 8, address + 8, 1), "address overlap");
    Assert(Compare(unchecked((uint)-1), 0, MemoryValueType.Int32) < 0, "signed comparison");
    Assert(Compare(BitConverter.SingleToUInt32Bits(float.NaN), 0, MemoryValueType.Float) == 0, "unordered float");
    Assert(Normalize(BitConverter.DoubleToUInt64Bits(-0.0), MemoryValueType.Double) == 0, "signed zero");
    Assert(MemoryDisassembly.TryParseAddress($" 0x{address:X} ", out var ip) && ip == address, "code address input");
    foreach (string invalid in new[] { "0", "0x", "-1", "xyz", "FFFFFFFFFFFFFFFF" })
        Assert(!MemoryDisassembly.TryParseAddress(invalid, out _), "invalid code address");
    Assert(MemoryDisassembly.ReadCode(address, out _).Length == 0, "reject data address");

    // 1. Module resolution + 2-level pointer chain
    Assert(TryGetModuleBase("kernel32.dll", out ulong k32) && k32 > 0, "resolve kernel32 base");
    Assert(TryResolveModule(k32 + 0x100, out string modName, out ulong modOff) &&
        modName.Equals("kernel32.dll", StringComparison.OrdinalIgnoreCase) && modOff == 0x100, "reverse module resolution");
    Assert(TryResolveSimpleAddress("kernel32.dll+0x100", out ulong resolvedMod) && resolvedMod == k32 + 0x100, "module+offset parse");
    Assert(TryResolveSimpleAddress("kernel32.dll - 0x80", out ulong beforeModule) && beforeModule == k32 - 0x80,
        "module negative displacement");
    foreach (string outside in new[] { "0x100+0xFFFFFFFFFFFFFF80", "0x7FFFFFFFFFFF+1", "0x100-0x100", "0x100-0x101" })
        Assert(!TryResolveSimpleAddress(outside, out _), "reject overflowing or zero address: " + outside);

    const string testBuild = "test-build-1";
    Assert(!MemoryValuePreset.TryCreate(testBuild, address, [], MemoryValueType.Int32, "12", "", out _),
        "cannot persist a temporary numeric address");
    Assert(MemoryValuePreset.TryCreate(testBuild, resolvedMod, [16, -8], MemoryValueType.Int32, "0xFFFFFFFF", "Debug value",
        out var preset) && preset is { Value: "-1" }, "save a typed value and a module-relative pointer chain");
    string presetFolder = Directory.CreateTempSubdirectory("spt-memory-value-presets-").FullName;
    string presetFile = Path.Combine(presetFolder, "values.json");
    try
    {
        MemoryValuePresets.Save(presetFile, preset!);
        var restored = MemoryValuePresets.Load(presetFile).Single();
        Assert(restored.SameTarget(preset!) && restored.Value == "-1" &&
            restored.TryResolveBase(testBuild, out ulong restoredBase) && restoredBase == resolvedMod &&
            !restored.TryResolveBase("test-build-2", out _), "resolve a saved base only for the matching build");
        Assert(!(restored with { Offset = AddressLimit }).TryResolveBase(testBuild, out _), "reject invalid module displacement");
        Assert(MemoryValuePresets.Save(presetFile, restored with { Value = "20" }).Single().Value == "20",
            "overwrite an existing target rather than duplicating it");
        Assert(MemoryValuePresets.Remove(presetFile, restored).Count == 0 && MemoryValuePresets.Load(presetFile).Count == 0,
            "remove a persisted preset");
    }
    finally
    {
        File.Delete(presetFile);
        File.Delete(presetFile + ".bak");
        File.Delete(presetFile + ".tmp");
        Directory.Delete(presetFolder);
    }

    ulong ptrBase = address + 0x100;
    ulong ptrMid = address + 0x200;
    ulong ptrTarget = address + 0x300 + 0x18;
    Marshal.WriteInt64((IntPtr)(long)ptrBase, (long)ptrMid);
    Marshal.WriteInt64((IntPtr)(long)(ptrMid + 0x10), (long)(address + 0x300));
    Assert(TryParsePointerExpression($"[[0x{ptrBase:X}]+0x10]+0x18", out ulong parsedBase, out long[] offs, out _), "parse 2-level pointer");
    Assert(parsedBase == ptrBase && offs.Length == 2 && offs[0] == 0x10 && offs[1] == 0x18, "pointer offsets");
    Assert(TryResolvePointerChain(parsedBase, offs, out ulong finalPtr) && finalPtr == ptrTarget, "resolve 2-level pointer");

    // 2. AOB + Rounded float + Range scan filters
    Assert(TryParseAob("48 89 ?? 24 ? 57", out byte[] pat, out byte[] mask) &&
        pat.Length == 6 && pat[0] == 0x48 && mask[2] == 0 && mask[4] == 0 && pat[5] == 0x57, "AOB pattern parsing");
    Assert(!TryParseAob("?? ??", out _, out _), "reject all-wildcard AOB");
    Assert(TryParseRoundedBounds("100", MemoryValueType.Float, out double lo, out double hi) && lo == 99.5 && hi == 100.5, "rounded float bounds");
    Assert(MatchesFilter(BitConverter.SingleToUInt32Bits(99.83f), 0, MemoryValueType.Float, MemoryScanMode.Rounded, 0, 0, lo, hi), "99.83 matches rounded 100");
    Assert(!MatchesFilter(BitConverter.SingleToUInt32Bits(100.6f), 0, MemoryValueType.Float, MemoryScanMode.Rounded, 0, 0, lo, hi), "100.6 outside rounded 100");
    Assert(MatchesFilter(50, 0, MemoryValueType.Int32, MemoryScanMode.Between, 10, 60, 0, 0), "between filter");

    // 3. Disassembly boundary test (original test)
    ulong start = secondPage - 262;
    byte[] sample = Enumerable.Repeat((byte)0x90, 262).ToArray();
    sample[255] = 0xE9;
    Array.Clear(sample, 256, 4);
    sample[^1] = 0xC3;
    Marshal.Copy(sample, 0, (IntPtr)(long)start, sample.Length);
    Assert(VirtualProtect(address, (nuint)pageSize, 0x20, out _), "readable code page");
    var rows = MemoryDisassembly.ReadCode(start, out var continuation);
    Assert(rows.Length == 256 && rows[0].StartsWith($"0x{start:X} ") &&
        rows[^1].Contains("jmp") && rows[^1].Contains((start + 260).ToString("X"), StringComparison.OrdinalIgnoreCase)
        && continuation == start + 260, "live addresses, relative branch and whole-instruction continuation");
    rows = MemoryDisassembly.ReadCode(continuation, out var end);
    Assert(rows.Length == 2 && rows[^1].Contains("ret") && end == 0, "stop at data page");

    ulong leading = MemoryDisassembly.FindLeadingContext(continuation, 48, out int before);
    var context = MemoryDisassembly.ReadInstructions(leading, before + 36, out _);
    Assert(before == 48 && leading == start + 208 && context.Length == 50 &&
        context[before - 1].Address == start + 255 && context[before - 1].Text.Contains("jmp") &&
        context[before].Address == continuation, "48 leading instructions preserve the highlighted boundary");
    context = MemoryDisassembly.ReadInstructionContext(continuation, 128, 0, out int anchor, out ulong nextContext);
    Assert(anchor == 128 && context.Length == 129 && context[0].Address == start + 128 &&
        context[anchor].Address == continuation && nextContext == continuation + 1, "configurable before-only context");
    context = MemoryDisassembly.ReadInstructionContext(continuation, 0, 14, out anchor, out nextContext);
    Assert(anchor == 0 && context.Length == 2 && context[0].Address == continuation && nextContext == 0,
        "after-only context ends at the non-executable page");
    Assert(MemoryDisassembly.ReadInstructionContext(secondPage, 48, 14, out anchor, out nextContext).Length == 0 &&
        anchor == -1 && nextContext == 0, "leading code does not make an unreadable target valid");

    Assert(VirtualProtect(address, (nuint)pageSize, 0x120, out _), "guarded code page");
    Assert(MemoryDisassembly.ReadCode(start, out _).Length == 0 && Query(start, out var guarded) &&
        (guarded.Protection & 0x100) != 0, "guard page stays untouched");
    Assert(MemoryDisassembly.FindLeadingContext(continuation, 48, out before) == continuation && before == 0,
        "leading context stays out of guarded code");

    Assert(VirtualProtect(address, (nuint)pageSize, executeReadWrite, out _) &&
        VirtualProtect(secondPage, (nuint)pageSize, executeReadWrite, out _), "write neighboring code pages");
    ulong crossStart = secondPage - 64;
    Marshal.Copy(Enumerable.Repeat((byte)0x90, 128).ToArray(), 0, (IntPtr)(long)crossStart, 128);
    Assert(VirtualProtect(secondPage, (nuint)pageSize, 0x20, out _), "create adjacent code protection boundary");
    ulong hit = secondPage + 8;
    leading = MemoryDisassembly.FindLeadingContext(hit, 48, out before);
    context = MemoryDisassembly.ReadInstructions(leading, before + 36, out _);
    Assert(before == 48 && leading == hit - 48 && context.Length == 84 && context[before].Address == hit,
        "leading context crosses adjacent executable ranges with the full following page");

    context = MemoryDisassembly.ReadInstructionContext(hit, 7, 13, out anchor, out nextContext);
    Assert(anchor == 7 && context.Length == 21 && context[anchor].Address == hit && nextContext == hit + 14,
        "independent context counts and next page");
    string folder = Directory.CreateTempSubdirectory("spt-disassembly-export-").FullName;
    string? exported = null;
    try
    {
        exported = MemoryDisassembly.WriteInstructionContext(folder, context, anchor);
        string[] lines = File.ReadAllLines(exported);
        Assert(Path.GetDirectoryName(exported) == folder && lines.Length == 23 && lines[0] == "Before: 7  After: 13" &&
            lines[anchor + 2].StartsWith($">>  0x{hit:X16}") && lines[anchor + 2].Contains("nop") &&
            !File.Exists(exported + ".tmp"), "text export includes actual counts and highlights the selected instruction");
    }
    finally
    {
        if (exported != null) File.Delete(exported);
        Directory.Delete(folder);
    }
}
finally
{
    Assert(VirtualFree(address, 0, release), "free test allocation");
}
Assert(!Query(address, out var freed) || !freed.Editable, "reject freed memory");

// Full-size context must read across more than two executable regions.
ulong largeCode = VirtualAlloc(0, (nuint)(5 * pageSize), reserveAndCommit, executeReadWrite);
Assert(largeCode != 0, "allocate large context region");
try
{
    const int limit = MemoryDisassembly.MaxContextInstructions;
    byte[] longNop = [0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x0F, 0x1F, 0x84, 0, 0, 0, 0, 0];
    byte[] instructions = new byte[(limit * 2 + 1) * longNop.Length];
    for (int i = 0; i < instructions.Length; i += longNop.Length)
        longNop.CopyTo(instructions, i);
    ulong start = largeCode + (ulong)(pageSize / 2);
    Assert(instructions.Length <= 5 * pageSize - pageSize / 2, "fixture fits code pages");
    Marshal.Copy(instructions, 0, (IntPtr)(long)start, instructions.Length);
    Assert(VirtualProtect(largeCode + (ulong)pageSize, (nuint)pageSize, 0x20, out _) &&
        VirtualProtect(largeCode + (ulong)(3 * pageSize), (nuint)pageSize, 0x20, out _),
        "split long context across separately protected executable pages");
    ulong hit = start + (ulong)(limit * longNop.Length);
    var full = MemoryDisassembly.ReadInstructionContext(hit, limit, limit, out int anchor, out _);
    Assert(anchor == limit && full.Length == limit * 2 + 1 && full[anchor].Address == hit &&
        full[^1].Address == hit + (ulong)(limit * longNop.Length), "maximum context keeps both sides across page boundaries");
}
finally { Assert(VirtualFree(largeCode, 0, release), "free large context region"); }

// 4. REAL LIVE x64 Hardware Breakpoint (Write & Access) + Code Patching (Invert, NOP, Assembler, Code Cave)
ulong codePage = VirtualAlloc(0, (nuint)pageSize, reserveAndCommit, executeReadWrite);
ulong dataPage = VirtualAlloc(0, (nuint)pageSize, reserveAndCommit, readWrite);
Assert(codePage != 0 && dataPage != 0, "allocate live x64 test pages");
try
{
    // Build native x64 float energy-subtraction function:
    //   void SubEnergy(float* valuePtr /*rcx*/, float delta /*xmm1*/)
    //   { movss xmm0, [rcx]; subss xmm0, xmm1; movss [rcx], xmm0; ret; }
    ulong subEnergyAddr = codePage;
    byte[] subEnergyCode;
    ulong subssOffset, storeOffset;
    {
        var asm = new Assembler(64);
        asm.movss(xmm0, __dword_ptr[rcx]);
        using var s0 = new MemoryStream();
        asm.Assemble(new StreamCodeWriter(s0), subEnergyAddr);
        subssOffset = (ulong)s0.Length;

        asm.subss(xmm0, xmm1);
        using var s1 = new MemoryStream();
        asm.Assemble(new StreamCodeWriter(s1), subEnergyAddr);
        storeOffset = (ulong)s1.Length;

        asm.movss(__dword_ptr[rcx], xmm0);
        asm.ret();
        using var s2 = new MemoryStream();
        asm.Assemble(new StreamCodeWriter(s2), subEnergyAddr);
        subEnergyCode = s2.ToArray();
    }
    Marshal.Copy(subEnergyCode, 0, (IntPtr)(long)subEnergyAddr, subEnergyCode.Length);

    // Build native read-only function:
    //   float ReadEnergy(float* valuePtr /*rcx*/) { movss xmm0, [rcx]; ret; }
    ulong readEnergyAddr = codePage + 0x100;
    byte[] readEnergyCode;
    {
        var asm = new Assembler(64);
        asm.movss(xmm0, __dword_ptr[rcx]);
        asm.ret();
        using var s = new MemoryStream();
        asm.Assemble(new StreamCodeWriter(s), readEnergyAddr);
        readEnergyCode = s.ToArray();
    }
    Marshal.Copy(readEnergyCode, 0, (IntPtr)(long)readEnergyAddr, readEnergyCode.Length);

    // Build native integer decrement function (at least 6 bytes for clean 5-byte cave jump):
    //   void DecInt(int* ptr /*rcx*/) { sub dword ptr [rcx], 10; nop; nop; nop; ret; }
    ulong decIntAddr = codePage + 0x200;
    byte[] decIntCode;
    {
        var asm = new Assembler(64);
        asm.sub(__dword_ptr[rcx], 10);
        asm.nop();
        asm.nop();
        asm.nop();
        asm.ret();
        using var s = new MemoryStream();
        asm.Assemble(new StreamCodeWriter(s), decIntAddr);
        decIntCode = s.ToArray();
    }
    Marshal.Copy(decIntCode, 0, (IntPtr)(long)decIntAddr, decIntCode.Length);
    FlushInstructionCache(CurrentProcess, codePage, (nuint)pageSize);

    var subEnergy = Marshal.GetDelegateForFunctionPointer<SubEnergyDelegate>((IntPtr)(long)subEnergyAddr);
    var readEnergy = Marshal.GetDelegateForFunctionPointer<ReadEnergyDelegate>((IntPtr)(long)readEnergyAddr);
    var decInt = Marshal.GetDelegateForFunctionPointer<DecIntDelegate>((IntPtr)(long)decIntAddr);

    ulong floatPtr = dataPage + 0x40;
    ulong intPtr = dataPage + 0x80;
    Marshal.WriteInt32((IntPtr)(long)floatPtr, BitConverter.SingleToInt32Bits(100.0f));
    Marshal.WriteInt32((IntPtr)(long)intPtr, 500);

    // 4a. HWBP Write Watchpoint test
    Assert(MemoryWatchpoint.StartWatch(floatPtr, 4, WatchMode.Write, "Energy", out int writeSlot, out string err), "start write HWBP: " + err);
    for (int i = 0; i < 5; i++) subEnergy((IntPtr)(long)floatPtr, 10.0f);
    // Verify ReadProcessMemory does NOT trigger false hits
    var tmpBuf = new byte[4];
    Assert(Read(floatPtr, tmpBuf, 4) && BitConverter.ToSingle(tmpBuf, 0) == 50.0f, "energy reduced to 50.0");
    var writeHits = MemoryWatchpoint.ReadHits(writeSlot);
    Assert(writeHits.Length == 1, $"expected 1 write hit site, got {writeHits.Length}");
    Assert(writeHits[0].Count == 5, $"expected 5 write hits, got {writeHits[0].Count}");
    Assert(writeHits[0].InstructionAddress == subEnergyAddr + storeOffset,
        $"expected store at 0x{subEnergyAddr + storeOffset:X}, got 0x{writeHits[0].InstructionAddress:X}");
    Assert(writeHits[0].Disassembly.Contains("movss", StringComparison.OrdinalIgnoreCase), "preceding instruction is movss");
    Assert(writeHits[0].Registers.Rcx == floatPtr, $"captured RCX 0x{writeHits[0].Registers.Rcx:X} == floatPtr 0x{floatPtr:X}");
    MemoryWatchpoint.StopWatch(writeSlot);

    // 4b. HWBP Access watchpoints work even when the data page is read-only.
    Assert(VirtualProtect(dataPage, (nuint)pageSize, readOnly, out _), "protect watch target");
    Assert(MemoryWatchpoint.StartWatch(floatPtr, 4, WatchMode.Access, "EnergyAccess", out int accessSlot, out err), "start access HWBP: " + err);
    for (int i = 0; i < 3; i++) Assert(readEnergy((IntPtr)(long)floatPtr) == 50.0f, "read energy 50");
    var accessHits = MemoryWatchpoint.ReadHits(accessSlot);
    Assert(accessHits.Length == 1 && accessHits[0].Count == 3 && accessHits[0].InstructionAddress == readEnergyAddr,
        $"expected 3 access hits at 0x{readEnergyAddr:X}, got len={accessHits.Length}");
    Assert(accessHits[0].Registers.Rcx == floatPtr, "read-only watch captures register data");
    MemoryWatchpoint.StopAll();
    Assert(VirtualProtect(dataPage, (nuint)pageSize, readWrite, out _), "restore writable test page");

    // Use AVX on capable CPUs and the equivalent SSE2 load on other Windows x64 hosts.
    ulong vectorCode = codePage + 0x300;
    ulong vecInput = dataPage + 0x110, vecOutput = dataPage + 0x140;
    const ulong vectorLow = 0x8877665544332211UL, vectorHigh = 0x0123456789ABCDEFUL;
    Marshal.WriteInt64((IntPtr)(long)vecInput, unchecked((long)vectorLow));
    Marshal.WriteInt64((IntPtr)(long)(vecInput + 8), unchecked((long)vectorHigh));
    var vectorAssembler = new Assembler(64);
    if (System.Runtime.Intrinsics.X86.Avx.IsSupported)
        vectorAssembler.vmovdqu(xmm0, __xmmword_ptr[rcx + 16]);
    else
        vectorAssembler.movdqu(xmm0, __xmmword_ptr[rcx + 16]);
    vectorAssembler.movdqu(__xmmword_ptr[rdx], xmm0);
    vectorAssembler.ret();
    using (var encoded = new MemoryStream())
    {
        vectorAssembler.Assemble(new StreamCodeWriter(encoded), vectorCode);
        Marshal.Copy(encoded.ToArray(), 0, (IntPtr)(long)vectorCode, (int)encoded.Length);
        FlushInstructionCache(CurrentProcess, vectorCode, (nuint)encoded.Length);
    }

    Assert(MemoryDisassembly.TryDecodeSingle(vectorCode, out var vectorLoad, out _, out _) &&
        vectorLoad.Op0Register == Register.XMM0, "vector load decode");
    Assert(MemoryWatchpoint.SampleAtNextInstruction(vectorCode, out int executeSlot, out err),
        "sample next instruction entry: " + err);
    Assert(MemoryWatchpoint.StartWatch(vecInput, 8, WatchMode.Access, "Vector input", out int vectorDataSlot, out err),
        "sample vector data access: " + err);
    var copyVector = Marshal.GetDelegateForFunctionPointer<CopyVectorDelegate>((IntPtr)(long)vectorCode);
    for (int i = 0; i < 3; i++) copyVector((IntPtr)(long)(vecInput - 16), (IntPtr)(long)vecOutput);
    Assert((ulong)Marshal.ReadInt64((IntPtr)(long)vecOutput) == vectorLow &&
        (ulong)Marshal.ReadInt64((IntPtr)(long)(vecOutput + 8)) == vectorHigh,
        "resume execution at the breakpoint without altering the result");
    foreach (int slot in new[] { executeSlot, vectorDataSlot })
    {
        var vectorHits = MemoryWatchpoint.ReadHits(slot);
        Assert(vectorHits.Length == 1 && vectorHits[0].Count == 3 && vectorHits[0].TrapRip == vectorLoad.NextIP &&
            vectorHits[0].InstructionAddress == vectorCode, "vector capture location and count");
        Assert(vectorHits[0].Xmm is { Length: 16 } registers && registers[0] == new XmmRegister(vectorLow, vectorHigh),
            "XMM0 preserves the full loaded vector");
    }
    MemoryWatchpoint.StopAll();

    // 4c. Live Invert Patch: subss xmm0, xmm1 -> addss xmm0, xmm1 (reverses energy loss into gain!)
    ulong subssIp = subEnergyAddr + subssOffset;
    Assert(MemoryCodePatcher.ApplyInvert(subssIp, out var invertPatch, out err) && invertPatch != null, "invert subss: " + err);
    subEnergy((IntPtr)(long)floatPtr, 10.0f);
    Assert(readEnergy((IntPtr)(long)floatPtr) == 60.0f, "inverted subss increased energy 50 -> 60");
    Assert(MemoryCodePatcher.SetEnabled(invertPatch!.Id, false, out err), "disable invert patch: " + err);
    subEnergy((IntPtr)(long)floatPtr, 10.0f);
    Assert(readEnergy((IntPtr)(long)floatPtr) == 50.0f, "disabled patch restored 60 -> 50");
    Assert(MemoryCodePatcher.RemovePatch(invertPatch.Id, out err), "remove invert patch: " + err);

    // 4d. Live NOP Patch on movss [rcx], xmm0
    ulong storeIp = subEnergyAddr + storeOffset;
    Assert(MemoryCodePatcher.ApplyNop(storeIp, 0, out var nopPatch, out err) && nopPatch != null, "nop store: " + err);
    subEnergy((IntPtr)(long)floatPtr, 25.0f);
    Assert(readEnergy((IntPtr)(long)floatPtr) == 50.0f, "NOP prevented write to energy");
    Assert(MemoryCodePatcher.RemovePatch(nopPatch!.Id, out err), "remove nop patch: " + err);

    // 4e. Built-in x64+SSE Text Assembler check
    Assert(MemoryCodePatcher.TryParseCodeInput("addss xmm0, xmm1; movss dword ptr [rcx+0x10], xmm0; nop", codePage, out byte[] asmBytes, out err),
        "assemble SSE + memory: " + err);
    string formattedAsm = MemoryDisassembly.FormatBytes(asmBytes, codePage);
    Assert(formattedAsm.Contains("addss") && formattedAsm.Contains("movss") && formattedAsm.Contains("nop"), "assembled formatting: " + formattedAsm);

    // 4f. Live Code Cave Injection: run "add dword ptr [rcx], 100" BEFORE original "sub dword ptr [rcx], 10" -> net +90!
    Assert(MemoryCodePatcher.InjectCodeCave(decIntAddr, "add dword ptr [rcx], 100", runOriginalBefore: false, runOriginalAfter: true,
        out var cavePatch, out err) && cavePatch != null && cavePatch.CaveAddress != 0, "inject code cave: " + err);
    decInt((IntPtr)(long)intPtr);
    Assert(Marshal.ReadInt32((IntPtr)(long)intPtr) == 590, $"code cave net +90 expected 590, got {Marshal.ReadInt32((IntPtr)(long)intPtr)}");
    Assert(MemoryCodePatcher.SetEnabled(cavePatch!.Id, false, out err), "disable cave patch: " + err);
    decInt((IntPtr)(long)intPtr);
    Assert(Marshal.ReadInt32((IntPtr)(long)intPtr) == 580, "disabled cave restored -10");
    MemoryCodePatcher.RestoreAll();
    Assert(MemoryCodePatcher.GetPatches().Length == 0, "RestoreAll cleared all patches");

    // 4g. Assembler labels & conditional jumps
    Assert(MemoryCodePatcher.TryParseCodeInput("cmp eax, 0; je skip; add eax, 1; skip: nop", codePage, out byte[] labelBytes, out err),
        "assemble labels: " + err);
    string labelDisasm = MemoryDisassembly.FormatBytes(labelBytes, codePage);
    Assert(labelDisasm.Contains("je") && labelDisasm.Contains("nop"), "label branch formatting: " + labelDisasm);

    // 4h. AOB signature generation & persistent patch presets
    Assert(MemoryCodePatcher.GenerateAobSignature(decIntAddr, 8, out string decAob, out _, out _), "generate AOB");
    Assert(TryParseAob(decAob, out _, out _), "valid generated AOB: " + decAob);
    var patchDir = Directory.CreateTempSubdirectory("spt-patch-presets-").FullName;
    try
    {
        string patchFile = Path.Combine(patchDir, "patches.json");
        var presetItem = new MemoryPatchPreset("test-dec-nop", "test-ver", "", 0, decAob, "Raw", "90", 2);
        MemoryPatchPresets.Save(patchFile, presetItem);
        var loadedPresets = MemoryPatchPresets.Load(patchFile);
        Assert(loadedPresets.Count == 1 && loadedPresets[0].Name == "test-dec-nop", "save/load patch preset");
        MemoryPatchPresets.Remove(patchFile, "test-dec-nop");
        Assert(MemoryPatchPresets.Load(patchFile).Count == 0, "remove patch preset");
    }
    finally { Directory.Delete(patchDir, recursive: true); }

    // 4i. Disassembly context text formatting
    var ctxRows = MemoryDisassembly.ReadInstructionContext(subEnergyAddr + storeOffset, 2, 2, out int ctxHit, out _);
    string ctxText = MemoryDisassembly.FormatInstructionContext(ctxRows, ctxHit);
    Assert(ctxHit >= 0 && ctxText.Contains(">>") && ctxText.Contains("Before:"), "format context popup text");

    // 4j. Struct Dissect snapshot & diffing
    Marshal.WriteInt32((IntPtr)(long)(dataPage + 0x40), 123);
    Marshal.WriteInt64((IntPtr)(long)(dataPage + 0x48), (long)intPtr);
    var sRows1 = MemoryPointerScanner.ReadStruct(dataPage + 0x40, 64, 8, null, out byte[] sSnap1);
    Assert(sRows1.Length == 8 && sRows1[0].Int32Value == 123 && sRows1[1].PointerTarget == intPtr, "struct dissect initial");
    Marshal.WriteInt32((IntPtr)(long)(dataPage + 0x40), 456);
    var sRows2 = MemoryPointerScanner.ReadStruct(dataPage + 0x40, 64, 8, sSnap1, out _);
    Assert(sRows2[0].Changed && !sRows2[1].Changed, "struct dissect change highlight");

    // 4k. Unsigned formatting & module range
    Assert(Format(unchecked((uint)-1), MemoryValueType.Int32, hex: false, unsigned: true) == "4294967295", "unsigned format");
    Assert(TryGetModuleRange("kernel32.dll", out ulong kBase, out ulong kEnd) && kEnd > kBase, "module range");
}
finally
{
    MemoryWatchpoint.StopAll();
    MemoryCodePatcher.RestoreAll();
    VirtualFree(codePage, 0, release);
    VirtualFree(dataPage, 0, release);
}

Console.WriteLine("PASS memory access, scans, watchpoints, XMM snapshots, disassembly, assembler and code patches");

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate void SubEnergyDelegate(IntPtr ptr, float delta);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate float ReadEnergyDelegate(IntPtr ptr);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate void DecIntDelegate(IntPtr ptr);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate void CopyVectorDelegate(IntPtr input, IntPtr output);
