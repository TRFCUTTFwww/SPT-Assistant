using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using LocalGearMail;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;

internal static class RaidHistoryMailChecks
{
    internal static void Run(JsonUtil json)
    {
        var mail = typeof(RaidHistoryStore).Assembly.GetType("LocalGearMail.RaidHistoryMail", true)!;
        var prepare = mail.GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)!;
        var select = mail.GetMethod("Select", BindingFlags.Static | BindingFlags.NonPublic)!;
        var templates = (TemplateTable)RuntimeHelpers.GetUninitializedObject(typeof(TemplateTable));
        typeof(TemplateTable).GetProperty(nameof(TemplateTable.Items))!.SetValue(templates, new Dictionary<MongoId, TemplateItem>());
        MongoId template = new();
        MongoId quest = new();
        templates.Items[template] = new TemplateItem { Id = template, Properties = null! };
        templates.Items[quest] = json.Deserialize<TemplateItem>($"{{\"_id\":\"{quest}\",\"_props\":{{\"QuestItem\":true}}}}")!;

        HistoryItem Node(string parent, string slot, MongoId tpl, Upd? upd = null)
        {
            var item = new Item { Id = new MongoId(), Template = tpl, ParentId = parent, SlotId = slot, Upd = upd };
            using var doc = JsonDocument.Parse(json.Serialize(item)!);
            return new(item.Id.ToString(), tpl.ToString(), parent, slot, (int?)upd?.StackObjectsCount ?? 1, doc.RootElement.Clone());
        }
        string equipment = new MongoId().ToString();
        var bag = Node(equipment, "Backpack", template, new() { Repairable = new() { Durability = 17 } });
        var gun = Node(bag.Id, "main", template);
        var scope = Node(gun.Id, "mod_scope", template);
        var pockets = Node(equipment, "Pockets", template);
        var ammo = Node(pockets.Id, "pocket1", template, new() { StackObjectsCount = 24, SpawnedInSession = true });
        var questItem = Node(equipment, "main", quest);
        HistoryItem[] entry = [scope, gun, bag, pockets, ammo, questItem];
        var raid = new RaidHistoryRecord { EntryKnown = true, Settled = true, Entry = entry, Returned = [ammo] };
        if (!ReferenceEquals(select.Invoke(null, [raid, "entry"]), entry) ||
            !ReferenceEquals(select.Invoke(null, [raid, "returned"]), raid.Returned))
            throw new Exception("Raid history selected the wrong snapshot");
        var items = (List<Item>)prepare.Invoke(null, [entry, templates, json])!;
        var copiedBag = items.Single(item => item.Upd?.Repairable?.Durability == 17);
        var copiedGun = items.Single(item => item.ParentId == copiedBag.Id.ToString());
        var copiedAmmo = items.Single(item => item.Upd?.StackObjectsCount == 24);
        if (items.Count != 4 || items.Any(item => entry.Any(source => source.Id == item.Id.ToString()))
            || copiedAmmo.ParentId != null || copiedAmmo.SlotId != null || copiedAmmo.Upd?.SpawnedInSession != true
            || !items.Any(item => item.ParentId == copiedGun.Id.ToString() && item.SlotId == "mod_scope"))
            throw new Exception("Raid history attachment preparation changed item state or hierarchy");
        Console.WriteLine("PASS historical mail snapshot selection, fresh IDs, nested items, stack state and pocket contents");
    }
}
