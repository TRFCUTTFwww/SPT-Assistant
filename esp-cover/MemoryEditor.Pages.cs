using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using static LocalSpt5Esp.MemoryEditorAccess;

namespace LocalSpt5Esp;

internal static partial class MemoryEditor
{
    static Color CardBg => Color.Lerp(InterfaceMenu.Background, InterfaceMenu.Text, .04f);
    static Color RowAltBg => Color.Lerp(InterfaceMenu.Background, InterfaceMenu.Text, .03f);
    static Color RowSelBg => new(InterfaceMenu.Accent.r, InterfaceMenu.Accent.g, InterfaceMenu.Accent.b, .18f);
    static Color PatchedBg => new(.94f, .72f, .28f, .16f);
    static Color MutedText => Color.Lerp(InterfaceMenu.Text, InterfaceMenu.Background, .42f);
    static Color WarnColor => new(.98f, .48f, .48f);
    static Color PatchAccent => new(.96f, .76f, .34f);
    static GUIStyle? hexCellStyle;
    static Font? hexFont;

    static bool RightClicked(Rect row, bool insideList)
    {
        var ev = Event.current;
        if (!insideList || ev.type != EventType.MouseDown || ev.button != 1 || !row.Contains(ev.mousePosition))
            return false;
        menuAnchor = GUIUtility.GUIToScreenPoint(ev.mousePosition);
        ev.Use();
        return true;
    }

    static bool LeftClicked(Rect row, bool insideList)
    {
        var ev = Event.current;
        if (!insideList || ev.type != EventType.MouseDown || ev.button != 0 || !row.Contains(ev.mousePosition))
            return false;
        ev.Use();
        return true;
    }

    static bool ConsumeListDelete()
    {
        var ev = Event.current;
        if (!GUI.enabled || GUIUtility.keyboardControl != 0 || ev.type != EventType.KeyDown || ev.keyCode != KeyCode.Delete)
            return false;
        ev.Use();
        return true;
    }

    static (string, Action) CopyItem(string label, string value) =>
        (label, () => { GUIUtility.systemCopyBuffer = value; SetStatus("已复制：" + value); });

    static Rect MenuBounds(Rect content)
    {
        var point = GUIUtility.ScreenToGUIPoint(menuAnchor);
        float w = Math.Min(230, content.width), h = menuItems.Length * RowHeight + 8;
        return new(Math.Clamp(point.x, content.x, Math.Max(content.x, content.xMax - w)),
            Math.Clamp(point.y, content.y, Math.Max(content.y, content.yMax - h)), w, h);
    }

    static void HandleMenu(Rect content)
    {
        if (menuItems.Length == 0) return;
        var ev = Event.current;
        if (ev.type == EventType.MouseDown)
        {
            var rect = MenuBounds(content);
            var items = menuItems;
            CloseRowMenu();
            if (ev.button == 0 && rect.Contains(ev.mousePosition))
            {
                int index = (int)((ev.mousePosition.y - rect.y - 4) / RowHeight);
                ev.Use();
                if (index >= 0 && index < items.Length) items[index].Run();
            }
        }
        else if (ev.type is EventType.ScrollWheel or EventType.KeyDown)
        {
            CloseRowMenu();
            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape) ev.Use();
        }
        else if (ev.type == EventType.ContextClick || ev.type == EventType.MouseUp && ev.button == 1) ev.Use();
    }

    static void DrawMenu(Rect content)
    {
        if (menuItems.Length == 0 || Event.current.type != EventType.Repaint) return;
        var rect = MenuBounds(content);
        InterfaceMenu.Surface(new(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2),
            Color.Lerp(InterfaceMenu.Background, InterfaceMenu.Text, .24f), 8);
        InterfaceMenu.Surface(rect, Color.Lerp(InterfaceMenu.Background, InterfaceMenu.Text, .10f), 7);
        for (int i = 0; i < menuItems.Length; i++)
        {
            Rect row = new(rect.x + 4, rect.y + 4 + i * RowHeight, rect.width - 8, RowHeight);
            if (row.Contains(Event.current.mousePosition)) InterfaceMenu.Surface(row, RowSelBg, 5);
            InterfaceMenu.Label(new(row.x + 8, row.y, row.width - 16, row.height), menuItems[i].Label, size: 13);
        }
    }

    static void DrawCard(Rect rect, string? header = null)
    {
        InterfaceMenu.Surface(rect, CardBg, 9);
        if (!string.IsNullOrEmpty(header))
            InterfaceMenu.Label(new(rect.x + 12, rect.y + 6, rect.width - 24, 24), header, InterfaceMenu.Accent, 13);
    }

    static void DrawStatusBar(Rect rect)
    {
        InterfaceMenu.Surface(rect, CardBg, 7);
        string msg = string.IsNullOrEmpty(status)
            ? $"地址表 {entries.Count} 项 · 硬件监控 {ActiveWatchCount()}/4 · 代码补丁 {MemoryCodePatcher.GetPatches().Length} 项"
            : status;
        InterfaceMenu.Label(new(rect.x + 12, rect.y, rect.width - 24, rect.height), msg,
            statusIsError ? WarnColor : string.IsNullOrEmpty(status) ? MutedText : InterfaceMenu.Accent, 12,
            translate: !string.IsNullOrEmpty(status) && !status.Contains("0x"));
    }

    static int ActiveWatchCount()
    {
        int count = 0;
        foreach (var s in MemoryWatchpoint.GetSlots()) if (s.Active) count++;
        return count;
    }

    internal static void Draw(Rect rect, string view)
    {
        CheckContext();
        visibleUntil = Time.unscaledTime + 0.3f;
        if (lastPage != view)
        {
            panelScroll = default;
            lastPage = view;
            CloseRowMenu();
            if (view == "访问监控") RefreshWatchHits();
            if (view == "反汇编" && disasmOrigin != 0) LoadDisassembly(disasmOrigin);
            if (view == "内存浏览" && hexOrigin != 0) RefreshHexWindow();
        }

        HandleMenu(rect);
        if (showContextPopup)
        {
            DrawContextPopup(rect);
            return;
        }
        bool hexPage = view == "内存浏览";
        float width = Math.Max(680, rect.width - (hexPage ? 20 : 12));
        float minHeight = hexPage && !structDissectMode && width < 864 ? 750 : 460;
        float height = Math.Max(minHeight, rect.height - 8);
        bool needScroll = rect.width < (hexPage ? 700 : 692) || rect.height < minHeight + 8;
        if (needScroll)
            panelScroll = GUI.BeginScrollView(rect, panelScroll, new Rect(0, 0, width, height));
        else
            GUI.BeginGroup(new Rect(rect.x, rect.y, width, height));
        try
        {
            float bodyH = height - 36;
            switch (view)
            {
                case "内存扫描": DrawScanPage(width, bodyH); break;
                case "地址表": DrawEntriesPage(width, bodyH); break;
                case "访问监控": DrawWatchPage(width, bodyH); break;
                case "反汇编": DrawDisassemblyPage(width, bodyH); break;
                case "代码补丁": DrawPatchesPage(width, bodyH); break;
                case "内存浏览": DrawHexBrowserPage(width, bodyH); break;
                default: DrawScanPage(width, bodyH); break;
            }
            DrawStatusBar(new(0, height - 30, width, 28));
        }
        finally
        {
            if (needScroll) GUI.EndScrollView();
            else GUI.EndGroup();
        }
        DrawMenu(rect);
    }

    static void DrawContextPopup(Rect rect)
    {
        var ev = Event.current;
        if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape)
        {
            ev.Use();
            showContextPopup = false;
            return;
        }

        float w = Math.Max(620, rect.width - 24);
        float h = Math.Max(400, rect.height - 20);
        Rect box = new(rect.x + (rect.width - w) * 0.5f, rect.y + (rect.height - h) * 0.5f, w, h);
        InterfaceMenu.Surface(new(box.x - 2, box.y - 2, box.width + 4, box.height + 4),
            Color.Lerp(InterfaceMenu.Background, InterfaceMenu.Text, .26f), 10);
        InterfaceMenu.Surface(box, InterfaceMenu.Background, 9);

        GUI.BeginGroup(box);
        try
        {
            DrawCard(new(0, 0, w, 46));
            InterfaceMenu.Label(new(12, 10, w - 310, 26), contextPopupTitle, InterfaceMenu.Accent, 13, translate: false);
            if (InterfaceMenu.Button(new(w - 292, 9, 90, 28), "复制全部", true))
            {
                GUIUtility.systemCopyBuffer = contextPopupText;
                SetStatus("已复制反汇编上下文文本。");
            }
            bool canSave = selectedDisasmIndex >= 0 && selectedDisasmIndex < disasmRows.Length && disassemblyExport == null;
            bool prev = GUI.enabled;
            GUI.enabled = prev && canSave;
            if (InterfaceMenu.Button(new(w - 196, 9, 94, 28), "导出文件") && canSave &&
                TryReadContextCounts(out int exportBefore, out int exportAfter))
                BeginDisassemblyExport(disasmRows[selectedDisasmIndex].Address, exportBefore, exportAfter);
            GUI.enabled = prev;
            if (InterfaceMenu.Button(new(w - 96, 9, 86, 28), "关闭 (Esc)"))
                showContextPopup = false;

            Rect body = new(0, 52, w, h - 52);
            DrawCard(body);
            float contentH = Math.Max(body.height - 20, (contextPopupText.Split('\n').Length + 2) * 18f);
            float contentW = Math.Max(body.width - 34, 920f);
            Rect view = new(8, 60, w - 16, body.height - 16);
            contextPopupScroll = GUI.BeginScrollView(view, contextPopupScroll, new Rect(0, 0, contentW, contentH));
            try
            {
                GUI.TextArea(new Rect(4, 4, contentW - 8, contentH - 8), contextPopupText);
            }
            finally { GUI.EndScrollView(); }
        }
        finally { GUI.EndGroup(); }
    }

    // -------------------------------------------------------------------------
    // Page 1: Memory Scan (Split Two-Column CE Workspace)
    // -------------------------------------------------------------------------
    static void DrawScanPage(float width, float height)
    {
        if (scan == null && selectedMatch >= page * PageSize &&
            selectedMatch < Math.Min(matches.Count, (page + 1) * PageSize) && ConsumeListDelete())
            RemoveScanResult(selectedMatch);

        float gap = 12;
        float leftW = MathF.Floor((width - gap) * 0.56f);
        float rightX = leftW + gap;
        float rightW = width - rightX;

        // Left Card: Scan Results
        Rect leftCard = new(0, 0, leftW, height);
        DrawCard(leftCard);

        float ly = 10;
        if (scan is {} job)
        {
            string progressText = job.OutputSnapshot != null
                ? $"建立初始快照 · {job.SnapshotBytes / (1024.0 * 1024.0):F1} MB"
                : $"{I18n.Text("扫描中")} · {job.Results.Count:N0}";
            InterfaceMenu.Label(new(12, ly, leftW - 110, 26), progressText, InterfaceMenu.Accent, 13, translate: false);
            if (InterfaceMenu.Button(new(leftW - 92, ly, 80, 26), "取消扫描")) CancelScan();
        }
        else if (unknownSnapshot != null)
        {
            InterfaceMenu.Label(new(12, ly, leftW - 104, 26),
                $"初始快照就绪 · {unknownSnapshot.Count} 段（请继续筛选）", InterfaceMenu.Accent, 13, translate: false);
            if (InterfaceMenu.Button(new(leftW - 92, ly, 80, 26), "清空")) ResetScan();
        }
        else
        {
            int pages = Math.Max(1, (matches.Count + PageSize - 1) / PageSize);
            if (page >= pages) SetPage(pages - 1, pages);
            InterfaceMenu.Label(new(12, ly, leftW - 266, 26),
                $"{I18n.Text("扫描结果")} · {matches.Count:N0}", InterfaceMenu.Accent, 13, translate: false);
            if (InterfaceMenu.Button(new(leftW - 252, ly, 28, 26), "‹") && page > 0) SetPage(page - 1, pages);
            GUI.SetNextControlName("MemoryEditorPage");
            pageInput = InterfaceMenu.Field(new(leftW - 220, ly, 50, 26), pageInput);
            if (Event.current.type == EventType.KeyDown &&
                Event.current.keyCode is KeyCode.Return or KeyCode.KeypadEnter &&
                GUI.GetNameOfFocusedControl() == "MemoryEditorPage")
            {
                Event.current.Use();
                JumpToPage(pages);
            }
            InterfaceMenu.Label(new(leftW - 166, ly, 50, 26), $"/{pages}", MutedText, 12, translate: false);
            if (InterfaceMenu.Button(new(leftW - 114, ly, 42, 26), "跳转")) JumpToPage(pages);
            if (InterfaceMenu.Button(new(leftW - 68, ly, 28, 26), "›") && page + 1 < pages) SetPage(page + 1, pages);
            if (InterfaceMenu.Button(new(leftW - 36, ly, 26, 26), "×")) ResetScan();
        }

        ly += 34;
        InterfaceMenu.Surface(new(8, ly, leftW - 16, 24), RowAltBg, 4);
        float colValX = 148;
        float colPrevX = Math.Max(260, leftW - 178);
        InterfaceMenu.Label(new(14, ly, 130, 24), "地址", MutedText, 12);
        InterfaceMenu.Label(new(colValX, ly, colPrevX - colValX - 6, 24), "当前值", MutedText, 12);
        InterfaceMenu.Label(new(colPrevX, ly, 92, 24), "上次值", MutedText, 12);
        InterfaceMenu.Label(new(leftW - 78, ly, 64, 24), "操作", MutedText, 12);

        ly += 28;
        int start = page * PageSize, count = Math.Min(PageSize, Math.Max(0, matches.Count - start));
        SampleScanPage(start, count);
        Rect listView = new(8, ly, leftW - 16, Math.Max(60, height - ly - 10));
        bool insideList = listView.Contains(Event.current.mousePosition);
        resultScroll = GUI.BeginScrollView(listView, resultScroll, new Rect(0, 0, listView.width - 18, count * RowHeight));
        try
        {
            for (int i = 0; i < count; i++)
            {
                int idx = start + i;
                var row = matches[idx];
                var sample = liveScanValues[i];
                ulong? current = sample.Row == row ? sample.Value : row.Value;
                float y = i * RowHeight;
                Rect rowRect = new(0, y, listView.width - 18, RowHeight - 1);
                if (idx == selectedMatch) InterfaceMenu.Surface(rowRect, RowSelBg, 4);
                else if (i % 2 == 1) InterfaceMenu.Surface(rowRect, RowAltBg, 4);

                if (RightClicked(rowRect, insideList))
                {
                    GUIUtility.keyboardControl = 0;
                    selectedMatch = idx;
                    var st = resultType;
                    bool isCode = Query(row.Address, out var rInfo) && rInfo.ReadableCode;
                    menuItems =
                    [
                        ("添加到地址表", () => AddAddress(row.Address, st, row.Allocation)),
                        .. (isCode ? new (string, Action)[] { ("在反汇编中查看", () => OpenInDisassembler(row.Address, row.Address)) } : []),
                        ("反查指针 (Pointer Scan)", () => BeginPointerScan(row.Address)),
                        ("找出是什么改写了这个地址", () => BeginWatch(row.Address, Size(st), WatchMode.Write, $"0x{row.Address:X}")),
                        ("找出是什么访问了这个地址", () => BeginWatch(row.Address, Size(st), WatchMode.Access, $"0x{row.Address:X}")),
                        ("在内存浏览器中查看", () => OpenInHexBrowser(row.Address)),
                        CopyItem("复制地址", $"0x{row.Address:X}"),
                        .. (current is ulong value ? new[] { CopyItem("复制数值", Format(value, st, hexDisplay, unsignedDisplay)) } : []),
                        ("移除结果 (Del)", () => { if (idx < matches.Count && matches[idx] == row) RemoveScanResult(idx); })
                    ];
                }
                else if (LeftClicked(new(0, y, rowRect.width - 66, RowHeight), insideList))
                {
                    GUIUtility.keyboardControl = 0;
                    selectedMatch = idx;
                }

                InterfaceMenu.Label(new(6, y, 134, RowHeight), $"0x{row.Address:X}", size: 12, translate: false);
                InterfaceMenu.Label(new(colValX - 8, y, colPrevX - colValX - 4, RowHeight),
                    current is ulong sampled ? Format(sampled, resultType, hexDisplay) : "—",
                    current.HasValue ? InterfaceMenu.Text : MutedText, 12, translate: false);
                InterfaceMenu.Label(new(colPrevX - 8, y, 92, RowHeight),
                    Format(row.Previous, resultType, hexDisplay), MutedText, 12, translate: false);
                if (InterfaceMenu.Button(new(rowRect.width - 62, y + 1, 58, RowHeight - 3), "+ 添加"))
                    AddAddress(row.Address, resultType, row.Allocation);
            }
        }
        finally { GUI.EndScrollView(); }

        // Right Card: Scan Controls
        Rect rightCard = new(rightX, 0, rightW, height);
        DrawCard(rightCard, "扫描设置");

        float rx = rightX + 12, rw = rightW - 24, ry = 36;
        bool prevEnabled = GUI.enabled;
        GUI.enabled = prevEnabled && scan == null;
        try
        {
            InterfaceMenu.Label(new(rx, ry, rw, 20), "数据类型", MutedText, 12);
            ry += 22;
            int cols = 4;
            float cw = (rw - 6 * (cols - 1)) / cols;
            for (int i = 0; i < DataKinds.Length; i++)
            {
                Rect r = new(rx + (i % cols) * (cw + 6), ry + (i / cols) * 30, cw, 26);
                if (InterfaceMenu.Button(r, KindLabel(DataKinds[i]), dataKind == DataKinds[i]))
                {
                    dataKind = DataKinds[i];
                    if (IsNumericKind(dataKind))
                    {
                        entryType = ToValueType(dataKind);
                        if (!HasPriorScan)
                            scanMode = dataKind is ScanDataKind.Float or ScanDataKind.Double ? MemoryScanMode.Rounded : MemoryScanMode.Exact;
                    }
                }
            }
            ry += ((DataKinds.Length + cols - 1) / cols) * 30 + 8;

            if (IsNumericKind(dataKind))
            {
                InterfaceMenu.Label(new(rx, ry, rw, 20), HasPriorScan ? "继续筛选条件" : "首次扫描条件", MutedText, 12);
                ry += 22;
                var modes = HasPriorScan ? NextScanModes : FirstScanModes;
                int mCols = 3;
                float mw = (rw - 6 * (mCols - 1)) / mCols;
                for (int i = 0; i < modes.Length; i++)
                {
                    var m = modes[i];
                    bool modeAllowed = m != MemoryScanMode.Rounded || dataKind is ScanDataKind.Float or ScanDataKind.Double;
                    bool saved = GUI.enabled;
                    GUI.enabled = saved && modeAllowed;
                    Rect r = new(rx + (i % mCols) * (mw + 6), ry + (i / mCols) * 30, mw, 26);
                    if (InterfaceMenu.Button(r, ModeLabel(m), scanMode == m)) scanMode = m;
                    GUI.enabled = saved;
                }
                ry += ((modes.Length + mCols - 1) / mCols) * 30 + 10;

                bool needsInput = scanMode is MemoryScanMode.Exact or MemoryScanMode.Rounded
                    or MemoryScanMode.GreaterThan or MemoryScanMode.LessThan or MemoryScanMode.Between;
                if (needsInput)
                {
                    if (scanMode == MemoryScanMode.Between)
                    {
                        float half = (rw - 24) / 2;
                        InterfaceMenu.Label(new(rx, ry, half, 20), "最小值", MutedText, 12);
                        InterfaceMenu.Label(new(rx + half + 24, ry, half, 20), "最大值", MutedText, 12);
                        ry += 22;
                        query = InterfaceMenu.Field(new(rx, ry, half, 30), query);
                        InterfaceMenu.Label(new(rx + half + 6, ry, 16, 30), "~", MutedText, 13, translate: false);
                        queryMax = InterfaceMenu.Field(new(rx + half + 24, ry, half, 30), queryMax);
                        ry += 38;
                    }
                    else
                    {
                        string hint = scanMode == MemoryScanMode.Rounded ? "浮点数值（自动 ±0.5 容差，适合能量/血量）" : "目标数值（整数支持 0x 十六进制）";
                        InterfaceMenu.Label(new(rx, ry, rw, 20), hint, MutedText, 12);
                        ry += 22;
                        query = InterfaceMenu.Field(new(rx, ry, rw, 30), query);
                        ry += 38;
                    }
                }
                else
                {
                    InterfaceMenu.Label(new(rx, ry, rw, 26), "当前条件无需输入数值，直接点击扫描。", MutedText, 12);
                    ry += 32;
                }
            }
            else if (dataKind == ScanDataKind.Aob)
            {
                InterfaceMenu.Label(new(rx, ry, rw, 20), "字节数组（支持 ?? / ? 通配符）", MutedText, 12);
                ry += 22;
                query = InterfaceMenu.Field(new(rx, ry, rw, 30), query);
                ry += 38;
            }
            else
            {
                InterfaceMenu.Label(new(rx, ry, rw, 20), "目标字符串", MutedText, 12);
                ry += 22;
                query = InterfaceMenu.Field(new(rx, ry, rw, 30), query);
                ry += 36;
                float half = (rw - 8) / 2;
                if (InterfaceMenu.Button(new(rx, ry, half, 26), "UTF-8", !utf16String)) utf16String = false;
                if (InterfaceMenu.Button(new(rx + half + 8, ry, half, 26), "UTF-16 (Unicode)", utf16String)) utf16String = true;
                ry += 34;
            }

            // Primary action buttons
            float btnW = (rw - 12) / 3;
            if (InterfaceMenu.Button(new(rx, ry, btnW, 34), "首次扫描", !HasPriorScan)) StartScan(false);
            GUI.enabled = prevEnabled && scan == null && HasPriorScan && IsNumericKind(dataKind);
            if (InterfaceMenu.Button(new(rx + btnW + 6, ry, btnW, 34), "继续筛选", HasPriorScan)) StartScan(true);
            GUI.enabled = prevEnabled && scan == null && HasPriorScan;
            if (InterfaceMenu.Button(new(rx + (btnW + 6) * 2, ry, btnW, 34), "新扫描")) ResetScan();
            ry += 44;

            // Quick utility row
            GUI.enabled = prevEnabled;
            float uThird = (rw - 12) / 3;
            if (InterfaceMenu.Button(new(rx, ry, uThird, 26), hexDisplay ? "十六进制" : "十进制", hexDisplay))
                hexDisplay = !hexDisplay;
            if (InterfaceMenu.Button(new(rx + uThird + 6, ry, uThird, 26), "无符号", unsignedDisplay))
                unsignedDisplay = !unsignedDisplay;
            if (InterfaceMenu.Button(new(rx + (uThird + 6) * 2, ry, uThird, 26), $"地址表 ({entries.Count})"))
                InterfaceMenu.Navigate("内存编辑", "地址表");
            ry += 34;

            // Scan Scope + Alignment + Range
            GUI.enabled = prevEnabled && scan == null;
            InterfaceMenu.Label(new(rx, ry, rw, 18), "扫描范围与对齐", MutedText, 11);
            ry += 20;
            float sw = (rw - 12) / 3;
            if (InterfaceMenu.Button(new(rx, ry, sw, 24), "堆(Private)", scanScope == ScanScope.EditablePrivate)) scanScope = ScanScope.EditablePrivate;
            if (InterfaceMenu.Button(new(rx + sw + 6, ry, sw, 24), "全部可写", scanScope == ScanScope.AllWritable)) scanScope = ScanScope.AllWritable;
            if (InterfaceMenu.Button(new(rx + (sw + 6) * 2, ry, sw, 24), "全部可读", scanScope == ScanScope.AllReadable)) scanScope = ScanScope.AllReadable;
            ry += 28;
            if (InterfaceMenu.Button(new(rx, ry, (rw - 6) * 0.45f, 24), unalignedScan ? "非对齐 (1B)" : "类型对齐", unalignedScan))
                unalignedScan = !unalignedScan;
            float rInputW = ((rw - 6) * 0.55f - 14) / 2;
            float rStartX = rx + (rw - 6) * 0.45f + 6;
            scanRangeStartInput = InterfaceMenu.Field(new(rStartX, ry, rInputW, 24), scanRangeStartInput);
            InterfaceMenu.Label(new(rStartX + rInputW + 2, ry, 10, 24), "~", MutedText, 11, translate: false);
            scanRangeEndInput = InterfaceMenu.Field(new(rStartX + rInputW + 14, ry, rInputW, 24), scanRangeEndInput);
            ry += 26;
            InterfaceMenu.Label(new(rx, ry, rw, 18), "区间可填起止地址，或只填模块名（如 GameAssembly.dll）", MutedText, 10);
        }
        finally { GUI.enabled = prevEnabled; }
    }

    // -------------------------------------------------------------------------
    // Page 2: Address Table (Pointer Chains + Inline Controls + Bottom Inspector)
    // -------------------------------------------------------------------------
    static void DrawEntriesPage(float width, float height)
    {
        if (showValuePresets)
        {
            DrawValuePresets(width, height);
            return;
        }
        if (showPointerScanner)
        {
            DrawPointerScanner(width, height);
            return;
        }

        if (selectedEntry is {} selected && ConsumeListDelete())
            RemoveAddressEntry(selected);

        // Top Card: Add Address / Pointer
        Rect topCard = new(0, 0, width, 74);
        DrawCard(topCard);

        float pad = 10;
        float typeW = 46;
        for (int i = 0; i < ValueTypes.Length; i++)
        {
            if (InterfaceMenu.Button(new(pad + i * (typeW + 3), 8, typeW, 24), ValueTypes[i].ToString(), entryType == ValueTypes[i]))
                entryType = ValueTypes[i];
        }
        float rightCtrlX = pad + ValueTypes.Length * (typeW + 3) + 6;
        if (width - rightCtrlX >= 316)
        {
            if (InterfaceMenu.Button(new(width - 318, 8, 44, 24), hexDisplay ? "Hex" : "Dec", hexDisplay))
                hexDisplay = !hexDisplay;
            if (InterfaceMenu.Button(new(width - 270, 8, 54, 24), "无符号", unsignedDisplay))
                unsignedDisplay = !unsignedDisplay;
            if (InterfaceMenu.Button(new(width - 212, 8, 58, 24), "保存表")) SaveAddressTable();
            if (InterfaceMenu.Button(new(width - 150, 8, 58, 24), "载入表")) LoadAddressTable();
            bool anyLocked = entries.Exists(e => e.Locked.HasValue);
            bool prev = GUI.enabled;
            GUI.enabled = prev && anyLocked;
            if (InterfaceMenu.Button(new(width - 88, 8, 78, 24), "全部解锁"))
                foreach (var e in entries) e.Locked = null;
            GUI.enabled = prev;
        }

        InterfaceMenu.Label(new(pad, 40, 92, 26), "地址 / 指针", MutedText, 12);
        addressInput = InterfaceMenu.Field(new(pad + 94, 40, width - pad * 2 - 304, 26), addressInput);
        if (InterfaceMenu.Button(new(width - pad - 202, 40, 98, 26), "+ 添加地址")) AddManualAddress();
        if (InterfaceMenu.Button(new(width - pad - 98, 40, 98, 26), "调试预设")) OpenValuePresets();

        // Middle Card: Entries Table
        float midY = 82;
        float bottomH = 106;
        float midH = Math.Max(140, height - midY - bottomH - 8);
        Rect midCard = new(0, midY, width, midH);
        DrawCard(midCard);

        float colLock = 10, colDesc = 46, colAddr = 196, colType = Math.Max(390, width - 350);
        float colVal = colType + 66, colAct = width - 146;
        InterfaceMenu.Surface(new(8, midY + 8, width - 16, 24), RowAltBg, 4);
        InterfaceMenu.Label(new(colLock, midY + 8, 32, 24), "锁", MutedText, 12);
        InterfaceMenu.Label(new(colDesc, midY + 8, 144, 24), "描述", MutedText, 12);
        InterfaceMenu.Label(new(colAddr, midY + 8, colType - colAddr - 6, 24), "地址 / 指针", MutedText, 12);
        InterfaceMenu.Label(new(colType, midY + 8, 60, 24), "类型", MutedText, 12);
        InterfaceMenu.Label(new(colVal, midY + 8, colAct - colVal - 6, 24), "当前值", MutedText, 12);
        InterfaceMenu.Label(new(colAct, midY + 8, 130, 24), "快捷操作", MutedText, 12);

        Rect listView = new(8, midY + 36, width - 16, Math.Max(40, midH - 44));
        bool insideList = listView.Contains(Event.current.mousePosition);
        entryScroll = GUI.BeginScrollView(listView, entryScroll, new Rect(0, 0, listView.width - 18, entries.Count * RowHeight));
        try
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var row = entries[i];
                float y = i * RowHeight;
                Rect rowRect = new(0, y, listView.width - 18, RowHeight - 1);
                if (row == selectedEntry) InterfaceMenu.Surface(rowRect, RowSelBg, 4);
                else if (i % 2 == 1) InterfaceMenu.Surface(rowRect, RowAltBg, 4);

                if (RightClicked(rowRect, insideList))
                {
                    GUIUtility.keyboardControl = 0;
                    SelectEntry(row);
                    var items = new List<(string, Action)>();
                    if (row.Valid)
                    {
                        items.Add(("找出是什么改写了这个地址", () => BeginWatch(row, WatchMode.Write)));
                        items.Add(("找出是什么访问了这个地址", () => BeginWatch(row, WatchMode.Access)));
                        items.Add(("反查指针 (Pointer Scan)", () => BeginPointerScan(row.Address)));
                        items.Add(("在内存浏览器中查看", () => OpenInHexBrowser(row.Address)));
                        items.Add(CopyItem("复制数值", Format(row.Current, row.Type, hexDisplay, unsignedDisplay)));
                    }
                    if (row.Writable)
                    {
                        items.Add(("编辑数值", () => { SelectEntry(row); focusValue = true; }));
                        items.Add((row.Locked == null ? "锁定数值" : "解除锁定",
                            () => { if (row.Locked == null) SetEntryValue(row, true); else row.Locked = null; }));
                    }
                    items.Add(CopyItem("复制地址", $"0x{row.Address:X}"));
                    items.Add(("删除记录", () => RemoveAddressEntry(row)));
                    menuItems = items.ToArray();
                }

                if (row.Writable || row.Locked != null)
                {
                    if (InterfaceMenu.Button(new(2, y + 2, 26, RowHeight - 4), row.Locked != null ? "■" : "□", row.Locked != null))
                    {
                        SelectEntry(row);
                        if (row.Locked == null) SetEntryValue(row, true);
                        else row.Locked = null;
                    }
                }
                else if (row.Valid)
                    InterfaceMenu.Label(new(2, y, 32, RowHeight), "只读", MutedText, 10);

                if (LeftClicked(new(32, y, colAct - 40, RowHeight), insideList))
                {
                    GUIUtility.keyboardControl = 0;
                    SelectEntry(row);
                }

                string desc = (row.Hotkey != KeyCode.None ? $"[{row.Hotkey}] " : "") +
                    (string.IsNullOrEmpty(row.Description) ? "-" : row.Description);
                string addrLabel = row.IsPointer
                    ? $"P→ 0x{row.Address:X}"
                    : $"0x{row.Address:X}";
                InterfaceMenu.Label(new(colDesc - 8, y, 146, RowHeight), desc, size: 12, translate: false);
                InterfaceMenu.Label(new(colAddr - 8, y, colType - colAddr - 6, RowHeight), addrLabel,
                    row.IsPointer ? InterfaceMenu.Accent : InterfaceMenu.Text, 12, translate: false);
                InterfaceMenu.Label(new(colType - 8, y, 60, RowHeight), row.Type.ToString(), MutedText, 12, translate: false);
                InterfaceMenu.Label(new(colVal - 8, y, colAct - colVal - 6, RowHeight),
                    row.Valid ? Format(row.Current, row.Type, hexDisplay, unsignedDisplay) : I18n.Text("已失效"),
                    row.Valid ? InterfaceMenu.Text : WarnColor, 12, translate: false);

                if (row.Valid)
                {
                    if (InterfaceMenu.Button(new(colAct - 10, y + 1, 56, RowHeight - 3), "查改写"))
                        BeginWatch(row, WatchMode.Write);
                    if (InterfaceMenu.Button(new(colAct + 50, y + 1, 56, RowHeight - 3), "浏览"))
                        OpenInHexBrowser(row.Address);
                }
            }
        }
        finally { GUI.EndScrollView(); }

        // Bottom Inspector Card
        float botY = midY + midH + 8;
        Rect botCard = new(0, botY, width, bottomH);
        DrawCard(botCard);

        bool enabled = GUI.enabled;
        GUI.enabled = enabled && selectedEntry != null;
        try
        {
            float r1 = botY + 10, r2 = botY + 46, r3 = botY + 76;
            InterfaceMenu.Label(new(12, r1, 40, 28), "描述", MutedText, 12);
            string nextDesc = InterfaceMenu.Field(new(54, r1, 150, 28), editDescText);
            if (nextDesc != editDescText && selectedEntry != null)
            {
                editDescText = nextDesc;
                selectedEntry.Description = nextDesc;
            }

            GUI.enabled = enabled && selectedEntry is { Valid: true, Writable: true };
            InterfaceMenu.Label(new(214, r1, 40, 28), "数值", MutedText, 12);
            const string valueControl = "MemoryEditorValue";
            if (!valueDraftEdited && selectedEntry is { Valid: true } inspected && GUI.GetNameOfFocusedControl() != valueControl)
                editValueText = Format(inspected.Current, inspected.Type, hexDisplay, unsignedDisplay);
            GUI.SetNextControlName(valueControl);
            string nextValue = InterfaceMenu.Field(new(256, r1, Math.Max(100, width - 566), 28), editValueText);
            if (nextValue != editValueText)
            {
                editValueText = nextValue;
                valueDraftEdited = true;
            }
            if (focusValue) { GUI.FocusControl(valueControl); focusValue = false; }

            if (InterfaceMenu.Button(new(width - 300, r1, 66, 28), "写入") && selectedEntry is {} wEntry) SetEntryValue(wEntry, false);
            if (InterfaceMenu.Button(new(width - 228, r1, 68, 28), selectedEntry?.Locked == null ? "锁定" : "解锁", selectedEntry?.Locked != null) && selectedEntry is {} lEntry)
            {
                if (lEntry.Locked == null) SetEntryValue(lEntry, true);
                else lEntry.Locked = null;
            }
            GUI.enabled = enabled && selectedEntry is { Valid: true, Writable: true, Undo: not null };
            if (InterfaceMenu.Button(new(width - 154, r1, 70, 28), "撤销写入") && selectedEntry is {} uEntry) SetEntryValue(uEntry, false, true);
            GUI.enabled = enabled && selectedEntry != null;
            if (InterfaceMenu.Button(new(width - 78, r1, 66, 28), "删除 (Del)") && selectedEntry is {} dEntry)
                RemoveAddressEntry(dEntry);

            // Row 2: CE Action Pills
            GUI.enabled = enabled && selectedEntry is { Valid: true };
            float bw = Math.Min(120, (width - 140) / 5);
            if (InterfaceMenu.Button(new(12, r2, bw, 26), "查改写 (DR)") && selectedEntry is {} we)
                BeginWatch(we, WatchMode.Write);
            if (InterfaceMenu.Button(new(12 + (bw + 6), r2, bw, 26), "查访问 (DR)") && selectedEntry is {} ae)
                BeginWatch(ae, WatchMode.Access);
            if (InterfaceMenu.Button(new(12 + (bw + 6) * 2, r2, bw, 26), "内存浏览") && selectedEntry is {} he)
                OpenInHexBrowser(he.Address);
            if (InterfaceMenu.Button(new(12 + (bw + 6) * 3, r2, bw, 26), "反查指针") && selectedEntry is {} pe)
                BeginPointerScan(pe.Address);
            GUI.enabled = enabled && selectedEntry != null;
            string hkLabel = selectedEntry is { Hotkey: not KeyCode.None } hke ? $"热键:{hke.Hotkey}" : "热键:无";
            if (InterfaceMenu.Button(new(12 + (bw + 6) * 4, r2, bw, 26), hkLabel) && selectedEntry is {} ke)
            {
                int nextIdx = (Array.IndexOf(HotkeyChoices, ke.Hotkey) + 1) % HotkeyChoices.Length;
                ke.Hotkey = HotkeyChoices[nextIdx];
            }
            GUI.enabled = enabled && entries.Exists(e => !e.Valid);
            if (InterfaceMenu.Button(new(width - 108, r2, 96, 26), "清除失效"))
            {
                entries.RemoveAll(e => !e.Valid);
                if (selectedEntry is { Valid: false }) SelectEntry(null);
            }

            GUI.enabled = enabled && selectedEntry != null;
            if (InterfaceMenu.Button(new(12, r3, 64, 24), selectedEntry?.Type.ToString() ?? "Type") && selectedEntry is {} te)
            {
                var nextT = ValueTypes[(Array.IndexOf(ValueTypes, te.Type) + 1) % ValueTypes.Length];
                UpdateSelectedEntryTarget(te, nextT, editAddrText);
            }
            editAddrText = InterfaceMenu.Field(new(82, r3, Math.Max(140, width - 300), 24), editAddrText);
            if (InterfaceMenu.Button(new(width - 212, r3, 92, 24), "更新定位") && selectedEntry is {} ue)
                UpdateSelectedEntryTarget(ue, ue.Type, editAddrText);
            GUI.enabled = enabled && selectedEntry is { Valid: true, Writable: true };
            if (InterfaceMenu.Button(new(width - 114, r3, 102, 24), "保存调试值") && selectedEntry is {} entry)
                SaveValuePreset(entry);
        }
        finally { GUI.enabled = enabled; }
    }

    static void DrawPointerScanner(float width, float height)
    {
        DrawCard(new(0, 0, width, 46));
        if (InterfaceMenu.Button(new(10, 9, 104, 28), "返回地址表")) showPointerScanner = false;
        InterfaceMenu.Label(new(122, 9, 210, 28), $"指针反查 · 0x{pointerScanTarget:X}", InterfaceMenu.Accent, 13, translate: false);
        InterfaceMenu.Label(new(338, 9, 64, 28), "最大偏移", MutedText, 12);
        pointerMaxOffsetInput = InterfaceMenu.Field(new(404, 9, 64, 28), pointerMaxOffsetInput);
        if (InterfaceMenu.Button(new(474, 9, 70, 28), $"深度:{pointerMaxDepth}"))
            pointerMaxDepth = pointerMaxDepth >= 3 ? 1 : pointerMaxDepth + 1;
        if (InterfaceMenu.Button(new(width - 104, 9, 94, 28), "重新扫描", true))
            RunPointerScan();

        Rect listCard = new(0, 54, width, height - 54);
        DrawCard(listCard);
        if (pointerScanResults.Count == 0)
        {
            InterfaceMenu.Label(new(16, 74, width - 32, 30), "未找到指向该目标的静态模块指针链（可尝试增大最大偏移或深度）。", MutedText, 12);
            return;
        }
        Rect view = new(8, 62, width - 16, height - 70);
        pointerScanScroll = GUI.BeginScrollView(view, pointerScanScroll, new Rect(0, 0, view.width - 18, pointerScanResults.Count * RowHeight));
        try
        {
            for (int i = 0; i < pointerScanResults.Count; i++)
            {
                var r = pointerScanResults[i];
                float y = i * RowHeight;
                if (i % 2 == 1) InterfaceMenu.Surface(new(0, y, view.width - 18, RowHeight - 1), RowAltBg, 4);
                InterfaceMenu.Label(new(8, y, view.width - 110, RowHeight), r.Expression, InterfaceMenu.Text, 12, translate: false);
                if (InterfaceMenu.Button(new(view.width - 96, y + 1, 74, RowHeight - 3), "+ 添加"))
                {
                    if (AddAddress(r.BaseAddress, entryType, offsets: r.Offsets, baseAddr: r.BaseAddress))
                        showPointerScanner = false;
                }
            }
        }
        finally { GUI.EndScrollView(); }
    }

    static void DrawValuePresets(float width, float height)
    {
        DrawCard(new(0, 0, width, height));
        if (InterfaceMenu.Button(new(10, 8, 106, 28), "返回地址表")) showValuePresets = false;
        InterfaceMenu.Label(new(126, 8, width - 136, 28), "调试预设", InterfaceMenu.Accent, 13);
        if (valuePresets.Count == 0)
        {
            InterfaceMenu.Label(new(12, 44, width - 24, 28), "没有已保存的调试预设。", MutedText, 12);
            return;
        }

        const int rowHeight = 54;
        var view = new Rect(8, 44, width - 16, height - 52);
        float rowWidth = view.width - 18;
        MemoryValuePreset? remove = null;
        presetScroll = GUI.BeginScrollView(view, presetScroll, new Rect(0, 0, rowWidth, rowHeight * valuePresets.Count));
        try
        {
            for (int i = 0; i < valuePresets.Count; i++)
            {
                var preset = valuePresets[i];
                float y = i * rowHeight;
                if (i % 2 == 1) InterfaceMenu.Surface(new(0, y, rowWidth, rowHeight - 1), RowAltBg, 4);
                string label = string.IsNullOrWhiteSpace(preset.Description) ? preset.Type.ToString() : preset.Description;
                InterfaceMenu.Label(new(8, y + 3, rowWidth - 176, 24), $"{label} · {preset.Value}", size: 12, translate: false);
                InterfaceMenu.Label(new(8, y + 26, rowWidth - 176, 22), $"{preset.Location} · {preset.GameVersion}",
                    MutedText, 11, translate: false);
                if (InterfaceMenu.Button(new(rowWidth - 156, y + 12, 70, 28), "填入")) LoadValuePreset(preset);
                if (InterfaceMenu.Button(new(rowWidth - 78, y + 12, 70, 28), "删除")) remove = preset;
            }
        }
        finally { GUI.EndScrollView(); }
        if (remove != null) RemoveValuePreset(remove);
    }

    // -------------------------------------------------------------------------
    // Page 3: Hardware Watchpoint Monitor (DR0-DR3) & Value Change Log
    // -------------------------------------------------------------------------
    static void DrawWatchPage(float width, float height)
    {
        // Top Sub-tab & DR0-DR3 Slot Bar
        Rect topCard = new(0, 0, width, watchSubTab == 0 ? 104 : 44);
        DrawCard(topCard);

        if (InterfaceMenu.Button(new(10, 8, 166, 28), "硬件断点监控 (DR0–DR3)", watchSubTab == 0))
        {
            watchSubTab = 0;
            RefreshWatchHits();
        }
        if (InterfaceMenu.Button(new(182, 8, 134, 28), $"数值变化记录 ({history.Count})", watchSubTab == 1))
            watchSubTab = 1;

        if (watchSubTab == 1)
        {
            if (InterfaceMenu.Button(new(width - 108, 8, 98, 28), "清空记录")) history.Clear();
            DrawValueHistoryTable(new(0, 52, width, height - 52));
            return;
        }

        var slots = MemoryWatchpoint.GetSlots();
        if (InterfaceMenu.Button(new(width - 344, 8, 64, 28), watchPaused ? "已暂停" : "暂停", watchPaused))
            watchPaused = !watchPaused;
        if (InterfaceMenu.Button(new(width - 274, 8, 80, 28), "刷新命中")) RefreshWatchHits();
        if (InterfaceMenu.Button(new(width - 188, 8, 80, 28), "清空命中"))
        {
            MemoryWatchpoint.ClearHits(activeWatchSlot);
            RefreshWatchHits();
        }
        if (InterfaceMenu.Button(new(width - 102, 8, 92, 28), "停止全部"))
        {
            MemoryWatchpoint.StopAll();
            cachedHits = [];
            selectedHitIndex = -1;
            selectFirstSample = false;
            SetStatus("已停止全部硬件断点监控并释放 VEH。");
        }

        // 4 DR Slot Cards
        float sw = (width - 20 - 18) / 4;
        for (int i = 0; i < MemoryWatchpoint.MaxSlots; i++)
        {
            var s = slots[i];
            Rect sr = new(10 + i * (sw + 6), 44, sw, 52);
            bool sel = activeWatchSlot == i;
            InterfaceMenu.Surface(sr, sel ? RowSelBg : RowAltBg, 6);
            if (LeftClicked(new(sr.x, sr.y, sr.width - (s.Active ? 44 : 0), sr.height), true))
            {
                activeWatchSlot = i;
                selectedHitIndex = -1;
                selectFirstSample = false;
                RefreshWatchHits();
            }
            string title = s.Active
                ? $"DR{i} · {I18n.Text(s.Mode == WatchMode.Execute ? "执行采样" : s.Mode == WatchMode.Write ? "改写" : "访问")}" +
                    (s.Mode == WatchMode.Execute ? "" : $" ({s.Size}B)")
                : $"DR{i} · 空闲";
            InterfaceMenu.Label(new(sr.x + 8, sr.y + 4, sr.width - 52, 20), title,
                s.Active ? InterfaceMenu.Accent : MutedText, 12, translate: false);
            string sub = s.Active ? $"0x{s.Address:X} · {s.AttachedThreads}线程" : "在地址表右键启动";
            InterfaceMenu.Label(new(sr.x + 8, sr.y + 26, sr.width - 16, 20), sub, MutedText, 11, translate: false);
            if (s.Active && InterfaceMenu.Button(new(sr.xMax - 42, sr.y + 5, 36, 22), "停"))
            {
                MemoryWatchpoint.StopWatch(i);
                if (activeWatchSlot == i) selectFirstSample = false;
                RefreshWatchHits();
                SetStatus($"已停止 DR{i} 监控。");
            }
        }

        // Middle Hit List Card
        float midY = 112;
        float regH = 172;
        float midH = Math.Max(120, height - midY - regH - 8);
        Rect midCard = new(0, midY, width, midH);
        DrawCard(midCard);

        InterfaceMenu.Surface(new(8, midY + 8, width - 16, 24), RowAltBg, 4);
        InterfaceMenu.Label(new(14, midY + 8, 66, 24), "命中次数", MutedText, 12);
        InterfaceMenu.Label(new(84, midY + 8, 250, 24), "指令地址（模块+偏移）", MutedText, 12);
        InterfaceMenu.Label(new(340, midY + 8, width - 520, 24), "触发指令（x64 反汇编）", MutedText, 12);
        InterfaceMenu.Label(new(width - 178, midY + 8, 36, 24), "过滤", MutedText, 11);
        watchFilterText = InterfaceMenu.Field(new(width - 142, midY + 9, 130, 22), watchFilterText);

        var visibleHits = string.IsNullOrWhiteSpace(watchFilterText)
            ? cachedHits
            : Array.FindAll(cachedHits, h =>
                h.ModuleOffset.Contains(watchFilterText.Trim(), StringComparison.OrdinalIgnoreCase) ||
                h.Disassembly.Contains(watchFilterText.Trim(), StringComparison.OrdinalIgnoreCase));

        Rect listView = new(8, midY + 36, width - 16, Math.Max(40, midH - 44));
        bool insideList = listView.Contains(Event.current.mousePosition);
        hitScroll = GUI.BeginScrollView(listView, hitScroll, new Rect(0, 0, listView.width - 18, visibleHits.Length * RowHeight));
        try
        {
            for (int i = 0; i < visibleHits.Length; i++)
            {
                var h = visibleHits[i];
                int realIdx = Array.IndexOf(cachedHits, h);
                float y = i * RowHeight;
                Rect rowRect = new(0, y, listView.width - 18, RowHeight - 1);
                if (realIdx == selectedHitIndex) InterfaceMenu.Surface(rowRect, RowSelBg, 4);
                else if (i % 2 == 1) InterfaceMenu.Surface(rowRect, RowAltBg, 4);

                if (RightClicked(rowRect, insideList))
                {
                    selectedHitIndex = realIdx;
                    var hit = h;
                    string invLabel = "";
                    bool canInv = MemoryDisassembly.TryDecodeSingle(hit.InstructionAddress, out var insn, out _, out _) &&
                                  MemoryDisassembly.TryGetInvertCode(insn.Code, out _, out invLabel);
                    var items = new System.Collections.Generic.List<(string, Action)>
                    {
                        ("在反汇编中查看", () => OpenInDisassembler(hit.InstructionAddress, hit.InstructionAddress)),
                        ("查看寄存器", () => ShowInstructionRegisters(hit.InstructionAddress)),
                        ("查看前导上下文", () => OpenLeadingContext(hit.InstructionAddress)),
                        ("列出上下文", () => OpenContextPopup(hit.InstructionAddress, leadingContextRows?.Value ?? DefaultLeadingContextRows, followingContextRows?.Value ?? DefaultFollowingContextRows)),
                        ("一键 NOP 此指令", () => QuickNopFromHit(hit)),
                    };
                    if (canInv) items.Add(($"一键反转 ({invLabel})", () => QuickInvertFromHit(hit)));
                    items.Add(CopyItem("复制地址", $"0x{hit.InstructionAddress:X}"));
                    items.Add(CopyItem("复制模块+偏移", hit.ModuleOffset));
                    items.Add(CopyItem("复制指令", hit.Disassembly));
                    menuItems = items.ToArray();
                }
                else if (LeftClicked(rowRect, insideList))
                {
                    selectedHitIndex = realIdx;
                }

                InterfaceMenu.Label(new(6, y, 66, RowHeight), h.Count.ToString("N0"), InterfaceMenu.Accent, 12, translate: false);
                InterfaceMenu.Label(new(76, y, 250, RowHeight), h.ModuleOffset, size: 12, translate: false);
                InterfaceMenu.Label(new(332, y, rowRect.width - 338, RowHeight), h.Disassembly, size: 12, translate: false);
            }
        }
        finally { GUI.EndScrollView(); }

        // Bottom Register Snapshot + Quick Action Card
        float regY = midY + midH + 8;
        Rect regCard = new(0, regY, width, regH);
        DrawCard(regCard);

        WatchHit? selHit = selectedHitIndex >= 0 && selectedHitIndex < cachedHits.Length ? cachedHits[selectedHitIndex] : null;
        bool prev = GUI.enabled;
        GUI.enabled = prev && selHit.HasValue;
        try
        {
            float ay = regY + 8;
            if (InterfaceMenu.Button(new(10, ay, 116, 28), "在反汇编中查看") && selHit is {} sh1)
                OpenInDisassembler(sh1.InstructionAddress, sh1.InstructionAddress);
            if (InterfaceMenu.Button(new(132, ay, 86, 28), "一键 NOP") && selHit is {} sh2)
                QuickNopFromHit(sh2);
            string invText = "反转指令";
            bool canInvert = selHit is {} sh3 &&
                MemoryDisassembly.TryDecodeSingle(sh3.InstructionAddress, out var invInsn, out _, out _) &&
                MemoryDisassembly.TryGetInvertCode(invInsn.Code, out _, out invText);
            GUI.enabled = prev && canInvert;
            if (InterfaceMenu.Button(new(224, ay, 112, 28), invText) && selHit is {} sh4)
                QuickInvertFromHit(sh4);
            GUI.enabled = prev && selHit.HasValue;
            if (InterfaceMenu.Button(new(342, ay, 108, 28), "查看前导上下文") && selHit is {} sh5)
                OpenLeadingContext(sh5.InstructionAddress);
            if (InterfaceMenu.Button(new(456, ay, 94, 28), "列出上下文") && selHit is {} sh5b)
                OpenContextPopup(sh5b.InstructionAddress, leadingContextRows?.Value ?? DefaultLeadingContextRows, followingContextRows?.Value ?? DefaultFollowingContextRows);
            if (InterfaceMenu.Button(new(width - 102, ay, 92, 28), "复制指令") && selHit is {} sh6)
            {
                GUIUtility.systemCopyBuffer = $"{sh6.ModuleOffset} : {sh6.Disassembly}";
                SetStatus("已复制命中指令。");
            }
        }
        finally { GUI.enabled = prev; }

        if (selHit is {} activeHit)
        {
            if (InterfaceMenu.Button(new(10, regY + 42, 116, 22), "通用寄存器", !showXmm)) showXmm = false;
            if (InterfaceMenu.Button(new(132, regY + 42, 80, 22), "XMM", showXmm)) showXmm = true;
            InterfaceMenu.Label(new(222, regY + 42, width - 234, 22), $"RIP 0x{activeHit.TrapRip:X}", MutedText, 11, translate: false);
            if (showXmm)
            {
                DrawXmmRegisters(activeHit, width, regY + 68);
                return;
            }
            var r = activeHit.Registers;
            (string Name, ulong Val)[] regs =
            [
                ("RAX", r.Rax), ("RBX", r.Rbx), ("RCX", r.Rcx), ("RDX", r.Rdx),
                ("RSI", r.Rsi), ("RDI", r.Rdi), ("RBP", r.Rbp), ("RSP", r.Rsp),
                ("R8", r.R8), ("R9", r.R9), ("R10", r.R10), ("R11", r.R11),
                ("R12", r.R12), ("R13", r.R13), ("R14", r.R14), ("R15", r.R15)
            ];
            float rw = (width - 28) / 4;
            for (int i = 0; i < regs.Length; i++)
            {
                float rx = 10 + (i % 4) * (rw + 2);
                float ry = regY + 68 + (i / 4) * 24;
                Rect rr = new(rx, ry, rw - 4, 22);
                if (InterfaceMenu.Button(rr, $"{regs[i].Name} {regs[i].Val:X12}"))
                {
                    if (!OpenInHexBrowser(regs[i].Val))
                    {
                        GUIUtility.systemCopyBuffer = $"0x{regs[i].Val:X}";
                        SetStatus($"0x{regs[i].Val:X} 无法按地址读取，已复制 {regs[i].Name} 的值。");
                    }
                }
            }
        }
        else
        {
            InterfaceMenu.Label(new(14, regY + 56, width - 28, 40),
                "选中一条命中记录后查看通用寄存器和 XMM 快照。",
                MutedText, 12);
        }
    }

    static void DrawXmmRegisters(WatchHit hit, float width, float y)
    {
        if (hit.Xmm is not {} registers)
        {
            InterfaceMenu.Label(new(12, y + 10, width - 24, 24), "本次命中未提供 XMM 状态。", MutedText, 12);
            return;
        }
        float buttonWidth = (width - 62) / 8;
        for (int i = 0; i < registers.Length; i++)
        {
            if (InterfaceMenu.Button(new(10 + i % 8 * (buttonWidth + 6), y + i / 8 * 24, buttonWidth, 22),
                $"XMM{i}", i == selectedXmm))
                selectedXmm = i;
        }
        var value = registers[selectedXmm];
        string hex = $"0x{value.High:X16}{value.Low:X16}";
        InterfaceMenu.Label(new(12, y + 49, width - 114, 26), $"XMM{selectedXmm}  {hex}", InterfaceMenu.Accent, 13, translate: false);
        if (InterfaceMenu.Button(new(width - 92, y + 49, 80, 26), "复制数值")) GUIUtility.systemCopyBuffer = hex;
        string lanes = string.Join("  ", new[] { (uint)value.Low, (uint)(value.Low >> 32), (uint)value.High, (uint)(value.High >> 32) }
            .Select(bits => BitConverter.UInt32BitsToSingle(bits).ToString("G9", CultureInfo.InvariantCulture)));
        InterfaceMenu.Label(new(12, y + 77, width - 24, 22), $"Float32 · {lanes}", MutedText, 11, translate: false);
    }

    static void QuickNopFromHit(WatchHit hit)
    {
        if (MemoryCodePatcher.ApplyNop(hit.InstructionAddress, 0, out _, out string err))
            SetStatus($"已对 {hit.ModuleOffset} 应用 NOP 补丁。");
        else
            SetStatus(err, true);
    }

    static void QuickInvertFromHit(WatchHit hit)
    {
        if (MemoryCodePatcher.ApplyInvert(hit.InstructionAddress, out var p, out string err) && p != null)
            SetStatus($"已反转 {hit.ModuleOffset}：{p.Label}");
        else
            SetStatus(err, true);
    }

    static void DrawValueHistoryTable(Rect rect)
    {
        DrawCard(rect);
        InterfaceMenu.Surface(new(rect.x + 8, rect.y + 8, rect.width - 16, 24), RowAltBg, 4);
        InterfaceMenu.Label(new(rect.x + 14, rect.y + 8, 96, 24), "时间", MutedText, 12);
        InterfaceMenu.Label(new(rect.x + 114, rect.y + 8, 110, 24), "来源", MutedText, 12);
        InterfaceMenu.Label(new(rect.x + 228, rect.y + 8, 148, 24), "地址", MutedText, 12);
        InterfaceMenu.Label(new(rect.x + 380, rect.y + 8, rect.width - 396, 24), "原值 → 新值", MutedText, 12);

        Rect listView = new(rect.x + 8, rect.y + 36, rect.width - 16, Math.Max(40, rect.height - 44));
        bool insideList = listView.Contains(Event.current.mousePosition);
        logScroll = GUI.BeginScrollView(listView, logScroll, new Rect(0, 0, listView.width - 18, history.Count * RowHeight));
        try
        {
            int i = history.Count;
            foreach (var row in history)
            {
                float y = --i * RowHeight;
                if (y + RowHeight < logScroll.y || y > logScroll.y + listView.height) continue;
                Rect rowRect = new(0, y, listView.width - 18, RowHeight - 1);
                if (i % 2 == 1) InterfaceMenu.Surface(rowRect, RowAltBg, 4);
                if (RightClicked(rowRect, insideList))
                {
                    menuItems =
                    [
                        CopyItem("复制地址", $"0x{row.Address:X}"),
                        CopyItem("复制原值", Format(row.Before, row.Type)),
                        CopyItem("复制新值", Format(row.After, row.Type))
                    ];
                }
                InterfaceMenu.Label(new(6, y, 96, RowHeight), row.Time.ToString("HH:mm:ss.fff"), MutedText, 12, translate: false);
                InterfaceMenu.Label(new(106, y, 110, RowHeight), row.Source, size: 12);
                InterfaceMenu.Label(new(220, y, 148, RowHeight), $"0x{row.Address:X}", size: 12, translate: false);
                InterfaceMenu.Label(new(372, y, rowRect.width - 378, RowHeight),
                    Format(row.Before, row.Type) + " → " + Format(row.After, row.Type), size: 12, translate: false);
            }
        }
        finally { GUI.EndScrollView(); }
    }

    // -------------------------------------------------------------------------
    // Page 4: Interactive Disassembler + Inline Assembler / Code Cave Drawer
    // -------------------------------------------------------------------------
    static void DrawDisassemblyPage(float width, float height)
    {
        float topHeight = browseCode ? 108 : 74;
        DrawCard(new(0, 0, width, topHeight));

        if (InterfaceMenu.Button(new(10, 8, 128, 24), "实时代码", browseCode))
        {
            browseCode = true;
            inlineEditMode = 0;
        }
        if (InterfaceMenu.Button(new(144, 8, 148, 24), "十六进制解码", !browseCode))
        {
            browseCode = false;
            inlineEditMode = 0;
        }

        if (browseCode)
        {
            bool prev = GUI.enabled;
            GUI.enabled = prev && navHistory.Count > 0;
            if (InterfaceMenu.Button(new(width - 270, 8, 62, 24), "◀ 回退"))
                ReturnToDisassemblyOrigin();
            GUI.enabled = prev && disasmOrigin != 0;
            if (InterfaceMenu.Button(new(width - 202, 8, 56, 24), "上翻"))
                PageDisassembly(forward: false);
            GUI.enabled = prev && nextCodeAddress != 0;
            if (InterfaceMenu.Button(new(width - 140, 8, 56, 24), "下翻"))
                PageDisassembly(forward: true);
            GUI.enabled = prev && disasmOrigin != 0;
            if (InterfaceMenu.Button(new(width - 78, 8, 68, 24), "刷新"))
                LoadDisassembly(disasmOrigin);
            GUI.enabled = prev;

            InterfaceMenu.Label(new(10, 40, 100, 26), "地址 / 模块+偏移", MutedText, 12);
            const string addressControl = "MemoryEditorDisassemblyAddress";
            var ev = Event.current;
            bool enter = prev && ev.type == EventType.KeyDown && ev.keyCode is KeyCode.Return or KeyCode.KeypadEnter &&
                GUI.GetNameOfFocusedControl() == addressControl;
            if (enter) ev.Use();
            GUI.SetNextControlName(addressControl);
            codeAddressInput = InterfaceMenu.Field(new(114, 40, width - 210, 26), codeAddressInput);
            if (InterfaceMenu.Button(new(width - 90, 40, 80, 26), "转到") || enter)
            {
                if (MemoryDisassembly.TryParseAddress(codeAddressInput, out ulong target))
                    OpenLeadingContext(target);
                else SetStatus("请输入有效的十六进制地址或 模块名+偏移（如 GameAssembly.dll+1234）。", true);
            }

            DrawDisassemblySettings(width);
        }
        else
        {
            InterfaceMenu.Label(new(10, 40, 96, 26), "x64 机器码", MutedText, 12);
            hexDecodeInput = InterfaceMenu.Field(new(110, 40, width - 206, 26), hexDecodeInput);
            if (InterfaceMenu.Button(new(width - 90, 40, 80, 26), "解码"))
            {
                decodedHexRows = MemoryDisassembly.Decode(hexDecodeInput);
                if (decodedHexRows.Length == 0) SetStatus("请输入 1 至 256 字节的十六进制机器码。", true);
                asmScroll = default;
            }
        }

        float editorH = browseCode ? (inlineEditMode != 0 ? 132 : 46) : 0;
        float midY = topHeight + 8;
        float midH = Math.Max(140, height - midY - (editorH > 0 ? editorH + 8 : 0));
        Rect codeCard = new(0, midY, width, midH);
        DrawCard(codeCard);

        InterfaceMenu.Surface(new(8, midY + 8, width - 16, 24), RowAltBg, 4);
        if (browseCode)
        {
            InterfaceMenu.Label(new(14, midY + 8, 248, 24), "地址（模块+偏移）", MutedText, 12);
            InterfaceMenu.Label(new(266, midY + 8, 190, 24), "机器码", MutedText, 12);
            InterfaceMenu.Label(new(460, midY + 8, width - 476, 24), "汇编指令", MutedText, 12);
        }
        else
        {
            InterfaceMenu.Label(new(14, midY + 8, width - 28, 24), "偏移 / 字节 / 指令", MutedText, 12);
        }

        Rect listView = new(8, midY + 36, width - 16, Math.Max(40, midH - 44));
        bool insideList = listView.Contains(Event.current.mousePosition);
        int rowCount = browseCode ? disasmRows.Length : decodedHexRows.Length;
        if (browseCode && revealDisasmHighlight)
        {
            float centered = selectedDisasmIndex * RowHeight - (listView.height - RowHeight) / 2;
            asmScroll.y = Math.Clamp(centered, 0, Math.Max(0, rowCount * RowHeight - listView.height));
            revealDisasmHighlight = false;
        }
        asmScroll = GUI.BeginScrollView(listView, asmScroll, new Rect(0, 0, listView.width - 18, rowCount * RowHeight));
        try
        {
            int first = Math.Clamp((int)(asmScroll.y / RowHeight), 0, rowCount);
            int end = Math.Min(rowCount, (int)Math.Ceiling((asmScroll.y + listView.height) / RowHeight));
            if (browseCode)
            {
                for (int i = first; i < end; i++)
                {
                    var row = disasmRows[i];
                    float y = i * RowHeight;
                    Rect rowRect = new(0, y, listView.width - 18, RowHeight - 1);
                    if (i == selectedDisasmIndex) InterfaceMenu.Surface(rowRect, RowSelBg, 4);
                    else if (row.IsPatched) InterfaceMenu.Surface(rowRect, PatchedBg, 4);
                    else if (i % 2 == 1) InterfaceMenu.Surface(rowRect, RowAltBg, 4);

                    if (RightClicked(rowRect, insideList))
                    {
                        selectedDisasmIndex = i;
                        var r = row;
                        var items = new System.Collections.Generic.List<(string, Action)>
                        {
                            ("查看寄存器", () => ShowInstructionRegisters(r.Address)),
                            ("在下一条指令采样", () => BeginInstructionSample(r.Address)),
                            ("列出上下文", () => { if (TryReadContextCounts(out int b, out int a)) OpenContextPopup(r.Address, b, a); }),
                            ("NOP 替换此指令", () => ApplyDisasmNop(r)),
                        };
                        if (r.InvertLabel != null)
                            items.Add(($"反转指令 ({r.InvertLabel})", () => ApplyDisasmInvert(r)));
                        items.Add(("编辑汇编 / 机器码", () => BeginInlineEdit(1, r)));
                        items.Add(("代码洞注入 (Code Cave)", () => BeginInlineEdit(2, r)));
                        if (r.BranchTarget != 0)
                            items.Add(($"跟随跳转 → 0x{r.BranchTarget:X}", () => OpenInDisassembler(r.BranchTarget, r.BranchTarget)));
                        items.Add(CopyItem("复制地址", $"0x{r.Address:X}"));
                        items.Add(CopyItem("复制模块+偏移", r.ModuleOffset));
                        items.Add(CopyItem("复制指令", r.Text));
                        menuItems = items.ToArray();
                    }
                    else if (LeftClicked(rowRect, insideList))
                    {
                        selectedDisasmIndex = i;
                    }

                    string prefix = row.Address == highlightIp ? "▶ " : row.IsPatched ? "★ " : "  ";
                    Color addrCol = row.Address == highlightIp ? InterfaceMenu.Accent : row.IsPatched ? PatchAccent : InterfaceMenu.Text;
                    InterfaceMenu.Label(new(4, y, 250, RowHeight), prefix + row.ModuleOffset, addrCol, 12, translate: false);
                    InterfaceMenu.Label(new(258, y, 190, RowHeight), row.HexBytes, MutedText, 11, translate: false);
                    InterfaceMenu.Label(new(452, y, rowRect.width - 458, RowHeight), row.Text,
                        row.IsPatched ? PatchAccent : InterfaceMenu.Text, 12, translate: false);
                }
            }
            else
            {
                for (int i = first; i < end; i++)
                {
                    float y = i * RowHeight;
                    Rect rowRect = new(0, y, listView.width - 18, RowHeight - 1);
                    if (i % 2 == 1) InterfaceMenu.Surface(rowRect, RowAltBg, 4);
                    if (RightClicked(rowRect, insideList))
                        menuItems = [CopyItem("复制指令行", decodedHexRows[i])];
                    InterfaceMenu.Label(new(6, y, rowRect.width - 12, RowHeight), decodedHexRows[i], size: 12, translate: false);
                }
            }
        }
        finally { GUI.EndScrollView(); }

        if (!browseCode) return;

        // Bottom Action Bar or Inline Editor Drawer
        float botY = midY + midH + 8;
        Rect botCard = new(0, botY, width, editorH);
        DrawCard(botCard);

        DisasmRow? selRow = selectedDisasmIndex >= 0 && selectedDisasmIndex < disasmRows.Length ? disasmRows[selectedDisasmIndex] : null;
        if (inlineEditMode == 0)
        {
            bool prev = GUI.enabled;
            GUI.enabled = prev && selRow.HasValue;
            try
            {
                float ay = botY + 9;
                if (InterfaceMenu.Button(new(10, ay, 96, 28), "NOP 替换") && selRow is {} r1) ApplyDisasmNop(r1);
                GUI.enabled = prev && selRow is { InvertLabel: not null };
                if (InterfaceMenu.Button(new(112, ay, 134, 28), selRow?.InvertLabel ?? "反转指令") && selRow is {} r2) ApplyDisasmInvert(r2);
                GUI.enabled = prev && selRow.HasValue;
                if (InterfaceMenu.Button(new(252, ay, 126, 28), "编辑汇编/机器码") && selRow is {} r3) BeginInlineEdit(1, r3);
                if (InterfaceMenu.Button(new(384, ay, 126, 28), "代码洞注入") && selRow is {} r4) BeginInlineEdit(2, r4);
                GUI.enabled = prev && selRow is { BranchTarget: not 0 };
                if (InterfaceMenu.Button(new(516, ay, 96, 28), "跟随跳转") && selRow is {} r5) OpenInDisassembler(r5.BranchTarget, r5.BranchTarget);
                GUI.enabled = prev && MemoryCodePatcher.GetPatches().Length > 0;
                if (InterfaceMenu.Button(new(width - 128, ay, 118, 28), $"代码补丁 ({MemoryCodePatcher.GetPatches().Length})"))
                    InterfaceMenu.Navigate("内存编辑", "代码补丁");
            }
            finally { GUI.enabled = prev; }
        }
        else if (selRow is {} editRow)
        {
            string modeTitle = inlineEditMode == 1
                ? $"原地修改 {editRow.ModuleOffset}（输入汇编或十六进制机器码，例如 vaddss xmm0, xmm1, xmm2）"
                : $"代码洞注入 {editRow.ModuleOffset}（自动分配跳板页并重定位被覆盖指令）";
            InterfaceMenu.Label(new(12, botY + 6, width - 24, 22), modeTitle, InterfaceMenu.Accent, 12, translate: false);
            inlineCodeInput = InterfaceMenu.Field(new(12, botY + 32, width - 24, 28), inlineCodeInput);

            // Live encode preview
            var source = (inlineCodeInput, editRow.Address);
            if (inlinePreviewSource != source)
            {
                inlinePreviewSource = source;
                inlinePreviewValid = MemoryCodePatcher.TryParseCodeInput(inlineCodeInput, editRow.Address,
                    out var previewBytes, out string error);
                inlinePreview = inlinePreviewValid
                    ? $"编码预览 ({previewBytes.Length}B)：{BitConverter.ToString(previewBytes).Replace("-", " ")}"
                    : error;
            }
            InterfaceMenu.Label(new(12, botY + 64, width - 24, 22), inlinePreview,
                inlinePreviewValid ? MutedText : WarnColor, 11, translate: false);

            float ay = botY + 92;
            if (inlineEditMode == 2)
            {
                InterfaceMenu.Label(new(12, ay, 72, 28), "原指令：", MutedText, 12);
                if (InterfaceMenu.Button(new(84, ay, 82, 28), "先执行", caveOrder == 0)) caveOrder = 0;
                if (InterfaceMenu.Button(new(170, ay, 82, 28), "后执行", caveOrder == 1)) caveOrder = 1;
                if (InterfaceMenu.Button(new(256, ay, 82, 28), "跳过原指令", caveOrder == 2)) caveOrder = 2;
            }
            if (InterfaceMenu.Button(new(width - 306, ay, 104, 28), "保存 AOB 脚本"))
            {
                if (MemoryCodePatcher.TryParseCodeInput(inlineCodeInput, editRow.Address, out _, out string err))
                    SaveInlineAsPreset(editRow.Address, inlineCodeInput, inlineEditMode == 2, caveOrder, null);
                else SetStatus(err, true);
            }
            if (InterfaceMenu.Button(new(width - 196, ay, 96, 28), inlineEditMode == 1 ? "应用补丁" : "注入代码洞", true))
            {
                bool ok = inlineEditMode == 1
                    ? MemoryCodePatcher.ApplyHexOrAssembly(editRow.Address, inlineCodeInput, out _, out string err)
                    : MemoryCodePatcher.InjectCodeCave(editRow.Address, inlineCodeInput, caveOrder == 0, caveOrder == 1, out _, out err);
                if (ok)
                {
                    inlineEditMode = 0;
                    LoadDisassembly(disasmOrigin);
                    SetStatus("代码补丁已生效，可在“代码补丁”页随时停用或还原。");
                }
                else SetStatus(err, true);
            }
            if (InterfaceMenu.Button(new(width - 94, ay, 82, 28), "取消")) inlineEditMode = 0;
        }
    }

    static void DrawDisassemblySettings(float width)
    {
        var settings = (History: navigationHistoryLimit?.Value ?? DefaultNavigationHistoryLimit,
            Before: leadingContextRows?.Value ?? DefaultLeadingContextRows,
            After: followingContextRows?.Value ?? DefaultFollowingContextRows);
        if (shownDisassemblySettings != settings)
        {
            shownDisassemblySettings = settings;
            historyLimitInput = settings.History.ToString(CultureInfo.InvariantCulture);
            leadingContextInput = settings.Before.ToString(CultureInfo.InvariantCulture);
            followingContextInput = settings.After.ToString(CultureInfo.InvariantCulture);
            TrimNavigationHistory(settings.History);
        }

        InterfaceMenu.Label(new(10, 74, 86, 26), "回退(0关)", MutedText, 12);
        historyLimitInput = InterfaceMenu.Field(new(98, 74, 46, 26), historyLimitInput);
        InterfaceMenu.Label(new(152, 74, 58, 26), "前导", MutedText, 12);
        leadingContextInput = InterfaceMenu.Field(new(212, 74, 46, 26), leadingContextInput);
        InterfaceMenu.Label(new(266, 74, 58, 26), "后续", MutedText, 12);
        followingContextInput = InterfaceMenu.Field(new(326, 74, 46, 26), followingContextInput);
        if (InterfaceMenu.Button(new(380, 74, 62, 26), "保存") && navigationHistoryLimit != null &&
            leadingContextRows != null && followingContextRows != null)
        {
            if (!int.TryParse(historyLimitInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out int history) ||
                history is < 0 or > MaxNavigationHistoryLimit)
                SetStatus($"回退步数请输入 0 到 {MaxNavigationHistoryLimit} 之间的整数。", true);
            else if (TryReadContextCounts(out int before, out int after))
            {
                navigationHistoryLimit.Value = history;
                leadingContextRows.Value = before;
                followingContextRows.Value = after;
                TrimNavigationHistory(history);
                SetStatus("已保存反汇编设置。");
                if ((settings.Before != before || settings.After != after) &&
                    selectedDisasmIndex >= 0 && selectedDisasmIndex < disasmRows.Length)
                    OpenLeadingContext(disasmRows[selectedDisasmIndex].Address);
            }
        }

        bool canExport = selectedDisasmIndex >= 0 && selectedDisasmIndex < disasmRows.Length && disassemblyExport == null;
        bool previous = GUI.enabled;
        GUI.enabled = previous && selectedDisasmIndex >= 0 && selectedDisasmIndex < disasmRows.Length;
        if (InterfaceMenu.Button(new(width - 210, 74, 94, 26), "列出上下文") &&
            TryReadContextCounts(out int listBefore, out int listAfter))
            OpenContextPopup(disasmRows[selectedDisasmIndex].Address, listBefore, listAfter);
        GUI.enabled = previous && canExport;
        if (InterfaceMenu.Button(new(width - 110, 74, 100, 26), "导出上下文") && canExport &&
            TryReadContextCounts(out int exportBefore, out int exportAfter))
            BeginDisassemblyExport(disasmRows[selectedDisasmIndex].Address, exportBefore, exportAfter);
        GUI.enabled = previous;
    }

    static bool TryReadContextCounts(out int before, out int after)
    {
        bool validBefore = int.TryParse(leadingContextInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out before);
        bool validAfter = int.TryParse(followingContextInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out after);
        if (validBefore && validAfter && before is >= 0 and <= MemoryDisassembly.MaxContextInstructions &&
            after is >= 0 and <= MemoryDisassembly.MaxContextInstructions)
            return true;
        SetStatus($"前导和后续条数各自请输入 0 到 {MemoryDisassembly.MaxContextInstructions} 之间的整数。", true);
        return false;
    }

    static void BeginInlineEdit(int mode, DisasmRow row)
    {
        inlineEditMode = mode;
        inlineCodeInput = mode == 1 ? row.Text : "nop";
    }

    static void ApplyDisasmNop(DisasmRow row)
    {
        if (MemoryCodePatcher.ApplyNop(row.Address, row.Bytes.Length, out _, out string err))
        {
            LoadDisassembly(disasmOrigin);
            SetStatus($"已对 {row.ModuleOffset} 应用 NOP（{row.Bytes.Length}B）。");
        }
        else SetStatus(err, true);
    }

    static void ApplyDisasmInvert(DisasmRow row)
    {
        if (MemoryCodePatcher.ApplyInvert(row.Address, out var p, out string err) && p != null)
        {
            LoadDisassembly(disasmOrigin);
            SetStatus($"已反转 {row.ModuleOffset}：{p.Label}");
        }
        else SetStatus(err, true);
    }

    // -------------------------------------------------------------------------
    // Page 5: Code Patch Manager
    // -------------------------------------------------------------------------
    static void DrawPatchesPage(float width, float height)
    {
        EnsurePatchPresetsLoaded();
        var patches = MemoryCodePatcher.GetPatches();
        Rect topCard = new(0, 0, width, 46);
        DrawCard(topCard);

        if (InterfaceMenu.Button(new(10, 9, 130, 28), $"生效补丁 ({patches.Length})", patchSubTab == 0))
            patchSubTab = 0;
        if (InterfaceMenu.Button(new(146, 9, 148, 28), $"补丁脚本 ({patchPresets.Count})", patchSubTab == 1))
            patchSubTab = 1;

        bool prev = GUI.enabled;
        if (patchSubTab == 0)
        {
            InterfaceMenu.Label(new(304, 10, width - 440, 26),
                "热卸载时自动全部还原", MutedText, 12);
            GUI.enabled = prev && patches.Length > 0;
            if (InterfaceMenu.Button(new(width - 126, 9, 114, 28), "全部还原"))
            {
                MemoryCodePatcher.RestoreAll();
                if (disasmOrigin != 0) LoadDisassembly(disasmOrigin);
                SetStatus("已还原全部代码补丁并释放所有代码洞。");
            }
            GUI.enabled = prev;

            Rect listCard = new(0, 54, width, height - 54);
            DrawCard(listCard);

            if (patches.Length == 0)
            {
                InterfaceMenu.Label(new(16, 74, width - 32, 36),
                    "当前没有生效的代码补丁。可在“访问监控”或“反汇编”页右键指令执行 NOP、反转指令、汇编修改或代码洞注入。",
                    MutedText, 13);
                return;
            }

            const float itemH = 66;
            Rect view = new(8, 62, width - 16, height - 70);
            patchScroll = GUI.BeginScrollView(view, patchScroll, new Rect(0, 0, view.width - 18, patches.Length * (itemH + 6)));
            try
            {
                for (int i = 0; i < patches.Length; i++)
                {
                    var p = patches[i];
                    float y = i * (itemH + 6);
                    Rect card = new(0, y, view.width - 18, itemH);
                    InterfaceMenu.Surface(card, p.Enabled ? PatchedBg : RowAltBg, 7);

                    string badge = p.Enabled ? "● 生效中" : "○ 已停用";
                    InterfaceMenu.Label(new(10, y + 6, 86, 22), badge, p.Enabled ? PatchAccent : MutedText, 12);
                    string header = $"#{p.Id}  {p.ModuleOffset}  ·  {p.Label}" + (p.CaveAddress != 0 ? $"  [Cave 0x{p.CaveAddress:X}]" : "");
                    InterfaceMenu.Label(new(98, y + 6, card.width - 450, 22), header, InterfaceMenu.Text, 12, translate: false);

                    string diff = $"{p.OriginalDisasm}   →   {p.PatchedDisasm}";
                    InterfaceMenu.Label(new(10, y + 34, card.width - 362, 24), diff, MutedText, 12, translate: false);

                    if (InterfaceMenu.Button(new(card.width - 346, y + 18, 80, 30), "保存脚本"))
                        SavePatchAsPreset(p);
                    if (InterfaceMenu.Button(new(card.width - 260, y + 18, 76, 30), p.Enabled ? "停用" : "启用", p.Enabled))
                    {
                        if (MemoryCodePatcher.SetEnabled(p.Id, !p.Enabled, out string err))
                            SetStatus(p.Enabled ? $"已启用补丁 #{p.Id}" : $"已停用补丁 #{p.Id}");
                        else SetStatus(err, true);
                    }
                    if (InterfaceMenu.Button(new(card.width - 178, y + 18, 82, 30), "反汇编"))
                        OpenInDisassembler(p.Address, p.Address);
                    if (InterfaceMenu.Button(new(card.width - 90, y + 18, 82, 30), "还原删除"))
                    {
                        if (MemoryCodePatcher.RemovePatch(p.Id, out string err))
                            SetStatus($"已还原并删除补丁 #{p.Id}。");
                        else SetStatus(err, true);
                    }
                }
            }
            finally { GUI.EndScrollView(); }
            return;
        }

        GUI.enabled = prev && patchPresets.Count > 0;
        if (InterfaceMenu.Button(new(width - 140, 9, 130, 28), "一键全部应用", true))
            ApplyAllSavedPatchPresets();
        GUI.enabled = prev;

        Rect scriptCard = new(0, 54, width, height - 54);
        DrawCard(scriptCard);
        if (patchPresets.Count == 0)
        {
            InterfaceMenu.Label(new(16, 74, width - 32, 36),
                "暂无已保存的 AOB 补丁脚本。可在反汇编内联编辑或生效补丁卡片中点击“保存脚本”。",
                MutedText, 13);
            return;
        }
        const float scriptH = 68;
        Rect sView = new(8, 62, width - 16, height - 70);
        MemoryPatchPreset? removePreset = null;
        patchScroll = GUI.BeginScrollView(sView, patchScroll, new Rect(0, 0, sView.width - 18, patchPresets.Count * (scriptH + 6)));
        try
        {
            for (int i = 0; i < patchPresets.Count; i++)
            {
                var sp = patchPresets[i];
                float y = i * (scriptH + 6);
                Rect card = new(0, y, sView.width - 18, scriptH);
                InterfaceMenu.Surface(card, RowAltBg, 7);
                string title = $"{sp.Name}  ·  [{sp.Mode}]  {sp.Module}+0x{sp.FallbackOffset:X}  ({sp.GameVersion})";
                InterfaceMenu.Label(new(10, y + 6, card.width - 210, 22), title, InterfaceMenu.Text, 12, translate: false);
                InterfaceMenu.Label(new(10, y + 32, card.width - 210, 24), $"AOB: {sp.AobPattern}", MutedText, 11, translate: false);
                if (InterfaceMenu.Button(new(card.width - 194, y + 18, 108, 30), "定位并应用", true))
                    ApplySavedPatchPreset(sp);
                if (InterfaceMenu.Button(new(card.width - 80, y + 18, 70, 30), "删除"))
                    removePreset = sp;
            }
        }
        finally { GUI.EndScrollView(); }
        if (removePreset != null) RemoveSavedPatchPreset(removePreset);
    }

    // -------------------------------------------------------------------------
    // Page 6: Hex Memory Browser + Live Data Inspector
    // -------------------------------------------------------------------------
    static void DrawHexBrowserPage(float width, float height)
    {
        Rect topCard = new(0, 0, width, 78);
        DrawCard(topCard);

        if (InterfaceMenu.Button(new(10, 8, 108, 24), "十六进制视图", !structDissectMode))
            structDissectMode = false;
        if (InterfaceMenu.Button(new(124, 8, 108, 24), "结构体剖析", structDissectMode))
        {
            structDissectMode = true;
            if (hexOrigin != 0) RefreshStructDissect(resetBaseline: structBaselineBytes == null);
        }

        const string addressControl = "MemoryEditorHexAddress";
        var ev = Event.current;
        bool enter = GUI.enabled && ev.type == EventType.KeyDown && ev.keyCode is KeyCode.Return or KeyCode.KeypadEnter &&
            GUI.GetNameOfFocusedControl() == addressControl;
        if (enter) ev.Use();
        GUI.SetNextControlName(addressControl);
        hexAddressInput = InterfaceMenu.Field(new(240, 8, width - 538, 24), hexAddressInput);
        if (InterfaceMenu.Button(new(width - 292, 8, 60, 24), "转到") || enter)
        {
            if (TryParsePointerExpression(hexAddressInput, out ulong baseAddr, out long[] offs, out _))
            {
                ulong target = baseAddr;
                if (offs.Length == 0 || TryResolvePointerChain(baseAddr, offs, out target))
                {
                    if (structDissectMode)
                    {
                        structHistory.Clear();
                        hexOrigin = target;
                        hexAddressInput = $"0x{target:X}";
                        RefreshStructDissect(resetBaseline: true);
                    }
                    else OpenInHexBrowser(target);
                }
                else SetStatus("无法解析该指针链。", true);
            }
            else SetStatus("请输入有效的十六进制地址或指针表达式。", true);
        }
        bool prev = GUI.enabled;
        GUI.enabled = prev && hexOrigin != 0;
        if (InterfaceMenu.Button(new(width - 226, 8, 66, 24), "-0x100"))
            PageHexWindow(forward: false);
        GUI.enabled = prev && hexViewBytes.Length != 0;
        if (InterfaceMenu.Button(new(width - 154, 8, 66, 24), "+0x100"))
            PageHexWindow(forward: true);
        GUI.enabled = prev && hexOrigin != 0;
        if (InterfaceMenu.Button(new(width - 82, 8, 72, 24), "刷新"))
        {
            if (structDissectMode) RefreshStructDissect(resetBaseline: false);
            else RefreshHexWindow();
        }
        GUI.enabled = prev;

        if (structDissectMode)
        {
            DrawStructDissectPage(width, height);
            return;
        }

        // Second toolbar row: display mode + in-page search
        string[] modeLabels = ["Hex8", "Int32", "Float", "Ptr64"];
        for (int i = 0; i < 4; i++)
        {
            var m = (HexDisplayMode)i;
            if (InterfaceMenu.Button(new(10 + i * 62, 42, 58, 26), modeLabels[i], hexDisplayMode == m))
                hexDisplayMode = m;
        }
        InterfaceMenu.Label(new(268, 42, 62, 26), "页内搜索", MutedText, 11);
        hexSearchInput = InterfaceMenu.Field(new(334, 42, Math.Max(120, width - 486), 26), hexSearchInput);
        if (InterfaceMenu.Button(new(width - 146, 42, 64, 26), "查找"))
            SearchHexWindow();
        if (InterfaceMenu.Button(new(width - 76, 42, 66, 26), "反查指针") && hexOrigin != 0)
            BeginPointerScan(hexOrigin + (ulong)Math.Clamp(hexSelectedOffset, 0, Math.Max(0, hexViewBytes.Length - 1)));

        if (GUIUtility.keyboardControl == 0 && ev.type == EventType.KeyDown && hexViewBytes.Length > 0)
        {
            if (ev.keyCode == KeyCode.PageDown) { ev.Use(); PageHexWindow(forward: true); }
            else if (ev.keyCode == KeyCode.PageUp && hexOrigin != 0) { ev.Use(); PageHexWindow(forward: false); }
            else if (ev.keyCode == KeyCode.RightArrow) { ev.Use(); hexSelectedOffset = Math.Min(hexViewBytes.Length - 1, hexSelectedOffset + 1); }
            else if (ev.keyCode == KeyCode.LeftArrow) { ev.Use(); hexSelectedOffset = Math.Max(0, hexSelectedOffset - 1); }
            else if (ev.keyCode == KeyCode.DownArrow) { ev.Use(); hexSelectedOffset = Math.Min(hexViewBytes.Length - 1, hexSelectedOffset + 16); }
            else if (ev.keyCode == KeyCode.UpArrow) { ev.Use(); hexSelectedOffset = Math.Max(0, hexSelectedOffset - 16); }
        }

        const float bodyY = 86, gap = 12;
        const float addressW = 112, asciiW = 128, rowHeight = 24, headerHeight = 24, groupGap = 4;
        bool stacked = width < 864;
        float rightW = stacked ? width : Math.Clamp(MathF.Floor(width * .32f), 300, width - gap - 552);
        float leftW = stacked ? width : width - gap - rightW;
        float gridH = stacked ? 284 : height - bodyY;
        float rightX = stacked ? 0 : leftW + gap;
        float rightY = stacked ? bodyY + gridH + gap : bodyY;

        // Left: grouped hex or typed values, followed by ASCII.
        Rect leftCard = new(0, bodyY, leftW, gridH);
        DrawCard(leftCard, hexOrigin != 0 ? $"十六进制视图 · 0x{hexOrigin:X}" : "十六进制视图");

        float rowWidth = leftW - 36;
        float dataW = rowWidth - addressW - asciiW - 12;
        float byteWidth = MathF.Floor((dataW - 3 * groupGap) / 16);
        float asciiX = rowWidth - asciiW;
        float ByteX(int column) => addressW + column * byteWidth + (column / 4) * groupGap;
        float headerY = bodyY + 30;
        InterfaceMenu.Label(new(12, headerY, addressW - 8, headerHeight), "内存地址", MutedText, 11);
        InterfaceMenu.Label(new(8 + asciiX, headerY, asciiW, headerHeight), "ASCII", MutedText, 11, translate: false);
        Rect hexView = new(8, headerY + headerHeight, leftW - 16, gridH - 62);
        bool insideHex = hexView.Contains(Event.current.mousePosition);
        int rows = (hexViewBytes.Length + 15) / 16;
        ulong nearby = 0;
        hexFont ??= Font.CreateDynamicFontFromOSFont("Consolas", 12);
        var cellStyle = hexCellStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter, font = hexFont, fontSize = 12, wordWrap = false,
            clipping = TextClipping.Clip, padding = new RectOffset(0, 0, 0, 0)
        };
        void Cell(Rect rect, string value, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            cellStyle.normal.textColor = color;
            GUI.Label(rect, value, cellStyle);
        }
        if (hexDisplayMode == HexDisplayMode.Hex8)
            for (int c = 0; c < 16; c++)
                Cell(new(8 + ByteX(c), headerY, byteWidth, headerHeight), c.ToString("X"), MutedText);
        if (hexRevealSelection)
        {
            float selectedY = hexSelectedOffset / 16 * rowHeight;
            hexScroll.y = Math.Clamp(selectedY - (hexView.height - rowHeight) / 2,
                0, Math.Max(0, rows * rowHeight - hexView.height));
            hexRevealSelection = false;
        }
        hexScroll = GUI.BeginScrollView(hexView, hexScroll, new Rect(0, 0, rowWidth, rows * rowHeight), false, true);
        try
        {
            for (int r = 0; r < rows; r++)
            {
                float y = r * rowHeight;
                if (r % 2 == 1) InterfaceMenu.Surface(new(0, y, rowWidth, rowHeight), RowAltBg, 3);
                ulong rowAddr = hexOrigin + (ulong)(r * 16);
                if (InterfaceMenu.Button(new(4, y, addressW - 12, rowHeight), $"{rowAddr:X12}")) nearby = rowAddr;

                if (hexDisplayMode == HexDisplayMode.Hex8)
                {
                    for (int c = 0; c < 16; c++)
                    {
                        int off = r * 16 + c;
                        if (off >= hexViewBytes.Length) break;
                        byte b = hexViewBytes[off];
                        Rect cr = new(ByteX(c), y, byteWidth, rowHeight);
                        bool sel = off == hexSelectedOffset;
                        if (sel) InterfaceMenu.Surface(cr, RowSelBg, 3);
                        DrawHexCellMenu(cr, insideHex, off, 1);
                        Cell(cr, b.ToString("X2"), sel ? InterfaceMenu.Accent : InterfaceMenu.Text);
                    }
                }
                else
                {
                    int stride = hexDisplayMode == HexDisplayMode.Ptr64 ? 8 : 4;
                    int cols = 16 / stride;
                    float cellW = (dataW - groupGap * (cols - 1)) / cols;
                    for (int c = 0; c < cols; c++)
                    {
                        int off = r * 16 + c * stride;
                        if (off + stride > hexViewBytes.Length) break;
                        Rect cr = new(addressW + c * (cellW + groupGap), y, cellW, rowHeight);
                        bool sel = hexSelectedOffset >= off && hexSelectedOffset < off + stride;
                        if (sel) InterfaceMenu.Surface(cr, RowSelBg, 3);
                        DrawHexCellMenu(cr, insideHex, off, stride);
                        var spanCell = hexViewBytes.AsSpan(off, stride);
                        string txt = hexDisplayMode switch
                        {
                            HexDisplayMode.Int32 => BinaryPrimitives.ReadInt32LittleEndian(spanCell).ToString(CultureInfo.InvariantCulture),
                            HexDisplayMode.Float => BitConverter.ToSingle(spanCell).ToString("G6", CultureInfo.InvariantCulture),
                            _ => $"0x{BinaryPrimitives.ReadUInt64LittleEndian(spanCell):X}"
                        };
                        Cell(cr, txt, sel ? InterfaceMenu.Accent : InterfaceMenu.Text);
                    }
                }
                for (int c = 0; c < 16 && r * 16 + c < hexViewBytes.Length; c++)
                {
                    int off = r * 16 + c;
                    byte b = hexViewBytes[off];
                    Rect cr = new(asciiX + c * 8, y, 8, rowHeight);
                    if (LeftClicked(cr, insideHex)) hexSelectedOffset = off;
                    if (off == hexSelectedOffset) InterfaceMenu.Surface(cr, RowSelBg, 3);
                    Cell(cr, b is >= 32 and <= 126 ? ((char)b).ToString() : ".",
                        off == hexSelectedOffset ? InterfaceMenu.Accent : MutedText);
                }
            }
        }
        finally { GUI.EndScrollView(); }
        if (nearby != 0) OpenInHexBrowser(nearby);

        // Right: Live Data Inspector
        Rect rightCard = new(rightX, rightY, rightW, height - rightY);
        ulong selAddr = hexOrigin + (ulong)Math.Clamp(hexSelectedOffset, 0, Math.Max(0, hexViewBytes.Length - 1));
        DrawCard(rightCard, hexViewBytes.Length > 0 ? $"数据解析 · 0x{selAddr:X}" : "数据解析");

        if (hexViewBytes.Length == 0 || hexSelectedOffset >= hexViewBytes.Length)
        {
            InterfaceMenu.Paragraph(new(rightX + 12, rightY + 42, rightW - 24, 50),
                "输入内存地址或在地址表右键“在内存浏览器中查看”。", MutedText, 12);
            return;
        }

        var span = hexViewBytes.AsSpan(hexSelectedOffset);
        float ix = rightX + 12, iw = rightW - 24, iy = rightY + 36;
        void Row(string name, string value)
        {
            InterfaceMenu.Label(new(ix, iy, 64, 20), name, MutedText, 11, translate: false);
            InterfaceMenu.Label(new(ix + 68, iy, iw - 68, 20), value, InterfaceMenu.Text, 11, translate: false);
            iy += 20;
        }

        Row("Byte", $"{span[0]} (0x{span[0]:X2})");
        Row("Int16", span.Length >= 2 ? BinaryPrimitives.ReadInt16LittleEndian(span).ToString(CultureInfo.InvariantCulture) : "-");
        Row("Int32", span.Length >= 4 ? BinaryPrimitives.ReadInt32LittleEndian(span).ToString(CultureInfo.InvariantCulture) : "-");
        Row("UInt32", span.Length >= 4 ? BinaryPrimitives.ReadUInt32LittleEndian(span).ToString(CultureInfo.InvariantCulture) : "-");
        Row("Int64", span.Length >= 8 ? BinaryPrimitives.ReadInt64LittleEndian(span).ToString(CultureInfo.InvariantCulture) : "-");
        Row("Float", span.Length >= 4 ? BitConverter.ToSingle(span[..4]).ToString("G7", CultureInfo.InvariantCulture) : "-");
        Row("Double", span.Length >= 8 ? BitConverter.ToDouble(span[..8]).ToString("G10", CultureInfo.InvariantCulture) : "-");
        int strLen = Math.Min(24, span.Length);
        var sbText = new StringBuilder(strLen);
        for (int i = 0; i < strLen && span[i] != 0; i++)
            sbText.Append(span[i] is >= 32 and <= 126 ? (char)span[i] : '.');
        Row("UTF-8", sbText.Length > 0 ? sbText.ToString() : "-");

        ulong ptrVal = span.Length >= 8 ? BinaryPrimitives.ReadUInt64LittleEndian(span) : 0;
        bool validPtr = ptrVal > 0x10000 && ptrVal < AddressLimit && Query(ptrVal, out var pr) && pr.Readable;
        Row("Pointer", span.Length >= 8 ? $"0x{ptrVal:X}" : "-");

        iy += 4;
        // Quick write controls (all 6 types)
        float tw = (iw - 10) / 3;
        for (int i = 0; i < ValueTypes.Length; i++)
        {
            Rect tr = new(ix + (i % 3) * (tw + 5), iy + (i / 3) * 24, tw, 22);
            if (InterfaceMenu.Button(tr, ValueTypes[i].ToString(), hexWriteType == ValueTypes[i]))
                hexWriteType = ValueTypes[i];
        }
        iy += 50;
        hexWriteInput = InterfaceMenu.Field(new(ix, iy, iw - 72, 26), hexWriteInput);
        if (InterfaceMenu.Button(new(ix + iw - 66, iy, 66, 26), "写入"))
        {
            if (TryParse(hexWriteInput, hexWriteType, out ulong raw))
            {
                var tmp = new byte[8];
                BinaryPrimitives.WriteUInt64LittleEndian(tmp, raw);
                if (WriteRaw(selAddr, tmp[..Size(hexWriteType)]))
                {
                    RefreshHexWindow();
                    SetStatus($"已向 0x{selAddr:X} 写入 {hexWriteInput} ({hexWriteType})");
                }
                else SetStatus("写入失败：目标地址不可写。", true);
            }
            else SetStatus("请输入与所选类型匹配的数值。", true);
        }
        iy += 32;

        float bw = (iw - 6) / 2;
        if (InterfaceMenu.Button(new(ix, iy, bw, 26), "添加到地址表"))
            AddAddress(selAddr, hexWriteType);
        bool oldEn = GUI.enabled;
        GUI.enabled = oldEn && validPtr;
        if (InterfaceMenu.Button(new(ix + bw + 6, iy, bw, 26), "跟随指针"))
            OpenInHexBrowser(ptrVal);
        GUI.enabled = oldEn;
        iy += 30;

        if (InterfaceMenu.Button(new(ix, iy, bw, 26), "监控改写 (DR)"))
            BeginWatch(selAddr, Size(hexWriteType), WatchMode.Write, $"0x{selAddr:X}");
        if (InterfaceMenu.Button(new(ix + bw + 6, iy, bw, 26), "监控访问 (DR)"))
            BeginWatch(selAddr, Size(hexWriteType), WatchMode.Access, $"0x{selAddr:X}");
    }

    static void DrawHexCellMenu(Rect cr, bool insideHex, int off, int size)
    {
        if (RightClicked(cr, insideHex))
        {
            hexSelectedOffset = off;
            ulong address = hexOrigin + (ulong)off;
            int aobLen = Math.Min(16, hexViewBytes.Length - off);
            string aob = aobLen > 0 ? BitConverter.ToString(hexViewBytes, off, aobLen).Replace("-", " ") : "";
            var items = new System.Collections.Generic.List<(string, Action)>
            {
                ("添加到地址表", () => AddAddress(address, hexWriteType)),
                ("监控改写 (DR)", () => BeginWatch(address, size, WatchMode.Write, $"0x{address:X}")),
                ("监控访问 (DR)", () => BeginWatch(address, size, WatchMode.Access, $"0x{address:X}")),
                ("反查指针 (Pointer Scan)", () => BeginPointerScan(address)),
                ("查看附近内存", () => OpenInHexBrowser(address)),
                CopyItem("复制地址", $"0x{address:X}")
            };
            if (aob.Length > 0) items.Add(CopyItem("复制 16B 特征码", aob));
            if (Query(address, out var reg) && reg.ReadableCode)
                items.Add(("在反汇编中查看", () => OpenInDisassembler(address, address)));
            menuItems = items.ToArray();
        }
        else if (LeftClicked(cr, insideHex))
            hexSelectedOffset = off;
    }

    static void DrawStructDissectPage(float width, float height)
    {
        bool prev = GUI.enabled;
        GUI.enabled = prev && structHistory.Count > 0;
        if (InterfaceMenu.Button(new(10, 42, 86, 26), "◀ 返回上级"))
            PopStructHistory();
        GUI.enabled = prev;

        InterfaceMenu.Label(new(104, 42, 36, 26), "大小", MutedText, 11);
        structSizeInput = InterfaceMenu.Field(new(142, 42, 54, 26), structSizeInput);
        if (InterfaceMenu.Button(new(202, 42, 58, 26), $"步长:{structStride}B"))
        {
            structStride = structStride == 8 ? 4 : 8;
            RefreshStructDissect(resetBaseline: true);
        }
        if (InterfaceMenu.Button(new(266, 42, 86, 26), "重置基准"))
            RefreshStructDissect(resetBaseline: true);

        float bodyY = 86, bodyH = height - bodyY;
        DrawCard(new(0, bodyY, width, bodyH), hexOrigin != 0 ? $"结构体剖析 · 0x{hexOrigin:X}" : "结构体剖析");

        InterfaceMenu.Surface(new(8, bodyY + 32, width - 16, 24), RowAltBg, 4);
        InterfaceMenu.Label(new(14, bodyY + 32, 62, 24), "偏移", MutedText, 11);
        InterfaceMenu.Label(new(78, bodyY + 32, 164, 24), "Hex", MutedText, 11);
        InterfaceMenu.Label(new(246, bodyY + 32, 94, 24), "Int32", MutedText, 11);
        InterfaceMenu.Label(new(344, bodyY + 32, 94, 24), "Float", MutedText, 11);
        InterfaceMenu.Label(new(442, bodyY + 32, width - 580, 24), "指针 / ASCII", MutedText, 11);

        if (structRows.Length == 0)
        {
            InterfaceMenu.Label(new(16, bodyY + 66, width - 32, 30), "请在上方输入结构体基址并点击“转到”。", MutedText, 12);
            return;
        }

        Rect view = new(8, bodyY + 60, width - 16, Math.Max(40, bodyH - 68));
        float rowW = view.width - 18;
        structScroll = GUI.BeginScrollView(view, structScroll, new Rect(0, 0, rowW, structRows.Length * RowHeight));
        try
        {
            int first = Math.Clamp((int)(structScroll.y / RowHeight), 0, structRows.Length);
            int end = Math.Min(structRows.Length, (int)Math.Ceiling((structScroll.y + view.height) / RowHeight));
            for (int i = first; i < end; i++)
            {
                var r = structRows[i];
                float y = i * RowHeight;
                if (r.Changed) InterfaceMenu.Surface(new(0, y, rowW, RowHeight - 1), PatchedBg, 4);
                else if (i % 2 == 1) InterfaceMenu.Surface(new(0, y, rowW, RowHeight - 1), RowAltBg, 4);

                Color valCol = r.Changed ? PatchAccent : InterfaceMenu.Text;
                InterfaceMenu.Label(new(6, y, 60, RowHeight), $"+0x{r.Offset:X3}", InterfaceMenu.Accent, 11, translate: false);
                InterfaceMenu.Label(new(70, y, 166, RowHeight), r.HexBytes, MutedText, 11, translate: false);
                InterfaceMenu.Label(new(240, y, 94, RowHeight), r.Int32Value.ToString(CultureInfo.InvariantCulture), valCol, 11, translate: false);
                InterfaceMenu.Label(new(338, y, 94, RowHeight), r.FloatValue.ToString("G5", CultureInfo.InvariantCulture), valCol, 11, translate: false);
                string tail = r.PointerTarget != 0 ? $"→ {r.PointerLabel}" : r.AsciiPreview;
                InterfaceMenu.Label(new(436, y, rowW - 566, RowHeight), tail,
                    r.PointerTarget != 0 ? InterfaceMenu.Accent : MutedText, 11, translate: false);

                if (r.PointerTarget != 0 && InterfaceMenu.Button(new(rowW - 122, y + 1, 54, RowHeight - 3), "钻取"))
                    DrillIntoStruct(r.PointerTarget, $"+0x{r.Offset:X}");
                if (InterfaceMenu.Button(new(rowW - 62, y + 1, 56, RowHeight - 3), "+ 添加"))
                    AddAddress(r.Address, MemoryValueType.Float);
            }
        }
        finally { GUI.EndScrollView(); }
    }
}
