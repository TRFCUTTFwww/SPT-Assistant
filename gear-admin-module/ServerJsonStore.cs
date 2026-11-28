using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using LocalSpt.Shared;

namespace LocalGearMail;

public sealed record ServerJsonCommand(string Action, string Id = "", string Token = "", string Path = "$", int Offset = 0, string Query = "", string? Value = null, bool Delete = false, string? ChildKey = null, bool Append = false);
public sealed class ServerJsonStore(string root, Func<string, string, string?>? modelDoc = null) : IDisposable
{
    public const int PageSize = 60, InlineLimit = 250000, MaxSearchHits = 2000;
    static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, MaxDepth = 1024 };
    readonly object gate = new();
    readonly Dictionary<string, Snapshot> snapshots = new(StringComparer.Ordinal);
    sealed class Snapshot(string owner, string id, byte[] bytes) : IDisposable
    {
        public string Owner = owner, Id = id, Token = Guid.NewGuid().ToString("N"), Revision = Hash(bytes);
        public bool Changed;
        public DateTime Used = DateTime.UtcNow;
        public JsonDocument Document = Parse(bytes);
        public void Dispose() => Document.Dispose();
    }
    static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    static string FileHash(string path) { using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); return Convert.ToHexString(SHA256.HashData(file)); }
    static JsonDocument Parse(byte[] bytes) => JsonDocument.Parse(bytes.AsMemory(bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 }) ? 3 : 0), JsonEditPath.ParseOptions);
    public static bool Allowed(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Contains('\\') || id.Contains(':') || id.StartsWith('/') || !id.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return false;
        string[] parts = id.Split('/');
        if (parts.Any(p => p is "" or "." or ".." || p.TrimEnd(' ', '.') != p)) return false;
        return !parts.Any(p => p.Equals(".git", StringComparison.OrdinalIgnoreCase) || p.Equals("node_modules", StringComparison.OrdinalIgnoreCase));
    }
    string Resolve(string id)
    {
        if (!Allowed(id)) throw new ArgumentException("请选择服务端目录中的有效 JSON 文件路径。");
        string fullRoot = System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
        string current = fullRoot;
        foreach (string part in id.Split('/'))
        {
            current = System.IO.Path.Combine(current, part);
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("不支持编辑链接指向的 JSON 文件。");
        }
        string path = System.IO.Path.GetFullPath(current);
        if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("JSON 文件路径无效。");
        return path;
    }
    public object Execute(string owner, ServerJsonCommand cmd, CancellationToken ct = default)
    {
        lock (gate)
        {
            ct.ThrowIfCancellationRequested();
            if (cmd.Action == "serverJsonList") return new { ok = true, files = Files().ToArray() };
            if (cmd.Action == "serverJsonOpen")
            {
                string path = Resolve(cmd.Id);
                byte[] bytes = File.ReadAllBytes(path);
                var next = new Snapshot(owner, cmd.Id, bytes);
                foreach (var old in snapshots.Values.Where(s => s.Owner == owner).ToArray()) { snapshots.Remove(old.Token); old.Dispose(); }
                while (snapshots.Count >= 4) { var oldest = snapshots.Values.MinBy(s => s.Used)!; snapshots.Remove(oldest.Token); oldest.Dispose(); }
                snapshots.Add(next.Token, next);
                return View(next, "$", 0);
            }
            if (!snapshots.TryGetValue(cmd.Token, out var snapshot) || snapshot.Owner != owner || snapshot.Id != cmd.Id) throw new ArgumentException("文件浏览会话已失效；草稿仍可复制或导出，请重新打开文件。");
            snapshot.Used = DateTime.UtcNow;
            if (cmd.Action == "serverJsonNode") return View(snapshot, cmd.Path, cmd.Offset);
            if (cmd.Action == "serverJsonCheck")
            {
                try { snapshot.Changed |= FileHash(Resolve(snapshot.Id)) != snapshot.Revision; }
                catch (IOException) { snapshot.Changed = true; }
                return new { ok = true, changed = snapshot.Changed };
            }
            if (cmd.Action == "serverJsonSearch") return Search(snapshot, cmd.Query, ct);
            if (cmd.Action is "serverJsonExport" or "serverJsonLoadFull") return new { ok = true, raw = JsonEditPath.Read(snapshot.Document.RootElement, cmd.Path).GetRawText() };
            if (cmd.Action == "serverJsonBackups") return Backups(snapshot);
            if (cmd.Action == "serverJsonRestore") return Restore(snapshot, cmd.Value, ct);
            if (cmd.Action == "serverJsonSave") return Save(snapshot, cmd, ct);
            throw new ArgumentException("未知服务端 JSON 操作。");
        }
    }
    IEnumerable<object> Files()
    {
        if (!Directory.Exists(root)) yield break;
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true };
        foreach (string path in Directory.EnumerateFiles(root, "*.json", options).Order(StringComparer.OrdinalIgnoreCase))
        {
            string id = System.IO.Path.GetRelativePath(root, path).Replace('\\', '/');
            if (!Allowed(id)) continue;
            if (id.StartsWith("user/mods/LocalGearMail/backups/", StringComparison.OrdinalIgnoreCase)) continue;
            try { Resolve(id); } catch { continue; }
            yield return new { id, bytes = new FileInfo(path).Length, purpose = ServerJsonCatalog.File(id) };
        }
    }
    JsonPurpose Purpose(Snapshot s, string path, JsonElement value) => ServerJsonCatalog.Field(s.Id, path, value.ValueKind, modelDoc);
    object Brief(Snapshot s, string path, string key, JsonElement value) => new { path, key, type = value.ValueKind.ToString(), count = Count(value), summary = Summary(value), purpose = Purpose(s, path, value) };
    static int Count(JsonElement value) => value.ValueKind == JsonValueKind.Object ? value.GetPropertyCount() : value.ValueKind == JsonValueKind.Array ? value.GetArrayLength() : 0;
    static string Summary(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => "{ " + Count(value) + " }",
        JsonValueKind.Array => "[ " + Count(value) + " ]",
        JsonValueKind.String => Short(value.GetString() ?? "", 100),
        _ => Short(value.GetRawText(), 100)
    };
    static string Short(string text, int limit) => text.Length <= limit ? text : text[..limit] + "…";
    sealed class PreviewFullException : Exception { }
    sealed class PreviewStream : Stream
    {
        readonly MemoryStream inner = new();
        public byte[] ToArray() => inner.ToArray();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(ReadOnlySpan<byte> bytes) { if (Length + bytes.Length > InlineLimit) throw new PreviewFullException(); inner.Write(bytes); }
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }
    static string? Preview(JsonElement value)
    {
        try
        {
            using var stream = new PreviewStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, MaxDepth = 1024 });
            value.WriteTo(writer); writer.Flush();
            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (PreviewFullException) { return null; }
    }
    object View(Snapshot s, string path, int offset, bool saved = false, string? backup = null)
    {
        var value = JsonEditPath.Read(s.Document.RootElement, path);
        int count = Count(value);
        offset = Math.Clamp(offset, 0, Math.Max(0, (count - 1) / PageSize * PageSize));
        var rows = new List<object>();
        if (value.ValueKind == JsonValueKind.Object)
            rows.AddRange(value.EnumerateObject().Skip(offset).Take(PageSize).Select(p => Brief(s, JsonEditPath.Key(path, p.Name), p.Name, p.Value)));
        else if (value.ValueKind == JsonValueKind.Array)
            rows.AddRange(value.EnumerateArray().Skip(offset).Take(PageSize).Select((v, i) => Brief(s, JsonEditPath.Index(path, offset + i), "[" + (offset + i) + "]", v)));
        string? raw = Preview(value);
        return new { ok = true, token = s.Token, id = s.Id, path, type = value.ValueKind.ToString(), count, offset, children = rows, raw = raw ?? "", large = raw == null, purpose = Purpose(s, path, value), filePurpose = ServerJsonCatalog.File(s.Id), changed = s.Changed, saved, backup };
    }
    object Search(Snapshot s, string query, CancellationToken ct)
    {
        string[] terms = query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0) return new { ok = true, hits = Array.Empty<object>(), truncated = false };
        var rows = new List<object>();
        void Walk(JsonElement value, string path, string key)
        {
            ct.ThrowIfCancellationRequested();
            if (rows.Count > MaxSearchHits) return;
            var purpose = Purpose(s, path, value);
            string text = path + " " + purpose.TitleZh + " " + purpose.DetailZh + " " + purpose.DetailEn;
            string scalar = value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ValueKind is JsonValueKind.Object or JsonValueKind.Array ? "" : value.GetRawText();
            if (terms.All(t => text.Contains(t, StringComparison.OrdinalIgnoreCase) || scalar.Contains(t, StringComparison.OrdinalIgnoreCase))) rows.Add(Brief(s, path, key, value));
            if (value.ValueKind == JsonValueKind.Object) foreach (var p in value.EnumerateObject()) { if (rows.Count > MaxSearchHits) break; Walk(p.Value, JsonEditPath.Key(path, p.Name), p.Name); }
            else if (value.ValueKind == JsonValueKind.Array) { int i = 0; foreach (var v in value.EnumerateArray()) { if (rows.Count > MaxSearchHits) break; Walk(v, JsonEditPath.Index(path, i), "[" + i++ + "]"); } }
        }
        Walk(s.Document.RootElement, "$", "$");
        return new { ok = true, hits = rows.Take(MaxSearchHits), truncated = rows.Count > MaxSearchHits };
    }
    object Backups(Snapshot s)
    {
        string baseDir = System.IO.Path.Combine(root, "user", "mods", "LocalGearMail", "backups", "server-json");
        if (!Directory.Exists(baseDir)) return new { ok = true, backups = Array.Empty<object>() };
        var list = new List<object>();
        foreach (string dir in Directory.EnumerateDirectories(baseDir).OrderDescending(StringComparer.OrdinalIgnoreCase))
        {
            string candidate = System.IO.Path.Combine(dir, s.Id.Replace('/', System.IO.Path.DirectorySeparatorChar));
            if (!File.Exists(candidate)) continue;
            var info = new FileInfo(candidate);
            list.Add(new { backupId = System.IO.Path.GetFileName(dir), time = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"), bytes = info.Length });
            if (list.Count >= 40) break;
        }
        return new { ok = true, backups = list };
    }
    object Restore(Snapshot s, string? backupId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(backupId) || backupId.Contains('/') || backupId.Contains('\\') || backupId.Contains(':') || backupId.Contains(".."))
            throw new ArgumentException("备份编号无效。");
        string baseDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, "user", "mods", "LocalGearMail", "backups", "server-json"));
        string source = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, backupId, s.Id.Replace('/', System.IO.Path.DirectorySeparatorChar)));
        if (!source.StartsWith(baseDir + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(source))
            throw new ArgumentException("所选备份文件不存在。");
        byte[] restoredBytes = File.ReadAllBytes(source);
        JsonDocument? nextSnapshot = Parse(restoredBytes);
        string targetPath = Resolve(s.Id);
        string temp = targetPath + ".assistant-" + Guid.NewGuid().ToString("N") + ".tmp";
        string safetyId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-restore-" + Guid.NewGuid().ToString("N")[..6];
        string safetyRoot = System.IO.Path.Combine(baseDir, safetyId);
        try
        {
            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.Combine(safetyRoot, s.Id))!);
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(restoredBytes); stream.Flush(true); }
            Resolve(s.Id);
            if (File.Exists(targetPath))
                File.Replace(temp, targetPath, System.IO.Path.Combine(safetyRoot, s.Id));
            else
                File.Move(temp, targetPath, true);
            s.Document.Dispose(); s.Document = nextSnapshot; nextSnapshot = null;
            s.Revision = Hash(restoredBytes);
            s.Changed = false;
            return View(s, "$", 0, true, System.IO.Path.GetRelativePath(root, safetyRoot).Replace('\\', '/'));
        }
        finally { nextSnapshot?.Dispose(); if (File.Exists(temp)) File.Delete(temp); }
    }
    object Save(Snapshot s, ServerJsonCommand cmd, CancellationToken ct)
    {
        string path = Resolve(s.Id);
        string selected = cmd.Delete ? JsonEditPath.Parent(cmd.Path) : cmd.Path;
        // Keep the browse snapshot independent from external edits until an explicit reopen.
        var local = JsonNode.Parse(s.Document.RootElement.GetRawText(), documentOptions: JsonEditPath.ParseOptions);
        local = JsonEditPath.Apply(local, cmd.Path, cmd.Value, cmd.Delete, cmd.ChildKey, cmd.Append);
        byte[] localBytes = Encoding.UTF8.GetBytes(local?.ToJsonString() ?? "null");
        JsonDocument? nextSnapshot = Parse(localBytes);
        string temp = path + ".assistant-" + Guid.NewGuid().ToString("N") + ".tmp";
        string backupId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N")[..8];
        string backupRoot = System.IO.Path.Combine(root, "user", "mods", "LocalGearMail", "backups", "server-json", backupId);
        try
        {
            byte[] original = File.ReadAllBytes(path);
            s.Changed |= Hash(original) != s.Revision;
            var latest = JsonNode.Parse(Encoding.UTF8.GetString(original).TrimStart('\uFEFF'), documentOptions: JsonEditPath.ParseOptions);
            latest = JsonEditPath.Apply(latest, cmd.Path, cmd.Value, cmd.Delete, cmd.ChildKey, cmd.Append);
            byte[] output = Encoding.UTF8.GetBytes((latest?.ToJsonString(Pretty) ?? "null") + "\n");
            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.Combine(backupRoot, s.Id))!);
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(output); stream.Flush(true); }
            Resolve(s.Id);
            // ReplaceFile captures the exact displaced bytes, including an external last-moment write.
            File.Replace(temp, path, System.IO.Path.Combine(backupRoot, s.Id));
            byte[] displaced = File.ReadAllBytes(System.IO.Path.Combine(backupRoot, s.Id));
            if (!displaced.AsSpan().SequenceEqual(original))
            {
                if (File.ReadAllBytes(path).AsSpan().SequenceEqual(output))
                {
                    File.Copy(System.IO.Path.Combine(backupRoot, s.Id), temp);
                    File.Move(temp, path, true);
                }
                s.Changed = true;
                throw new IOException("保存时文件再次更新，未覆盖该更新；草稿已保留，可直接重试保存。");
            }
            byte[] persisted = File.ReadAllBytes(path);
            if (!persisted.AsSpan().SequenceEqual(output)) { s.Changed = true; throw new IOException("保存后文件再次更新；草稿与备份已保留。"); }
            s.Document.Dispose(); s.Document = nextSnapshot; nextSnapshot = null;
            s.Revision = Hash(output);
            return View(s, selected, cmd.Offset, true, System.IO.Path.GetRelativePath(root, backupRoot).Replace('\\', '/'));
        }
        finally { nextSnapshot?.Dispose(); if (File.Exists(temp)) File.Delete(temp); }
    }
    public void Dispose() { lock (gate) { foreach (var s in snapshots.Values) s.Dispose(); snapshots.Clear(); } }
}
