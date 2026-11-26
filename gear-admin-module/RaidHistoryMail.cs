using System.Text.Json;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Utils;

namespace LocalGearMail;

internal static class RaidHistoryMail
{
    const string ReceiptKey = "localRaidHistoryMailReceipts";
    const long MailLifetime = 30L * 24 * 60 * 60;
    sealed record Receipt(string Raid, string Snapshot, int ItemCount, long Sent);

    internal static async ValueTask<string> Send(CharacterDataRequest request, MongoId session,
        SptProfile profile, SaveServer saves, JsonUtil json, HttpResponseUtil http,
        TemplateTable templates, MailSendService mail)
    {
        string snapshot = request.Categories is [var category] ? category : "";
        if (snapshot is not ("entry" or "returned") ||
            !Guid.TryParseExact(request.Revision, "N", out var requestId))
            throw new ArgumentException("请选择要发送的战局物品。");

        var receipts = new Dictionary<string, Receipt>();
        if (profile.ExtensionData?.TryGetValue(ReceiptKey, out var saved) == true)
            receipts = JsonSerializer.Deserialize<Dictionary<string, Receipt>>(JsonSerializer.Serialize(saved)) ?? [];
        string receiptId = requestId.ToString("N");
        if (receipts.TryGetValue(receiptId, out var previous))
        {
            if (previous.Raid != request.Id || previous.Snapshot != snapshot)
                throw new ArgumentException("邮件请求与战局快照不匹配。");
            await saves.SaveProfileAsync(session);
            return http.GetBody(new { ok = true, mailedItems = previous.ItemCount });
        }

        var raid = RaidHistoryService.Find(session, request.Id);
        var items = Prepare(Select(raid, snapshot), templates, json);
        if (items.Count == 0)
            throw new ArgumentException("这份快照没有可寄送的物品。");

        var label = snapshot == "entry" ? "入场装备" : "带出与保留物品";
        mail.SendSystemMessageToPlayer(session, $"历史战局 · {label}\n{raid.Started} · {raid.Map} · {raid.Side}", items, MailLifetime);
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var expired in receipts.Where(pair => now - pair.Value.Sent > MailLifetime).ToArray())
            receipts.Remove(expired.Key);
        receipts[receiptId] = new Receipt(raid.Id, snapshot, items.Count, now);
        profile.ExtensionData ??= [];
        profile.ExtensionData[ReceiptKey] = JsonSerializer.SerializeToElement(receipts);
        await saves.SaveProfileAsync(session);
        return http.GetBody(new { ok = true, mailedItems = items.Count });
    }

    internal static HistoryItem[] Select(RaidHistoryRecord raid, string snapshot) => snapshot switch
    {
        "entry" when raid.EntryKnown => raid.Entry,
        "returned" when raid.Settled => raid.Returned,
        "entry" => throw new ArgumentException("这场战局没有入场快照。"),
        _ => throw new ArgumentException("这场战局尚未完成结算。")
    };

    internal static List<Item> Prepare(HistoryItem[] snapshot, TemplateTable templates, JsonUtil json)
    {
        var nodes = new Dictionary<string, HistoryItem>(StringComparer.Ordinal);
        foreach (var node in snapshot)
            if (node.Id.Length == 0 || !nodes.TryAdd(node.Id, node))
                throw new ArgumentException("战局物品层级无效。");

        var excluded = new HashSet<string>(StringComparer.Ordinal);
        var pockets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in snapshot)
        {
            if (node.Slot == "Pockets" && !nodes.ContainsKey(node.Parent))
                pockets.Add(node.Id);
            else if (node.Raw.ValueKind != JsonValueKind.Object ||
                !templates.Items.TryGetValue(new MongoId(node.Template), out var template))
                throw new ArgumentException("快照包含缺失或已停用的物品模板。");
            else if (template.Properties?.QuestItem == true)
                excluded.Add(node.Id);
        }

        var roots = new List<Item>();
        var children = new List<Item>();
        var ids = nodes.Keys.ToDictionary(id => id, _ => new MongoId(), StringComparer.Ordinal);
        foreach (var node in snapshot)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal) { node.Id };
            var ancestor = node;
            while (nodes.TryGetValue(ancestor.Parent, out var parent))
            {
                if (!visited.Add(parent.Id))
                    throw new ArgumentException("战局物品层级无效。");
                ancestor = parent;
            }
            if (visited.Any(excluded.Contains) || pockets.Contains(node.Id))
                continue;

            var item = json.Deserialize<Item>(node.Raw.GetRawText())
                ?? throw new ArgumentException("快照中的物品数据不完整。");
            item.Id = ids[node.Id];
            if (!nodes.ContainsKey(node.Parent) || pockets.Contains(node.Parent))
            {
                item.ParentId = null;
                item.SlotId = null;
                item.Location = null;
                roots.Add(item);
            }
            else
            {
                item.ParentId = ids[node.Parent].ToString();
                children.Add(item);
            }
        }
        roots.AddRange(children);
        return roots;
    }
}
