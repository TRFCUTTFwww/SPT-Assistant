using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;

namespace LocalSpt5Esp;

internal enum WatchMode { Write, Access, Execute }

internal readonly record struct WatchRegisters(
    ulong Rax, ulong Rcx, ulong Rdx, ulong Rbx,
    ulong Rsp, ulong Rbp, ulong Rsi, ulong Rdi,
    ulong R8, ulong R9, ulong R10, ulong R11,
    ulong R12, ulong R13, ulong R14, ulong R15);

internal readonly record struct XmmRegister(ulong Low, ulong High);

internal readonly record struct WatchHit(
    ulong TrapRip,
    ulong InstructionAddress,
    string ModuleOffset,
    string Disassembly,
    long Count,
    WatchRegisters Registers,
    XmmRegister[]? Xmm);

internal readonly record struct WatchSlotInfo(
    int Slot,
    ulong Address,
    ulong AlignedAddress,
    int Size,
    WatchMode Mode,
    string Label,
    bool Active,
    int AttachedThreads);

/// <summary>
/// Real x64 hardware breakpoint (DR0-DR3) watchpoint engine backed by a pure native
/// x64 Vectored Exception Handler stub (zero CLR/GC interaction inside the VEH).
/// </summary>
internal static class MemoryWatchpoint
{
    internal const int MaxSlots = 4;
    const int MaxHitsPerSlot = 64;
    const int OffsetSnapshotVersion = 0x10;
    const int OffsetHasXmm = 0x98;
    const int OffsetHitXmm = 0xA0;
    internal const int XmmRegisterCount = 16;
    const int HitRecordSize = OffsetHitXmm + XmmRegisterCount * 16;
    const int SlotTableBytes = MaxHitsPerSlot * HitRecordSize;

    const uint ExceptionSingleStep = 0x80000004;
    const uint ContextDebugRegisters = 0x00100010; // CONTEXT_AMD64 | CONTEXT_DEBUG_REGISTERS
    const uint ContextFloatingPointBit = 0x08;
    const uint ResumeFlag = 0x10000;
    const uint Th32csSnapThread = 0x00000004;
    const uint ThreadSuspendResume = 0x0002;
    const uint ThreadGetContext = 0x0008;
    const uint ThreadSetContext = 0x0010;
    const uint MemCommitReserve = 0x3000;
    const uint MemRelease = 0x8000;
    const uint PageExecuteReadWrite = 0x40;

    // Windows x64 CONTEXT struct offsets (size = 0x4D0 = 1232, 16-byte aligned)
    internal const int ContextSize = 1232;
    internal const int OffsetContextFlags = 0x30;
    internal const int OffsetEFlags = 0x44;
    internal const int OffsetDr0 = 0x48;
    internal const int OffsetDr1 = 0x50;
    internal const int OffsetDr2 = 0x58;
    internal const int OffsetDr3 = 0x60;
    internal const int OffsetDr6 = 0x68;
    internal const int OffsetDr7 = 0x70;
    internal const int OffsetRax = 0x78;
    internal const int OffsetRip = 0xF8;
    internal const int OffsetXmm0 = 0x1A0;

    sealed class SlotState
    {
        internal bool Active;
        internal ulong Address;
        internal ulong AlignedAddress;
        internal ulong SourceInstruction;
        internal int Size;
        internal WatchMode Mode;
        internal string Label = "";
        internal int AttachedThreads;
    }

    static readonly object Gate = new();
    static readonly SlotState[] Slots = [new(), new(), new(), new()];
    static readonly HashSet<uint> AttachedTids = [];

    static IntPtr vehHandle;
    static ulong stubAddress;
    static IntPtr headerPtr; // +0: ActiveMask, +4: Paused, +8: TableBase, +16: ExecuteMask
    static IntPtr tablePtr;

    [StructLayout(LayoutKind.Sequential)]
    struct ThreadEntry32
    {
        internal uint Size;
        internal uint Usage;
        internal uint ThreadId;
        internal uint OwnerProcessId;
        internal int BasePriority;
        internal int DeltaPriority;
        internal uint Flags;
    }

    [DllImport("kernel32.dll")]
    static extern IntPtr AddVectoredExceptionHandler(uint first, ulong handler);

    [DllImport("kernel32.dll")]
    static extern uint RemoveVectoredExceptionHandler(IntPtr handle);

    [DllImport("kernel32.dll")]
    static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll")]
    static extern bool Thread32First(IntPtr snapshot, ref ThreadEntry32 entry);

    [DllImport("kernel32.dll")]
    static extern bool Thread32Next(IntPtr snapshot, ref ThreadEntry32 entry);

    [DllImport("kernel32.dll")]
    static extern IntPtr OpenThread(uint desiredAccess, bool inheritHandle, uint threadId);

    [DllImport("kernel32.dll")]
    static extern uint SuspendThread(IntPtr thread);

    [DllImport("kernel32.dll")]
    static extern uint ResumeThread(IntPtr thread);

    [DllImport("kernel32.dll")]
    static extern bool GetThreadContext(IntPtr thread, IntPtr context);

    [DllImport("kernel32.dll")]
    static extern bool SetThreadContext(IntPtr thread, IntPtr context);

    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll")]
    static extern uint GetCurrentThreadId();

    internal static bool AnyActive
    {
        get
        {
            lock (Gate)
            {
                for (int i = 0; i < MaxSlots; i++)
                    if (Slots[i].Active) return true;
                return false;
            }
        }
    }

    internal static WatchSlotInfo[] GetSlots()
    {
        lock (Gate)
        {
            var result = new WatchSlotInfo[MaxSlots];
            for (int i = 0; i < MaxSlots; i++)
            {
                var s = Slots[i];
                result[i] = new(i, s.Address, s.AlignedAddress, s.Size, s.Mode, s.Label, s.Active, s.AttachedThreads);
            }
            return result;
        }
    }

    internal static bool StartWatch(ulong address, int valueSize, WatchMode mode, string label, out int slotIndex, out string error) =>
        StartWatch(address, valueSize, mode, label, 0, out slotIndex, out error);

    internal static bool SampleAtNextInstruction(ulong source, out int slotIndex, out string error)
    {
        slotIndex = -1;
        error = "该指令没有可读取的顺序后继指令。";
        if (!MemoryDisassembly.TryDecodeSingle(source, out var instruction, out _, out _) ||
            instruction.FlowControl != FlowControl.Next ||
            !MemoryEditorAccess.Query(instruction.NextIP, out var region) || !region.ReadableCode)
            return false;
        return StartWatch(instruction.NextIP, 1, WatchMode.Execute, MemoryEditorAccess.FormatModuleAddress(source),
            source, out slotIndex, out error);
    }

    static bool StartWatch(ulong address, int valueSize, WatchMode mode, string label, ulong source,
        out int slotIndex, out string error)
    {
        slotIndex = -1;
        error = "";
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess)
        {
            error = "硬件断点仅支持 64 位 Windows。";
            return false;
        }
        if (address == 0 || address >= MemoryEditorAccess.AddressLimit)
        {
            error = "无效的目标地址。";
            return false;
        }

        ulong alignedAddress = address;
        int watchSize = 1;
        if (mode != WatchMode.Execute)
            AlignWatchRange(address, valueSize, out alignedAddress, out watchSize);

        lock (Gate)
        {
            // Reuse existing slot if same aligned address & mode
            for (int i = 0; i < MaxSlots; i++)
            {
                if (Slots[i].Active && Slots[i].AlignedAddress == alignedAddress && Slots[i].Mode == mode &&
                    Slots[i].SourceInstruction == source)
                {
                    slotIndex = i;
                    return true;
                }
            }
            for (int i = 0; i < MaxSlots; i++)
            {
                if (!Slots[i].Active) { slotIndex = i; break; }
            }
            if (slotIndex < 0)
            {
                error = "最多同时监控 4 个硬件断点（DR0–DR3），请先停止一个监控。";
                return false;
            }

            if (!EnsureInfrastructureLocked(out error))
            {
                slotIndex = -1;
                return false;
            }

            ZeroSlotHitsLocked(slotIndex);
            var s = Slots[slotIndex];
            s.Active = true;
            s.Address = address;
            s.AlignedAddress = alignedAddress;
            s.SourceInstruction = source;
            s.Size = watchSize;
            s.Mode = mode;
            s.Label = label ?? "";

            UpdateHeaderMaskLocked();
            AttachedTids.Clear();
            int attached = ApplyRegistersOnHelperThreadLocked(onlyNewThreads: false);
            s.AttachedThreads = attached;
            if (attached == 0)
            {
                s.Active = false;
                UpdateHeaderMaskLocked();
                slotIndex = -1;
                error = "无法向任何线程应用硬件调试寄存器。";
                return false;
            }
            return true;
        }
    }

    internal static void StopWatch(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxSlots) return;
        lock (Gate)
        {
            if (!Slots[slotIndex].Active) return;
            if (headerPtr != IntPtr.Zero) Marshal.WriteInt32(headerPtr, 4, 1); // pause recording
            Slots[slotIndex].Active = false;
            Slots[slotIndex].Address = 0;
            Slots[slotIndex].AlignedAddress = 0;
            Slots[slotIndex].SourceInstruction = 0;
            Slots[slotIndex].AttachedThreads = 0;

            AttachedTids.Clear();
            ApplyRegistersOnHelperThreadLocked(onlyNewThreads: false);
            UpdateHeaderMaskLocked();
            if (headerPtr != IntPtr.Zero) Marshal.WriteInt32(headerPtr, 4, 0);

            if (!AnyActiveLocked())
                TeardownInfrastructureLocked();
        }
    }

    internal static void StopAll()
    {
        lock (Gate)
        {
            if (vehHandle == IntPtr.Zero && !AnyActiveLocked()) return;
            if (headerPtr != IntPtr.Zero) Marshal.WriteInt32(headerPtr, 4, 1);
            for (int i = 0; i < MaxSlots; i++)
            {
                Slots[i].Active = false;
                Slots[i].Address = 0;
                Slots[i].AlignedAddress = 0;
                Slots[i].SourceInstruction = 0;
                Slots[i].AttachedThreads = 0;
            }
            AttachedTids.Clear();
            ApplyRegistersOnHelperThreadLocked(onlyNewThreads: false);
            UpdateHeaderMaskLocked();
            TeardownInfrastructureLocked();
        }
    }

    internal static void ClearHits(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxSlots) return;
        lock (Gate)
        {
            if (tablePtr == IntPtr.Zero) return;
            Marshal.WriteInt32(headerPtr, 4, 1);
            ZeroSlotHitsLocked(slotIndex);
            Marshal.WriteInt32(headerPtr, 4, 0);
        }
    }

    /// <summary>
    /// Attaches active hardware breakpoints to any newly created process threads.
    /// </summary>
    internal static void RefreshNewThreads()
    {
        lock (Gate)
        {
            if (!AnyActiveLocked() || vehHandle == IntPtr.Zero) return;
            int added = ApplyRegistersOnHelperThreadLocked(onlyNewThreads: true);
            if (added > 0)
            {
                for (int i = 0; i < MaxSlots; i++)
                    if (Slots[i].Active) Slots[i].AttachedThreads = AttachedTids.Count;
            }
        }
    }

    internal static WatchHit[] ReadHits(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxSlots) return [];
        lock (Gate)
        {
            if (tablePtr == IntPtr.Zero) return [];
            var hits = new List<WatchHit>();
            IntPtr baseSlot = tablePtr + slotIndex * SlotTableBytes;
            for (int i = 0; i < MaxHitsPerSlot; i++)
            {
                IntPtr entry = baseSlot + i * HitRecordSize;
                ulong trapRip = (ulong)Marshal.ReadInt64(entry, 0);
                if (trapRip == 0) continue;
                long count = Marshal.ReadInt64(entry, 8);
                if (count <= 0) continue;
                long version = Marshal.ReadInt64(entry, OffsetSnapshotVersion);
                if (version == 0 || (version & 1) != 0) continue;

                var regs = new WatchRegisters(
                    (ulong)Marshal.ReadInt64(entry, 0x18),
                    (ulong)Marshal.ReadInt64(entry, 0x20),
                    (ulong)Marshal.ReadInt64(entry, 0x28),
                    (ulong)Marshal.ReadInt64(entry, 0x30),
                    (ulong)Marshal.ReadInt64(entry, 0x38),
                    (ulong)Marshal.ReadInt64(entry, 0x40),
                    (ulong)Marshal.ReadInt64(entry, 0x48),
                    (ulong)Marshal.ReadInt64(entry, 0x50),
                    (ulong)Marshal.ReadInt64(entry, 0x58),
                    (ulong)Marshal.ReadInt64(entry, 0x60),
                    (ulong)Marshal.ReadInt64(entry, 0x68),
                    (ulong)Marshal.ReadInt64(entry, 0x70),
                    (ulong)Marshal.ReadInt64(entry, 0x78),
                    (ulong)Marshal.ReadInt64(entry, 0x80),
                    (ulong)Marshal.ReadInt64(entry, 0x88),
                    (ulong)Marshal.ReadInt64(entry, 0x90));

                XmmRegister[]? xmm = null;
                if (Marshal.ReadInt32(entry, OffsetHasXmm) != 0)
                {
                    xmm = new XmmRegister[XmmRegisterCount];
                    for (int j = 0; j < xmm.Length; j++)
                    {
                        int offset = OffsetHitXmm + j * 16;
                        xmm[j] = new((ulong)Marshal.ReadInt64(entry, offset),
                            (ulong)Marshal.ReadInt64(entry, offset + 8));
                    }
                }
                Thread.MemoryBarrier();
                if (version != Marshal.ReadInt64(entry, OffsetSnapshotVersion)) continue;

                ulong insnAddr;
                string disasm;
                if (Slots[slotIndex].Mode == WatchMode.Execute)
                {
                    insnAddr = Slots[slotIndex].SourceInstruction;
                    if (insnAddr == 0) insnAddr = trapRip;
                    MemoryDisassembly.TryDecodeSingle(insnAddr, out _, out _, out disasm);
                }
                else insnAddr = MemoryDisassembly.FindPrecedingInstruction(trapRip, out disasm);
                string mod = MemoryEditorAccess.FormatModuleAddress(insnAddr);
                hits.Add(new(trapRip, insnAddr, mod, disasm, count, regs, xmm));
            }
            hits.Sort((a, b) => b.Count.CompareTo(a.Count));
            return hits.ToArray();
        }
    }

    internal static void AlignWatchRange(ulong address, int requestedSize, out ulong alignedAddress, out int watchSize)
    {
        int size = requestedSize switch { <= 1 => 1, <= 2 => 2, <= 4 => 4, _ => 8 };
        // Ensure address is aligned to watchSize; if not, widen to 8 if within an 8-byte slot, else narrow
        while (size > 1 && (address & ((ulong)size - 1)) != 0)
            size >>= 1;
        alignedAddress = address & ~((ulong)size - 1);
        watchSize = size;
    }

    internal static ulong EncodeDr7Slot(int slot, WatchMode mode, int watchSize)
    {
        ulong enableBit = 1UL << (slot * 2);
        if (mode == WatchMode.Execute) return enableBit;
        ulong rw = mode == WatchMode.Write ? 0b01UL : 0b11UL;
        ulong len = watchSize switch { 1 => 0b00UL, 2 => 0b01UL, 4 => 0b11UL, _ => 0b10UL };
        int shift = 16 + slot * 4;
        return enableBit | (rw << shift) | (len << (shift + 2));
    }

    static bool AnyActiveLocked()
    {
        for (int i = 0; i < MaxSlots; i++)
            if (Slots[i].Active) return true;
        return false;
    }

    static void ZeroSlotHitsLocked(int slotIndex)
    {
        if (tablePtr == IntPtr.Zero) return;
        IntPtr dst = tablePtr + slotIndex * SlotTableBytes;
        for (int off = 0; off < SlotTableBytes; off += 8)
            Marshal.WriteInt64(dst, off, 0);
    }

    static void UpdateHeaderMaskLocked()
    {
        if (headerPtr == IntPtr.Zero) return;
        uint mask = 0, executeMask = 0;
        for (int i = 0; i < MaxSlots; i++)
        {
            if (!Slots[i].Active) continue;
            mask |= 1u << i;
            if (Slots[i].Mode == WatchMode.Execute) executeMask |= 1u << i;
        }
        Marshal.WriteInt32(headerPtr, 16, (int)executeMask);
        Marshal.WriteInt32(headerPtr, 0, (int)mask);
    }

    static bool EnsureInfrastructureLocked(out string error)
    {
        error = "";
        if (vehHandle != IntPtr.Zero) return true;

        try
        {
            headerPtr = Marshal.AllocHGlobal(32);
            tablePtr = Marshal.AllocHGlobal(MaxSlots * SlotTableBytes);
            for (int off = 0; off < 32; off += 8) Marshal.WriteInt64(headerPtr, off, 0);
            for (int i = 0; i < MaxSlots; i++) ZeroSlotHitsLocked(i);
            Marshal.WriteInt64(headerPtr, 8, tablePtr.ToInt64());

            stubAddress = MemoryEditorAccess.VirtualAlloc(0, 4096, MemCommitReserve, PageExecuteReadWrite);
            if (stubAddress == 0)
            {
                error = "无法分配原生 VEH 缓冲页。";
                return false;
            }

            byte[] stubBytes = AssembleVehStub(stubAddress, (ulong)headerPtr.ToInt64());
            Marshal.Copy(stubBytes, 0, (IntPtr)(long)stubAddress, stubBytes.Length);
            if (!MemoryEditorAccess.FlushInstructionCache(MemoryEditorAccess.CurrentProcess, stubAddress, (nuint)stubBytes.Length))
            {
                error = "无法刷新原生 VEH 指令缓存。";
                return false;
            }

            vehHandle = AddVectoredExceptionHandler(1, stubAddress);
            if (vehHandle != IntPtr.Zero) return true;
            error = "注册 Vectored Exception Handler 失败。";
            return false;
        }
        catch (Exception ex)
        {
            error = "初始化硬件断点失败：" + ex.Message;
            return false;
        }
        finally
        {
            if (vehHandle == IntPtr.Zero)
                TeardownInfrastructureLocked();
        }
    }

    static void TeardownInfrastructureLocked()
    {
        if (vehHandle != IntPtr.Zero)
        {
            RemoveVectoredExceptionHandler(vehHandle);
            vehHandle = IntPtr.Zero;
        }
        if (stubAddress != 0)
        {
            MemoryEditorAccess.VirtualFree(stubAddress, 0, MemRelease);
            stubAddress = 0;
        }
        if (tablePtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(tablePtr);
            tablePtr = IntPtr.Zero;
        }
        if (headerPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(headerPtr);
            headerPtr = IntPtr.Zero;
        }
    }

    static int ApplyRegistersOnHelperThreadLocked(bool onlyNewThreads)
    {
        // Snapshot desired register values before spawning worker
        ulong[] dr = new ulong[MaxSlots];
        ulong dr7Mask = 0, dr7Value = 0;
        for (int i = 0; i < MaxSlots; i++)
        {
            int shift = 16 + i * 4;
            dr7Mask |= (3UL << (i * 2)) | (0xFUL << shift);
            if (Slots[i].Active)
            {
                dr[i] = Slots[i].AlignedAddress;
                dr7Value |= EncodeDr7Slot(i, Slots[i].Mode, Slots[i].Size);
            }
        }

        var existingTids = onlyNewThreads ? new HashSet<uint>(AttachedTids) : null;
        var applied = new List<uint>();

        var worker = new Thread(() =>
        {
            uint selfTid = GetCurrentThreadId();
            uint pid = (uint)Environment.ProcessId;
            var tids = EnumerateProcessThreads(pid, selfTid);

            IntPtr rawCtx = Marshal.AllocHGlobal(ContextSize + 16);
            try
            {
                long aligned = (rawCtx.ToInt64() + 15L) & ~15L;
                IntPtr ctxPtr = new(aligned);

                foreach (uint tid in tids)
                {
                    if (existingTids != null && existingTids.Contains(tid)) continue;
                    IntPtr hThread = OpenThread(ThreadSuspendResume | ThreadGetContext | ThreadSetContext, false, tid);
                    if (hThread == IntPtr.Zero) continue;
                    try
                    {
                        uint suspendCount = SuspendThread(hThread);
                        if (suspendCount == 0xFFFFFFFFu) continue;
                        try
                        {
                            // Zero context buffer without any managed allocation
                            for (int off = 0; off < ContextSize; off += 8)
                                Marshal.WriteInt64(ctxPtr, off, 0);
                            Marshal.WriteInt32(ctxPtr, OffsetContextFlags, (int)ContextDebugRegisters);

                            if (!GetThreadContext(hThread, ctxPtr)) continue;

                            Marshal.WriteInt64(ctxPtr, OffsetDr0, (long)dr[0]);
                            Marshal.WriteInt64(ctxPtr, OffsetDr1, (long)dr[1]);
                            Marshal.WriteInt64(ctxPtr, OffsetDr2, (long)dr[2]);
                            Marshal.WriteInt64(ctxPtr, OffsetDr3, (long)dr[3]);
                            Marshal.WriteInt64(ctxPtr, OffsetDr6, 0);

                            ulong currentDr7 = (ulong)Marshal.ReadInt64(ctxPtr, OffsetDr7);
                            ulong nextDr7 = (currentDr7 & ~dr7Mask) | dr7Value;
                            Marshal.WriteInt64(ctxPtr, OffsetDr7, (long)nextDr7);
                            Marshal.WriteInt32(ctxPtr, OffsetContextFlags, (int)ContextDebugRegisters);

                            if (SetThreadContext(hThread, ctxPtr))
                                applied.Add(tid);
                        }
                        finally
                        {
                            ResumeThread(hThread);
                        }
                    }
                    finally
                    {
                        CloseHandle(hThread);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(rawCtx);
            }
        })
        { IsBackground = true, Name = "SptAssistant-HwBp" };

        worker.Start();
        worker.Join(3000);
        foreach (uint tid in applied) AttachedTids.Add(tid);
        return applied.Count;
    }

    static List<uint> EnumerateProcessThreads(uint pid, uint excludeTid)
    {
        var tids = new List<uint>(64);
        IntPtr snap = CreateToolhelp32Snapshot(Th32csSnapThread, 0);
        if (snap == IntPtr.Zero || snap == new IntPtr(-1)) return tids;
        try
        {
            var entry = new ThreadEntry32 { Size = (uint)Marshal.SizeOf<ThreadEntry32>() };
            if (!Thread32First(snap, ref entry)) return tids;
            do
            {
                if (entry.OwnerProcessId == pid && entry.ThreadId != excludeTid)
                    tids.Add(entry.ThreadId);
                entry.Size = (uint)Marshal.SizeOf<ThreadEntry32>();
            }
            while (Thread32Next(snap, ref entry));
        }
        finally
        {
            CloseHandle(snap);
        }
        return tids;
    }

    /// <summary>
    /// Emits a pure native x64 Vectored Exception Handler using Iced.Intel.Assembler.
    /// Touches only volatile registers (RAX, RCX, RDX, R8-R11) and zero CLR/GC state.
    /// </summary>
    static byte[] AssembleVehStub(ulong baseRip, ulong headerAddress)
    {
        var asm = new Assembler(64);
        var notOurs = asm.CreateLabel();
        var resumeReady = asm.CreateLabel();
        var continueExec = asm.CreateLabel();
        var pickTriggered = asm.CreateLabel();
        var nextTriggered = asm.CreateLabel();
        var loopStart = asm.CreateLabel();
        var nextSlot = asm.CreateLabel();
        var foundSlot = asm.CreateLabel();
        var publishSnapshot = asm.CreateLabel();

        // RCX = EXCEPTION_POINTERS*
        asm.mov(rax, __qword_ptr[rcx]);                  // rax = ExceptionRecord
        asm.cmp(__dword_ptr[rax], ExceptionSingleStep);  // EXCEPTION_SINGLE_STEP?
        asm.jne(notOurs);

        asm.mov(rdx, __qword_ptr[rcx + 8]);              // rdx = ContextRecord
        asm.mov(r8, __qword_ptr[rdx + OffsetDr6]);       // r8 = Context.Dr6
        asm.mov(r9, headerAddress);                      // r9 = SharedHeader*
        asm.mov(r10d, __dword_ptr[r9]);                  // r10d = ActiveSlotMask
        asm.and(r8d, r10d);                              // r8d = triggered active bits
        asm.jz(notOurs);

        // Clear only our triggered bits in Context.Dr6
        asm.mov(eax, r8d);
        asm.not(rax);
        asm.and(__qword_ptr[rdx + OffsetDr6], rax);

        // An execution breakpoint fires before the instruction. RF lets that instruction resume.
        asm.test(__dword_ptr[r9 + 16], r8d);
        asm.jz(resumeReady);
        asm.or(__dword_ptr[rdx + OffsetEFlags], ResumeFlag);
        asm.Label(ref resumeReady);

        // If Paused != 0, resume immediately
        asm.cmp(__dword_ptr[r9 + 4], 0);
        asm.jne(continueExec);

        asm.Label(ref pickTriggered);
        asm.bsf(eax, r8d);
        asm.imul(eax, eax, SlotTableBytes);
        asm.mov(r10, __qword_ptr[r9 + 8]);               // r10 = TableBase
        asm.add(r10, rax);                               // r10 = &Table[slotIndex][0]

        asm.mov(r11, __qword_ptr[rdx + OffsetRip]);      // r11 = Context.Rip
        asm.test(r11, r11);
        asm.jz(continueExec);

        asm.mov(ecx, MaxHitsPerSlot);

        asm.Label(ref loopStart);
        asm.mov(rax, __qword_ptr[r10]);
        asm.cmp(rax, r11);
        asm.je(foundSlot);
        asm.test(rax, rax);
        asm.jnz(nextSlot);

        // Atomically claim empty slot: cmpxchg [r10], r11 with rax == 0
        asm.xor(eax, eax);
        asm.@lock.cmpxchg(__qword_ptr[r10], r11);
        asm.je(foundSlot);
        asm.cmp(rax, r11);
        asm.je(foundSlot);

        asm.Label(ref nextSlot);
        asm.add(r10, HitRecordSize);
        asm.dec(ecx);
        asm.jnz(loopStart);
        asm.jmp(nextTriggered);

        asm.Label(ref foundSlot);
        asm.@lock.inc(__qword_ptr[r10 + 8]);
        // Publish even versions only after the entire register snapshot is complete.
        asm.@lock.bts(__qword_ptr[r10 + OffsetSnapshotVersion], 0);
        asm.jc(nextTriggered);

        // Snapshot 16 GPRs (Context +0x78..+0xF0 -> HitSlot +0x18..+0x90)
        for (int i = 0; i < 16; i++)
        {
            asm.mov(rax, __qword_ptr[rdx + OffsetRax + i * 8]);
            asm.mov(__qword_ptr[r10 + 0x18 + i * 8], rax);
        }
        asm.mov(__dword_ptr[r10 + OffsetHasXmm], 0);
        asm.test(__dword_ptr[rdx + OffsetContextFlags], ContextFloatingPointBit);
        asm.jz(publishSnapshot);
        for (int i = 0; i < XmmRegisterCount * 2; i++)
        {
            asm.mov(rax, __qword_ptr[rdx + OffsetXmm0 + i * 8]);
            asm.mov(__qword_ptr[r10 + OffsetHitXmm + i * 8], rax);
        }
        asm.mov(__dword_ptr[r10 + OffsetHasXmm], 1);

        asm.Label(ref publishSnapshot);
        asm.@lock.inc(__qword_ptr[r10 + OffsetSnapshotVersion]);

        asm.Label(ref nextTriggered);
        asm.lea(eax, __[r8 - 1]);
        asm.and(r8d, eax);
        asm.jnz(pickTriggered);

        asm.Label(ref continueExec);
        asm.mov(eax, -1); // EXCEPTION_CONTINUE_EXECUTION
        asm.ret();

        asm.Label(ref notOurs);
        asm.xor(eax, eax); // EXCEPTION_CONTINUE_SEARCH
        asm.ret();

        using var stream = new MemoryStream();
        asm.Assemble(new StreamCodeWriter(stream), baseRip);
        return stream.ToArray();
    }
}
