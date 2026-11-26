using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using UnityEngine;

namespace LocalSpt5Esp;

internal static class ProfileJsonEditorPanel
{
    const int PageSize = 60;
    const int MaxSearchResults = 160;
    const int MaxInlineChars = 14000;
    const int LargePreviewChars = 6000;
    const string EditorControl = "LocalProfileJsonEditorArea";

    static readonly JsonSerializerOptions PrettyOptions = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    static readonly JsonDocumentOptions LooseParseOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    sealed record TreeRow(
        string Path,
        string Label,
        string Tag,
        int Depth,
        bool Expandable,
        bool Expanded,
        bool Locked,
        int PageIndex = -1,
        int PageCount = 0,
        int TotalChildren = 0);

    sealed record SearchHit(string Path, string Display, string Purpose);

    static readonly HashSet<string> expanded = new(StringComparer.Ordinal) { "$", "$.characters", "$.characters.pmc" };
    static readonly Dictionary<string, int> pages = new(StringComparer.Ordinal);
    static readonly Dictionary<string, string> entityCache = new(StringComparer.Ordinal);
    static List<TreeRow> treeRows = new();
    static List<SearchHit> searchHits = new();

    static JsonObject? rootNode;
    static Task<string>? request;
    static Task<string?>? dialogTask;
    static string dialogMode = "", pendingAction = "";
    static string loadedOwner = "", session = "", profilePath = "", nickname = "", revision = "";
    static int profileBytes;
    static bool requested, ready, draining, treeDirty = true;

    static string selectedPath = "$.characters.pmc.Info";
    static string? pendingSelectPath, pendingTogglePath;
    static (string Path, int NextPage)? pendingPageChange;
    static bool pendingJumpMod;

    static string searchQuery = "", lastSearchQuery = "";
    static bool lastEnglish;
    static string originalText = "", draftText = "";
    static bool isOversizeNode, addingChild;
    static string newChildKey = "", newChildValue = "";
    static string confirmDeletePath = "";
    static string notice = "";
    static bool noticeError;

    static Vector2 treeScroll, codeScroll;
    static GUIStyle? codeStyle;
    static string measuredText = "";
    static float measuredWidth = -1, measuredHeight;

    static Color CardBg => Color.Lerp(InterfaceMenu.Background, InterfaceMenu.Text, .045f);
    static Color RowHoverBg => new(1, 1, 1, .06f);
    static Color RowSelectedBg => new(InterfaceMenu.Accent.r, InterfaceMenu.Accent.g, InterfaceMenu.Accent.b, .20f);
    static Color MutedText => Color.Lerp(InterfaceMenu.Text, InterfaceMenu.Background, .38f);
    static Color WarnColor => new(.98f, .46f, .42f);

    static readonly (string Label, string Path)[] QuickJumps =
    [
        ("根级", "$"),
        ("PMC 资料", "$.characters.pmc.Info"),
        ("PMC 仓库", "$.characters.pmc.Inventory"),
        ("技能", "$.characters.pmc.Skills"),
        ("任务", "$.characters.pmc.Quests"),
        ("商人", "$.characters.pmc.TradersInfo"),
        ("藏身处", "$.characters.pmc.Hideout"),
        ("Scav", "$.characters.scav"),
        ("MOD 扩展", "@mod")
    ];

    static readonly string[] QuestStatusLabels =
    [
        "未解锁(0)", "可接取(1)", "进行中(2)", "可交付(3)", "已完成(4)",
        "已失败(5)", "可重试(6)", "失败(7)", "已过期(8)", "等待冷却(9)"
    ];

    internal static bool PrepareForReload()
    {
        draining = true;
        return request == null && dialogTask == null;
    }

    internal static void Reset()
    {
        request = null;
        dialogTask = null;
        dialogMode = pendingAction = "";
        rootNode = null;
        loadedOwner = session = profilePath = nickname = revision = "";
        profileBytes = 0;
        requested = ready = draining = false;
        treeDirty = true;
        treeRows.Clear();
        searchHits.Clear();
        entityCache.Clear();
        pages.Clear();
        expanded.Clear();
        expanded.Add("$");
        expanded.Add("$.characters");
        expanded.Add("$.characters.pmc");
        selectedPath = "$.characters.pmc.Info";
        pendingSelectPath = pendingTogglePath = null;
        pendingPageChange = null;
        pendingJumpMod = false;
        searchQuery = lastSearchQuery = "";
        originalText = draftText = "";
        isOversizeNode = addingChild = false;
        newChildKey = newChildValue = confirmDeletePath = notice = "";
        noticeError = false;
        treeScroll = codeScroll = Vector2.zero;
        codeStyle = null;
        measuredText = "";
        measuredWidth = -1;
    }

    static void SetNotice(string message, bool error = false)
    {
        notice = message;
        noticeError = error;
    }

    static void BeginRead()
    {
        if (request != null || draining) return;
        if (ItemSpawner.InRaid())
        {
            SetNotice("请退出战局后再使用存档 JSON 编辑器。", true);
            return;
        }
        pendingAction = "read";
        loadedOwner = ScavEquipmentSync.Owner;
        request = CharacterDataPanel.Call("/local/gear/data", "characterData", new() { { "action", "profileJsonRead" } });
        SetNotice("正在读取角色存档 JSON…");
    }

    static void BeginSave(string targetPath, string? jsonPayload, bool deleteNode = false, string? childKey = null)
    {
        if (request != null || dialogTask != null || draining) return;
        if (ItemSpawner.InRaid())
        {
            SetNotice("请退出战局后再修改存档。", true);
            return;
        }
        pendingAction = deleteNode ? "delete:" + targetPath : childKey != null ? "add:" + targetPath : "save:" + targetPath;
        confirmDeletePath = "";
        var payload = new Dictionary<string, object?>
        {
            ["action"] = "profileJsonSave",
            ["id"] = targetPath,
            ["revision"] = revision,
            ["deleteNode"] = deleteNode
        };
        if (childKey != null) payload["childKey"] = childKey;
        if (!deleteNode && jsonPayload != null) payload["preview"] = jsonPayload;
        request = CharacterDataPanel.Call("/local/gear/data", "characterData", payload);
        SetNotice("正在保存…");
    }

    internal static void Tick()
    {
        PollDialog();
        string owner = ScavEquipmentSync.Owner;
        if (ready && owner.Length > 0 && loadedOwner.Length > 0 && owner != loadedOwner)
        {
            Reset();
            return;
        }

        if (request is not { IsCompleted: true } completed) return;
        request = null;

        try
        {
            using var doc = JsonDocument.Parse(completed.GetAwaiter().GetResult());
            var root = doc.RootElement;
            if (root.TryGetProperty("err", out var err) && err.TryGetInt32(out int code) && code != 0)
            {
                string msg = root.TryGetProperty("errmsg", out var m) ? m.GetString() ?? "操作失败。" : "操作失败。";
                if (msg == "未知数据操作。")
                    msg = "请先点击「重载服务模块」以启用新版存档 JSON 编辑器接口。";
                SetNotice(msg, true);
                return;
            }

            var data = CharacterDataPanel.Data(root);
            if (data.TryGetProperty("profile", out var profileElem))
            {
                rootNode = JsonNode.Parse(profileElem.GetRawText()) as JsonObject;
                session = data.TryGetProperty("session", out var s) ? s.GetString() ?? "" : "";
                profilePath = data.TryGetProperty("profilePath", out var p) ? p.GetString() ?? "" : "";
                nickname = data.TryGetProperty("nickname", out var n) ? n.GetString() ?? "" : "";
                revision = data.TryGetProperty("revision", out var r) ? r.GetString() ?? "" : "";
                profileBytes = data.TryGetProperty("bytes", out var b) ? b.GetInt32() : 0;
                loadedOwner = ScavEquipmentSync.Owner;
                ready = rootNode != null;
                entityCache.Clear();
                treeDirty = true;
                lastSearchQuery = "\0";

                if (pendingAction.StartsWith("delete:", StringComparison.Ordinal))
                {
                    selectedPath = ParentOf(selectedPath);
                    ExpandAncestors(selectedPath);
                }
                else if (pendingAction.StartsWith("add:", StringComparison.Ordinal) && addingChild)
                {
                    if (newChildKey.Length > 0 && ReadAt(rootNode, AppendKey(selectedPath, newChildKey)) != null)
                    {
                        expanded.Add(selectedPath);
                        selectedPath = AppendKey(selectedPath, newChildKey);
                    }
                    addingChild = false;
                    newChildKey = newChildValue = "";
                }

                if (ReadAt(rootNode, selectedPath) == null)
                    selectedPath = "$";
                LoadSelectionIntoEditor(selectedPath);

                if (data.TryGetProperty("saved", out var savedFlag) && savedFlag.ValueKind == JsonValueKind.True)
                {
                    SetNotice("已保存，重新登录角色生效。");
                }
                else
                {
                    SetNotice("已读取角色存档。");
                }
            }
            else if (pendingAction == "reload")
            {
                requested = false;
                BeginRead();
            }
        }
        catch (Exception ex)
        {
            SetNotice($"服务响应解析失败：{ex.GetBaseException().Message}", true);
        }
    }

    static void PollDialog()
    {
        if (dialogTask is not { IsCompleted: true } done) return;
        string mode = dialogMode;
        dialogTask = null;
        dialogMode = "";
        try
        {
            string? path = done.GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(path)) return;
            if (mode == "export" && rootNode != null)
            {
                string pretty = rootNode.ToJsonString(PrettyOptions) + Environment.NewLine;
                File.WriteAllText(path, pretty, new UTF8Encoding(false));
                SetNotice($"已导出完整存档 JSON：{path}");
            }
            else if (mode == "import")
            {
                string imported = File.ReadAllText(path, Encoding.UTF8);
                var parsed = JsonNode.Parse(imported, documentOptions: LooseParseOptions) as JsonObject
                    ?? throw new ArgumentException("所选文件不是有效的存档 JSON 对象。");
                BeginSave("$", parsed.ToJsonString());
            }
        }
        catch (Exception ex)
        {
            SetNotice($"文件操作失败：{ex.GetBaseException().Message}", true);
        }
    }

    internal static void Draw(Rect rect)
    {
        Tick();
        if (ItemSpawner.InRaid())
        {
            InterfaceMenu.Paragraph(rect, "请退出战局后再使用存档 JSON 编辑器。", InterfaceMenu.Accent, 14);
            return;
        }

        if (!requested)
        {
            requested = true;
            BeginRead();
        }

        ApplyDeferredNavigation();

        if (lastEnglish != I18n.English)
        {
            lastEnglish = I18n.English;
            entityCache.Clear();
            treeDirty = true;
            lastSearchQuery = "\0";
        }

        bool prevEnabled = GUI.enabled;
        bool busy = request != null || dialogTask != null || draining;
        GUI.enabled = prevEnabled && !busy;

        try
        {
            DrawTopBar(new Rect(rect.x, rect.y, rect.width, 32));
            DrawFilePathLine(new Rect(rect.x, rect.y + 34, rect.width, 20));
            DrawSearchAndJumps(new Rect(rect.x, rect.y + 58, rect.width, 66));

            var workspace = new Rect(rect.x, rect.y + 130, rect.width, Math.Max(180, rect.height - 172));
            if (ready && rootNode != null)
            {
                if (treeDirty) RebuildTreeRows();
                UpdateSearchHits();

                float gap = 10;
                float leftW = MathF.Floor((workspace.width - gap) * 0.43f);
                Rect leftRect = new(workspace.x, workspace.y, leftW, workspace.height);
                Rect rightRect = new(workspace.x + leftW + gap, workspace.y, workspace.width - leftW - gap, workspace.height);

                DrawTreePane(leftRect);
                DrawInspectorPane(rightRect);
            }
            else
            {
                InterfaceMenu.Surface(workspace, CardBg, 9);
                InterfaceMenu.Paragraph(
                    new Rect(workspace.x + 16, workspace.y + 16, workspace.width - 32, 80),
                    busy ? "正在连接本地服务端读取角色存档…" : "尚未载入角色存档。可点击右上角「重新读取」或「重载服务模块」。",
                    MutedText, 14);
            }
        }
        finally
        {
            GUI.enabled = prevEnabled;
        }

        var footer = new Rect(rect.x, rect.yMax - 38, rect.width, 36);
        InterfaceMenu.Surface(footer, CardBg, 7);
        InterfaceMenu.Paragraph(
            new Rect(footer.x + 10, footer.y + 3, footer.width - 20, 30),
            notice,
            noticeError ? WarnColor : InterfaceMenu.Accent,
            12);
    }

    static void ApplyDeferredNavigation()
    {
        if (pendingJumpMod && rootNode != null)
        {
            pendingJumpMod = false;
            string target = rootNode.Select(p => p.Key).FirstOrDefault(k => k.StartsWith("local", StringComparison.OrdinalIgnoreCase)) is { } modKey
                ? AppendKey("$", modKey)
                : "$.spt";
            SelectNode(target);
        }
        if (pendingTogglePath is { } toggle)
        {
            pendingTogglePath = null;
            if (!expanded.Remove(toggle)) expanded.Add(toggle);
            treeDirty = true;
        }
        if (pendingPageChange is { } pg)
        {
            pendingPageChange = null;
            pages[pg.Path] = pg.NextPage;
            treeDirty = true;
        }
        if (pendingSelectPath is { } next)
        {
            pendingSelectPath = null;
            SelectNode(next);
        }
    }

    static void SelectNode(string path)
    {
        if (rootNode == null || ReadAt(rootNode, path) == null) return;
        ExpandAncestors(path);
        selectedPath = path;
        addingChild = false;
        confirmDeletePath = "";
        LoadSelectionIntoEditor(path);
        treeDirty = true;
    }

    static void ExpandAncestors(string path)
    {
        if (rootNode == null) return;
        expanded.Add("$");
        var segments = ParseSegments(path);
        string cur = "$";
        JsonNode? node = rootNode;
        for (int i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            if (node is JsonArray arr && seg.IsIndex)
            {
                if (arr.Count > PageSize) pages[cur] = seg.Index / PageSize;
                node = seg.Index >= 0 && seg.Index < arr.Count ? arr[seg.Index] : null;
            }
            else if (node is JsonObject obj && !seg.IsIndex)
            {
                if (obj.Count > PageSize)
                {
                    int idx = 0;
                    foreach (var kv in obj)
                    {
                        if (kv.Key == seg.Key) { pages[cur] = idx / PageSize; break; }
                        idx++;
                    }
                }
                obj.TryGetPropertyValue(seg.Key, out node);
            }
            if (i < segments.Count - 1)
            {
                cur = seg.IsIndex ? $"{cur}[{seg.Index}]" : AppendKey(cur, seg.Key);
                expanded.Add(cur);
            }
        }
    }

    static void LoadSelectionIntoEditor(string path)
    {
        var node = ReadAt(rootNode, path);
        codeScroll = Vector2.zero;
        measuredText = "";
        measuredWidth = -1;
        if (node == null)
        {
            originalText = draftText = "null";
            isOversizeNode = false;
            return;
        }

        // Estimate serialized size before pretty-printing huge subtrees
        string raw = node.ToJsonString();
        if (raw.Length > MaxInlineChars)
        {
            isOversizeNode = true;
            string preview = FormatTruncatedPreview(node, LargePreviewChars);
            originalText = draftText = LocalSpt.Shared.JsonEditPath.EditorText(preview);
            return;
        }

        string formatted = node.ToJsonString(PrettyOptions);
        if (formatted.Length > MaxInlineChars)
        {
            isOversizeNode = true;
            originalText = draftText = formatted[..LargePreviewChars] + "\n/* ... */";
            return;
        }

        isOversizeNode = false;
        originalText = draftText = formatted;
    }

    static string FormatTruncatedPreview(JsonNode node, int maxChars)
    {
        var sb = new StringBuilder();
        if (node is JsonObject obj)
        {
            sb.AppendLine("{");
            int shown = 0;
            foreach (var kv in obj)
            {
                string summary = ValueSummary(kv.Value);
                var desc = ProfileFieldCatalog.Describe(AppendKey(selectedPath, kv.Key), I18n.English);
                sb.Append("  \"").Append(kv.Key).Append("\": ").Append(summary)
                  .Append("   // ").AppendLine(desc.Title);
                if (sb.Length >= maxChars || ++shown >= 80)
                {
                    if (shown < obj.Count) sb.AppendLine($"  // ... 其余 {obj.Count - shown} 项");
                    break;
                }
            }
            sb.Append('}');
        }
        else if (node is JsonArray arr)
        {
            sb.AppendLine($"[ // 共 {arr.Count} 项");
            int limit = Math.Min(arr.Count, 25);
            for (int i = 0; i < limit; i++)
            {
                string itemPath = $"{selectedPath}[{i}]";
                string entity = ResolveEntityLabel(itemPath, arr[i]);
                sb.Append($"  [{i}] ").Append(ValueSummary(arr[i]));
                if (entity.Length > 0) sb.Append("   // ").Append(entity);
                sb.AppendLine();
                if (sb.Length >= maxChars) break;
            }
            if (arr.Count > limit) sb.AppendLine($"  // ... 另有 {arr.Count - limit} 项");
            sb.Append(']');
        }
        return sb.ToString();
    }

    static void DrawTopBar(Rect r)
    {
        const float btnReload = 112, btnRead = 84, btnExport = 88, btnImport = 88, gap = 6;
        float rightTotal = btnReload + btnRead + btnExport + btnImport + gap * 3;
        string header = ready
            ? I18n.Text($"存档 JSON 编辑器 · {(nickname.Length > 0 ? nickname : session)} · {Math.Max(1, profileBytes / 1024)} KB · 修订 {(revision.Length >= 8 ? revision[..8] : revision)}")
            : I18n.Text("存档 JSON 编辑器");
        InterfaceMenu.Label(new Rect(r.x, r.y, Math.Max(120, r.width - rightTotal - 10), 30), header, InterfaceMenu.Accent, 17, translate: false);

        float x = r.xMax - rightTotal;
        if (InterfaceMenu.Button(new Rect(x, r.y, btnReload, 30), "重载服务模块"))
        {
            pendingAction = "reload";
            request = CharacterDataPanel.Call("/local/gear/reload", "adminHotReload", new() { { "action", "reload" } });
            SetNotice("正在热重载服务端管理模块…");
        }
        x += btnReload + gap;
        if (InterfaceMenu.Button(new Rect(x, r.y, btnRead, 30), "重新读取"))
            BeginRead();
        x += btnRead + gap;
        bool old = GUI.enabled;
        GUI.enabled = old && ready && rootNode != null;
        if (InterfaceMenu.Button(new Rect(x, r.y, btnExport, 30), "导出整档"))
        {
            dialogMode = "export";
            string initialDir = profilePath.Length > 0 ? Path.GetDirectoryName(profilePath) ?? "" : "";
            string defaultName = (session.Length > 0 ? session : "profile") + ".edited.json";
            dialogTask = WindowsDialogs.PickSaveFile(I18n.Text("导出完整存档 JSON"), "json", defaultName, initialDirectory: initialDir);
        }
        x += btnExport + gap;
        if (InterfaceMenu.Button(new Rect(x, r.y, btnImport, 30), "导入整档"))
        {
            dialogMode = "import";
            string initialDir = profilePath.Length > 0 ? Path.GetDirectoryName(profilePath) ?? "" : "";
            dialogTask = WindowsDialogs.PickOpenFile(I18n.Text("导入存档 JSON"), initialDirectory: initialDir);
        }
        GUI.enabled = old;
    }

    static void DrawFilePathLine(Rect r)
    {
        if (profilePath.Length == 0) return;
        InterfaceMenu.Label(
            new Rect(r.x, r.y, r.width - 82, 20),
            I18n.Text($"当前文件：{profilePath}"),
            MutedText, 11, translate: false);
        if (InterfaceMenu.Button(new Rect(r.xMax - 76, r.y - 1, 76, 22), "复制路径"))
        {
            GUIUtility.systemCopyBuffer = profilePath;
            SetNotice("已复制存档文件路径。");
        }
    }

    static void DrawSearchAndJumps(Rect r)
    {
        const float clearW = 64;
        GUI.SetNextControlName("LocalProfileJsonSearch");
        string nextSearch = InterfaceMenu.Field(new Rect(r.x, r.y, r.width - clearW - 6, 30), searchQuery);
        if (nextSearch != searchQuery)
        {
            searchQuery = nextSearch;
            treeScroll = Vector2.zero;
        }
        if (searchQuery.Length == 0 && GUI.GetNameOfFocusedControl() != "LocalProfileJsonSearch")
            InterfaceMenu.Label(new Rect(r.x + 10, r.y, r.width - clearW - 24, 30), "搜索字段名、中文用途说明、物品／任务／商人名称或模板 ID…", MutedText, 12);
        if (InterfaceMenu.Button(new Rect(r.xMax - clearW, r.y, clearW, 30), "清空"))
        {
            searchQuery = "";
            GUI.FocusControl(null);
        }

        float jy = r.y + 36;
        float chipW = (r.width - (QuickJumps.Length - 1) * 5) / QuickJumps.Length;
        for (int i = 0; i < QuickJumps.Length; i++)
        {
            var jump = QuickJumps[i];
            bool active = jump.Path != "@mod" && (selectedPath == jump.Path || (jump.Path != "$" && selectedPath.StartsWith(jump.Path, StringComparison.Ordinal)));
            if (InterfaceMenu.Button(new Rect(r.x + i * (chipW + 5), jy, chipW, 27), jump.Label, active))
            {
                searchQuery = "";
                if (jump.Path == "@mod") pendingJumpMod = true;
                else pendingSelectPath = jump.Path;
            }
        }
    }

    static void DrawTreePane(Rect r)
    {
        InterfaceMenu.Surface(r, CardBg, 9);
        bool searching = !string.IsNullOrWhiteSpace(searchQuery);
        string title = searching
            ? I18n.Text($"搜索结果（{searchHits.Count} 项）")
            : I18n.Text($"存档结构树（已展开 {treeRows.Count} 行）");
        InterfaceMenu.Label(new Rect(r.x + 10, r.y + 6, r.width - 100, 24), title, InterfaceMenu.Accent, 13, translate: false);
        if (!searching && InterfaceMenu.Button(new Rect(r.xMax - 86, r.y + 5, 78, 24), "收起下级"))
        {
            expanded.Clear();
            expanded.Add("$");
            expanded.Add("$.characters");
            expanded.Add("$.characters.pmc");
            treeDirty = true;
        }

        var view = new Rect(r.x + 6, r.y + 34, r.width - 12, r.height - 40);
        const float rowH = 30f;
        int totalCount = searching ? searchHits.Count : treeRows.Count;
        float contentH = Math.Max(view.height - 1, totalCount * rowH + 6);
        float innerW = view.width - 18;

        treeScroll = GUI.BeginScrollView(view, treeScroll, new Rect(0, 0, innerW, contentH));
        try
        {
            int first = Math.Max(0, (int)(treeScroll.y / rowH) - 1);
            int last = Math.Min(totalCount, (int)((treeScroll.y + view.height) / rowH) + 2);

            if (searching)
            {
                if (searchHits.Count == 0)
                    InterfaceMenu.Paragraph(new Rect(8, 8, innerW - 16, 48), "没有匹配的字段、说明或实体名称。", MutedText, 12);
                for (int i = first; i < last; i++)
                {
                    var hit = searchHits[i];
                    Rect rowRect = new(0, i * rowH, innerW, rowH - 2);
                    bool sel = hit.Path == selectedPath;
                    if (InterfaceMenu.Button(rowRect, "", sel))
                    {
                        searchQuery = "";
                        pendingSelectPath = hit.Path;
                    }
                    float tagW = Math.Min(138, innerW * 0.36f);
                    InterfaceMenu.Label(new Rect(8, rowRect.y, innerW - tagW - 14, rowRect.height), hit.Display, sel ? InterfaceMenu.Accent : InterfaceMenu.Text, 12, translate: false);
                    InterfaceMenu.Label(new Rect(innerW - tagW - 4, rowRect.y, tagW, rowRect.height), hit.Purpose, MutedText, 11, translate: false);
                }
            }
            else
            {
                for (int i = first; i < last; i++)
                {
                    var row = treeRows[i];
                    Rect rowRect = new(0, i * rowH, innerW, rowH - 2);
                    float indent = Math.Min(132, row.Depth * 14f);

                    if (row.PageCount > 0)
                    {
                        InterfaceMenu.Surface(rowRect, new Color(InterfaceMenu.Accent.r, InterfaceMenu.Accent.g, InterfaceMenu.Accent.b, .08f), 5);
                        float pagerX = indent + 6;
                        float btnW = 54;
                        bool canPrev = row.PageIndex > 0;
                        bool canNext = row.PageIndex + 1 < row.PageCount;
                        bool prevGui = GUI.enabled;
                        GUI.enabled = prevGui && canPrev;
                        if (InterfaceMenu.Button(new Rect(pagerX, rowRect.y + 2, btnW, rowRect.height - 4), "◀ 上页"))
                            pendingPageChange = (row.Path, row.PageIndex - 1);
                        GUI.enabled = prevGui && canNext;
                        if (InterfaceMenu.Button(new Rect(innerW - btnW - 4, rowRect.y + 2, btnW, rowRect.height - 4), "下页 ▶"))
                            pendingPageChange = (row.Path, row.PageIndex + 1);
                        GUI.enabled = prevGui;
                        string pageInfo = I18n.Text($"第 {row.PageIndex + 1}/{row.PageCount} 页（共 {row.TotalChildren} 项）");
                        InterfaceMenu.Label(new Rect(pagerX + btnW + 8, rowRect.y, innerW - pagerX - btnW * 2 - 18, rowRect.height), pageInfo, InterfaceMenu.Accent, 11, translate: false);
                        continue;
                    }

                    bool selected = row.Path == selectedPath;
                    if (selected)
                        InterfaceMenu.Surface(rowRect, RowSelectedBg, 6);
                    else if (rowRect.Contains(Event.current.mousePosition))
                        InterfaceMenu.Surface(rowRect, RowHoverBg, 6);

                    if (row.Expandable)
                    {
                        if (InterfaceMenu.Button(new Rect(indent + 2, rowRect.y + 2, 24, rowRect.height - 4), row.Expanded ? "▾" : "▸", row.Expanded))
                            pendingTogglePath = row.Path;
                    }
                    else
                    {
                        InterfaceMenu.Label(new Rect(indent + 6, rowRect.y, 20, rowRect.height), row.Locked ? "🔒" : "•", row.Locked ? WarnColor : MutedText, 11, translate: false);
                    }

                    Rect selectArea = new(indent + 28, rowRect.y, Math.Max(40, innerW - indent - 28), rowRect.height);
                    if (GUI.Button(selectArea, "", GUIStyle.none))
                        pendingSelectPath = row.Path;

                    float badgeW = row.Tag.Length > 0 ? Math.Min(132, selectArea.width * 0.36f) : 0;
                    InterfaceMenu.Label(
                        new Rect(selectArea.x + 2, rowRect.y, Math.Max(30, selectArea.width - badgeW - 8), rowRect.height),
                        row.Label,
                        selected ? InterfaceMenu.Accent : InterfaceMenu.Text,
                        12,
                        translate: false);
                    if (badgeW > 24)
                    {
                        InterfaceMenu.Label(
                            new Rect(innerW - badgeW - 4, rowRect.y, badgeW, rowRect.height),
                            row.Tag,
                            selected ? InterfaceMenu.Accent : MutedText,
                            11,
                            translate: false);
                    }
                }
            }
        }
        finally
        {
            GUI.EndScrollView();
        }
    }

    static void DrawInspectorPane(Rect r)
    {
        InterfaceMenu.Surface(r, CardBg, 9);
        float x = r.x + 10, y = r.y + 8, w = r.width - 20;

        var currentNode = ReadAt(rootNode, selectedPath);
        var desc = ProfileFieldCatalog.Describe(selectedPath, I18n.English);
        string entityHint = ResolveEntityDetail(selectedPath, currentNode);

        // Row 1: Selected JSON Path + Up + Copy Path
        const float upW = 64, copyPathW = 76, gap = 6;
        InterfaceMenu.Label(
            new Rect(x, y, Math.Max(80, w - upW - copyPathW - gap * 2), 26),
            selectedPath,
            InterfaceMenu.Accent, 13, translate: false);
        bool prevGui = GUI.enabled;
        GUI.enabled = prevGui && selectedPath != "$";
        if (InterfaceMenu.Button(new Rect(r.xMax - 10 - upW - copyPathW - gap, y, upW, 26), "上级"))
            pendingSelectPath = ParentOf(selectedPath);
        GUI.enabled = prevGui;
        if (InterfaceMenu.Button(new Rect(r.xMax - 10 - copyPathW, y, copyPathW, 26), "复制键路径"))
        {
            GUIUtility.systemCopyBuffer = selectedPath;
            SetNotice("已复制 JSON 路径：" + selectedPath);
        }
        y += 32;

        // Row 2: Field Purpose Identification Card ("顺便标识哪个字段是做什么的")
        float infoH = entityHint.Length > 0 ? 86f : 66f;
        var infoCard = new Rect(x, y, w, infoH);
        InterfaceMenu.Surface(infoCard, Color.Lerp(InterfaceMenu.Background, InterfaceMenu.Accent, .09f), 7);
        string lockPrefix = desc.Locked ? I18n.Text("[只读] ") : "";
        string kindBadge = NodeKindLabel(currentNode);
        InterfaceMenu.Label(
            new Rect(x + 10, y + 5, w - 20, 22),
            $"{lockPrefix}{desc.Title}  ·  {kindBadge}",
            desc.Locked ? WarnColor : InterfaceMenu.Accent,
            13,
            translate: false);
        InterfaceMenu.Paragraph(
            new Rect(x + 10, y + 27, w - 20, 36),
            desc.Detail,
            InterfaceMenu.Text,
            12);
        if (entityHint.Length > 0)
        {
            InterfaceMenu.Label(
                new Rect(x + 10, y + 63, w - 20, 20),
                entityHint,
                InterfaceMenu.Accent,
                11,
                translate: false);
        }
        y += infoH + 8;

        // Row 3: Node Action Toolbar
        bool isContainer = currentNode is JsonObject or JsonArray;
        bool isBool = !desc.Locked && currentNode is JsonValue bv && bv.TryGetValue<bool>(out _);
        bool canDelete = ProfileFieldCatalog.CanDeletePath(selectedPath);

        float toolBtnW = (w - gap * 4) / 5f;
        GUI.enabled = prevGui && !isOversizeNode && !desc.Locked;
        if (InterfaceMenu.Button(new Rect(x, y, toolBtnW, 28), "格式化"))
        {
            try
            {
                var parsed = JsonNode.Parse(draftText, documentOptions: LooseParseOptions);
                draftText = parsed == null ? "null" : parsed.ToJsonString(PrettyOptions);
                SetNotice("已格式化当前节点 JSON。");
            }
            catch (JsonException ex)
            {
                SetNotice($"JSON 语法错误（第 {ex.LineNumber + 1} 行）：{ex.Message}", true);
            }
        }
        GUI.enabled = prevGui;
        if (InterfaceMenu.Button(new Rect(x + (toolBtnW + gap), y, toolBtnW, 28), "复制 JSON"))
        {
            GUIUtility.systemCopyBuffer = isOversizeNode && currentNode != null
                ? currentNode.ToJsonString(PrettyOptions)
                : draftText;
            SetNotice("已将当前节点完整 JSON 复制到剪贴板。");
        }
        GUI.enabled = prevGui && !isOversizeNode && draftText != originalText;
        if (InterfaceMenu.Button(new Rect(x + 2 * (toolBtnW + gap), y, toolBtnW, 28), "还原草稿"))
        {
            draftText = originalText;
            SetNotice("已恢复为当前存档中的原始值。");
        }
        GUI.enabled = prevGui && (isBool || isContainer);
        string fourthLabel = isBool ? "切换布尔值" : (currentNode is JsonArray ? "+ 追加元素" : "+ 新增字段");
        if (InterfaceMenu.Button(new Rect(x + 3 * (toolBtnW + gap), y, toolBtnW, 28), fourthLabel, addingChild))
        {
            if (isBool && currentNode is JsonValue flagVal && flagVal.TryGetValue<bool>(out bool curBool))
            {
                draftText = (!curBool).ToString().ToLowerInvariant();
                BeginSave(selectedPath, draftText);
            }
            else
            {
                addingChild = !addingChild;
                if (addingChild && newChildValue.Length == 0) newChildValue = "null";
            }
        }
        GUI.enabled = prevGui && canDelete;
        bool confirmingDelete = confirmDeletePath == selectedPath;
        if (InterfaceMenu.Button(
            new Rect(x + 4 * (toolBtnW + gap), y, toolBtnW, 28),
            confirmingDelete ? "确认删除" : "删除此项",
            confirmingDelete))
        {
            if (confirmingDelete) BeginSave(selectedPath, null, deleteNode: true);
            else
            {
                confirmDeletePath = selectedPath;
                SetNotice("再次点击确认删除。", true);
            }
        }
        GUI.enabled = prevGui;
        y += 34;

        // Optional inline Add Child / Append Element drawer
        if (addingChild && isContainer)
        {
            var addBox = new Rect(x, y, w, 68);
            InterfaceMenu.Surface(addBox, Color.Lerp(InterfaceMenu.Background, InterfaceMenu.Text, .07f), 7);
            bool toArray = currentNode is JsonArray;
            if (!toArray)
            {
                InterfaceMenu.Label(new Rect(x + 8, y + 6, 62, 26), "新字段名", null, 12);
                newChildKey = InterfaceMenu.Field(new Rect(x + 72, y + 6, 160, 26), newChildKey);
                InterfaceMenu.Label(new Rect(x + 240, y + 6, 56, 26), "JSON 值", null, 12);
                newChildValue = InterfaceMenu.Field(new Rect(x + 298, y + 6, Math.Max(60, w - 306), 26), newChildValue);
            }
            else
            {
                InterfaceMenu.Label(new Rect(x + 8, y + 6, 94, 26), "追加元素 JSON", null, 12);
                newChildValue = InterfaceMenu.Field(new Rect(x + 106, y + 6, Math.Max(60, w - 114), 26), newChildValue);
            }
            if (InterfaceMenu.Button(new Rect(x + 8, y + 36, 132, 26), toArray ? "追加并保存" : "添加并保存", true))
            {
                if (!toArray && string.IsNullOrWhiteSpace(newChildKey))
                    SetNotice("请输入新字段名称。", true);
                else
                {
                    try
                    {
                        _ = JsonNode.Parse(newChildValue.Trim(), documentOptions: LooseParseOptions);
                        BeginSave(selectedPath, newChildValue.Trim(), childKey: toArray ? "[]" : newChildKey.Trim());
                    }
                    catch (JsonException ex)
                    {
                        SetNotice($"新增项 JSON 语法错误：{ex.Message}", true);
                    }
                }
            }
            if (InterfaceMenu.Button(new Rect(x + 148, y + 36, 76, 26), "取消"))
                addingChild = false;
            y += 74;
        }

        // Row 4: Multi-line JSON Code Box
        const float bottomBarH = 38f;
        float editorH = Math.Max(64, r.yMax - y - bottomBarH - 12);
        bool editable = !isOversizeNode && !desc.Locked && GUI.enabled;
        DrawJsonEditorBox(new Rect(x, y, w, editorH), editable, r.Contains(Event.current.mousePosition));
        y += editorH + 8;

        // Row 5: Bottom Validation & Save Bar
        bool dirty = !isOversizeNode && !desc.Locked && draftText != originalText;
        const float checkW = 110;
        float saveW = Math.Min(160, w - checkW - 10);
        GUI.enabled = prevGui && !isOversizeNode;
        if (InterfaceMenu.Button(new Rect(x, y, checkW, 32), "校验 JSON"))
        {
            try
            {
                var parsed = JsonNode.Parse(draftText, documentOptions: LooseParseOptions);
                SetNotice($"JSON 语法有效（类型：{NodeKindLabel(parsed)}）。");
            }
            catch (JsonException ex)
            {
                SetNotice($"JSON 语法错误（第 {ex.LineNumber + 1} 行）：{ex.Message}", true);
            }
        }
        GUI.enabled = prevGui && dirty;
        if (InterfaceMenu.Button(
            new Rect(x + checkW + 8, y, saveW, 32),
            "保存",
            dirty))
        {
            try
            {
                _ = JsonNode.Parse(draftText, documentOptions: LooseParseOptions);
                BeginSave(selectedPath, draftText);
            }
            catch (JsonException ex)
            {
                SetNotice($"无法保存，JSON 语法错误（第 {ex.LineNumber + 1} 行）：{ex.Message}", true);
            }
        }
        GUI.enabled = prevGui;
    }

    static void DrawJsonEditorBox(Rect viewport, bool editable, bool pointerInPane)
    {
        InterfaceMenu.Surface(viewport, Color.Lerp(InterfaceMenu.Background, Color.black, .22f), 7);
        codeStyle ??= new GUIStyle(GUI.skin.textArea)
        {
            fontSize = 13,
            wordWrap = true,
            richText = false,
            padding = new RectOffset(8, 8, 8, 8)
        };
        codeStyle.normal.textColor = editable ? InterfaceMenu.Text : MutedText;
        codeStyle.focused.textColor = InterfaceMenu.Text;

        var bar = GUI.skin.verticalScrollbar;
        float barWidth = Math.Max(18, bar.fixedWidth) + bar.margin.left + bar.margin.right;
        float width = Math.Max(1, viewport.width - barWidth - 4);
        if (measuredText != draftText || Math.Abs(measuredWidth - width) > 0.5f)
        {
            measuredText = draftText;
            measuredWidth = width;
            measuredHeight = codeStyle.CalcHeight(new GUIContent(draftText + "\n"), width) + 14;
        }
        float height = Math.Max(viewport.height, measuredHeight);
        codeScroll.x = 0;
        codeScroll.y = Math.Clamp(codeScroll.y, 0, Math.Max(0, height - viewport.height));

        var ev = Event.current;
        bool focused = GUI.GetNameOfFocusedControl() == EditorControl;
        if (pointerInPane && viewport.Contains(ev.mousePosition) && ev.type == EventType.ScrollWheel)
        {
            codeScroll.y = Math.Clamp(codeScroll.y + ev.delta.y * 42f, 0, Math.Max(0, height - viewport.height));
            ev.Use();
        }
        if (focused && !editable && IsMutatingTextEvent(ev))
            ev.Use();

        bool revealCaret = focused && editable && ev.type == EventType.KeyDown;
        codeScroll = GUI.BeginScrollView(viewport, codeScroll, new Rect(0, 0, width, height), false, true, GUIStyle.none, bar);
        try
        {
            GUI.SetNextControlName(EditorControl);
            string next = JsonEditorText.Accept(GUI.TextArea(new Rect(0, 0, width, height), draftText, MaxInlineChars, codeStyle));
            if (revealCaret)
            {
                var editor = GUIUtility.GetStateObject(Il2CppInterop.Runtime.Il2CppType.Of<TextEditor>(), GUIUtility.keyboardControl)?.TryCast<TextEditor>();
                if (editor != null)
                {
                    var caret = codeStyle.GetCursorPixelPosition(new Rect(0, 0, width, height), new GUIContent(next), Math.Clamp(editor.cursorIndex, 0, next.Length));
                    float lineH = Math.Max(18, codeStyle.lineHeight);
                    if (caret.y < codeScroll.y + 8) codeScroll.y = Math.Max(0, caret.y - 8);
                    else if (caret.y + lineH > codeScroll.y + viewport.height - 8) codeScroll.y = caret.y + lineH - viewport.height + 8;
                }
            }
            if (editable && next != draftText)
                draftText = next;
        }
        finally
        {
            GUI.EndScrollView();
        }
    }

    static bool IsMutatingTextEvent(Event ev)
    {
        if (ev.type is EventType.ValidateCommand or EventType.ExecuteCommand)
            return ev.commandName is "Cut" or "Paste" or "Delete" or "SoftDelete" or "UndoRedoPerformed";
        if (ev.type != EventType.KeyDown) return false;
        if (ev.control || ev.command) return ev.keyCode is KeyCode.V or KeyCode.X or KeyCode.Z or KeyCode.Y;
        return ev.character != '\0' || (ev.shift && ev.keyCode == KeyCode.Insert)
            || ev.keyCode is KeyCode.Backspace or KeyCode.Delete or KeyCode.Return or KeyCode.KeypadEnter;
    }

    static void RebuildTreeRows()
    {
        treeRows.Clear();
        treeDirty = false;
        if (rootNode == null) return;
        BuildNodeRows("$", "$ (存档根)", rootNode, 0);
    }

    static void BuildNodeRows(string path, string keyDisplay, JsonNode? node, int depth)
    {
        var desc = ProfileFieldCatalog.Describe(path, I18n.English);
        bool expandable = node is JsonObject { Count: > 0 } or JsonArray { Count: > 0 };
        bool isOpen = expandable && expanded.Contains(path);
        string entity = ResolveEntityLabel(path, node);
        string summary = ValueSummary(node);
        string label = entity.Length > 0
            ? $"{keyDisplay}: {entity}"
            : $"{keyDisplay}: {summary}";

        treeRows.Add(new TreeRow(path, label, desc.Title, depth, expandable, isOpen, desc.Locked));
        if (!isOpen) return;

        if (node is JsonObject obj)
        {
            int total = obj.Count;
            int pageCount = total > PageSize ? (total + PageSize - 1) / PageSize : 0;
            int pageIdx = pageCount > 0 ? Math.Clamp(pages.GetValueOrDefault(path, 0), 0, pageCount - 1) : 0;
            if (pageCount > 0)
                treeRows.Add(new TreeRow(path, "", "", depth + 1, false, false, false, pageIdx, pageCount, total));

            int start = pageIdx * PageSize;
            int end = pageCount > 0 ? Math.Min(total, start + PageSize) : total;
            int index = 0;
            foreach (var kv in obj)
            {
                if (index >= start && index < end)
                    BuildNodeRows(AppendKey(path, kv.Key), kv.Key, kv.Value, depth + 1);
                index++;
                if (index >= end) break;
            }
        }
        else if (node is JsonArray arr)
        {
            int total = arr.Count;
            int pageCount = total > PageSize ? (total + PageSize - 1) / PageSize : 0;
            int pageIdx = pageCount > 0 ? Math.Clamp(pages.GetValueOrDefault(path, 0), 0, pageCount - 1) : 0;
            if (pageCount > 0)
                treeRows.Add(new TreeRow(path, "", "", depth + 1, false, false, false, pageIdx, pageCount, total));

            int start = pageIdx * PageSize;
            int end = pageCount > 0 ? Math.Min(total, start + PageSize) : total;
            for (int i = start; i < end; i++)
                BuildNodeRows($"{path}[{i}]", $"[{i}]", arr[i], depth + 1);
        }
    }

    static void UpdateSearchHits()
    {
        string q = searchQuery.Trim();
        if (q == lastSearchQuery) return;
        lastSearchQuery = q;
        searchHits.Clear();
        if (q.Length == 0 || rootNode == null) return;

        var terms = MenuSearchRules.Terms(q);
        SearchWalk("$", "$", rootNode, terms, 0);
    }

    static void SearchWalk(string path, string keyName, JsonNode? node, string[] terms, int depth)
    {
        if (searchHits.Count >= MaxSearchResults || depth > 9) return;
        var desc = ProfileFieldCatalog.Describe(path, I18n.English);
        string entity = ResolveEntityLabel(path, node);
        string scalar = node is JsonValue val ? val.ToJsonString() : "";
        string haystack = $"{keyName} {path} {desc.Title} {desc.Detail} {entity} {scalar}";

        if (path != "$" && MenuSearchRules.Matches(terms, haystack))
        {
            string display = entity.Length > 0
                ? $"{ShortenPath(path)} · {entity}"
                : scalar.Length > 0 && scalar.Length <= 36
                    ? $"{ShortenPath(path)} = {scalar}"
                    : ShortenPath(path);
            searchHits.Add(new SearchHit(path, display, desc.Title));
            if (searchHits.Count >= MaxSearchResults) return;
        }

        if (node is JsonObject obj)
        {
            foreach (var kv in obj)
            {
                SearchWalk(AppendKey(path, kv.Key), kv.Key, kv.Value, terms, depth + 1);
                if (searchHits.Count >= MaxSearchResults) return;
            }
        }
        else if (node is JsonArray arr)
        {
            for (int i = 0; i < arr.Count; i++)
            {
                // Avoid diving into every property of 1,400 items if the item summary itself already checks _id/_tpl/name
                var child = arr[i];
                string childPath = $"{path}[{i}]";
                if (child is JsonObject itemObj && itemObj.ContainsKey("_tpl") && itemObj.ContainsKey("_id"))
                {
                    string idStr = itemObj["_id"]?.ToString() ?? "";
                    string tplStr = itemObj["_tpl"]?.ToString() ?? "";
                    string slotStr = itemObj["slotId"]?.ToString() ?? "";
                    string ent = ResolveEntityLabel(childPath, itemObj);
                    string itemHaystack = $"{childPath} {idStr} {tplStr} {slotStr} {ent} {desc.Title}";
                    if (MenuSearchRules.Matches(terms, itemHaystack))
                    {
                        searchHits.Add(new SearchHit(childPath, $"{ShortenPath(childPath)} · {ent}", I18n.Text("物品实例")));
                        if (searchHits.Count >= MaxSearchResults) return;
                    }
                    continue;
                }
                SearchWalk(childPath, $"[{i}]", child, terms, depth + 1);
                if (searchHits.Count >= MaxSearchResults) return;
            }
        }
    }

    static string ShortenPath(string path) =>
        path.StartsWith("$.characters.pmc.", StringComparison.Ordinal) ? "pmc." + path[17..] :
        path.StartsWith("$.characters.scav.", StringComparison.Ordinal) ? "scav." + path[18..] :
        path.StartsWith("$.", StringComparison.Ordinal) ? path[2..] : path;

    static string ResolveEntityLabel(string path, JsonNode? node)
    {
        if (entityCache.TryGetValue(path, out var cached)) return cached;
        string result = ComputeEntityLabel(path, node);
        if (entityCache.Count < 6000) entityCache[path] = result;
        return result;
    }

    static string ComputeEntityLabel(string path, JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            // Inventory / Build / Mail Item
            if (obj["_tpl"] is JsonValue tplVal && tplVal.TryGetValue<string>(out var tpl) && tpl.Length == 24)
            {
                string name = ItemSpawner.TemplateName(tpl);
                int count = obj["upd"]?["StackObjectsCount"] is JsonValue sv && sv.TryGetValue<int>(out int c) ? c : 1;
                string slot = obj["slotId"]?.ToString() ?? "";
                bool fir = obj["upd"]?["SpawnedInSession"] is JsonValue fv && fv.TryGetValue<bool>(out bool f) && f;
                var sb = new StringBuilder(name);
                if (count > 1) sb.Append(" ×").Append(count);
                if (fir) sb.Append(" ✓");
                if (slot.Length > 0 && slot != "hideout") sb.Append(" [").Append(slot).Append(']');
                return sb.ToString();
            }

            // Quest entry
            if (obj["qid"] is JsonValue qidVal && qidVal.TryGetValue<string>(out var qid) && qid.Length > 0)
            {
                string qName = CharacterDataPanel.Name(qid + " name", qid);
                int st = obj["status"] is JsonValue stVal && stVal.TryGetValue<int>(out int s) ? s : -1;
                string stText = st >= 0 && st < QuestStatusLabels.Length ? I18n.Text(QuestStatusLabels[st]) : st.ToString(CultureInfo.InvariantCulture);
                return $"{qName} · {stText}";
            }

            // Skill entry
            if (path.EndsWith("]", StringComparison.Ordinal) && path.Contains(".Skills.Common[", StringComparison.Ordinal)
                && obj["Id"] is JsonValue skId && skId.TryGetValue<string>(out var skillKey))
            {
                string skName = CharacterDataPanel.Name(skillKey, skillKey);
                double prog = obj["Progress"] is JsonValue pv && pv.TryGetValue<double>(out double p) ? p / 100.0 : 0;
                return $"{skName} (Lv.{prog:0.##})";
            }

            // Hideout Area entry
            if (path.EndsWith("]", StringComparison.Ordinal) && path.Contains(".Hideout.Areas[", StringComparison.Ordinal)
                && obj["type"] is JsonValue tVal && tVal.TryGetValue<int>(out int areaType))
            {
                string aKey = "hideout_area_" + areaType + "_name";
                string aName = CharacterDataPanel.Name(aKey, "Area " + areaType);
                int lvl = obj["level"] is JsonValue lv && lv.TryGetValue<int>(out int l) ? l : 0;
                return $"{aName} · Lv.{lvl}";
            }

            // Weapon build
            if (obj["Name"] is JsonValue wbName && wbName.TryGetValue<string>(out var buildName) && obj["Items"] is JsonArray buildItems)
                return $"{buildName} ({buildItems.Count})";

            // Trader standing map entry
            if (path.Contains(".TradersInfo.", StringComparison.Ordinal))
            {
                string tid = LeafKey(path);
                string tName = CharacterDataPanel.Name(tid + " Nickname", tid);
                int ll = obj["loyaltyLevel"] is JsonValue lv && lv.TryGetValue<int>(out int l) ? l : 1;
                double rep = obj["standing"] is JsonValue rv && rv.TryGetValue<double>(out double r) ? r : 0;
                return $"{tName} · LL{ll} ({rep:0.##})";
            }
        }

        // Dynamic ID keys (dialogues, traderPurchases, Encyclopedia, Achievements)
        if (path.StartsWith("$.dialogues.", StringComparison.Ordinal) && path.Count(c => c == '.') == 2)
        {
            string id = LeafKey(path);
            string tName = id == SharedNw11Id ? "Nw11" : CharacterDataPanel.Name(id + " Nickname", "");
            int msgCount = (node as JsonObject)?["messages"] is JsonArray arr ? arr.Count : 0;
            return tName.Length > 0 ? $"{tName} ({msgCount})" : $"{msgCount} msgs";
        }
        if (path.Contains(".Encyclopedia.", StringComparison.Ordinal))
        {
            string tpl = LeafKey(path);
            if (tpl.Length == 24) return ItemSpawner.TemplateName(tpl);
        }
        if (path.Contains(".Achievements.", StringComparison.Ordinal))
        {
            string ach = LeafKey(path);
            return CharacterDataPanel.Name(ach + " name", "");
        }

        return "";
    }

    const string SharedNw11Id = "68fe15990f29ba3fdbba9d55";

    static string ResolveEntityDetail(string path, JsonNode? node)
    {
        string label = ResolveEntityLabel(path, node);
        if (label.Length > 0)
            return I18n.Text($"游戏内实体识别：{label}");

        if (node is JsonValue val && path.EndsWith("._tpl", StringComparison.Ordinal) && val.TryGetValue<string>(out var tpl) && tpl.Length == 24)
            return I18n.Text($"对应物品模板：{ItemSpawner.TemplateName(tpl)} ({tpl})");
        if (node is JsonValue qVal && path.EndsWith(".qid", StringComparison.Ordinal) && qVal.TryGetValue<string>(out var qid) && qid.Length > 0)
            return I18n.Text($"对应任务名称：{CharacterDataPanel.Name(qid + " name", qid)} ({qid})");

        return "";
    }

    static string ValueSummary(JsonNode? node)
    {
        if (node == null) return "null";
        if (node is JsonObject obj) return $"{{…}} ({obj.Count})";
        if (node is JsonArray arr) return $"[…] ({arr.Count})";
        string raw = node.ToJsonString(PrettyOptions);
        return raw.Length > 34 ? raw[..31] + "…" : raw;
    }

    static string NodeKindLabel(JsonNode? node) => node switch
    {
        null => "null",
        JsonObject o => $"Object ({o.Count})",
        JsonArray a => $"Array [{a.Count}]",
        JsonValue v => v.GetValueKind() switch
        {
            JsonValueKind.String => "String",
            JsonValueKind.Number => "Number",
            JsonValueKind.True or JsonValueKind.False => "Boolean",
            _ => "Value"
        },
        _ => "Node"
    };

    static string AppendKey(string parentPath, string key)
    {
        foreach (char c in key)
            if (c is '.' or '[' or ']' or '"' or '\\' || char.IsWhiteSpace(c))
                return $"{parentPath}[\"{key.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"]";
        return $"{parentPath}.{key}";
    }

    static string ParentOf(string path)
    {
        var segs = ParseSegments(path);
        if (segs.Count <= 1) return "$";
        string cur = "$";
        for (int i = 0; i < segs.Count - 1; i++)
            cur = segs[i].IsIndex ? $"{cur}[{segs[i].Index}]" : AppendKey(cur, segs[i].Key);
        return cur;
    }

    static string LeafKey(string path)
    {
        var segs = ParseSegments(path);
        return segs.Count == 0 ? "$" : segs[^1].IsIndex ? $"[{segs[^1].Index}]" : segs[^1].Key;
    }

    static JsonNode? ReadAt(JsonNode? root, string path)
    {
        if (root == null) return null;
        if (string.IsNullOrEmpty(path) || path == "$") return root;
        var segs = ParseSegments(path);
        JsonNode? cur = root;
        foreach (var s in segs)
        {
            if (cur == null) return null;
            if (s.IsIndex)
            {
                if (cur is not JsonArray arr || s.Index < 0 || s.Index >= arr.Count) return null;
                cur = arr[s.Index];
            }
            else
            {
                if (cur is not JsonObject obj || !obj.TryGetPropertyValue(s.Key, out cur)) return null;
            }
        }
        return cur;
    }

    readonly record struct Seg(bool IsIndex, string Key, int Index);

    static List<Seg> ParseSegments(string path)
    {
        var list = new List<Seg>();
        if (string.IsNullOrEmpty(path) || path[0] != '$') return list;
        int i = 1;
        while (i < path.Length)
        {
            if (path[i] == '.')
            {
                i++;
                int start = i;
                while (i < path.Length && path[i] != '.' && path[i] != '[') i++;
                if (i > start) list.Add(new(false, path[start..i], -1));
            }
            else if (path[i] == '[')
            {
                i++;
                if (i < path.Length && path[i] == '"')
                {
                    i++;
                    var sb = new StringBuilder();
                    while (i < path.Length && path[i] != '"')
                    {
                        if (path[i] == '\\' && i + 1 < path.Length) i++;
                        sb.Append(path[i++]);
                    }
                    while (i < path.Length && path[i] != ']') i++;
                    if (i < path.Length) i++;
                    list.Add(new(false, sb.ToString(), -1));
                }
                else
                {
                    int start = i;
                    while (i < path.Length && path[i] != ']') i++;
                    int.TryParse(path[start..Math.Min(i, path.Length)], out int idx);
                    if (i < path.Length) i++;
                    list.Add(new(true, "", idx));
                }
            }
            else i++;
        }
        return list;
    }
}
