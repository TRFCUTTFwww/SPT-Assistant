using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LocalSpt.Shared;

public static class JsonEditPath
{
    public static string EditorText(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');
    public static int EditorPosition(string text, int position)
    {
        position = Math.Clamp(position, 0, text.Length);
        int result = position;
        for (int i = 0; i < position; i++)
            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') result--;
        return result;
    }
    public readonly record struct Part(string? Key, int Index);
    public static string Key(string path, string key) => path + "[" + JsonSerializer.Serialize(key) + "]";
    public static string Index(string path, int index) => path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
    public static List<Part> Parse(string path)
    {
        if (string.IsNullOrEmpty(path) || path[0] != '$') throw new ArgumentException("JSON 路径须以 $ 开头。");
        var result = new List<Part>();
        for (int i = 1; i < path.Length;)
        {
            if (path[i] == '.')
            {
                int start = ++i;
                while (i < path.Length && path[i] != '.' && path[i] != '[') i++;
                if (i == start) throw new ArgumentException("JSON 路径无效。");
                result.Add(new(path[start..i], -1));
                continue;
            }
            if (path[i++] != '[' || i == path.Length) throw new ArgumentException("JSON 路径无效。");
            int from = i;
            if (path[i] == '"')
            {
                i++;
                bool escaped = false, closed = false;
                while (i < path.Length)
                {
                    char c = path[i++];
                    if (escaped) { escaped = false; continue; }
                    if (c == '\\') { escaped = true; continue; }
                    if (c == '"') { closed = true; break; }
                }
                if (!closed || i >= path.Length || path[i++] != ']') throw new ArgumentException("JSON 路径无效。");
                result.Add(new(JsonSerializer.Deserialize<string>(path[from..(i - 1)])!, -1));
            }
            else
            {
                while (i < path.Length && path[i] != ']') i++;
                if (i >= path.Length || !int.TryParse(path[from..i], NumberStyles.None, CultureInfo.InvariantCulture, out int index)) throw new ArgumentException("JSON 数组下标无效。");
                i++;
                result.Add(new(null, index));
            }
        }
        return result;
    }
    public static string Parent(string path)
    {
        var parts = Parse(path);
        string parent = "$";
        for (int i = 0; i < parts.Count - 1; i++) parent = parts[i].Key is { } key ? Key(parent, key) : Index(parent, parts[i].Index);
        return parent;
    }
    public static JsonElement Read(JsonElement root, string path)
    {
        foreach (var p in Parse(path))
        {
            if (p.Key is { } key && root.ValueKind == JsonValueKind.Object && root.TryGetProperty(key, out var child)) root = child;
            else if (p.Key == null && root.ValueKind == JsonValueKind.Array && p.Index < root.GetArrayLength()) root = root[p.Index];
            else throw new ArgumentException("目标 JSON 节点已不存在。");
        }
        return root;
    }
    public static JsonNode? Apply(JsonNode? root, string path, string? value, bool delete, string? childKey, bool append)
    {
        var parts = Parse(path);
        JsonNode? replacement = delete ? null : JsonNode.Parse(value ?? throw new ArgumentException("未提供 JSON 值。"), documentOptions: ParseOptions);
        JsonNode? node = root, parent = null;
        foreach (var p in parts)
        {
            parent = node;
            if (p.Key is { } key && node is JsonObject obj && obj.TryGetPropertyValue(key, out var next)) node = next;
            else if (p.Key == null && node is JsonArray arr && p.Index < arr.Count) node = arr[p.Index];
            else throw new ArgumentException("目标 JSON 节点已不存在。");
        }
        if (append)
        {
            if (node is not JsonArray array) throw new ArgumentException("只有数组才能追加元素。");
            array.Add(replacement);
        }
        else if (childKey != null)
        {
            if (node is not JsonObject obj) throw new ArgumentException("只有对象才能新增字段。");
            if (obj.ContainsKey(childKey)) throw new ArgumentException("字段已存在。");
            obj.Add(childKey, replacement);
        }
        else if (parts.Count == 0)
        {
            if (delete) throw new ArgumentException("不能删除整个文件；可以替换根节点。");
            return replacement;
        }
        else if (parts[^1].Key is { } key)
        {
            var obj = (JsonObject)parent!;
            if (delete) obj.Remove(key); else obj[key] = replacement;
        }
        else
        {
            var array = (JsonArray)parent!;
            if (delete) array.RemoveAt(parts[^1].Index); else array[parts[^1].Index] = replacement;
        }
        return root;
    }
    public static readonly JsonDocumentOptions ParseOptions = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip, MaxDepth = 256 };
}
