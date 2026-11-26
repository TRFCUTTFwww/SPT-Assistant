using Comfort.Common;
using EFT;
using EFT.InventoryLogic;

namespace LocalSpt5Esp;

internal static class FleaListingTemplates
{
    private static readonly HashSet<string> InvalidAncestors = new(StringComparer.Ordinal)
    {
        "566abbb64bdc2d144c8b457d", // Stash
        "602543c13fee350cd564d032", // SortingTable
        "55d7217a4bdc2d86028b456d", // Inventory
        "557596e64bdc2dc2118b4571", // Pockets
        "566965d44bdc2d814c8b4571", // LootContainer
        "63da6da4784a55176c018dba", // HideoutAreaContainer
        "543be5dd4bdc2deb348b4569"  // Money
    };

    private sealed record Applied(ItemTemplate Template, bool Original, bool Allowed);
    private static readonly Dictionary<string, Applied> edits = new(StringComparer.Ordinal);
    private static bool appliedUnbanAll;
    private static IntPtr appliedTemplatesPtr;

    internal static void Apply(Dictionary<string, bool>? rules, bool unbanAll)
    {
        var templates = Singleton<ItemFactory>.Instance?.ItemTemplates;
        if (templates == null)
            return;

        rules ??= [];
        bool unbanChanged = unbanAll != appliedUnbanAll || templates.Pointer != appliedTemplatesPtr;
        appliedUnbanAll = unbanAll;
        appliedTemplatesPtr = templates.Pointer;

        if (!unbanAll)
        {
            foreach (var (id, edit) in edits.ToArray())
            {
                if (rules.ContainsKey(id))
                    continue;
                Restore(edit);
                edits.Remove(id);
            }

            foreach (var (id, allowed) in rules)
            {
                if (!templates.TryGetValue(new MongoID(id), out var template) || template == null)
                    continue;
                if (template.QuestItem)
                    continue;
                SetTemplate(id, template, allowed);
            }
            return;
        }

        if (unbanChanged)
        {
            var allItems = ItemSpawner.CommandItems.Search("");
            foreach (var entry in allItems)
            {
                string id = entry.Id;
                if (ItemCatalog.Category(id) == LootCategory.任务物品 || IsExcludedContainer(id))
                    continue;
                if (!templates.TryGetValue(new MongoID(id), out var template) || template == null || template.QuestItem)
                    continue;
                bool desired = !rules.TryGetValue(id, out bool explicitRule) || explicitRule;
                SetTemplate(id, template, desired);
            }
        }

        foreach (var (id, allowed) in rules)
        {
            if (!templates.TryGetValue(new MongoID(id), out var template) || template == null || template.QuestItem)
                continue;
            SetTemplate(id, template, allowed);
        }

        foreach (var (id, edit) in edits.ToArray())
        {
            if (rules.ContainsKey(id))
                continue;
            if (!edit.Allowed)
                SetTemplate(id, edit.Template, true);
        }
    }

    private static bool IsExcludedContainer(string id)
    {
        foreach (string parent in InvalidAncestors)
            if (ItemCatalog.IsChildOf(id, parent))
                return true;
        return false;
    }

    private static void SetTemplate(string id, ItemTemplate template, bool allowed)
    {
        bool original = template.CanSellOnRagfair;
        if (edits.TryGetValue(id, out var edit))
        {
            if (edit.Template.Pointer != template.Pointer)
                Restore(edit);
            else if (original == edit.Allowed)
                original = edit.Original;
        }
        if (original == allowed && !edits.ContainsKey(id))
            return;
        template.CanSellOnRagfair = allowed;
        edits[id] = new(template, original, allowed);
    }

    internal static void Reset()
    {
        foreach (var edit in edits.Values)
            Restore(edit);
        edits.Clear();
        appliedUnbanAll = false;
        appliedTemplatesPtr = IntPtr.Zero;
    }

    private static void Restore(Applied edit)
    {
        if (edit.Template != null && edit.Template.Pointer != IntPtr.Zero && edit.Template.CanSellOnRagfair == edit.Allowed)
            edit.Template.CanSellOnRagfair = edit.Original;
    }
}
