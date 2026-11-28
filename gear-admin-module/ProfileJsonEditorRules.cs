using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LocalSpt5Esp;

namespace LocalGearMail;

public static class ProfileJsonEditorRules
{
    public const string ArrayAppendToken = "[]";

    public readonly record struct Segment(bool IsIndex, string Key, int Index);

    static readonly string[][] LockedIdentityChains =
    [
        ["info", "id"],
        ["info", "scavId"],
        ["info", "aid"],
        ["inraid"],
        ["characters", "pmc", "_id"],
        ["characters", "pmc", "aid"],
        ["characters", "pmc", "sessionId"],
        ["characters", "pmc", "savage"],
        ["characters", "scav", "_id"],
        ["characters", "scav", "aid"],
        ["characters", "scav", "sessionId"],
        ["characters", "scav", "savage"]
    ];

    public static string AppendProperty(string parentPath, string key)
    {
        if (string.IsNullOrEmpty(parentPath)) parentPath = "$";
        if (NeedsBracketProperty(key))
        {
            var escaped = key.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return $"{parentPath}[\"{escaped}\"]";
        }
        return $"{parentPath}.{key}";
    }

    public static string AppendIndex(string parentPath, int index) =>
        $"{(string.IsNullOrEmpty(parentPath) ? "$" : parentPath)}[{index}]";

    public static string ParentPath(string path)
    {
        var segments = ParsePath(path);
        if (segments.Count <= 1) return "$";
        string result = "$";
        for (int i = 0; i < segments.Count - 1; i++)
            result = segments[i].IsIndex ? AppendIndex(result, segments[i].Index) : AppendProperty(result, segments[i].Key);
        return result;
    }

    static bool NeedsBracketProperty(string key)
    {
        if (key.Length == 0) return true;
        foreach (char c in key)
            if (c is '.' or '[' or ']' or '"' or '\\' || char.IsWhiteSpace(c) || char.IsControl(c))
                return true;
        return false;
    }

    public static List<Segment> ParsePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path[0] != '$')
            throw new ArgumentException("无效的 JSON 路径（须以 $ 开头）。");

        var segments = new List<Segment>();
        int i = 1;
        while (i < path.Length)
        {
            char c = path[i];
            if (c == '.')
            {
                i++;
                int start = i;
                while (i < path.Length && path[i] != '.' && path[i] != '[') i++;
                if (i == start) throw new ArgumentException("JSON 路径包含空字段名。");
                segments.Add(new(false, path[start..i], -1));
            }
            else if (c == '[')
            {
                i++;
                if (i < path.Length && path[i] == '"')
                {
                    i++;
                    var sb = new StringBuilder();
                    bool closed = false;
                    while (i < path.Length)
                    {
                        if (path[i] == '\\')
                        {
                            i++;
                            if (i >= path.Length) throw new ArgumentException("JSON 路径转义未闭合。");
                            sb.Append(path[i++]);
                        }
                        else if (path[i] == '"')
                        {
                            i++;
                            closed = true;
                            break;
                        }
                        else sb.Append(path[i++]);
                    }
                    if (!closed || i >= path.Length || path[i] != ']')
                        throw new ArgumentException("JSON 路径括号未闭合。");
                    i++;
                    segments.Add(new(false, sb.ToString(), -1));
                }
                else
                {
                    int start = i;
                    while (i < path.Length && path[i] != ']') i++;
                    if (i >= path.Length || !int.TryParse(path[start..i], out int idx) || idx < 0)
                        throw new ArgumentException("JSON 数组下标无效。");
                    i++;
                    segments.Add(new(true, "", idx));
                }
            }
            else throw new ArgumentException("JSON 路径语法无效：" + path);
        }
        return segments;
    }

    public static JsonNode? ReadNode(JsonNode? root, string path)
    {
        var segments = ParsePath(path);
        JsonNode? current = root;
        foreach (var seg in segments)
        {
            if (current == null) return null;
            if (seg.IsIndex)
            {
                if (current is not JsonArray arr || seg.Index < 0 || seg.Index >= arr.Count)
                    return null;
                current = arr[seg.Index];
            }
            else
            {
                if (current is not JsonObject obj || !obj.TryGetPropertyValue(seg.Key, out current))
                    return null;
            }
        }
        return current;
    }

    public static string Apply(
        string currentJson,
        string path,
        string? valueJson,
        bool deleteNode,
        string? childKey,
        Func<string, bool>? installed,
        string? baselineJson = null)
    {
        var original = JsonNode.Parse(currentJson) as JsonObject
            ?? throw new ArgumentException("当前角色存档不是有效的 JSON 对象。");
        string trimmedPath = string.IsNullOrWhiteSpace(path) ? "$" : path.Trim();
        var segments = ParsePath(trimmedPath);
        if (baselineJson != null)
            RequireUnchanged(JsonNode.Parse(baselineJson), original, segments, deleteNode ? null : childKey);

        var candidate = (JsonObject)original.DeepClone();

        if (deleteNode)
        {
            if (trimmedPath == "$")
                throw new ArgumentException("不能删除存档根节点。");
            DeleteAt(candidate, trimmedPath);
        }
        else if (!string.IsNullOrEmpty(childKey))
        {
            var target = ReadNode(candidate, trimmedPath)
                ?? throw new ArgumentException("目标父节点已不存在，请刷新后重试。");
            JsonNode? parsedChild = ParseReplacement(valueJson);
            if (childKey == ArrayAppendToken)
            {
                if (target is not JsonArray arr)
                    throw new ArgumentException("只有 JSON 数组才能追加元素。");
                arr.Add(parsedChild);
            }
            else
            {
                if (target is not JsonObject obj)
                    throw new ArgumentException("只有 JSON 对象才能新增命名字段。");
                if (obj.ContainsKey(childKey))
                    throw new ArgumentException($"字段「{childKey}」已存在，请直接选中它进行编辑。");
                obj[childKey] = parsedChild;
            }
        }
        else
        {
            JsonNode? replacement = ParseReplacement(valueJson);
            if (trimmedPath == "$")
            {
                candidate = replacement as JsonObject
                    ?? throw new ArgumentException("存档根节点必须是 JSON 对象 {...}。");
            }
            else
            {
                SetAt(candidate, trimmedPath, replacement);
            }
        }

        return candidate.ToJsonString();
    }

    internal const string EditConflict = "服务端存档已发生变化，请点击「重新读取」后再修改保存。";

    internal static void VerifySaved(string expectedJson, string savedJson, string path, bool deleteNode, string? childKey)
    {
        var expected = JsonNode.Parse(expectedJson);
        var saved = JsonNode.Parse(savedJson);
        if (childKey == ArrayAppendToken && !deleteNode)
            path = AppendIndex(path, ((JsonArray)ReadNode(expected, path)!).Count - 1);
        else if (childKey != null && !deleteNode)
            path = AppendProperty(path, childKey);
        else if (deleteNode && ParsePath(path)[^1].IsIndex)
            path = ParentPath(path);

        RequireUnchanged(expected, saved, ParsePath(path), null);
    }

    static void RequireUnchanged(JsonNode? baseline, JsonNode? current, List<Segment> path, string? childKey)
    {
        foreach (var segment in path)
        {
            if (segment.IsIndex)
            {
                if (baseline is not JsonArray before || current is not JsonArray now
                    || segment.Index >= before.Count || segment.Index >= now.Count)
                    throw new ArgumentException(EditConflict);

                baseline = before[segment.Index];
                current = now[segment.Index];
                // An index alone cannot identify an element after insertions or removals.
                if (ArrayEntryId(baseline) is { } id
                    ? !JsonNode.DeepEquals(id, ArrayEntryId(current))
                    : !JsonNode.DeepEquals(before, now))
                    throw new ArgumentException(EditConflict);
            }
            else
            {
                if (baseline is not JsonObject before || current is not JsonObject now)
                    throw new ArgumentException(EditConflict);

                bool existed = before.TryGetPropertyValue(segment.Key, out baseline);
                bool exists = now.TryGetPropertyValue(segment.Key, out current);
                if (existed != exists)
                    throw new ArgumentException(EditConflict);
            }
        }

        bool unchanged = childKey switch
        {
            null => JsonNode.DeepEquals(baseline, current),
            ArrayAppendToken => baseline is JsonArray && current is JsonArray,
            _ => baseline is JsonObject before && current is JsonObject now
                && !before.ContainsKey(childKey) && !now.ContainsKey(childKey)
        };
        if (!unchanged)
            throw new ArgumentException(EditConflict);
    }

    static JsonValue? ArrayEntryId(JsonNode? node)
    {
        if (node is not JsonObject obj) return null;
        return (obj["_id"] ?? obj["qid"] ?? obj["Id"]) as JsonValue;
    }

    static JsonNode? ParseReplacement(string? valueJson)
    {
        if (valueJson == null)
            throw new ArgumentException("请提供有效的 JSON 内容。");
        string trimmed = valueJson.Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException("JSON 内容不能为空（空字符串请写 \"\"，空值请写 null）。");
        try
        {
            return JsonNode.Parse(trimmed, documentOptions: new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"JSON 语法错误（第 {ex.LineNumber + 1} 行）：{ex.Message}");
        }
    }

    static void SetAt(JsonObject root, string path, JsonNode? value)
    {
        var segments = ParsePath(path);
        JsonNode current = root;
        for (int i = 0; i < segments.Count - 1; i++)
        {
            var seg = segments[i];
            if (seg.IsIndex)
            {
                if (current is not JsonArray arr || seg.Index < 0 || seg.Index >= arr.Count || arr[seg.Index] == null)
                    throw new ArgumentException("JSON 路径中的父级数组元素不存在，请刷新后重试。");
                current = arr[seg.Index]!;
            }
            else
            {
                if (current is not JsonObject obj || !obj.TryGetPropertyValue(seg.Key, out var next) || next == null)
                    throw new ArgumentException($"JSON 路径中的父级对象「{seg.Key}」不存在，请刷新后重试。");
                current = next;
            }
        }

        var leaf = segments[^1];
        if (leaf.IsIndex)
        {
            if (current is not JsonArray arr || leaf.Index < 0 || leaf.Index >= arr.Count)
                throw new ArgumentException("目标数组下标已超出范围，请刷新后重试。");
            arr[leaf.Index] = value;
        }
        else
        {
            if (current is not JsonObject obj)
                throw new ArgumentException("目标节点的父级不是 JSON 对象。");
            obj[leaf.Key] = value;
        }
    }

    static void DeleteAt(JsonObject root, string path)
    {
        var segments = ParsePath(path);
        if (segments.Count == 0) throw new ArgumentException("不能删除存档根节点。");
        JsonNode current = root;
        for (int i = 0; i < segments.Count - 1; i++)
        {
            var seg = segments[i];
            if (seg.IsIndex)
            {
                if (current is not JsonArray arr || seg.Index < 0 || seg.Index >= arr.Count || arr[seg.Index] == null)
                    throw new ArgumentException("目标路径父节点已不存在。");
                current = arr[seg.Index]!;
            }
            else
            {
                if (current is not JsonObject obj || !obj.TryGetPropertyValue(seg.Key, out var next) || next == null)
                    throw new ArgumentException("目标路径父节点已不存在。");
                current = next;
            }
        }

        var leaf = segments[^1];
        if (leaf.IsIndex)
        {
            if (current is not JsonArray arr || leaf.Index < 0 || leaf.Index >= arr.Count)
                throw new ArgumentException("目标数组元素不存在，请刷新后重试。");
            arr.RemoveAt(leaf.Index);
        }
        else
        {
            if (current is not JsonObject obj || !obj.Remove(leaf.Key))
                throw new ArgumentException($"目标字段「{leaf.Key}」不存在，请刷新后重试。");
        }
    }

    static void VerifyCoreStructureAndIdentity(JsonObject original, JsonObject candidate)
    {
        if (candidate["info"] is not JsonObject || candidate["characters"] is not JsonObject chars
            || chars["pmc"] is not JsonObject pmc || pmc["Info"] is not JsonObject
            || pmc["Health"] is not JsonObject || pmc["Inventory"] is not JsonObject)
        {
            throw new ArgumentException("存档缺少必需的核心结构（info / characters.pmc / Info / Health / Inventory）。");
        }

        if (original["_id"] != null && !JsonNode.DeepEquals(original["_id"], candidate["_id"]))
            throw new ArgumentException("存档顶层 _id 只读。");

        foreach (var chain in LockedIdentityChains)
        {
            bool origExists = TryFollow(original, chain, out var origVal);
            bool candExists = TryFollow(candidate, chain, out var candVal);
            if (origExists != candExists || !JsonNode.DeepEquals(origVal, candVal))
                throw new ArgumentException($"核心身份或战局状态字段（{string.Join(".", chain)}）不可修改。");
        }
    }

    static bool TryFollow(JsonObject root, string[] chain, out JsonNode? value)
    {
        JsonNode? cur = root;
        foreach (string key in chain)
        {
            if (cur is not JsonObject obj || !obj.TryGetPropertyValue(key, out cur))
            {
                value = null;
                return false;
            }
        }
        value = cur;
        return true;
    }
}
