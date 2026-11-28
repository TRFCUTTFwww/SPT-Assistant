using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using LocalSpt.Shared;
using UnityEngine;

namespace LocalSpt5Esp;

internal static class ServerJsonEditorPanel
{
    const int Limit = 250000;
    const string Control = "LocalServerJsonEditorArea";
    static Task<string>? request, check;
    static Task<string?>? dialog;
    static Task? fileWrite;
    static JsonElement node;
    static JsonElement[] files = [], hits = [], backups = [];
    static readonly Dictionary<string, string> drafts = new(StringComparer.Ordinal);
    static readonly List<string> recentFiles = new();
    static string id = "", token = "", checkToken = "", path = "$", owner = "", action = "", dialogMode = "", exportPath = "", confirmRestore = "";
    static string fileQuery = "", query = "", jump = "$", original = "", draft = "", childKey = "", childValue = "null", notice = "";
    static string? nextNode;
    static int nextOffset;
    static bool requested, ready, choosing = true, searching, changed, error, draining, adding, deleting, recentOnly, showingBackups;
    static float nextCheck, lastDraw;
    static Vector2 listScroll, codeScroll, helpScroll, backupScroll;
    static GUIStyle? codeStyle;
    static string measured = "", diffSource = "", diffSummary = "";
    static float measuredWidth, measuredHeight;
    static bool Busy => request != null || dialog != null || fileWrite != null || draining;
    static bool Dirty => draft != original;
        static bool Large => ready && Flag(node, "large") && original.Length == 0 && draft.Length == 0;
    static Color Muted => Color.Lerp(InterfaceMenu.Text, InterfaceMenu.Background, .38f);
    static Color Card => Color.Lerp(InterfaceMenu.Background, InterfaceMenu.Text, .045f);
    static JsonElement Prop(JsonElement e, string name)
    {
        if (e.ValueKind == JsonValueKind.Object && (e.TryGetProperty(name, out var p) || e.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out p))) return p;
        return default;
    }
    static string Str(JsonElement e, string name) => Prop(e, name) is { ValueKind: JsonValueKind.String } p ? p.GetString() ?? "" : "";
    static bool Flag(JsonElement e, string name) => Prop(e, name).ValueKind == JsonValueKind.True;
    static int Num(JsonElement e, string name) => Prop(e, name).TryGetInt32(out int n) ? n : 0;
    static long LongNum(JsonElement e, string name) => Prop(e, name).TryGetInt64(out long n) ? n : 0;
    static string Detail(JsonElement e) => Str(e, I18n.English ? "detailEn" : "detailZh");
    static JsonElement[] Rows(JsonElement e, string name) => Prop(e, name) is { ValueKind: JsonValueKind.Array } p ? p.EnumerateArray().Select(v => v.Clone()).ToArray() : [];
    static void Notice(string text, bool bad = false) { notice = text; error = bad; }
    static void RememberRecent(string fileId)
    {
        if (string.IsNullOrEmpty(fileId)) return;
        recentFiles.Remove(fileId);
        recentFiles.Insert(0, fileId);
        if (recentFiles.Count > 15) recentFiles.RemoveRange(15, recentFiles.Count - 15);
    }
    static string ComputeDiffSummary()
    {
        string key = original.Length + ":" + draft.Length + ":" + draft;
        if (diffSource == key) return diffSummary;
        diffSource = key;
        string[] a = original.Split('\n'), b = draft.Split('\n');
        int firstDiff = 1, minLen = Math.Min(a.Length, b.Length);
        while (firstDiff <= minLen && a[firstDiff - 1] == b[firstDiff - 1]) firstDiff++;
        int deltaChars = draft.Length - original.Length;
        string sign = deltaChars >= 0 ? "+" : "";
        diffSummary = $"草稿变更：原 {a.Length} 行 → 现 {b.Length} 行（{sign}{deltaChars} 字符）· 第 {firstDiff} 行起不同";
        return diffSummary;
    }
    static void KeepDraft() { if (ready) { string key = id + "\n" + path; if (Dirty) drafts[key] = draft; else drafts.Remove(key); } }
    static Dictionary<string, object?> Payload(string op) => new() { ["action"] = op, ["id"] = id, ["jsonToken"] = token, ["jsonPath"] = path };
    static void Begin(string op, Dictionary<string, object?>? payload = null)
    {
        if (Busy || ItemSpawner.InRaid()) return;
        owner = ScavEquipmentSync.Owner;
        action = op;
        request = CharacterDataPanel.Call("/local/gear/data", "characterData", payload ?? Payload(op), SPTushonka.Common.Http.RequestHandler.SessionId);
        Notice(op is "serverJsonSave" or "serverJsonRestore" ? "正在保存…" : "正在读取…");
    }
    internal static bool PrepareForReload() { draining = true; return request == null && check == null && dialog == null && fileWrite == null; }
    internal static void Reset()
    {
        request = check = null; dialog = null; fileWrite = null;
        files = hits = backups = []; node = default; drafts.Clear(); recentFiles.Clear();
        id = token = checkToken = owner = action = dialogMode = exportPath = fileQuery = query = original = draft = childKey = notice = confirmRestore = "";
        path = jump = "$"; childValue = "null"; nextNode = null;
        requested = ready = searching = changed = error = draining = adding = deleting = recentOnly = showingBackups = false; choosing = true;
        nextCheck = lastDraw = 0; listScroll = codeScroll = helpScroll = backupScroll = Vector2.zero; codeStyle = null; measured = diffSource = diffSummary = "";
    }
    static JsonElement Response(Task<string> task)
    {
        using var doc = JsonDocument.Parse(task.GetAwaiter().GetResult());
        var root = doc.RootElement;
        if (Prop(root, "err") is { ValueKind: JsonValueKind.Number } err && err.GetInt32() != 0)
        {
            string msg = Str(root, "errmsg");
            if (msg == "未知数据操作。" || msg == "未知服务端 JSON 操作。") msg = "请先点击右上角“重载管理模块”以启用最新服务端 JSON 功能。";
            throw new InvalidOperationException(msg);
        }
        return CharacterDataPanel.Data(root).Clone();
    }
    internal static void Tick()
    {
        if (owner.Length > 0 && ScavEquipmentSync.Owner.Length > 0 && owner != ScavEquipmentSync.Owner) { Reset(); return; }
        if (check is { IsCompleted: true } doneCheck)
        {
            check = null;
            try { var result = Response(doneCheck); if (checkToken == token) changed |= Flag(result, "changed"); } catch { nextCheck = Time.realtimeSinceStartup + 15; }
        }
        if (fileWrite is { IsCompleted: true } write)
        {
            fileWrite = null;
            try { write.GetAwaiter().GetResult(); Notice("已导出 JSON。"); } catch { Notice("JSON 文件写入失败。", true); }
        }
        if (dialog is { IsCompleted: true } pick)
        {
            dialog = null;
            try
            {
                string? selected = pick.GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(selected))
                {
                    if (dialogMode == "export")
                    {
                        exportPath = selected;
                        if (Dirty) fileWrite = File.WriteAllTextAsync(selected, draft, new UTF8Encoding(false));
                        else Begin("serverJsonExport");
                    }
                    else
                    {
                        string raw = File.ReadAllText(selected);
                        using var parsed = JsonDocument.Parse(raw, JsonEditPath.ParseOptions);
                        draft = JsonEditPath.EditorText(raw); Notice("已导入草稿，保存后写入服务端。");
                    }
                }
            }
            catch (Exception ex) { Notice(ex is JsonException ? "JSON 语法无效。" : "JSON 文件读取失败。", true); }
        }
        if (request is { IsCompleted: true } completed)
        {
            request = null;
            try
            {
                var data = Response(completed);
                if (action == "serverJsonList") { files = Rows(data, "files"); requested = true; Notice("已读取 JSON 文件列表。"); }
                else if (action == "serverJsonSearch") { hits = Rows(data, "hits"); searching = true; listScroll = Vector2.zero; Notice(Flag(data, "truncated") ? "仅显示前 2000 项，请缩小搜索范围。" : "搜索完成。"); }
                else if (action == "serverJsonBackups") { backups = Rows(data, "backups"); showingBackups = true; confirmRestore = ""; backupScroll = Vector2.zero; Notice($"已读取 {backups.Length} 条历史备份。"); }
                else if (action == "serverJsonExport") { fileWrite = File.WriteAllTextAsync(exportPath, Str(data, "raw"), new UTF8Encoding(false)); }
                else if (action == "serverJsonLoadFull")
                {
                    string rawText = Str(data, "raw");
                    try { var parsed = JsonNode.Parse(rawText, documentOptions: JsonEditPath.ParseOptions); rawText = parsed?.ToJsonString(new JsonSerializerOptions { WriteIndented = true, MaxDepth = 1024, NewLine = "\n" }) ?? rawText; } catch { }
                    original = JsonEditPath.EditorText(rawText);
                    draft = drafts.GetValueOrDefault(id + "\n" + path, original);
                    codeScroll = Vector2.zero; measured = diffSource = "";
                    Notice("已加载当前节点完整文本，可直接内联编辑保存。");
                }
                else if (action == "reload") Notice("管理模块已重载，请重新打开文件。");
                else
                {
                    bool opened = action == "serverJsonOpen", restored = action == "serverJsonRestore", saved = Flag(data, "saved");
                    node = data; id = Str(data, "id"); token = Str(data, "token"); path = jump = Str(data, "path");
                    RememberRecent(id);
                    ready = true; choosing = false; adding = deleting = false;
                    if (restored) { showingBackups = false; confirmRestore = ""; foreach (string k in drafts.Keys.Where(k => k.StartsWith(id + "\n", StringComparison.Ordinal)).ToArray()) drafts.Remove(k); }
                    if (opened || restored) { changed = Flag(data, "changed"); searching = false; }
                    else changed |= Flag(data, "changed");
                    original = JsonEditPath.EditorText(Str(data, "raw"));
                    if (saved) { drafts.Remove(id + "\n" + path); draft = original; }
                    else draft = drafts.GetValueOrDefault(id + "\n" + path, original);
                    codeScroll = helpScroll = Vector2.zero; measured = diffSource = "";
                    Notice(restored ? "已从历史备份还原文件，当前前态也已自动留存备份。" : saved ? "已备份并保存；配置通常需重启服务端生效。" : "已读取 JSON 节点。");
                    nextCheck = Time.realtimeSinceStartup + 4;
                }
            }
            catch (Exception ex) { Notice(ex is JsonException ? "服务响应无法读取。" : ex.Message, true); }
        }
        if (!Busy && check == null && ready && !ItemSpawner.InRaid() && Time.realtimeSinceStartup < lastDraw + 2 && Time.realtimeSinceStartup >= nextCheck)
        {
            nextCheck = Time.realtimeSinceStartup + 5;
            checkToken = token;
            check = CharacterDataPanel.Call("/local/gear/data", "characterData", Payload("serverJsonCheck"), SPTushonka.Common.Http.RequestHandler.SessionId);
        }
    }
    static void Select(string target, int offset = 0)
    {
        KeepDraft();
        var payload = Payload("serverJsonNode"); payload["jsonPath"] = target; payload["jsonOffset"] = offset;
        Begin("serverJsonNode", payload); searching = false; listScroll = Vector2.zero;
    }
    static void Save(bool delete = false, bool add = false)
    {
        try
        {
            string value = add ? childValue : draft;
            if (!delete) { using var parsed = JsonDocument.Parse(value, JsonEditPath.ParseOptions); }
            var payload = Payload("serverJsonSave");
            payload["preview"] = value; payload["deleteNode"] = delete;
            if (add) { payload["jsonAppend"] = Str(node, "type") == "Array"; payload["jsonAddField"] = Str(node, "type") == "Object"; payload["childKey"] = childKey; }
            Begin("serverJsonSave", payload);
        }
        catch (JsonException) { Notice("JSON 语法无效。", true); }
    }
    internal static void Draw(Rect rect)
    {
        lastDraw = Time.realtimeSinceStartup;
        Tick();
        if (ItemSpawner.InRaid()) { InterfaceMenu.Paragraph(rect, "请退出战局后再编辑服务端 JSON。", InterfaceMenu.Accent, 14); return; }
        if (!requested && request == null) { requested = true; Begin("serverJsonList"); }
        if (nextNode != null && !Busy) { string next = nextNode; int offset = nextOffset; nextNode = null; nextOffset = 0; Select(next, offset); }
        bool old = GUI.enabled; GUI.enabled = old && !Busy;
        try
        {
            float x = rect.x, y = rect.y, w = rect.width;
            InterfaceMenu.Label(new(x, y, w, 30), "服务端 JSON 编辑器", InterfaceMenu.Accent, 18);y+=34;
            string[] fileActions=ready?["重载管理模块","历史备份","丢弃草稿并刷新","选择文件"]:["重载管理模块","选择文件"];
            int fileAction=ActionButtons.Draw(new(x,y,w,28),fileActions,out float actionHeight);
            if(fileAction==fileActions.Length-1){KeepDraft();choosing=!choosing;searching=false;listScroll=Vector2.zero;}
            if(fileAction==0){action="reload";request=CharacterDataPanel.Call("/local/gear/reload","adminHotReload",new(){["action"]="reload"});}
            if (ready&&fileAction==1)
            {
                if (showingBackups) { showingBackups = false; confirmRestore = ""; }
                else Begin("serverJsonBackups");
            }
            if (ready&&fileAction==2)
            {
                foreach (string key in drafts.Keys.Where(k => k.StartsWith(id + "\n", StringComparison.Ordinal)).ToArray()) drafts.Remove(key);
                Begin("serverJsonOpen");
            }
            y += actionHeight+4;
            InterfaceMenu.Label(new(x, y, w, 22), id, Muted, 11, translate: false); y += 25;
            float purposeHeight = ready ? Math.Min(64, InterfaceMenu.ParagraphHeight(Detail(Prop(node, "filePurpose")), w, 12) + 4) : 24;
            InterfaceMenu.Paragraph(new(x, y, w, purposeHeight), ready ? Detail(Prop(node, "filePurpose")) : "选择要编辑的服务端 JSON 文件。", null, 12); y += purposeHeight + 6;
            if (changed) { InterfaceMenu.Paragraph(new(x, y, w, 34), "此文件自开始编辑以来有更新。", new Color(1, .65f, .30f), 13); y += 36; }
            var workspace = new Rect(x, y, w, Math.Max(120, rect.yMax - y - 43));
            float leftW = MathF.Floor(w * .39f);
            var left = new Rect(x, y, leftW, workspace.height);
            var right = new Rect(x + leftW + 10, y, w - leftW - 10, workspace.height);
            InterfaceMenu.Surface(left, Card, 8); InterfaceMenu.Surface(right, Card, 8);
            if (choosing || !ready) DrawFiles(left); else DrawNodes(left);
            if (ready) { if (showingBackups) DrawBackups(right); else DrawEditor(right); }
        }
        finally { GUI.enabled = old; }
        InterfaceMenu.Paragraph(new(rect.x + 4, rect.yMax - 36, rect.width - 8, 34), notice, error ? new Color(1, .42f, .38f) : InterfaceMenu.Accent, 12);
    }
    static void DrawBackups(Rect r)
    {
        float x = r.x + 10, y = r.y + 8, w = r.width - 20;
        InterfaceMenu.Label(new(x, y, w - 170, 28), "当前文件历史备份", InterfaceMenu.Accent, 15);
        if (InterfaceMenu.Button(new(x + w - 160, y, 76, 28), "刷新备份")) Begin("serverJsonBackups");
        if (InterfaceMenu.Button(new(x + w - 78, y, 78, 28), "返回编辑")) { showingBackups = false; confirmRestore = ""; return; }
        y += 34;
        InterfaceMenu.Paragraph(new(x, y, w, 38), "每次保存前都会完整备份原文件。还原时也会自动将当前磁盘内容另存备份。", Muted, 12);
        y += 42;
        var view = new Rect(x, y, w, Math.Max(40, r.yMax - y - 10));
        backupScroll = GUI.BeginScrollView(view, backupScroll, new(0, 0, w - 18, Math.Max(view.height, backups.Length * 58)));
        string? restoreId = null;
        try
        {
            if (backups.Length == 0) InterfaceMenu.Paragraph(new(8, 8, w - 32, 48), "当前文件暂无历史备份；保存修改后将在此列出。", Muted, 13);
            for (int i = 0; i < backups.Length; i++)
            {
                var b = backups[i]; string bid = Str(b, "backupId"); float top = i * 58;
                InterfaceMenu.Surface(new(0, top, w - 20, 52), Color.Lerp(InterfaceMenu.Background, InterfaceMenu.Text, .035f), 6);
                InterfaceMenu.Label(new(10, top + 5, w - 140, 22), $"{Str(b, "time")}  ·  {LongNum(b, "bytes"):N0} B", InterfaceMenu.Accent, 12, translate: false);
                InterfaceMenu.Label(new(10, top + 27, w - 140, 20), bid, Muted, 10, translate: false);
                bool confirming = confirmRestore == bid;
                if (InterfaceMenu.Button(new(w - 124, top + 11, 96, 30), confirming ? "确认还原" : "还原此版", confirming))
                {
                    if (confirming) restoreId = bid;
                    else { confirmRestore = bid; Notice("再次点击“确认还原”以恢复该备份。"); }
                }
            }
        }
        finally { GUI.EndScrollView(); }
        if (restoreId != null)
        {
            var payload = Payload("serverJsonRestore"); payload["preview"] = restoreId;
            Begin("serverJsonRestore", payload);
        }
    }
    static void DrawFiles(Rect r)
    {
        float x = r.x + 8, y = r.y + 8, w = r.width - 16;
        fileQuery = InterfaceMenu.Field(new(x, y, Math.Max(60, w - 134), 30), fileQuery);
        if (InterfaceMenu.Button(new(x + w - 128, y, 58, 30), "最近", recentOnly)) { recentOnly = !recentOnly; listScroll = Vector2.zero; }
        if (InterfaceMenu.Button(new(x + w - 64, y, 64, 30), "刷新列表")) Begin("serverJsonList");
        y += 36;
        if (fileQuery.Length == 0) InterfaceMenu.Label(new(x + 6, r.y + 8, Math.Max(40, w - 146), 30), "搜索文件或用途…", Muted, 11);
        var queryFiltered = files.Where(f => (!recentOnly || recentFiles.Contains(Str(f, "id"))) && (Str(f, "id") + " " + Detail(Prop(f, "purpose"))).Contains(fileQuery, StringComparison.OrdinalIgnoreCase));
        var filtered = (fileQuery.Length == 0
            ? queryFiltered.OrderBy(f => { int idx = recentFiles.IndexOf(Str(f, "id")); return idx >= 0 ? idx : 1000; })
            : queryFiltered).ToArray();
        float h = 82;
        var view = new Rect(x, y, w, Math.Max(30, r.yMax - y - 8));
        listScroll = GUI.BeginScrollView(view, listScroll, new(0, 0, w - 18, Math.Max(view.height, filtered.Length * h)));
        try
        {
            if (filtered.Length == 0) InterfaceMenu.Paragraph(new(8, 8, w - 32, 48), recentOnly ? "本次会话尚未打开任何文件。" : "没有匹配的 JSON 文件。", Muted, 12);
            int from = Math.Max(0, (int)(listScroll.y / h)), end = Math.Min(filtered.Length, (int)((listScroll.y + view.height) / h) + 2);
            for (int i = from; i < end; i++)
            {
                var f = filtered[i]; string file = Str(f, "id"); float top = i * h;
                bool isRecent = recentFiles.Contains(file);
                if (InterfaceMenu.Button(new(0, top, w - 20, h - 4), "", file == id))
                {
                    KeepDraft(); var payload = Payload("serverJsonOpen"); payload["id"] = file;
                    Begin("serverJsonOpen", payload);
                }
                InterfaceMenu.Label(new(8, top + 3, w - 36, 23), (isRecent ? "★ " : "") + Path.GetFileName(file), InterfaceMenu.Accent, 13, translate: false);
                InterfaceMenu.Label(new(8, top + 27, w - 36, 20), Path.GetDirectoryName(file) ?? "", Muted, 10, translate: false);
                InterfaceMenu.Paragraph(new(8, top + 48, w - 36, 28), Detail(Prop(f, "purpose")), null, 11);
            }
        }
        finally { GUI.EndScrollView(); }
    }
    static void DrawNodes(Rect r)
    {
        float x = r.x + 8, y = r.y + 8, w = r.width - 16;
        if (InterfaceMenu.Button(new(x, y, 54, 28), "根级")) nextNode = "$";
        if (InterfaceMenu.Button(new(x + 60, y, 54, 28), "上级")) nextNode = JsonEditPath.Parent(path);
        if (InterfaceMenu.Button(new(x + 120, y, w - 120, 28), searching ? "返回节点" : "复制键路径")) { if (searching) searching = false; else GUIUtility.systemCopyBuffer = path; }
        y += 34;
        jump = InterfaceMenu.Field(new(x, y, w - 62, 28), jump);
        if (InterfaceMenu.Button(new(x + w - 56, y, 56, 28), "跳转")) { try { JsonEditPath.Parse(jump); nextNode = jump; } catch { Notice("JSON 路径无效。", true); } }
        y += 34;
        query = InterfaceMenu.Field(new(x, y, w - 62, 28), query);
        if (query.Length == 0) InterfaceMenu.Label(new(x + 5, y, w - 76, 28), "搜索字段、值或说明…", Muted, 11);
        if (InterfaceMenu.Button(new(x + w - 56, y, 56, 28), "搜索")) { var payload = Payload("serverJsonSearch"); payload["jsonQuery"] = query; Begin("serverJsonSearch", payload); }
        y += 35;
        var rows = searching ? hits : Rows(node, "children");
        float h = 64, footer = searching ? 8 : 36;
        var view = new Rect(x, y, w, Math.Max(20, r.yMax - y - footer));
        listScroll = GUI.BeginScrollView(view, listScroll, new(0, 0, w - 18, Math.Max(view.height, rows.Length * h)));
        try
        {
            int from = Math.Max(0, (int)(listScroll.y / h)), end = Math.Min(rows.Length, (int)((listScroll.y + view.height) / h) + 2);
            for (int i = from; i < end; i++)
            {
                var row = rows[i]; float top = i * h;
                if (InterfaceMenu.Button(new(0, top, w - 20, h - 4), "")) { nextNode = Str(row, "path"); nextOffset = 0; }
                string title = searching ? Str(row, "path") : Str(row, "key") + "  " + Str(row, "summary");
                InterfaceMenu.Label(new(8, top + 3, w - 36, 24), title, InterfaceMenu.Accent, 12, translate: false);
                InterfaceMenu.Paragraph(new(8, top + 28, w - 36, 29), Detail(Prop(row, "purpose")), Muted, 11);
            }
        }
        finally { GUI.EndScrollView(); }
        if (searching) return;
        int offset = Num(node, "offset"), count = Num(node, "count");
        bool old = GUI.enabled; GUI.enabled = old && offset > 0;
        if (InterfaceMenu.Button(new(x, r.yMax - 32, 60, 26), "◀ 上页")) { nextNode = path; nextOffset = offset - 60; }
        GUI.enabled = old && offset + 60 < count;
        if (InterfaceMenu.Button(new(r.xMax - 68, r.yMax - 32, 60, 26), "下页 ▶")) { nextNode = path; nextOffset = offset + 60; }
        GUI.enabled = old;
        InterfaceMenu.Label(new(x + 66, r.yMax - 32, w - 132, 26), $"{offset / 60 + 1} / {Math.Max(1, (count + 59) / 60)}", Muted, 11, translate: false);
    }
    static void DrawEditor(Rect r)
    {
        float x = r.x + 10, y = r.y + 8, w = r.width - 20;
        InterfaceMenu.Label(new(x, y, w, 23), path, InterfaceMenu.Accent, 12, translate: false); y += 27;
        string help = Detail(Prop(node, "purpose"));
        float infoHeight = Math.Min(92, Math.Max(42, InterfaceMenu.ParagraphHeight(help, w - 18, 12)));
        var helpView = new Rect(x, y, w, infoHeight);
        helpScroll = GUI.BeginScrollView(helpView, helpScroll, new(0, 0, w - 18, Math.Max(infoHeight, InterfaceMenu.ParagraphHeight(help, w - 18, 12))));
        InterfaceMenu.Paragraph(new(0, 0, w - 18, Math.Max(infoHeight, InterfaceMenu.ParagraphHeight(help, w - 18, 12))), help, null, 12); GUI.EndScrollView(); y += infoHeight + 8;
        bool old = GUI.enabled;
        string type = Str(node, "type");
        var operations=new List<(string Label,string Op)>();
        if(Large)operations.Add(("展开完整文本","loadFull"));
        else{operations.Add(("格式化","format"));operations.Add(("还原草稿","reset"));}
        operations.Add(("复制","copy"));operations.Add(("粘贴","paste"));
        operations.Add(("导出节点","export"));operations.Add(("导入节点","import"));
        if(!Dirty&&type is "Object" or "Array")operations.Add((type=="Array"?"+ 追加元素":"+ 新增字段","add"));
        if(!Dirty&&path!="$")operations.Add((deleting?"确认删除":"删除此项","delete"));
        int operation=ActionButtons.Draw(new(x,y,w,28),operations.Select(o=>o.Label).ToArray(),out float operationHeight,4);
        if(operation>=0)switch(operations[operation].Op){
            case "loadFull":action="serverJsonLoadFull";request=CharacterDataPanel.Call("/local/gear/data","characterData",Payload("serverJsonExport"),SPTushonka.Common.Http.RequestHandler.SessionId);Notice("正在读取完整节点文本…");break;
            case "format":try{var value=JsonNode.Parse(draft,documentOptions:JsonEditPath.ParseOptions);draft=value?.ToJsonString(new JsonSerializerOptions{WriteIndented=true,MaxDepth=1024,NewLine="\n"})??"null";}catch{Notice("JSON 语法无效。",true);}break;
            case "reset":draft=original;drafts.Remove(id+"\n"+path);break;
            case "copy":if(draft.Length>0){GUIUtility.systemCopyBuffer=draft;Notice("已复制节点 JSON 到剪贴板。");}break;
            case "paste":{string clip=GUIUtility.systemCopyBuffer??"";if(!string.IsNullOrWhiteSpace(clip)){draft=JsonEditorText.Accept(clip);Notice("已粘贴草稿，点击保存写入服务端。");}}break;
            case "export":dialogMode="export";dialog=WindowsDialogs.PickSaveFile(I18n.Text("导出 JSON 节点"),"json",Path.GetFileNameWithoutExtension(id)+".node.json");break;
            case "import":dialogMode="import";dialog=WindowsDialogs.PickOpenFile(I18n.Text("导入 JSON 节点"));break;
            case "add":adding=!adding;childValue="null";break;
            case "delete":if(deleting)Save(delete:true);else deleting=true;break;
        }
        y+=operationHeight;
        if (adding)
        {
            if (type == "Object") { childKey = InterfaceMenu.Field(new(x, y, w, 28), childKey); y += 32; }
            childValue = InterfaceMenu.Field(new(x, y, w - 90, 28), childValue);
            if (InterfaceMenu.Button(new(x + w - 84, y, 84, 28), "保存新增")) Save(add: true);
            y += 34;
        }
        bool showDiff = Dirty && !Large;
        float foot = showDiff ? 64 : 38;
        var area = new Rect(x, y, w, Math.Max(30, r.yMax - y - foot - 8));
        if (Large) InterfaceMenu.Paragraph(area, Dirty ? "已导入大型节点草稿，点击保存写入。" : "此节点较大，请浏览子节点，或导出后编辑并导入保存。", Muted, 13);
        else DrawCode(area);
        if (showDiff)
            InterfaceMenu.Label(new(x, r.yMax - 62, w, 22), ComputeDiffSummary(), new Color(1, .78f, .38f), 11, translate: false);
        GUI.enabled = old && Dirty;
        if (InterfaceMenu.Button(new(x, r.yMax - 38, Math.Min(190, w * .55f), 30), "保存当前节点", true)) Save();
        GUI.enabled = old && !Large && type is "True" or "False";
        if (InterfaceMenu.Button(new(x + Math.Min(190, w * .55f) + 8, r.yMax - 38, w - Math.Min(190, w * .55f) - 8, 30), "切换布尔值")) draft = draft.Trim() == "true" ? "false" : "true";
        GUI.enabled = old;
    }
    static void DrawCode(Rect viewport)
    {
        codeStyle ??= new GUIStyle(GUI.skin.textArea) { fontSize = 13, wordWrap = true, richText = false, padding = new RectOffset(8, 8, 8, 8) };
        codeStyle.normal.textColor = codeStyle.focused.textColor = InterfaceMenu.Text;
        float width = Math.Max(1, viewport.width - 24);
        if (measured != draft || Math.Abs(measuredWidth - width) > .5f) { measured = draft; measuredWidth = width; measuredHeight = codeStyle.CalcHeight(new GUIContent(draft + "\n"), width) + 14; }
        float height = Math.Max(viewport.height, measuredHeight);
        codeScroll.x = 0;
        codeScroll.y = Math.Clamp(codeScroll.y, 0, Math.Max(0, height - viewport.height));
        var ev = Event.current;
        if (viewport.Contains(ev.mousePosition) && ev.type == EventType.ScrollWheel) { codeScroll.y = Math.Clamp(codeScroll.y + ev.delta.y * 42, 0, Math.Max(0, height - viewport.height)); ev.Use(); }
        bool revealCaret = GUI.GetNameOfFocusedControl() == Control && ev.type == EventType.KeyDown;
        codeScroll = GUI.BeginScrollView(viewport, codeScroll, new(0, 0, width, height), false, true, GUIStyle.none, GUI.skin.verticalScrollbar);
        try
        {
            GUI.SetNextControlName(Control);
            draft = JsonEditorText.Accept(GUI.TextArea(new(0, 0, width, height), draft, codeStyle));
            if (revealCaret)
            {
                var editor = GUIUtility.GetStateObject(Il2CppInterop.Runtime.Il2CppType.Of<TextEditor>(), GUIUtility.keyboardControl)?.TryCast<TextEditor>();
                if (editor != null)
                {
                    var caret = codeStyle.GetCursorPixelPosition(new(0, 0, width, height), new GUIContent(draft), Math.Clamp(editor.cursorIndex, 0, draft.Length));
                    float lineHeight = Math.Max(18, codeStyle.lineHeight);
                    if (caret.y < codeScroll.y + 8) codeScroll.y = Math.Max(0, caret.y - 8);
                    else if (caret.y + lineHeight > codeScroll.y + viewport.height - 8) codeScroll.y = caret.y + lineHeight - viewport.height + 8;
                }
            }
        }
        finally { GUI.EndScrollView(); }
    }
}
