using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using UnityEngine;
using UnityEngine.SceneManagement;
using static LocalSpt5Esp.MemoryEditorAccess;

namespace LocalSpt5Esp;

internal static partial class MemoryEditor
{
    const int MatchLimit = 100_000;
    const int EntryLimit = 128;
    const int PageSize = 50;
    const int BlockSize = 256 * 1024;
    const long SnapshotBudgetBytes = 256L * 1024 * 1024;
    const int RowHeight = 26;
    const int HistoryLimit = 256;
    const int DisassemblyPageRows = 36;
    const int DisassemblyPagingLimit = 64;
    const int DefaultLeadingContextRows = 128;
    const int DefaultFollowingContextRows = 64;
    const int HexWindowSize = 256;
    const int DefaultNavigationHistoryLimit = 128;
    const int MaxNavigationHistoryLimit = 4096;

    internal enum ScanDataKind { Byte, Int16, Int32, Int64, Float, Double, Aob, String }
    internal enum ScanScope { EditablePrivate, AllWritable, AllReadable }
    internal enum HexDisplayMode { Hex8, Int32, Float, Ptr64 }

    static readonly ScanDataKind[] DataKinds = Enum.GetValues<ScanDataKind>();
    static readonly MemoryValueType[] ValueTypes = Enum.GetValues<MemoryValueType>();
    static readonly KeyCode[] HotkeyChoices =
    [
        KeyCode.None, KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5, KeyCode.F6,
        KeyCode.F7, KeyCode.F8, KeyCode.F9, KeyCode.F10, KeyCode.F11, KeyCode.F12
    ];

    static readonly MemoryScanMode[] FirstScanModes =
    [
        MemoryScanMode.Exact, MemoryScanMode.Rounded, MemoryScanMode.Unknown,
        MemoryScanMode.GreaterThan, MemoryScanMode.LessThan, MemoryScanMode.Between
    ];

    static readonly MemoryScanMode[] NextScanModes =
    [
        MemoryScanMode.Exact, MemoryScanMode.Rounded, MemoryScanMode.Changed,
        MemoryScanMode.Unchanged, MemoryScanMode.Increased, MemoryScanMode.Decreased,
        MemoryScanMode.GreaterThan, MemoryScanMode.LessThan, MemoryScanMode.Between
    ];

    static readonly byte[] valueBytes = GC.AllocateArray<byte>(8, pinned: true);
    static byte[]? scanBytes;

    // Scan state
    static List<Match> matches = [];
    static readonly (Match Row, ulong? Value)[] liveScanValues = new (Match, ulong?)[PageSize];
    static int sampledScanStart = -1;
    static MemoryValueType sampledScanType;
    static float nextScanSample;
    static List<SnapshotChunk>? unknownSnapshot;
    static Scan? scan;
    static ScanDataKind dataKind = ScanDataKind.Float;
    static MemoryValueType entryType = MemoryValueType.Float, resultType = MemoryValueType.Float;
    static MemoryScanMode scanMode = MemoryScanMode.Rounded;
    static bool utf16String, hexDisplay, unsignedDisplay, unalignedScan;
    static ScanScope scanScope = ScanScope.EditablePrivate;
    static string query = "", queryMax = "", pageInput = "1", scanRangeStartInput = "", scanRangeEndInput = "";
    static int page, selectedMatch = -1;

    // Address table state
    static readonly List<Entry> entries = [];
    static Entry? selectedEntry;
    static string addressInput = "", editValueText = "", editDescText = "", editAddrText = "";
    static bool focusValue, valueDraftEdited;
    static bool showValuePresets, showPointerScanner;
    static List<MemoryValuePreset> valuePresets = [];
    static Vector2 presetScroll, pointerScanScroll;
    static ulong pointerScanTarget;
    static string pointerMaxOffsetInput = "400";
    static int pointerMaxDepth = 2;
    static List<PointerChainMatch> pointerScanResults = [];
    static string ValuePresetPath => Path.Combine(BepInEx.Paths.PluginPath, "LocalSpt5ESP", "memory-value-presets.json");
    static string AddressTablePath => Path.Combine(BepInEx.Paths.PluginPath, "LocalSpt5ESP", "memory-address-table.json");
    static string PatchPresetPath => Path.Combine(BepInEx.Paths.PluginPath, "LocalSpt5ESP", "memory-code-patches.json");

    // Watchpoint & History state
    static int watchSubTab; // 0 = HWBP watchpoints, 1 = Value change history
    static int activeWatchSlot;
    static WatchHit[] cachedHits = [];
    static int selectedHitIndex = -1;
    static int selectedXmm;
    static bool showXmm, selectFirstSample, watchPaused;
    static string watchFilterText = "";
    static float nextWatchRefresh, nextThreadScan;
    static readonly Queue<Change> history = new(HistoryLimit);

    // Disassembler & inline editor state
    static bool browseCode = true;
    static string codeAddressInput = "", hexDecodeInput = "";
    static ulong disasmOrigin, nextCodeAddress, highlightIp;
    static DisasmRow[] disasmRows = [];
    static string[] decodedHexRows = [];
    static int selectedDisasmIndex = -1;
    static int disasmRowLimit = DisassemblyPageRows;
    static bool revealDisasmHighlight;
    static readonly List<DisassemblyPage> disassemblyPages = [];
    static int disassemblyPageIndex;
    static readonly List<DisassemblyPage> navHistory = new(DefaultNavigationHistoryLimit);
    static ConfigEntry<int>? navigationHistoryLimit;
    static ConfigEntry<int>? leadingContextRows, followingContextRows;
    static (int History, int Before, int After)? shownDisassemblySettings;
    static string historyLimitInput = "", leadingContextInput = "", followingContextInput = "";
    static Task<string?>? disassemblyExport;
    static bool showContextPopup;
    static string contextPopupTitle = "", contextPopupText = "";
    static Vector2 contextPopupScroll;
    static int inlineEditMode; // 0 = none, 1 = assemble/hex patch, 2 = code cave
    static string inlineCodeInput = "";
    static (string Input, ulong Address)? inlinePreviewSource;
    static string inlinePreview = "";
    static bool inlinePreviewValid;
    static int caveOrder = 1; // 0 = orig before, 1 = orig after, 2 = replace orig

    // Code patches & scripts state
    static int patchSubTab; // 0 = active patches, 1 = saved AOB scripts
    static List<MemoryPatchPreset> patchPresets = [];
    static bool patchPresetsLoaded;

    // Hex Memory Browser & Struct Dissect state
    static string hexAddressInput = "", hexSearchInput = "";
    static ulong hexOrigin;
    static byte[] hexViewBytes = [];
    static int hexSelectedOffset;
    static bool hexRevealSelection;
    static string hexWriteInput = "";
    static MemoryValueType hexWriteType = MemoryValueType.Float;
    static HexDisplayMode hexDisplayMode = HexDisplayMode.Hex8;
    static bool structDissectMode;
    static string structSizeInput = "200";
    static int structStride = 8;
    static byte[]? structBaselineBytes;
    static StructFieldRow[] structRows = [];
    static readonly List<(ulong Addr, string Label)> structHistory = [];
    static Vector2 structScroll;

    // Shared UI & Context state
    static Context? context;
    static string status = "", lastPage = "";
    static bool statusIsError;
    static float nextRefresh, visibleUntil;
    static Vector2 panelScroll, resultScroll, entryScroll, hitScroll, logScroll, asmScroll, patchScroll, hexScroll;
    static (string Label, Action Run)[] menuItems = [];
    static Vector2 menuAnchor;

    readonly record struct Context(int Scene, IntPtr World, IntPtr Player, bool InRaid);
    readonly record struct DisassemblyPage(ulong Origin, int RowLimit, ulong Highlight);
    readonly record struct Match(ulong Address, ulong Allocation, ulong Value, ulong Previous);
    readonly record struct SnapshotChunk(ulong Base, ulong Allocation, byte[] Data);
    readonly record struct Change(DateTime Time, ulong Address, MemoryValueType Type, ulong Before, ulong After, string Source);

    sealed class Entry
    {
        internal ulong BaseAddress;
        internal long[] Offsets = [];
        internal ulong Address;
        internal ulong Allocation;
        internal MemoryValueType Type;
        internal string Description = "";
        internal ulong Current;
        internal ulong? Locked, Undo;
        internal KeyCode Hotkey = KeyCode.None;
        internal bool Valid = true, Writable;
        internal bool IsPointer => Offsets.Length > 0;
    }

    sealed class Scan
    {
        internal readonly ScanDataKind Kind;
        internal readonly MemoryValueType ValueType;
        internal readonly int Size;
        internal readonly int Step;
        internal readonly ScanScope Scope;
        internal readonly ulong RangeStart, RangeEnd;
        internal readonly MemoryScanMode Mode;
        internal readonly ulong Target, TargetMax;
        internal readonly double LowBound, HighBound;
        internal readonly byte[] Pattern, Mask;
        internal readonly List<Match>? InputMatches;
        internal readonly List<SnapshotChunk>? InputSnapshot;
        internal readonly List<Match> Results = [];
        internal readonly List<SnapshotChunk>? OutputSnapshot;
        internal ulong Cursor;
        internal Region Region;
        internal int Index, ChunkOffset;
        internal long SnapshotBytes;
        internal bool SnapshotCapped;

        internal Scan(ScanDataKind kind, MemoryValueType valueType, MemoryScanMode mode,
            ulong target, ulong targetMax, double lowBound, double highBound,
            byte[] pattern, byte[] mask, List<Match>? inputMatches, List<SnapshotChunk>? inputSnapshot,
            bool captureSnapshot, ScanScope scope, int step, ulong rangeStart, ulong rangeEnd)
        {
            Kind = kind;
            ValueType = valueType;
            Size = kind is ScanDataKind.Aob or ScanDataKind.String ? pattern.Length : MemoryEditorAccess.Size(valueType);
            Step = Math.Max(1, step);
            Scope = scope;
            RangeStart = rangeStart;
            RangeEnd = rangeEnd > rangeStart ? rangeEnd : AddressLimit;
            Cursor = RangeStart;
            Mode = mode;
            Target = target;
            TargetMax = targetMax;
            LowBound = lowBound;
            HighBound = highBound;
            Pattern = pattern;
            Mask = mask;
            InputMatches = inputMatches;
            InputSnapshot = inputSnapshot;
            OutputSnapshot = captureSnapshot ? [] : null;
        }
    }

    internal static void CloseRowMenu() => menuItems = [];

    internal static void Initialize(ConfigFile config)
    {
        navigationHistoryLimit = config.Bind("Memory Editor", "Disassembly back steps", DefaultNavigationHistoryLimit,
            new ConfigDescription("Number of disassembly locations to keep for Back. Zero disables history.",
                new AcceptableValueRange<int>(0, MaxNavigationHistoryLimit)));
        var contextRange = new AcceptableValueRange<int>(0, MemoryDisassembly.MaxContextInstructions);
        leadingContextRows = config.Bind("Memory Editor", "Disassembly context before", DefaultLeadingContextRows,
            new ConfigDescription("Number of instructions to show before the selected instruction.", contextRange));
        followingContextRows = config.Bind("Memory Editor", "Disassembly context after", DefaultFollowingContextRows,
            new ConfigDescription("Number of instructions to show after the selected instruction.", contextRange));
    }

    static void SetStatus(string text, bool error = false)
    {
        status = text;
        statusIsError = error;
    }

    static bool IsNumericKind(ScanDataKind kind) => kind is not (ScanDataKind.Aob or ScanDataKind.String);

    static MemoryValueType ToValueType(ScanDataKind kind) => kind switch
    {
        ScanDataKind.Byte => MemoryValueType.Byte,
        ScanDataKind.Int16 => MemoryValueType.Int16,
        ScanDataKind.Int32 => MemoryValueType.Int32,
        ScanDataKind.Int64 => MemoryValueType.Int64,
        ScanDataKind.Float => MemoryValueType.Float,
        ScanDataKind.Double => MemoryValueType.Double,
        _ => MemoryValueType.Byte
    };

    static string KindLabel(ScanDataKind kind) => kind switch
    {
        ScanDataKind.Aob => "字节数组",
        ScanDataKind.String => "字符串",
        _ => kind.ToString()
    };

    static string ModeLabel(MemoryScanMode mode) => mode switch
    {
        MemoryScanMode.Exact => "精确值",
        MemoryScanMode.Rounded => "四舍五入",
        MemoryScanMode.Unknown => "未知初始值",
        MemoryScanMode.Changed => "已变化",
        MemoryScanMode.Unchanged => "未变化",
        MemoryScanMode.Increased => "已增加",
        MemoryScanMode.Decreased => "已减少",
        MemoryScanMode.GreaterThan => "大于",
        MemoryScanMode.LessThan => "小于",
        MemoryScanMode.Between => "介于",
        _ => mode.ToString()
    };

    static bool HasPriorScan => matches.Count > 0 || unknownSnapshot != null;

    static Context CurrentContext()
    {
        var world = Singleton<GameWorld>.Instance;
        return new(SceneManager.GetActiveScene().handle, world?.Pointer ?? IntPtr.Zero,
            world?.MainPlayer?.Pointer ?? IntPtr.Zero, ItemSpawner.InRaid());
    }

    static void CheckContext()
    {
        var current = CurrentContext();
        if (context is {} previous && previous != current)
        {
            ResetHeapState();
            SetStatus("游戏场景已切换，堆扫描与硬件断点已清空（代码补丁保留）。");
        }
        context = current;
    }

    static void Record(Entry entry, ulong before, ulong after, string source)
    {
        if (history.Count == HistoryLimit) history.Dequeue();
        history.Enqueue(new(DateTime.Now, entry.Address, entry.Type, before, after, source));
    }

    internal static void Tick()
    {
        FinishDisassemblyExport();
        if (context == null && !MemoryWatchpoint.AnyActive) return;
        CheckContext();
        if (scan is {} job) ScanStep(job);

        float now = Time.unscaledTime;
        if (MemoryWatchpoint.AnyActive && now >= nextThreadScan)
        {
            nextThreadScan = now + 1.0f;
            MemoryWatchpoint.RefreshNewThreads();
        }

        if (now < nextRefresh) return;
        nextRefresh = now + 0.15f;

        bool visible = InterfaceMenu.Open && now < visibleUntil;
        if (visible && lastPage == "访问监控" && watchSubTab == 0 && !watchPaused && now >= nextWatchRefresh)
        {
            nextWatchRefresh = now + 0.25f;
            RefreshWatchHits();
        }
        if (visible && lastPage == "内存浏览" && hexOrigin != 0)
        {
            if (structDissectMode) RefreshStructDissect(resetBaseline: false);
            else RefreshHexWindow();
        }

        if (GUIUtility.keyboardControl == 0)
        {
            foreach (var entry in entries)
            {
                if (entry.Hotkey != KeyCode.None && Input.GetKeyDown(entry.Hotkey) && RefreshEntry(entry) && entry.Writable)
                {
                    if (entry.Locked != null) entry.Locked = null;
                    else entry.Locked = entry.Current;
                    SetStatus($"{entry.Hotkey}：{(entry.Locked != null ? "已锁定" : "已解锁")} {(string.IsNullOrEmpty(entry.Description) ? $"0x{entry.Address:X}" : entry.Description)}");
                }
            }
        }

        foreach (var entry in entries)
        {
            if (!visible && entry.Locked == null) continue;
            if (!RefreshEntry(entry)) continue;
            if (entry.Locked is {} target && target != entry.Current)
            {
                if (Write(entry.Address, entry.Allocation, valueBytes, Size(entry.Type), target))
                {
                    Record(entry, entry.Current, target, "锁定回写");
                    entry.Current = target;
                }
                else
                {
                    entry.Locked = null;
                    RefreshEntry(entry);
                }
            }
        }
    }

    static bool RefreshEntry(Entry entry)
    {
        bool pointer = entry.IsPointer;
        ulong address = entry.Address;
        ulong? expectedAlloc = pointer || entry.Locked.HasValue ? (entry.Allocation == 0 ? null : entry.Allocation) : null;
        if (pointer) expectedAlloc = null;
        if ((!pointer || TryResolvePointerChain(entry.BaseAddress, entry.Offsets, out address)) &&
            TryRead(address, entry.Type, expectedAlloc, out var match, out bool writable, requireEditable: false))
        {
            ApplyEntrySample(entry, match, writable);
            return true;
        }
        if (pointer)
        {
            entry.Valid = entry.Writable = false;
            entry.Undo = null;
        }
        else Invalidate(entry);
        return false;
    }

    static void ApplyEntrySample(Entry entry, Match match, bool writable)
    {
        if (!writable || entry.Address != match.Address || entry.Allocation != match.Allocation) entry.Undo = null;
        if (!writable) entry.Locked = null;
        entry.Address = match.Address;
        entry.Allocation = match.Allocation;
        entry.Valid = true;
        entry.Writable = writable;
        if (entry.Current != match.Value) Record(entry, entry.Current, match.Value, "采样变化");
        entry.Current = match.Value;
    }

    static void Invalidate(Entry entry)
    {
        entry.Valid = false;
        entry.Writable = false;
        entry.Locked = null;
        entry.Undo = null;
        if (selectedEntry == entry) SetStatus("所选地址已失效。", true);
    }

    static bool TryRead(ulong address, MemoryValueType type, ulong? allocation, out Match match, out bool writable,
        bool requireEditable = true)
    {
        match = default;
        writable = false;
        int size = Size(type);
        if (OverlapsBuffer(address, size, valueBytes) ||
            scanBytes != null && OverlapsBuffer(address, size, scanBytes) ||
            !Query(address, out var region) || !(requireEditable ? region.Editable : region.Readable) || !region.Contains(address, size) ||
            allocation is {} expected && expected != 0 && expected != region.Allocation || !Read(address, valueBytes, size))
            return false;
        ulong val = Decode(valueBytes, size);
        match = new(address, region.Allocation, val, val);
        writable = region.Writable;
        return true;
    }

    static void SampleScanPage(int start, int count)
    {
        float now = Time.unscaledTime;
        if (sampledScanStart == start && sampledScanType == resultType && now < nextScanSample) return;
        sampledScanStart = start;
        sampledScanType = resultType;
        nextScanSample = now + 0.15f;
        for (int i = 0; i < count; i++)
        {
            var row = matches[start + i];
            liveScanValues[i] = (row, TryRead(row.Address, resultType, row.Allocation, out var live, out _, requireEditable: false)
                ? live.Value : null);
        }
    }

    static ulong BufferStart(byte[] buffer) =>
        (ulong)Marshal.UnsafeAddrOfPinnedArrayElement(buffer, 0).ToInt64();

    static bool OverlapsBuffer(ulong address, int size, byte[] buffer) =>
        Overlaps(address, size, BufferStart(buffer), buffer.Length);

    static void SetPage(int targetPage, int totalPages)
    {
        int maxPage = Math.Max(0, totalPages - 1);
        int nextPage = Math.Clamp(targetPage, 0, maxPage);
        if (nextPage != page) selectedMatch = -1;
        page = nextPage;
        pageInput = (page + 1).ToString(CultureInfo.InvariantCulture);
        resultScroll = default;
    }

    static void RemoveScanResult(int index)
    {
        if (scan != null || index < 0 || index >= matches.Count) return;
        matches.RemoveAt(index);
        selectedMatch = -1;
        sampledScanStart = -1;
        int pages = Math.Max(1, (matches.Count + PageSize - 1) / PageSize);
        if (page >= pages) SetPage(pages - 1, pages);
        SetStatus("已移除选中的扫描结果。");
    }

    static void JumpToPage(int totalPages)
    {
        if (int.TryParse(pageInput.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int requested) && requested >= 1)
            SetPage(requested - 1, totalPages);
        else
            SetStatus("请输入有效的页码。", true);
    }

    static void CancelScan()
    {
        if (scan is not {} job) return;
        scan = null;
        scanBytes = null;
        sampledScanStart = -1;
        if (job.OutputSnapshot != null)
        {
            if (job.OutputSnapshot.Count > 0)
            {
                unknownSnapshot = job.OutputSnapshot;
                matches = [];
                resultType = job.ValueType;
                scanMode = MemoryScanMode.Decreased;
                double mb = job.SnapshotBytes / (1024.0 * 1024.0);
                SetStatus($"已取消快照扫描，保留已采集的 {unknownSnapshot.Count} 段内存（{mb:F1} MB），可继续筛选。");
            }
            else
            {
                SetStatus("已取消扫描。");
            }
            return;
        }

        if (job.Results.Count > 0)
        {
            unknownSnapshot = null;
            matches = job.Results;
            resultType = job.ValueType;
            selectedMatch = -1;
            SetPage(0, Math.Max(1, (matches.Count + PageSize - 1) / PageSize));
            SetStatus($"已取消扫描，保留已找到的 {matches.Count:N0} 条结果，可继续筛选。");
        }
        else
        {
            SetStatus("已取消扫描（当前尚未找到新结果）。");
        }
    }

    static void ResetScan()
    {
        scan = null;
        scanBytes = null;
        matches = [];
        sampledScanStart = -1;
        unknownSnapshot = null;
        selectedMatch = -1;
        page = 0;
        pageInput = "1";
        resultScroll = default;
        if (!FirstScanModes.Contains(scanMode))
            scanMode = dataKind is ScanDataKind.Float or ScanDataKind.Double ? MemoryScanMode.Rounded : MemoryScanMode.Exact;
        SetStatus("已清空扫描结果，可开始新扫描。");
    }

    static bool TryResolveScanRange(out ulong start, out ulong end)
    {
        start = 0;
        end = AddressLimit;
        if (!string.IsNullOrWhiteSpace(scanRangeStartInput))
        {
            if (TryGetModuleRange(scanRangeStartInput.Trim(), out ulong modBase, out ulong modEnd) &&
                string.IsNullOrWhiteSpace(scanRangeEndInput))
            {
                start = modBase;
                end = modEnd;
                return true;
            }
            if (!TryResolveSimpleAddress(scanRangeStartInput, out start)) return false;
        }
        if (!string.IsNullOrWhiteSpace(scanRangeEndInput))
        {
            if (!TryResolveSimpleAddress(scanRangeEndInput, out end) || end <= start) return false;
        }
        return true;
    }

    static bool RegionMatchesScope(in Region region, ScanScope scope, bool isAob)
    {
        if (isAob && scope == ScanScope.EditablePrivate) return region.Readable;
        return scope switch
        {
            ScanScope.AllWritable => region.Writable,
            ScanScope.AllReadable => region.Readable,
            _ => region.Editable
        };
    }

    static void StartScan(bool refine)
    {
        if (refine && !HasPriorScan)
        {
            SetStatus("请先执行首次扫描。", true);
            return;
        }

        if (!IsNumericKind(dataKind))
        {
            if (refine) { SetStatus("字节数组/字符串模式请直接重新扫描。", true); return; }
            byte[] pat, msk;
            if (dataKind == ScanDataKind.Aob)
            {
                if (!TryParseAob(query, out pat, out msk))
                {
                    SetStatus("请输入有效字节数组，例如：48 89 ?? 24 57", true);
                    return;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(query))
                {
                    SetStatus("请输入要搜索的字符串。", true);
                    return;
                }
                pat = utf16String ? Encoding.Unicode.GetBytes(query) : Encoding.UTF8.GetBytes(query);
                msk = new byte[pat.Length];
                Array.Fill(msk, (byte)0xFF);
            }
            if (!TryResolveScanRange(out ulong rangeStart, out ulong rangeEnd))
            {
                SetStatus("扫描地址范围无效（支持十六进制或模块名）。", true);
                return;
            }
            unknownSnapshot = null;
            scan = new(dataKind, MemoryValueType.Byte, MemoryScanMode.Exact, 0, 0, 0, 0, pat, msk, null, null, false,
                scanScope, 1, rangeStart, rangeEnd);
            SetStatus("正在扫描特征码/字符串…");
            return;
        }

        MemoryValueType vt = ToValueType(dataKind);
        MemoryScanMode mode = scanMode;
        if (!refine && !FirstScanModes.Contains(mode))
            mode = vt is MemoryValueType.Float or MemoryValueType.Double ? MemoryScanMode.Rounded : MemoryScanMode.Exact;
        if (mode == MemoryScanMode.Rounded && vt is not (MemoryValueType.Float or MemoryValueType.Double))
            mode = MemoryScanMode.Exact;

        ulong target = 0, targetMax = 0;
        double low = 0, high = 0;
        if (mode is MemoryScanMode.Exact or MemoryScanMode.GreaterThan or MemoryScanMode.LessThan)
        {
            if (!TryParse(query, vt, out target))
            {
                SetStatus("请输入与当前类型匹配的数值。", true);
                return;
            }
        }
        else if (mode == MemoryScanMode.Between)
        {
            if (!TryParse(query, vt, out target) || !TryParse(queryMax, vt, out targetMax))
            {
                SetStatus("请输入有效的最小值和最大值。", true);
                return;
            }
            if (Compare(target, targetMax, vt) > 0) (target, targetMax) = (targetMax, target);
        }
        else if (mode == MemoryScanMode.Rounded)
        {
            if (!TryParseRoundedBounds(query, vt, out low, out high))
            {
                SetStatus("请输入有效的浮点数（例如 100 或 99.5）。", true);
                return;
            }
        }

        if (!TryResolveScanRange(out ulong rStart, out ulong rEnd))
        {
            SetStatus("扫描地址范围无效（支持十六进制或模块名）。", true);
            return;
        }
        int step = unalignedScan ? 1 : Size(vt);

        if (!refine)
        {
            unknownSnapshot = null;
            matches = [];
            bool captureSnap = mode == MemoryScanMode.Unknown;
            scan = new(dataKind, vt, mode, target, targetMax, low, high, [], [], null, null, captureSnap,
                scanScope, step, rStart, rEnd);
            SetStatus(captureSnap ? "正在建立未知初始值快照（最多 256 MB）…" : "正在首次扫描…");
        }
        else if (unknownSnapshot != null)
        {
            scan = new(dataKind, vt, mode, target, targetMax, low, high, [], [], null, unknownSnapshot, false,
                scanScope, step, rStart, rEnd);
            SetStatus("正在对比初始快照筛选…");
        }
        else
        {
            if (resultType != vt)
            {
                SetStatus("继续筛选需保持与首次扫描相同的数值类型。", true);
                return;
            }
            scan = new(dataKind, vt, mode, target, targetMax, low, high, [], [], matches, null, false,
                scanScope, step, rStart, rEnd);
            SetStatus("正在继续筛选…");
        }
    }

    static void ScanStep(Scan job)
    {
        long started = Stopwatch.GetTimestamp();
        do
        {
            bool more;
            if (job.OutputSnapshot != null) more = CaptureSnapshotBlock(job);
            else if (job.InputSnapshot != null) more = RefineSnapshotBlock(job);
            else if (job.InputMatches != null) more = RefineMatchBatch(job);
            else more = ScanLiveBlock(job);

            if (!more || (job.OutputSnapshot == null && job.Results.Count >= MatchLimit))
            {
                if (job.OutputSnapshot != null)
                {
                    unknownSnapshot = job.OutputSnapshot;
                    matches = [];
                    resultType = job.ValueType;
                    double mb = job.SnapshotBytes / (1024.0 * 1024.0);
                    SetStatus(job.SnapshotCapped
                        ? $"已建立初始快照 {mb:F1} MB（达到 256 MB 上限），请选择“已变化/已减少/已增加”继续筛选。"
                        : $"已建立初始快照 {mb:F1} MB（{unknownSnapshot.Count} 段），请改变数值后继续筛选。");
                    scanMode = MemoryScanMode.Decreased;
                }
                else
                {
                    unknownSnapshot = null; // Free snapshot immediately after first refine
                    matches = job.Results;
                    resultType = job.ValueType;
                    SetStatus(matches.Count >= MatchLimit
                        ? $"扫描完成，已达 {MatchLimit:N0} 条结果上限，请继续筛选。"
                        : $"扫描完成，找到 {matches.Count:N0} 条结果。");
                }
                scan = null;
                scanBytes = null;
                sampledScanStart = -1;
                page = 0;
                pageInput = "1";
                selectedMatch = -1;
                resultScroll = default;
                return;
            }
        }
        while (Stopwatch.GetElapsedTime(started).TotalMilliseconds < 3.0);
    }

    static bool CaptureSnapshotBlock(Scan job)
    {
        if (job.SnapshotBytes >= SnapshotBudgetBytes || job.Cursor >= job.RangeEnd) { job.SnapshotCapped = job.SnapshotBytes >= SnapshotBudgetBytes; return false; }
        if (job.Region.End <= job.Cursor && !Query(job.Cursor, out job.Region)) return false;
        if (!RegionMatchesScope(in job.Region, job.Scope, false))
        {
            job.Cursor = job.Region.End;
            return job.Cursor < job.RangeEnd;
        }
        ulong start = job.Cursor;
        ulong regionEnd = Math.Min(job.Region.End, job.RangeEnd);
        int len = (int)Math.Min((ulong)BlockSize, Math.Min(regionEnd - start, (ulong)(SnapshotBudgetBytes - job.SnapshotBytes)));
        job.Cursor += (ulong)len;
        if (len < job.Size) return job.Cursor < job.RangeEnd;
        var copy = new byte[len];
        if (!Read(start, copy, len)) return job.Cursor < job.RangeEnd;
        job.OutputSnapshot!.Add(new(start, job.Region.Allocation, copy));
        job.SnapshotBytes += len;
        return job.SnapshotBytes < SnapshotBudgetBytes && job.Cursor < job.RangeEnd;
    }

    static bool RefineSnapshotBlock(Scan job)
    {
        var chunks = job.InputSnapshot!;
        if (job.Index >= chunks.Count) return false;
        var chunk = chunks[job.Index];
        var live = scanBytes ??= GC.AllocateArray<byte>(BlockSize, pinned: true);
        if (chunk.Data.Length > live.Length)
        {
            job.Index++;
            job.ChunkOffset = 0;
            return job.Index < chunks.Count;
        }
        if (job.ChunkOffset == 0 && !Read(chunk.Base, live, chunk.Data.Length))
        {
            job.Index++;
            return job.Index < chunks.Count;
        }
        ulong scratchStart = BufferStart(live), smallStart = BufferStart(valueBytes);
        int end = Math.Min(chunk.Data.Length - job.Size, job.ChunkOffset + 64 * 1024);
        for (int off = job.ChunkOffset; off <= end && job.Results.Count < MatchLimit; off += job.Size)
        {
            ulong cur = Decode(live.AsSpan(off), job.Size);
            ulong prev = Decode(chunk.Data.AsSpan(off), job.Size);
            if (!MatchesFilter(cur, prev, job.ValueType, job.Mode, job.Target, job.TargetMax, job.LowBound, job.HighBound))
                continue;
            ulong addr = chunk.Base + (ulong)off;
            if (Overlaps(addr, job.Size, scratchStart, live.Length) || Overlaps(addr, job.Size, smallStart, valueBytes.Length))
                continue;
            job.Results.Add(new(addr, chunk.Allocation, cur, prev));
        }
        job.ChunkOffset = end + job.Size;
        if (job.ChunkOffset > chunk.Data.Length - job.Size)
        {
            job.Index++;
            job.ChunkOffset = 0;
        }
        return job.Index < chunks.Count;
    }

    static bool ScanLiveBlock(Scan job)
    {
        if (job.Cursor >= job.RangeEnd) return false;
        if (job.Region.End <= job.Cursor && !Query(job.Cursor, out job.Region)) return false;
        bool scannable = RegionMatchesScope(in job.Region, job.Scope, job.Kind == ScanDataKind.Aob);
        if (!scannable)
        {
            job.Cursor = job.Region.End;
            return job.Cursor < job.RangeEnd;
        }
        var bytes = scanBytes ??= GC.AllocateArray<byte>(BlockSize, pinned: true);
        ulong start = job.Cursor;
        ulong regionEnd = Math.Min(job.Region.End, job.RangeEnd);
        int length = (int)Math.Min((ulong)bytes.Length, regionEnd - start);
        job.Cursor += (ulong)length;
        if (length < job.Size || !Read(start, bytes, length)) return job.Cursor < job.RangeEnd;

        ulong scratchStart = BufferStart(bytes), smallStart = BufferStart(valueBytes);
        if (job.Kind is ScanDataKind.Aob or ScanDataKind.String)
        {
            var pat = job.Pattern;
            var msk = job.Mask;
            int patLen = pat.Length;
            int limit = length - patLen;
            for (int off = 0; off <= limit && job.Results.Count < MatchLimit; off++)
            {
                bool ok = true;
                for (int j = 0; j < patLen; j++)
                {
                    if ((bytes[off + j] & msk[j]) != pat[j]) { ok = false; break; }
                }
                if (!ok) continue;
                ulong addr = start + (ulong)off;
                if (Overlaps(addr, patLen, scratchStart, bytes.Length) || Overlaps(addr, patLen, smallStart, valueBytes.Length))
                    continue;
                job.Results.Add(new(addr, job.Region.Allocation, bytes[off], bytes[off]));
            }
            return job.Cursor < job.RangeEnd;
        }

        int step = Math.Max(1, job.Step);
        for (int off = 0; off <= length - job.Size && job.Results.Count < MatchLimit; off += step)
        {
            ulong value = Decode(bytes.AsSpan(off), job.Size);
            if (!MatchesFilter(value, value, job.ValueType, job.Mode, job.Target, job.TargetMax, job.LowBound, job.HighBound))
                continue;
            ulong addr = start + (ulong)off;
            if (Overlaps(addr, job.Size, scratchStart, bytes.Length) || Overlaps(addr, job.Size, smallStart, valueBytes.Length))
                continue;
            job.Results.Add(new(addr, job.Region.Allocation, value, value));
        }
        return job.Cursor < job.RangeEnd;
    }

    static bool RefineMatchBatch(Scan job)
    {
        var input = job.InputMatches!;
        int end = Math.Min(input.Count, job.Index + 256);
        while (job.Index < end && job.Results.Count < MatchLimit)
        {
            var prev = input[job.Index++];
            if (!TryRead(prev.Address, job.ValueType, prev.Allocation, out var cur, out _, requireEditable: false)) continue;
            if (MatchesFilter(cur.Value, prev.Value, job.ValueType, job.Mode, job.Target, job.TargetMax, job.LowBound, job.HighBound))
                job.Results.Add(new(cur.Address, cur.Allocation, cur.Value, prev.Value));
        }
        return job.Index < input.Count;
    }

    static bool AddAddress(ulong address, MemoryValueType valueType, ulong? allocation = null, string description = "", long[]? offsets = null, ulong baseAddr = 0)
    {
        ulong target = address;
        if (offsets is { Length: > 0 })
        {
            if (!TryResolvePointerChain(baseAddr, offsets, out target))
            {
                SetStatus("无法解析该多级指针链（中间指针无效或不可读）。", true);
                return false;
            }
        }
        if (!TryRead(target, valueType, offsets is { Length: > 0 } ? null : allocation, out var match, out bool writable,
            requireEditable: false))
        {
            SetStatus("无法读取目标地址，请检查地址和数值类型。", true);
            return false;
        }
        var existing = entries.Find(e => e.Type == valueType && e.Offsets.AsSpan().SequenceEqual(offsets) &&
            (e.IsPointer ? e.BaseAddress == baseAddr : e.Address == target));
        if (existing is { Valid: true })
        {
            ApplyEntrySample(existing, match, writable);
            SelectEntry(existing);
            SetStatus($"地址 0x{target:X} 已在地址表中。");
            return true;
        }
        if (entries.Count >= EntryLimit)
        {
            SetStatus($"地址表已满（上限 {EntryLimit} 条），请先删除不需要的记录。", true);
            return false;
        }
        var entry = new Entry
        {
            BaseAddress = offsets is { Length: > 0 } ? baseAddr : target,
            Offsets = offsets ?? [],
            Address = match.Address,
            Allocation = match.Allocation,
            Type = valueType,
            Description = description,
            Current = match.Value,
            Writable = writable,
            Valid = true
        };
        entries.Add(entry);
        SelectEntry(entry);
        SetStatus($"已添加到地址表：0x{entry.Address:X}");
        return true;
    }

    static void AddManualAddress()
    {
        if (!TryParsePointerExpression(addressInput, out ulong baseAddr, out long[] offsets, out _))
        {
            SetStatus("格式无效，请输入十六进制地址、模块+偏移，或 [基址]+偏移1+偏移2。", true);
            return;
        }
        AddAddress(baseAddr, entryType, null, "", offsets, baseAddr);
    }

    static void SaveValuePreset(Entry entry)
    {
        try
        {
            if (!TryParse(editValueText, entry.Type, out _))
            {
                SetStatus("请先输入有效的数值。", true);
                return;
            }
            if (!MemoryValuePreset.TryCreate(ClientEventVersion.Version, entry.BaseAddress, entry.Offsets,
                entry.Type, editValueText, entry.Description, out var preset) || preset == null)
            {
                SetStatus("保存需要有效的游戏版本和模块内基址。临时地址可以先换成以模块为起点的指针链。", true);
                return;
            }
            valuePresets = MemoryValuePresets.Save(ValuePresetPath, preset);
            SetStatus("已保存输入的数值和定位方式。");
        }
        catch (Exception ex) { SetStatus("保存调试预设失败：" + ex.Message, true); }
    }

    static void OpenValuePresets()
    {
        try
        {
            valuePresets = MemoryValuePresets.Load(ValuePresetPath);
            presetScroll = default;
            showValuePresets = true;
        }
        catch (Exception ex) { SetStatus("读取调试预设失败：" + ex.Message, true); }
    }

    static void LoadValuePreset(MemoryValuePreset preset)
    {
        try
        {
            string version = ClientEventVersion.Version;
            if (preset.GameVersion != version)
            {
                SetStatus("此预设来自其他游戏版本，请在当前版本重新确认地址。", true);
                return;
            }
            if (!preset.TryResolveBase(version, out ulong baseAddress))
            {
                SetStatus("暂时找不到预设对应的模块地址。", true);
                return;
            }
            if (!AddAddress(baseAddress, preset.Type, description: preset.Description,
                offsets: preset.Offsets, baseAddr: baseAddress))
                return;
            editValueText = preset.Value;
            valueDraftEdited = true;
            showValuePresets = false;
            SetStatus(selectedEntry?.Writable == true
                ? "已填入保存的数值。确认地址后可点击「写入」或「锁定」。"
                : "已定位到只读地址，可以查看或监控。");
        }
        catch (Exception ex) { SetStatus("载入调试预设失败：" + ex.Message, true); }
    }

    static void RemoveValuePreset(MemoryValuePreset preset)
    {
        try
        {
            valuePresets = MemoryValuePresets.Remove(ValuePresetPath, preset);
            SetStatus("已删除调试预设。");
        }
        catch (Exception ex) { SetStatus("删除调试预设失败：" + ex.Message, true); }
    }

    static void SelectEntry(Entry? entry)
    {
        selectedEntry = entry;
        editValueText = entry != null ? Format(entry.Current, entry.Type, hexDisplay, unsignedDisplay) : "";
        editDescText = entry?.Description ?? "";
        editAddrText = entry == null ? "" :
            entry.IsPointer ? FormatPointerExpression(entry.BaseAddress, entry.Offsets) : $"0x{entry.Address:X}";
        valueDraftEdited = false;
    }

    static bool UpdateSelectedEntryTarget(Entry entry, MemoryValueType nextType, string addrText)
    {
        if (!TryParsePointerExpression(addrText, out ulong baseAddr, out long[] offsets, out _))
        {
            SetStatus("地址或指针链格式无效。", true);
            return false;
        }
        ulong target = baseAddr;
        if (offsets.Length > 0 && !TryResolvePointerChain(baseAddr, offsets, out target))
        {
            SetStatus("无法解析该多级指针链。", true);
            return false;
        }
        if (!TryRead(target, nextType, null, out var match, out bool writable, requireEditable: false))
        {
            SetStatus("目标地址不可读。", true);
            return false;
        }
        entry.BaseAddress = offsets.Length > 0 ? baseAddr : target;
        entry.Offsets = offsets;
        entry.Type = nextType;
        entry.Locked = null;
        entry.Undo = null;
        ApplyEntrySample(entry, match, writable);
        SelectEntry(entry);
        SetStatus("已更新条目类型与定位。");
        return true;
    }

    static void SaveAddressTable()
    {
        try
        {
            string version = ClientEventVersion.Version;
            var list = new List<MemoryTableEntryPreset>(entries.Count);
            foreach (var e in entries)
            {
                string mod = "";
                ulong off = e.BaseAddress;
                if (TryResolveModule(e.BaseAddress, out string m, out ulong mo))
                {
                    mod = m;
                    off = mo;
                }
                list.Add(new(e.Description, version, mod, off, e.Offsets, e.Type,
                    Format(e.Current, e.Type, false, false), e.Locked.HasValue, e.Hotkey.ToString()));
            }
            PresetStorage.Save(AddressTablePath, list);
            SetStatus($"已保存 {list.Count} 条地址表记录到 memory-address-table.json。");
        }
        catch (Exception ex) { SetStatus("保存地址表失败：" + ex.Message, true); }
    }

    static void LoadAddressTable()
    {
        try
        {
            var list = PresetStorage.Load<MemoryTableEntryPreset>(AddressTablePath);
            if (list.Count == 0) { SetStatus("未找到已保存的地址表记录。", true); return; }
            string version = ClientEventVersion.Version;
            int added = 0;
            foreach (var item in list)
            {
                ulong baseAddr = item.Offset;
                if (!string.IsNullOrEmpty(item.Module))
                {
                    if (!TryGetModuleBase(item.Module, out ulong modBase)) continue;
                    baseAddr = modBase + item.Offset;
                }
                else if (item.GameVersion != version) continue;

                if (AddAddress(baseAddr, item.Type, description: item.Description,
                    offsets: item.Offsets, baseAddr: baseAddr) && selectedEntry is {} addedEntry)
                {
                    if (Enum.TryParse<KeyCode>(item.Hotkey, out var hk)) addedEntry.Hotkey = hk;
                    if (item.Locked && addedEntry.Writable && TryParse(item.Value, item.Type, out ulong v))
                        addedEntry.Locked = v;
                    added++;
                }
            }
            SetStatus($"已载入 {added}/{list.Count} 条地址表记录。");
        }
        catch (Exception ex) { SetStatus("载入地址表失败：" + ex.Message, true); }
    }

    static void BeginPointerScan(ulong address)
    {
        pointerScanTarget = address;
        showPointerScanner = true;
        showValuePresets = false;
        RunPointerScan();
        InterfaceMenu.Navigate("内存编辑", "地址表");
    }

    static void RunPointerScan()
    {
        if (pointerScanTarget == 0) { pointerScanResults = []; return; }
        if (!ulong.TryParse(pointerMaxOffsetInput.Trim().TrimStart('0', 'x', 'X'), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong maxOff))
            maxOff = 0x400;
        pointerScanResults = MemoryPointerScanner.Scan(pointerScanTarget, pointerMaxDepth, maxOff);
        pointerScanScroll = default;
        SetStatus($"指针反查完成：找到 {pointerScanResults.Count} 条静态指针链。");
    }

    static void RemoveAddressEntry(Entry entry)
    {
        if (!entries.Remove(entry)) return;
        if (selectedEntry == entry) SelectEntry(null);
        SetStatus("已移除选中的地址。");
    }

    static void SetEntryValue(Entry entry, bool lockValue, bool undo = false)
    {
        if (!RefreshEntry(entry)) return;
        if (!entry.Writable)
        {
            SetStatus("该地址只读，可以查看或监控。", true);
            return;
        }
        ulong value;
        if (undo)
        {
            if (entry.Undo is not {} original) return;
            value = original;
        }
        else if (!TryParse(editValueText, entry.Type, out value))
        {
            SetStatus("请输入与当前类型匹配的数值。", true);
            return;
        }
        ulong before = entry.Current;
        if (!Write(entry.Address, entry.Allocation, valueBytes, Size(entry.Type), value))
        {
            RefreshEntry(entry);
            SetStatus("无法写入当前地址。", true);
            return;
        }
        Record(entry, before, value, undo ? "撤销写入" : lockValue ? "锁定" : "写入");
        entry.Undo = undo ? null : before;
        entry.Current = value;
        entry.Locked = !undo && (lockValue || entry.Locked.HasValue) ? value : null;
        editValueText = Format(value, entry.Type, hexDisplay);
        valueDraftEdited = false;
        SetStatus(undo ? "已恢复上次写入前的值。" : lockValue ? "已写入并锁定该地址。" : "已写入数值。");
    }

    static void BeginWatch(ulong address, int size, WatchMode mode, string label)
    {
        if (!MemoryWatchpoint.StartWatch(address, size, mode, label, out int slot, out string err))
        {
            SetStatus(err, true);
            return;
        }
        activeWatchSlot = slot;
        watchSubTab = 0;
        selectedHitIndex = -1;
        selectFirstSample = false;
        RefreshWatchHits();
        InterfaceMenu.Navigate("内存编辑", "访问监控");
        SetStatus($"已启动 DR{slot} {(mode == WatchMode.Write ? "改写" : "访问")}监控：0x{address:X}（{size}B）");
    }

    static void BeginWatch(Entry entry, WatchMode mode)
    {
        if (!RefreshEntry(entry)) return;
        string label = string.IsNullOrEmpty(entry.Description) ? $"0x{entry.Address:X}" : entry.Description;
        BeginWatch(entry.Address, Size(entry.Type), mode, label);
    }

    static void RefreshWatchHits()
    {
        ulong? rip = selectedHitIndex >= 0 && selectedHitIndex < cachedHits.Length
            ? cachedHits[selectedHitIndex].TrapRip : null;
        cachedHits = MemoryWatchpoint.ReadHits(activeWatchSlot);
        selectedHitIndex = rip is ulong previous ? Array.FindIndex(cachedHits, hit => hit.TrapRip == previous) : -1;
        if (selectFirstSample && cachedHits.Length != 0)
        {
            selectedHitIndex = 0;
            selectFirstSample = false;
            SetStatus("已采样。可在下方查看寄存器快照。");
        }
    }

    static void BeginInstructionSample(ulong address)
    {
        if (!MemoryWatchpoint.SampleAtNextInstruction(address, out int slot, out string error))
        {
            SetStatus(error, true);
            return;
        }
        activeWatchSlot = slot;
        selectedHitIndex = -1;
        selectFirstSample = true;
        watchSubTab = 0;
        SelectRegisterView(address, preferXmm: true);
        SetStatus("等待执行到下一条指令。命中后查看下方 XMM 快照。");
        RefreshWatchHits();
        hitScroll = default;
        InterfaceMenu.Navigate("内存编辑", "访问监控");
    }

    static void SelectRegisterView(ulong address, bool preferXmm)
    {
        if (MemoryDisassembly.TryDecodeSingle(address, out var instruction, out _, out _) && instruction.OpCount > 0)
        {
            int index = (int)instruction.Op0Register - (int)Iced.Intel.Register.XMM0;
            if (index >= 0 && index < MemoryWatchpoint.XmmRegisterCount)
            {
                selectedXmm = index;
                showXmm = true;
                return;
            }
        }
        selectedXmm = 0;
        showXmm = preferXmm;
    }

    static void ShowInstructionRegisters(ulong address)
    {
        var slots = MemoryWatchpoint.GetSlots();
        for (int offset = 0; offset < slots.Length; offset++)
        {
            int slot = (activeWatchSlot + offset) % slots.Length;
            if (!slots[slot].Active) continue;
            var hits = MemoryWatchpoint.ReadHits(slot);
            int index = Array.FindIndex(hits, hit => hit.InstructionAddress == address);
            if (index < 0) continue;
            activeWatchSlot = slot;
            cachedHits = hits;
            selectedHitIndex = index;
            selectFirstSample = false;
            watchSubTab = 0;
            SelectRegisterView(address, preferXmm: false);
            hitScroll.y = Math.Max(0, (index - 1) * RowHeight);
            InterfaceMenu.Navigate("内存编辑", "访问监控");
            return;
        }
        SetStatus("这条指令还没有寄存器快照。可在右键菜单选择「在下一条指令采样」。", true);
    }

    static bool OpenInDisassembler(ulong address, ulong highlight = 0, int rowLimit = DisassemblyPageRows)
    {
        if (!LoadDisassembly(address, rowLimit, highlight == 0 ? address : highlight, remember: true)) return false;
        FinishOpeningDisassembly();
        return true;
    }

    static void FinishOpeningDisassembly()
    {
        browseCode = true;
        inlineEditMode = 0;
        disassemblyPages.Clear();
        InterfaceMenu.Navigate("内存编辑", "反汇编");
    }

    static void OpenLeadingContext(ulong address)
    {
        int before = leadingContextRows?.Value ?? DefaultLeadingContextRows;
        int after = followingContextRows?.Value ?? DefaultFollowingContextRows;
        var rows = MemoryDisassembly.ReadInstructionContext(address, before, after, out int index, out ulong next);
        if (index < 0)
        {
            SetStatus("无法读取该代码地址，请检查地址或页面权限。", true);
            return;
        }
        var view = new DisassemblyPage(rows[0].Address, index + 1 + after, address);
        RememberDisassemblyOrigin(view);
        ShowDisassembly(view, rows, next);
        FinishOpeningDisassembly();
        SetStatus($"已定位到 {codeAddressInput} · 前导 {index} 条，后续 {rows.Length - index - 1} 条。");
    }

    static void OpenContextPopup(ulong address, int before, int after)
    {
        var rows = MemoryDisassembly.ReadInstructionContext(address, before, after, out int hitIndex, out _);
        if (hitIndex < 0)
        {
            SetStatus("无法读取选中的指令。", true);
            return;
        }
        contextPopupTitle = $"反汇编上下文 · {rows[hitIndex].ModuleOffset}（前 {hitIndex} / 后 {rows.Length - hitIndex - 1}）";
        contextPopupText = MemoryDisassembly.FormatInstructionContext(rows, hitIndex);
        contextPopupScroll = default;
        showContextPopup = true;
    }

    static void EnsurePatchPresetsLoaded()
    {
        if (patchPresetsLoaded) return;
        patchPresetsLoaded = true;
        try { patchPresets = MemoryPatchPresets.Load(PatchPresetPath); }
        catch { patchPresets = []; }
    }

    static void SavePatchAsPreset(CodePatch patch, string? customName = null)
    {
        try
        {
            if (!MemoryCodePatcher.GenerateAobSignature(patch.Address, 16, out string aob, out string mod, out ulong modOff))
            {
                SetStatus("无法从该地址生成特征码。", true);
                return;
            }
            string name = string.IsNullOrWhiteSpace(customName) ? $"{patch.ModuleOffset} #{patch.Id}" : customName.Trim();
            string mode = patch.CaveAddress != 0 ? "CodeCave" : "Raw";
            string code = BitConverter.ToString(patch.PatchedBytes).Replace("-", " ");
            var preset = new MemoryPatchPreset(name, ClientEventVersion.Version, mod, modOff, aob, mode, code, 2);
            patchPresets = MemoryPatchPresets.Save(PatchPresetPath, preset);
            patchPresetsLoaded = true;
            SetStatus($"已保存补丁脚本「{name}」({aob})。");
        }
        catch (Exception ex) { SetStatus("保存补丁脚本失败：" + ex.Message, true); }
    }

    static void SaveInlineAsPreset(ulong address, string codeInput, bool isCave, int order, string? customName)
    {
        try
        {
            if (!MemoryCodePatcher.GenerateAobSignature(address, 16, out string aob, out string mod, out ulong modOff))
            {
                SetStatus("无法从该地址生成特征码。", true);
                return;
            }
            string modLabel = FormatModuleAddress(address);
            string name = string.IsNullOrWhiteSpace(customName) ? $"{modLabel} ({(isCave ? "Cave" : "Asm")})" : customName.Trim();
            var preset = new MemoryPatchPreset(name, ClientEventVersion.Version, mod, modOff, aob,
                isCave ? "CodeCave" : "Raw", codeInput.Trim(), order);
            patchPresets = MemoryPatchPresets.Save(PatchPresetPath, preset);
            patchPresetsLoaded = true;
            SetStatus($"已保存 AOB 补丁脚本「{name}」。");
        }
        catch (Exception ex) { SetStatus("保存补丁脚本失败：" + ex.Message, true); }
    }

    static void ApplySavedPatchPreset(MemoryPatchPreset preset)
    {
        if (MemoryPatchPresets.TryApplyPreset(preset, ClientEventVersion.Version, out _, out string err))
        {
            if (disasmOrigin != 0) LoadDisassembly(disasmOrigin);
            SetStatus($"已定位并应用补丁脚本「{preset.Name}」。");
        }
        else SetStatus(err, true);
    }

    static void ApplyAllSavedPatchPresets()
    {
        int ok = 0;
        foreach (var p in patchPresets)
        {
            if (MemoryPatchPresets.TryApplyPreset(p, ClientEventVersion.Version, out _, out _)) ok++;
        }
        if (disasmOrigin != 0) LoadDisassembly(disasmOrigin);
        SetStatus($"已成功应用 {ok}/{patchPresets.Count} 个补丁脚本。");
    }

    static void RemoveSavedPatchPreset(MemoryPatchPreset preset)
    {
        try
        {
            patchPresets = MemoryPatchPresets.Remove(PatchPresetPath, preset.Name);
            SetStatus($"已删除补丁脚本「{preset.Name}」。");
        }
        catch (Exception ex) { SetStatus("删除补丁脚本失败：" + ex.Message, true); }
    }

    static void RefreshStructDissect(bool resetBaseline)
    {
        if (hexOrigin == 0) { structRows = []; return; }
        if (!int.TryParse(structSizeInput.Trim().TrimStart('0', 'x', 'X'), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int size))
            size = 0x200;
        size = Math.Clamp(size, 16, 4096);
        var prev = resetBaseline ? null : structBaselineBytes;
        structRows = MemoryPointerScanner.ReadStruct(hexOrigin, size, structStride, prev, out var curBytes);
        if (resetBaseline || structBaselineBytes == null || structBaselineBytes.Length != curBytes.Length)
            structBaselineBytes = curBytes;
    }

    static void DrillIntoStruct(ulong address, string label)
    {
        if (hexOrigin != 0) structHistory.Add((hexOrigin, $"0x{hexOrigin:X}"));
        hexOrigin = address;
        hexAddressInput = $"0x{address:X}";
        structBaselineBytes = null;
        RefreshStructDissect(resetBaseline: true);
        SetStatus($"结构体剖析已进入 {label} (0x{address:X})");
    }

    static void PopStructHistory()
    {
        if (structHistory.Count == 0) return;
        var prev = structHistory[^1];
        structHistory.RemoveAt(structHistory.Count - 1);
        hexOrigin = prev.Addr;
        hexAddressInput = $"0x{prev.Addr:X}";
        structBaselineBytes = null;
        RefreshStructDissect(resetBaseline: true);
    }

    static void SearchHexWindow()
    {
        if (hexViewBytes.Length == 0 || string.IsNullOrWhiteSpace(hexSearchInput)) return;
        byte[] pat, msk;
        if (TryParseAob(hexSearchInput, out pat, out msk)) { }
        else
        {
            pat = Encoding.UTF8.GetBytes(hexSearchInput);
            msk = new byte[pat.Length];
            Array.Fill(msk, (byte)0xFF);
        }
        int start = (hexSelectedOffset + 1) % hexViewBytes.Length;
        for (int step = 0; step < hexViewBytes.Length; step++)
        {
            int idx = (start + step) % hexViewBytes.Length;
            if (idx + pat.Length > hexViewBytes.Length) continue;
            bool ok = true;
            for (int j = 0; j < pat.Length; j++)
            {
                if ((hexViewBytes[idx + j] & msk[j]) != pat[j]) { ok = false; break; }
            }
            if (ok)
            {
                hexSelectedOffset = idx;
                SetStatus($"在当前页偏移 +0x{idx:X} (0x{hexOrigin + (ulong)idx:X}) 找到匹配。");
                return;
            }
        }
        SetStatus("当前浏览页未找到匹配内容。", true);
    }

    static void BeginDisassemblyExport(ulong address, int before, int after)
    {
        if (disassemblyExport != null) return;
        var rows = MemoryDisassembly.ReadInstructionContext(address, before, after, out int hitIndex, out _);
        if (hitIndex < 0)
        {
            SetStatus("无法读取选中的指令。", true);
            return;
        }
        disassemblyExport = SaveDisassemblyExport(rows, hitIndex);
        SetStatus("请在 Windows 窗口中选择导出文件夹…");
    }

    static async Task<string?> SaveDisassemblyExport(DisasmRow[] rows, int hitIndex)
    {
        string? folder = await WindowsDialogs.PickFolder(I18n.Text("选择反汇编导出文件夹")).ConfigureAwait(false);
        return folder == null ? null : MemoryDisassembly.WriteInstructionContext(folder, rows, hitIndex);
    }

    static void FinishDisassemblyExport()
    {
        if (disassemblyExport is not { IsCompleted: true } export) return;
        disassemblyExport = null;
        try
        {
            string? path = export.GetAwaiter().GetResult();
            SetStatus(path == null ? "已取消导出。" : $"已导出反汇编上下文：{path}");
        }
        catch (Exception ex) { SetStatus("导出反汇编失败：" + ex.Message, true); }
    }

    static void RememberDisassemblyOrigin(DisassemblyPage destination)
    {
        var current = new DisassemblyPage(disasmOrigin, disasmRowLimit, highlightIp);
        if (current.Origin == 0 || current == destination) return;
        int limit = navigationHistoryLimit?.Value ?? DefaultNavigationHistoryLimit;
        TrimNavigationHistory(limit);
        if (limit == 0) return;
        if (navHistory.Count == limit) navHistory.RemoveAt(0);
        navHistory.Add(current);
    }

    static void TrimNavigationHistory(int limit)
    {
        int excess = navHistory.Count - limit;
        if (excess > 0) navHistory.RemoveRange(0, excess);
        if (navHistory.Capacity > limit) navHistory.Capacity = limit;
    }

    static void PageDisassembly(bool forward)
    {
        if (disassemblyPages.Count == 0)
        {
            disassemblyPages.Add(new(disasmOrigin, disasmRowLimit, highlightIp));
            disassemblyPageIndex = 0;
        }

        int neighbor = disassemblyPageIndex + (forward ? 1 : -1);
        if (neighbor >= 0 && neighbor < disassemblyPages.Count)
        {
            var known = disassemblyPages[neighbor];
            if (LoadDisassembly(known.Origin, known.RowLimit, known.Highlight))
            {
                disassemblyPageIndex = neighbor;
                inlineEditMode = 0;
            }
            return;
        }

        int rows = DisassemblyPageRows;
        ulong origin = forward ? nextCodeAddress : MemoryDisassembly.FindLeadingContext(disasmOrigin, rows, out rows);
        if (origin == 0 || origin == disasmOrigin)
        {
            SetStatus(forward ? "后面没有可读取的指令。" : "前面没有可读取的指令。");
            return;
        }
        if (!LoadDisassembly(origin, rows, origin)) return;
        inlineEditMode = 0;
        var page = new DisassemblyPage(origin, rows, origin);
        if (forward)
        {
            if (disassemblyPages.Count == DisassemblyPagingLimit) disassemblyPages.RemoveAt(0);
            disassemblyPages.Add(page);
            disassemblyPageIndex = disassemblyPages.Count - 1;
        }
        else
        {
            if (disassemblyPages.Count == DisassemblyPagingLimit) disassemblyPages.RemoveAt(disassemblyPages.Count - 1);
            disassemblyPages.Insert(0, page);
            disassemblyPageIndex = 0;
        }
    }

    static void ReturnToDisassemblyOrigin()
    {
        var previous = navHistory[^1];
        navHistory.RemoveAt(navHistory.Count - 1);
        if (!LoadDisassembly(previous.Origin, previous.RowLimit, previous.Highlight)) return;
        disassemblyPages.Clear();
        inlineEditMode = 0;
    }

    static bool LoadDisassembly(ulong address, int? rowLimit = null, ulong? highlight = null, bool remember = false)
    {
        int limit = rowLimit ?? (address == disasmOrigin ? disasmRowLimit : DisassemblyPageRows);
        var rows = MemoryDisassembly.ReadInstructions(address, limit, out ulong next);
        if (rows.Length == 0)
        {
            if (address == disasmOrigin)
            {
                disasmRows = [];
                nextCodeAddress = 0;
                selectedDisasmIndex = -1;
                disassemblyPages.Clear();
            }
            SetStatus("无法读取该代码地址，请检查地址或页面权限。", true);
            return false;
        }
        var view = new DisassemblyPage(address, limit, highlight ?? highlightIp);
        if (remember) RememberDisassemblyOrigin(view);
        ShowDisassembly(view, rows, next);
        return true;
    }

    static void ShowDisassembly(DisassemblyPage view, DisasmRow[] rows, ulong next)
    {
        disasmRowLimit = view.RowLimit;
        disasmOrigin = view.Origin;
        highlightIp = view.Highlight;
        disasmRows = rows;
        nextCodeAddress = next;
        selectedDisasmIndex = -1;
        for (int i = 0; i < disasmRows.Length; i++)
        {
            if (disasmRows[i].Address == highlightIp) { selectedDisasmIndex = i; break; }
        }
        if (selectedDisasmIndex < 0) selectedDisasmIndex = 0;
        codeAddressInput = disasmRows[selectedDisasmIndex].ModuleOffset;
        asmScroll = default;
        revealDisasmHighlight = selectedDisasmIndex > 0;
        if (statusIsError) SetStatus("");
    }

    static bool OpenInHexBrowser(ulong address)
    {
        if (!LoadHexWindow(address, HexWindowSize / 2))
        {
            SetStatus("无法读取该地址附近的内存。", true);
            return false;
        }
        SetStatus($"内存浏览器已定位到 0x{address:X} · 前方 {hexSelectedOffset} 字节。");
        InterfaceMenu.Navigate("内存编辑", "内存浏览");
        return true;
    }

    static bool LoadHexWindow(ulong address, int before)
    {
        if (!ReadHexWindow(address, before, HexWindowSize, out ulong origin, out var bytes)) return false;
        hexOrigin = origin;
        hexViewBytes = bytes;
        hexSelectedOffset = (int)(address - origin);
        hexAddressInput = $"0x{address:X}";
        hexScroll = default;
        hexRevealSelection = true;
        return true;
    }

    static void RefreshHexWindow()
    {
        ReadHexWindow(hexOrigin, 0, HexWindowSize, out _, out hexViewBytes);
    }

    static void PageHexWindow(bool forward)
    {
        ulong address = forward ? hexOrigin + (ulong)hexViewBytes.Length : hexOrigin - 1;
        if (!LoadHexWindow(address, forward ? 0 : HexWindowSize - 16))
        {
            SetStatus(forward ? "后方内存不可读。" : "前方内存不可读。", true);
            return;
        }
        hexSelectedOffset = 0;
        hexAddressInput = $"0x{hexOrigin:X}";
        SetStatus("");
    }

    static void ResetHeapState()
    {
        MemoryWatchpoint.StopAll();
        CloseRowMenu();
        focusValue = false;
        scan = null;
        scanBytes = null;
        matches = [];
        sampledScanStart = -1;
        unknownSnapshot = null;
        entries.Clear();
        history.Clear();
        cachedHits = [];
        SelectEntry(null);
        selectedMatch = -1;
        selectedHitIndex = -1;
        selectedXmm = 0;
        selectFirstSample = showXmm = false;
        page = 0;
        pageInput = "1";
        hexViewBytes = [];
        hexOrigin = 0;
        showPointerScanner = false;
        pointerScanResults = [];
        structBaselineBytes = null;
        structRows = [];
        structHistory.Clear();
        hexRevealSelection = false;
    }

    internal static void Reset()
    {
        ResetHeapState();
        hexCellStyle = null;
        if (hexFont != null) UnityEngine.Object.Destroy(hexFont);
        hexFont = null;
        MemoryCodePatcher.RestoreAll();
        disasmRows = [];
        decodedHexRows = [];
        navHistory.Clear();
        disassemblyPages.Clear();
        disassemblyPageIndex = 0;
        shownDisassemblySettings = null;
        showValuePresets = false;
        showContextPopup = false;
        contextPopupTitle = contextPopupText = "";
        valuePresets = [];
        patchPresets = [];
        patchPresetsLoaded = false;
        presetScroll = pointerScanScroll = contextPopupScroll = structScroll = default;
        disasmOrigin = nextCodeAddress = highlightIp = 0;
        disasmRowLimit = DisassemblyPageRows;
        revealDisasmHighlight = false;
        inlineEditMode = 0;
        inlineCodeInput = "";
        inlinePreviewSource = null;
        inlinePreview = "";
        codeAddressInput = hexDecodeInput = hexAddressInput = addressInput = editValueText = editDescText = status = lastPage = "";
        context = null;
        nextRefresh = visibleUntil = nextWatchRefresh = nextThreadScan = 0;
        panelScroll = resultScroll = entryScroll = hitScroll = logScroll = asmScroll = patchScroll = hexScroll = default;
    }
}
