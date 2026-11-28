using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LocalGearMail;
using LocalSpt.Shared;

int passed = 0;
void Assert(bool condition, string name) { if (!condition) throw new Exception(name); passed++; }
void Reject(Action action, string name) { try { action(); } catch (ArgumentException) { passed++; return; } throw new Exception(name); }
JsonElement Element(object value) => JsonSerializer.SerializeToElement(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
string work = Path.Combine(Path.GetTempPath(), "assistant-server-json-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(work);
try
{
    string id = "SPT_Data/configs/test.json", file = Path.Combine(work, id);
    Directory.CreateDirectory(Path.GetDirectoryName(file)!);
    string original = "{\"value\":1,\"external\":0,\"nullable\":null,\"array\":[1,null,3],\"object\":{},\"A\":1,\"a\":2}";
    File.WriteAllText(file, original);
    using var store = new ServerJsonStore(work);
    JsonElement Run(ServerJsonCommand command, string user = "test") => Element(store.Execute(user, command));
    var open = Run(new("serverJsonOpen", id));
    string token = open.GetProperty("token").GetString()!;
    Assert(!open.GetProperty("raw").GetString()!.Contains('\r'), "server node preview uses LF for native cursor positions");
    string pasted = "{\r\n\"value\":600\r\n}";
    int cursor = JsonEditPath.EditorPosition(pasted, pasted.IndexOf("600", StringComparison.Ordinal) + 3);
    string edited = JsonEditPath.EditorText(pasted).Insert(cursor, "0");
    Assert(JsonNode.Parse(edited)!["value"]!.GetValue<int>() == 6000, "server JSON edit preserves numeric caret after a Windows paste");
    JsonElement Call(string action, string path = "$", string? value = null, bool delete = false, string? key = null, bool append = false, int offset = 0, string query = "") =>
        Run(new(action, id, token, path, offset, query, value, delete, key, append));
    Assert(!open.GetProperty("large").GetBoolean(), "small inline preview");
    Assert(Call("serverJsonNode", "$.nullable").GetProperty("raw").GetString() == "null", "null selectable");
    Assert(Call("serverJsonNode", "$.a").GetProperty("raw").GetString() == "2", "case-distinct keys");
    File.WriteAllText(file, original.Replace("\"external\":0", "\"external\":77").Replace("\"value\":1", "\"value\":8"));
    Assert(Call("serverJsonCheck").GetProperty("changed").GetBoolean(), "external change notice");
    Assert(Call("serverJsonNode", "$.value").GetProperty("raw").GetString() == "1", "poll does not refresh selected value");
    var saved = Call("serverJsonSave", "$.value", "3");
    Assert(saved.GetProperty("saved").GetBoolean(), "save without refreshing permitted");
    Assert(saved.GetProperty("changed").GetBoolean(), "notice remains after save");
    using (var disk = JsonDocument.Parse(File.ReadAllText(file)))
    {
        Assert(disk.RootElement.GetProperty("value").GetInt32() == 3, "selected value wins explicit save");
        Assert(disk.RootElement.GetProperty("external").GetInt32() == 77, "unrelated external write preserved");
    }
    Assert(Call("serverJsonNode", "$.external").GetProperty("raw").GetString() == "0", "save does not refresh unrelated snapshot nodes");
    string backup = Path.Combine(work, saved.GetProperty("backup").GetString()!, id);
    Assert(JsonNode.Parse(File.ReadAllText(backup))!["value"]!.GetValue<int>() == 8, "backup captures disk before edit");
    Assert(Call("serverJsonNode", "$.value").GetProperty("raw").GetString() == "3", "saved snapshot reflects own edit");
    Call("serverJsonSave", "$.object", JsonSerializer.Serialize(new string('x', 200) + "needle-at-end"), key: "long-value");
    Assert(Call("serverJsonSearch", query: "needle-at-end").GetProperty("hits").GetArrayLength() == 1, "search includes full scalar value");
    Call("serverJsonSave", "$.nullable", "false");
    Call("serverJsonSave", "$.array", "{\"nested\":true}", append: true);
    Assert(Call("serverJsonNode", "$.array[3]").GetProperty("type").GetString() == "Object", "append arbitrary array value");
    Call("serverJsonSave", "$.array[1]", delete: true);
    Assert(Call("serverJsonNode", "$.array[1]").GetProperty("raw").GetString() == "3", "array deletion shifts indices");
    foreach (string key in new[] { "", "[]", "a.b", "quote\"", "back\\slash", "line\n\t\0", "汉字", "😀" })
    {
        Call("serverJsonSave", "$.object", "null", key: key);
        string path = JsonEditPath.Key("$.object", key);
        Assert(JsonEditPath.Parse(path)[1].Key == key, "path exact key " + JsonSerializer.Serialize(key));
        Assert(Call("serverJsonNode", path).GetProperty("raw").GetString() == "null", "null special key " + JsonSerializer.Serialize(key));
        Call("serverJsonSave", path, "42");
        Assert(Call("serverJsonNode", path).GetProperty("raw").GetString() == "42", "edit special key " + JsonSerializer.Serialize(key));
        Call("serverJsonSave", path, delete: true);
    }
    Reject(() => Call("serverJsonSave", "$.missing", "3"), "missing path must not be created implicitly");
    Reject(() => Call("serverJsonSave", "$", delete: true), "root delete denied");
    Reject(() => Call("serverJsonSave", "$", "1", key: "value"), "duplicate child denied");
    Reject(() => Run(new("serverJsonNode", id, token), "another-owner"), "snapshot ownership enforced");
    foreach (string forbidden in new[] { "../test.json", "SPT_Data/configs/../../../outside.json", "SPT_Data/configs/x.json:stream", "SPT_Data/configs/x.json.", "SPT_Data/configs/../database/test.json", "user/mods/x/.git/config.json", "D:/escape.json" })
        Reject(() => Run(new("serverJsonOpen", forbidden)), "path rejected " + forbidden);
    byte[] beforeInvalid = File.ReadAllBytes(file);
    try { Call("serverJsonSave", "$.value", "{invalid}"); throw new Exception("Invalid JSON saved"); } catch (JsonException) { passed++; }
    Assert(File.ReadAllBytes(file).SequenceEqual(beforeInvalid), "parse failure leaves disk unchanged");
    Call("serverJsonSave", "$", "[null,1,true]");
    Assert(Call("serverJsonNode", "$[0]").GetProperty("raw").GetString() == "null", "array root supported");
    Call("serverJsonSave", "$", "null");
    Assert(Call("serverJsonNode").GetProperty("type").GetString() == "Null", "null root supported");
    Call("serverJsonSave", "$", "\"text\"");
    Assert(Call("serverJsonExport").GetProperty("raw").GetString() == "\"text\"", "scalar root export supported");
    File.WriteAllBytes(file, new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("{/* comment */\"a\":1,}")).ToArray());
    open = Run(new("serverJsonOpen", id)); token = open.GetProperty("token").GetString()!;
    Call("serverJsonSave", "$.a", "2");
    Assert(Call("serverJsonNode", "$.a").GetProperty("raw").GetString() == "2", "BOM, comments and trailing commas supported");
    File.Delete(file);
    Assert(Call("serverJsonCheck").GetProperty("changed").GetBoolean(), "deletion generates update notice");
    Assert(Call("serverJsonNode", "$.a").GetProperty("raw").GetString() == "2", "deleted file snapshot still browsable");

    // Unrestricted directory discovery under SPT_Runtime (profiles, custom dirs, mods, while skipping own backup folder)
    Directory.CreateDirectory(Path.Combine(work, "user/profiles"));
    File.WriteAllText(Path.Combine(work, "user/profiles/custom.json"), "{\"ok\":true}");
    Directory.CreateDirectory(Path.Combine(work, "custom_folder"));
    File.WriteAllText(Path.Combine(work, "custom_folder/extra.json"), "{\"ok\":1}");
    var listedIds = Run(new("serverJsonList")).GetProperty("files").EnumerateArray().Select(f => f.GetProperty("id").GetString()!).ToList();
    Assert(listedIds.Contains("user/profiles/custom.json") && listedIds.Contains("custom_folder/extra.json"), "unrestricted server JSON store lists all runtime subdirectories");
    Assert(listedIds.All(fid => !fid.StartsWith("user/mods/LocalGearMail/backups/server-json", StringComparison.OrdinalIgnoreCase)), "own server-json backups excluded from inventory");

    var large = new JsonObject();
    for (int i = 0; i < 5000; i++) large["key" + i] = new JsonObject { ["number"] = i, ["text"] = new string('x', 100) };
    File.WriteAllText(file, large.ToJsonString());
    open = Run(new("serverJsonOpen", id)); token = open.GetProperty("token").GetString()!;
    Assert(open.GetProperty("large").GetBoolean() && open.GetProperty("raw").GetString() == "", "large root bounded preview");
    Assert(Call("serverJsonLoadFull").GetProperty("raw").GetString()!.Length > ServerJsonStore.InlineLimit, "serverJsonLoadFull expands complete node text on demand");
    Assert(open.GetProperty("children").GetArrayLength() == 60, "large children pagination");
    Assert(Call("serverJsonNode", offset: 4980).GetProperty("children").GetArrayLength() == 20, "last page exact");
    Assert(Call("serverJsonNode", offset: int.MaxValue).GetProperty("offset").GetInt32() == 4980, "out of range page clamped");
    Assert(Call("serverJsonNode", "$[\"key4999\"][\"number\"]").GetProperty("raw").GetString() == "4999", "deep field outside first page");
    Assert(Call("serverJsonSearch", query: "key4999 number").GetProperty("hits").GetArrayLength() == 1, "deep file search");
    var search = Call("serverJsonSearch", query: "key");
    Assert(search.GetProperty("hits").GetArrayLength() == 2000 && search.GetProperty("truncated").GetBoolean(), "search supports expanded hit limit");
    Call("serverJsonSave", "$[\"key4999\"][\"number\"]", "-9007199254740993");
    Assert(JsonNode.Parse(File.ReadAllText(file))!["key4999"]!["number"]!.GetValue<long>() == -9007199254740993L, "numeric precision preserved");
    Assert(Call("serverJsonExport").GetProperty("raw").GetString()!.Length > ServerJsonStore.InlineLimit, "large export is complete");
    Assert(!Directory.EnumerateFiles(work, "*.tmp", SearchOption.AllDirectories).Any(), "temporary writes cleaned");

    string runtime = "D:/SPT-5.0.0-48558/SPT_Runtime";
    if (Directory.Exists(runtime))
    {
        AssemblyLoadContext.Default.Resolving += (_, name) => File.Exists(Path.Combine(runtime, name.Name + ".dll")) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(runtime, name.Name + ".dll")) : null;
        var docs = new ServerJsonModelDocs(AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(runtime, "SPTarkov.Server.Core.dll")), Path.Combine(runtime, "SPTarkov.Server.Core.xml"));
        Assert(docs.Describe("SPT_Data/configs/bot.json", "$.presetBatch")?.Length > 0, "native model documentation resolves");
        var grenade = ServerJsonCatalog.Field("SPT_Data/database/bots/types/assault.json", "$.difficulty.normal.Grenade.NO_RUN_FROM_AI_GRENADES", JsonValueKind.False, docs.Describe);
        Assert(grenade.DetailZh.Contains("不控制玩家"), "AI grenade flag distinguished from player grenades");
        using var installed = new ServerJsonStore(runtime, docs.Describe);
        var list = Element(installed.Execute("readonly", new("serverJsonList"))).GetProperty("files");
        Assert(list.GetArrayLength() >= 443, "installed database and config inventory");
        int checkedFiles = 0; long checkedNodes = 0; int unknown = 0;
        var missing = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var entry in list.EnumerateArray())
        {
            string actualId = entry.GetProperty("id").GetString()!;
            Assert(entry.GetProperty("purpose").GetProperty("detailZh").GetString()!.Length > 0, "file purpose " + actualId);
            if (!actualId.StartsWith("SPT_Data/configs/") && !actualId.EndsWith("/bots/types/assault.json") && !actualId.EndsWith("/templates/items.json")) continue;
            using var parsed = JsonDocument.Parse(File.ReadAllText(Path.Combine(runtime, actualId)).TrimStart('\uFEFF'), JsonEditPath.ParseOptions);
            void Walk(JsonElement e, string path)
            {
                var description = ServerJsonCatalog.Field(actualId, path, e.ValueKind, docs.Describe);
                if (description.DetailZh.Length == 0 || description.DetailEn.Length == 0) throw new Exception("Missing field annotation " + actualId + ":" + path);
                if (description.DetailZh.Contains("未提供")) { unknown++; missing[description.TitleZh] = missing.GetValueOrDefault(description.TitleZh) + 1; }
                checkedNodes++;
                if (e.ValueKind == JsonValueKind.Object) foreach (var p in e.EnumerateObject()) Walk(p.Value, JsonEditPath.Key(path, p.Name));
                else if (e.ValueKind == JsonValueKind.Array) { int i = 0; foreach (var v in e.EnumerateArray()) Walk(v, JsonEditPath.Index(path, i++)); }
            }
            Walk(parsed.RootElement, "$"); checkedFiles++;
        }
        Console.WriteLine($"Field annotations: {checkedNodes} nodes in {checkedFiles} files; {unknown} explicitly lack native documentation.");
        if (args.Contains("--coverage")) foreach (var pair in missing.OrderByDescending(p => p.Value).Take(180)) Console.WriteLine(pair.Key + " " + pair.Value);
        string biggest = list.EnumerateArray().Where(f => f.GetProperty("id").GetString()!.StartsWith("SPT_Data/database/")).MaxBy(f => f.GetProperty("bytes").GetInt64()).GetProperty("id").GetString()!;
        var sw = Stopwatch.StartNew();
        var big = Element(installed.Execute("readonly", new("serverJsonOpen", biggest)));
        Assert(big.GetProperty("large").GetBoolean() && big.GetProperty("children").GetArrayLength() <= 60, "largest installed JSON opens with bounded response");
        Console.WriteLine($"Largest JSON: {Path.GetFileName(Path.GetDirectoryName(biggest))}/{Path.GetFileName(biggest)}; open {sw.ElapsedMilliseconds} ms; response {big.GetRawText().Length} chars.");
        sw.Restart();
        var noMatch = Element(installed.Execute("readonly", new("serverJsonSearch", biggest, big.GetProperty("token").GetString()!, Query: "__assistant_no_match_53cad601__")));
        Assert(noMatch.GetProperty("hits").GetArrayLength() == 0, "largest file full traversal search");
        Console.WriteLine($"Largest JSON full search: {sw.ElapsedMilliseconds} ms.");
    }
    Console.WriteLine($"PASS: {passed} server JSON checks.");
}
finally { Directory.Delete(work, true); }
