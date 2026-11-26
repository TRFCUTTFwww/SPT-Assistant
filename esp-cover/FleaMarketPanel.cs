using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using SPTushonka.Common.Http;
using UnityEngine;

namespace LocalSpt5Esp;

internal static partial class FleaMarketPanel
{
    internal sealed record RulesDto(
        [property: JsonPropertyName("unbanAll")] bool UnbanAll = false,
        [property: JsonPropertyName("ignoreLevelLocks")] bool IgnoreLevelLocks = false,
        [property: JsonPropertyName("autoPriceMode")] int AutoPriceMode = 0,
        [property: JsonPropertyName("autoPricePercent")] int AutoPricePercent = 100,
        [property: JsonPropertyName("showTraderCompare")] bool ShowTraderCompare = true,
        [property: JsonPropertyName("purchasesFir")] bool PurchasesFir = false,
        [property: JsonPropertyName("fullCondition")] bool FullCondition = false,
        [property: JsonPropertyName("roubleOnly")] bool RoubleOnly = false,
        [property: JsonPropertyName("noBarter")] bool NoBarter = false);

    private sealed record Prices(
        [property: JsonPropertyName("mode")] string Mode,
        [property: JsonPropertyName("updated")] DateTimeOffset? Updated,
        [property: JsonPropertyName("refreshing")] bool Refreshing,
        [property: JsonPropertyName("failed")] bool Failed,
        [property: JsonPropertyName("price")] double? Price,
        [property: JsonPropertyName("manual")] int? Manual,
        [property: JsonPropertyName("listing")] bool? Listing,
        [property: JsonPropertyName("overrides")] Dictionary<string, int>? Overrides,
        [property: JsonPropertyName("listings")] Dictionary<string, bool>? Listings,
        [property: JsonPropertyName("rules")] RulesDto? Rules,
        [property: JsonPropertyName("presets")] string[]? Presets,
        [property: JsonPropertyName("hasLegacy")] bool HasLegacy,
        [property: JsonPropertyName("stashRoubles")] long? StashRoubles,
        [property: JsonPropertyName("exportedJson")] string? ExportedJson,
        [property: JsonPropertyName("configMessage")] string? ConfigMessage,
        [property: JsonPropertyName("customOffers")] CustomOffer[]? CustomOffers);

    private sealed record PendingRaidBuy(string Id, string TemplateId, string ItemName, int Count, int StackMax,
        int? Added = null, bool Fir = false, long Cost = 0);

    private const float RowHeight = 33;
    private static readonly RulesDto DefaultRules = new();
    private static readonly (string Id, string Label)[] modes = [("local", "本地"), ("pvp", "PvP"), ("pve", "PvE")];
    private static readonly (bool? Allowed, string Label)[] listingModes = [(null, "默认"), (true, "允许"), (false, "禁止")];
    private static readonly (int Id, string Label)[] scopeFilters = [(0, "全部"), (1, "已改价"), (2, "已允许"), (3, "已禁止")];
    private static readonly (LootCategory Cat, string Label)[] categories =
    [
        (LootCategory.全部, "全部"),
        (LootCategory.武器, "武器"),
        (LootCategory.弹药, "弹药"),
        (LootCategory.配件, "配件"),
        (LootCategory.防具装备, "防具"),
        (LootCategory.医疗, "医疗"),
        (LootCategory.食物饮料, "食饮"),
        (LootCategory.钥匙, "钥匙"),
        (LootCategory.交易材料, "材料"),
        (LootCategory.其他, "其他")
    ];
    private static readonly (int Mode, string Label)[] autoPriceModes =
    [
        (0, "关闭"),
        (1, "最低价-1"),
        (2, "均价"),
        (3, "最高价")
    ];

    private static bool viewing, fillDraft, choosingCategory, batchOpen;
    private static int tab, scopeFilter, filterStamp = 1, appliedStamp = -1;
    private static LootCategory categoryFilter = LootCategory.全部;
    private static LootCategory appliedCategory = LootCategory.全部;
    private static int appliedScope;

    private static Task<string>? request, statusRequest;
    private static string? requestTemplate, statusTemplate;
    private static int stateRevision, statusRevision;
    private static Task<string>? buyTask;
    private static Task<(bool Success, string Message)>? mailFallbackTask;
    private static Task<string?>? dialogTask;
    private static string? pendingExportSavePath;
    private static bool dialogIsImport;
    private static string? dialogExportPreset;
    private static PendingRaidBuy? pendingBuy;
    private static string? buySession;
    private static IntPtr buyRaid;
    private static float nextBuyRetry;
    private static bool deliveryDirty;

    private static float nextPoll, configMessageUntil, confirmDeleteUntil;
    private static string? confirmDeleteTarget;
    private static Prices? prices;
    private static ChatItemSearch.Item? selected;
    private static ChatItemSearch? catalog;
    private static ChatItemSearch.Item[] results = [];
    private static string search = "", searched = "", draft = "", error = "";
    private static string configMessage = "", presetName = "";
    private static string batchScaleDraft = "1.0", autoPercentDraft = "100", buyCountDraft = "1";
    private static bool autoPercentSynced, requestSyncRules;
    private static Vector2 scroll, presetScroll, rulesScroll, itemSettingsScroll;

    internal static RulesDto ActiveRules => prices?.Rules ?? DefaultRules;
    internal static bool CanReload => request == null && !NativeFleaOfferEdit.Busy && !FleaOfferEditor.Saving && buyTask == null && dialogTask == null && mailFallbackTask == null && !deliveryDirty;

    internal static void Reset()
    {
        request = null;
        statusRequest = null;
        stateRevision++;
        buyTask = null;
        mailFallbackTask = null;
        dialogTask = null;
        pendingExportSavePath = null;
        pendingBuy = null;
        buySession = null;
        buyRaid = IntPtr.Zero;
        nextBuyRetry = 0;
        deliveryDirty = false;
        confirmDeleteTarget = null;
        FleaListingTemplates.Reset();
    }

    private static bool InLocalRaid() => ItemSpawner.InRaid() && ChatFikaSession.Role == 0;

    private static IntPtr CurrentRaidPtr()
    {
        var game = Singleton<AbstractGame>.Instance;
        return game != null && game.InRaid && game.TryCast<HideoutGame>() == null ? game.Pointer : IntPtr.Zero;
    }

    internal static void Tick()
    {
        FinishDialog();
        TickRaidBuy();
        FinishRequest();
        FinishStatus();

        bool currentView = InterfaceMenu.FleaMarketOpen || FleaMarketShortcut.Expanded;
        if (currentView && !viewing)
            nextPoll = 0;
        viewing = currentView;
        if (request == null && statusRequest == null && buyTask == null && Time.unscaledTime >= nextPoll &&
            (viewing || (prices == null && Singleton<ItemFactory>.Instance?.ItemTemplates != null)) &&
            !string.IsNullOrWhiteSpace(RequestHandler.SessionId))
            Begin("fleaPriceStatus", background: true);
    }

    private static void Begin(
        string action,
        bool fill = false,
        string? mode = null,
        double price = 0,
        bool? allowed = null,
        string? preset = null,
        string? targetPreset = null,
        string? fleaJson = null,
        RulesDto? rules = null,
        string? batchOp = null,
        string[]? batchIds = null,
        object? fleaOffer = null,
        string? offerId = null,
        bool background = false)
    {
        if (request != null || buyTask != null)
            return;
        if (background && statusRequest != null) return;
        if (!background) { fillDraft = fill; requestSyncRules = action is "fleaConfigLoad" or "fleaConfigImport" || action=="fleaRulesSave" && rules?.AutoPricePercent!=ActiveRules.AutoPricePercent; }
        var payload = new Dictionary<string, object?>
        {
            ["action"] = action,
            ["template"] = selected?.Id ?? "",
            ["currency"] = mode ?? "",
            ["value"] = price,
            ["listingAllowed"] = allowed,
            ["fleaPresetName"] = preset,
            ["fleaTargetName"] = targetPreset,
            ["fleaJson"] = fleaJson,
            ["batchOp"] = batchOp ?? ""
        };
        if (fleaOffer != null) payload["fleaOffer"] = fleaOffer;
        if (offerId != null) payload["id"] = offerId;
        if (rules != null)
            payload["fleaRules"] = rules;
        if (batchIds != null)
            payload["categories"] = batchIds;
        var task = CharacterDataPanel.Call("/local/gear/data", "characterData", payload).WaitAsync(TimeSpan.FromSeconds(20));
        if (background) { statusRequest = task; statusTemplate = selected?.Id; statusRevision = stateRevision; }
        else { request = task; requestTemplate = selected?.Id; stateRevision++; }
    }

    private static void ShowMessage(string text)
    {
        configMessage = text;
        configMessageUntil = Time.unscaledTime + 4.5f;
        error = "";
    }

    private static void FinishRequest()
    {
        if (request?.IsCompleted != true)
            return;
        try
        {
            using var json = JsonDocument.Parse(request.GetAwaiter().GetResult());
            var root = json.RootElement;
            if (root.TryGetProperty("err", out var code) && code.GetInt32() != 0)
                throw new ArgumentException(root.TryGetProperty("errmsg", out var message) ? message.GetString() ?? "操作失败" : "操作失败");
            var data = CharacterDataPanel.Data(root);
            if (!data.TryGetProperty("mode", out _))
                throw new ArgumentException("操作失败");
            ApplyPrices(data.Deserialize<Prices>() ?? throw new ArgumentException("操作失败"),
                fillDraft && requestTemplate == selected?.Id,
                requestSyncRules);
            if (prices!.ExportedJson is { Length: > 0 } exported && !string.IsNullOrWhiteSpace(pendingExportSavePath))
            {
                string savePath = pendingExportSavePath!;
                pendingExportSavePath = null;
                File.WriteAllText(savePath, exported, new UTF8Encoding(false));
                ShowMessage(string.Format(I18n.Text("已导出到：{0}"), Path.GetFileName(savePath)));
            }
            else if (prices.ConfigMessage is { } savedMessage)
            {
                ShowMessage(savedMessage);
            }
            else
            {
                error = "";
            }
        }
        catch (TimeoutException)
        {
            pendingExportSavePath = null;
            error = "连接超时";
        }
        catch (Exception failure)
        {
            pendingExportSavePath = null;
            error = failure is ArgumentException or IOException ? failure.Message : "操作失败";
        }
        finally
        {
            request = null;
            nextPoll = Time.unscaledTime + (viewing && prices?.Refreshing == true && error.Length == 0 ? 2 : 12);
        }
    }

    private static bool SameMap<T>(Dictionary<string,T>? a, Dictionary<string,T>? b) =>
        (a?.Count ?? 0) == (b?.Count ?? 0) && (a == null || a.All(p => b != null && b.TryGetValue(p.Key, out var v) && EqualityComparer<T>.Default.Equals(p.Value,v)));

    private static void ApplyPrices(Prices next, bool fill, bool syncRules)
    {
        if (!SameMap(prices?.Overrides,next.Overrides) || !SameMap(prices?.Listings,next.Listings)) filterStamp++;
        prices = next;
        FleaListingTemplates.Apply(prices.Listings, ActiveRules.UnbanAll);
        if (!autoPercentSynced || syncRules)
        {
            autoPercentDraft = ActiveRules.AutoPricePercent.ToString(CultureInfo.InvariantCulture);
            autoPercentSynced = true;
        }
        if (fill) draft = prices.Price?.ToString("0", CultureInfo.InvariantCulture) ?? "";
    }

    private static void FinishStatus()
    {
        if (statusRequest?.IsCompleted != true) return;
        try
        {
            string response = statusRequest.GetAwaiter().GetResult();
            // A mutation or selection made after this poll supersedes its response.
            if (statusRevision != stateRevision || statusTemplate != selected?.Id || request != null) return;
            using var json = JsonDocument.Parse(response);
            var root = json.RootElement;
            if (root.TryGetProperty("err",out var code) && code.GetInt32()!=0) throw new ArgumentException("同步失败");
            var data = CharacterDataPanel.Data(root);
            var next = data.Deserialize<Prices>() ?? throw new ArgumentException("同步失败");
            ApplyPrices(next, prices == null && draft.Length == 0, false);
        }
        catch (Exception failure) { if (prices == null) error = failure is TimeoutException ? "连接超时" : "同步失败"; }
        finally
        {
            statusRequest = null;
            nextPoll = Time.unscaledTime + (viewing && prices?.Refreshing == true ? 2 : 12);
        }
    }

    private static void FinishDialog()
    {
        if (dialogTask?.IsCompleted != true)
            return;
        var completed = dialogTask;
        bool isImport = dialogIsImport;
        string? exportPreset = dialogExportPreset;
        dialogTask = null;
        dialogExportPreset = null;
        try
        {
            string? chosen = completed.GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(chosen))
                return;
            if (isImport)
            {
                string raw = File.ReadAllText(chosen, Encoding.UTF8);
                string stem = presetName.Trim();
                if (stem.Length == 0)
                    stem = Path.GetFileNameWithoutExtension(chosen).Trim();
                if (stem.Length > 80)
                    stem = stem[..80];
                if (stem.Length == 0)
                    stem = "Imported";
                presetName = stem;
                Begin("fleaConfigImport", preset: stem, fleaJson: raw);
            }
            else
            {
                pendingExportSavePath = chosen;
                Begin("fleaConfigExport", preset: exportPreset);
            }
        }
        catch (Exception failure)
        {
            error = string.Format(I18n.Text("文件操作失败：{0}"), failure.Message);
        }
    }

    private static void BeginRaidBuy()
    {
        if (request != null || pendingBuy != null || buySession != RequestHandler.SessionId || selected == null)
            return;
        try
        {
            if (!InLocalRaid())
                throw new ArgumentException("仅支持本地单人战局内直购。");
            if (!int.TryParse(buyCountDraft.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count < 1)
                throw new ArgumentException("请输入 Int32 范围内的正整数购买数量。");
            if (ItemCatalog.Category(selected.Id) == LootCategory.任务物品)
                throw new ArgumentException("任务物品无法直购。");

            var world = Singleton<GameWorld>.Instance;
            var player = world?.MainPlayer;
            if (player == null || !player.IsYourPlayer || player.HealthController?.IsAlive != true)
                throw new ArgumentException("需要角色存活于战局中。");
            var controller = player.InventoryController;
            var factory = Singleton<ItemFactory>.Instance;
            if (controller == null || factory == null)
                throw new ArgumentException("库存尚未就绪。");

            var probe = factory.CreateItem(factory.NextId.ToString(), selected.Id, null)
                ?? throw new ArgumentException("无法创建该物品实例。");
            if (probe.QuestItem)
                throw new ArgumentException("任务物品无法直购。");
            int stackMax = Math.Max(1, probe.StackMaxSize);
            if (count > 95L * stackMax)
                throw new ArgumentException("购买数量超出单笔直购上限。");
            probe.StackObjectsCount = Math.Min(count, stackMax);
            if (FindRaidDestination(player, controller, probe) == null)
                throw new ArgumentException("背包、胸挂或口袋空间不足，请先腾出格位。");

            string displayName = I18n.English ? selected.Title : selected.Name;
            var order = new PendingRaidBuy(Guid.NewGuid().ToString("N"), selected.Id, displayName, count, stackMax);
            SaveRaidBuy(order);
            pendingBuy = order;
            buyRaid = CurrentRaidPtr();
            nextBuyRetry = 0;
        }
        catch (Exception failure)
        {
            error = failure.Message;
        }
    }

    private static ItemAddress? FindRaidDestination(Player player, InventoryController controller, Item item)
    {
        var equipment = player.Profile?.Inventory?.Equipment;
        if (equipment == null)
            return null;

        foreach (var containerSlot in new[] { EquipmentSlot.Backpack, EquipmentSlot.TacticalVest, EquipmentSlot.Pockets })
        {
            var container = equipment.GetSlot(containerSlot)?.ContainedItem?.TryCast<CompoundItem>();
            if (container?.Grids == null)
                continue;
            foreach (var grid in container.Grids)
            {
                if (grid == null)
                    continue;
                var candidate = grid.FindLocationForItem(item);
                if (candidate != null && ItemManipulator.Add(item, candidate, controller, true).Succeeded)
                    return candidate;
            }
        }

        foreach (var equipSlot in new[] { EquipmentSlot.Backpack, EquipmentSlot.TacticalVest, EquipmentSlot.ArmorVest, EquipmentSlot.Headwear, EquipmentSlot.FirstPrimaryWeapon, EquipmentSlot.SecondPrimaryWeapon, EquipmentSlot.Holster })
        {
            var slot = equipment.GetSlot(equipSlot);
            if (slot == null || slot.ContainedItem != null)
                continue;
            var candidate = slot.CreateItemAddress();
            if (ItemManipulator.Add(item, candidate, controller, true).Succeeded)
                return candidate;
        }
        return null;
    }

    private static string RaidBuyPath(string session)
    {
        string profile = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(session)));
        return Path.Combine(BepInEx.Paths.ConfigPath, "flea-raid-buy-" + profile + ".json");
    }

    private static void SaveRaidBuy(PendingRaidBuy order)
    {
        string path = RaidBuyPath(buySession!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(order));
        File.Move(path + ".tmp", path, overwrite: true);
    }

    private static void ClearRaidBuy()
    {
        File.Delete(RaidBuyPath(buySession!));
        pendingBuy = null;
        buyRaid = IntPtr.Zero;
    }

    private static void TickRaidBuy()
    {
        if (buyTask?.IsCompleted == true)
            FinishRaidBuy();
        if (Time.unscaledTime < nextBuyRetry || buyTask != null)
            return;

        try
        {
            if (mailFallbackTask?.IsCompleted == true)
            {
                var (success, message) = mailFallbackTask.GetAwaiter().GetResult();
                mailFallbackTask = null;
                if (success)
                {
                    var order = pendingBuy!;
                    ClearRaidBuy();
                    AnnounceRaidBuy(order);
                }
                else
                {
                    error = message;
                    nextBuyRetry = Time.unscaledTime + 5;
                    return;
                }
            }

            if (deliveryDirty)
            {
                SaveRaidBuy(pendingBuy!);
                deliveryDirty = false;
            }
            if (mailFallbackTask != null || string.IsNullOrWhiteSpace(RequestHandler.SessionId))
                return;
            if (buySession != RequestHandler.SessionId)
            {
                string session = RequestHandler.SessionId;
                string path = RaidBuyPath(session);
                pendingBuy = File.Exists(path) ? JsonSerializer.Deserialize<PendingRaidBuy>(File.ReadAllText(path)) : null;
                buySession = session;
                buyRaid = IntPtr.Zero;
            }
            if (pendingBuy is not { } pending)
                return;
            if (pending.Added == pending.Count)
            {
                ClearRaidBuy();
                AnnounceRaidBuy(pending);
            }
            else if (pending.Added is int added)
            {
                var items = BuildFallbackMail(pending.TemplateId, pending.Count - added, pending.StackMax, pending.Fir);
                mailFallbackTask = GearMailClient.Deliver(items, true, "flea_" + pending.Id, buySession)
                    .WaitAsync(TimeSpan.FromSeconds(15));
            }
            else
            {
                buyTask = CharacterDataPanel.Call("/local/gear/data", "characterData", new()
                {
                    ["action"] = "fleaRaidBuy",
                    ["id"] = pending.Id,
                    ["template"] = pending.TemplateId,
                    ["count"] = pending.Count
                }, buySession).WaitAsync(TimeSpan.FromSeconds(15));
            }
        }
        catch (Exception failure)
        {
            if (mailFallbackTask?.IsCompleted == true)
                mailFallbackTask = null;
            error = failure is TimeoutException ? "连接超时" : failure.Message;
            nextBuyRetry = Time.unscaledTime + 5;
        }
    }

    private static void FinishRaidBuy()
    {
        if (buyTask?.IsCompleted != true)
            return;
        var order = pendingBuy!;
        var completed = buyTask;
        buyTask = null;
        try
        {
            using var doc = JsonDocument.Parse(completed.GetAwaiter().GetResult());
            var root = doc.RootElement;
            var data = CharacterDataPanel.Data(root);
            if (root.TryGetProperty("err", out var err) && err.GetInt32() != 0)
            {
                if (data.TryGetProperty("rejected", out var rejected) && rejected.ValueKind == JsonValueKind.True)
                    ClearRaidBuy();
                throw new ArgumentException(root.TryGetProperty("errmsg", out var msg) ? msg.GetString() ?? "直购失败" : "直购失败");
            }
            if (!data.GetProperty("ok").GetBoolean() || data.GetProperty("count").GetInt32() != order.Count ||
                data.GetProperty("template").GetString() != order.TemplateId)
                throw new InvalidDataException("直购订单响应不匹配。");
            long totalCost = data.GetProperty("totalCost").GetInt64();
            bool fir = data.GetProperty("fir").GetBoolean();
            long? remainingRub = data.TryGetProperty("stashRoubles", out var rem) && rem.TryGetInt64(out long r) ? r : null;
            if (prices != null && remainingRub.HasValue)
                prices = prices with { StashRoubles = remainingRub };

            int added = 0;
            var player = Singleton<GameWorld>.Instance?.MainPlayer;
            var controller = player?.InventoryController;
            var factory = Singleton<ItemFactory>.Instance;
            if (buyRaid != IntPtr.Zero && CurrentRaidPtr() == buyRaid && buySession == RequestHandler.SessionId &&
                player != null && player.IsYourPlayer &&
                player.HealthController?.IsAlive == true && controller != null && factory != null)
            {
                try
                {
                    while (added < order.Count)
                    {
                        var item = factory.CreateItem(factory.NextId.ToString(), order.TemplateId, null);
                        if (item == null)
                            break;
                        int count = Math.Min(order.Count - added, order.StackMax);
                        item.StackObjectsCount = count;
                        ItemSpawner.SetCommandFir(item, fir);
                        var destination = FindRaidDestination(player, controller, item);
                        if (destination == null)
                            break;
                        try { controller.AddAndRaiseEvents(item, destination); }
                        finally { if (item.CurrentAddress != null) added += count; }
                        if (item.CurrentAddress == null)
                            break;
                    }
                    player.Profile?.Inventory?.Equipment?.RaiseRefreshEvent(false, true);
                }
                catch (Exception failure)
                {
                    error = failure.Message;
                }
            }
            pendingBuy = order with { Added = added, Fir = fir, Cost = totalCost };
            deliveryDirty = true;
        }
        catch (TimeoutException)
        {
            error = "连接超时";
            nextBuyRetry = Time.unscaledTime + 5;
        }
        catch (Exception failure)
        {
            error = failure is ArgumentException ? failure.Message : "直购失败";
            nextBuyRetry = Time.unscaledTime + 5;
            FeatureToasts.Show(error);
        }
    }

    private static void AnnounceRaidBuy(PendingRaidBuy order)
    {
        int added = order.Added!.Value;
        string cost = order.Cost.ToString("N0", CultureInfo.CurrentCulture);
        string message = added == order.Count
            ? string.Format(I18n.Text("已直购到背包：{0} ×{1}（-₽{2}）"), order.ItemName, added, cost)
            : string.Format(I18n.Text("已直购 {0} ×{1}（-₽{2}），×{3} 已转入邮件"), order.ItemName, added, cost, order.Count - added);
        error = "";
        ShowMessage(message);
        FeatureToasts.Show(message);
    }

    private static List<Dictionary<string, object?>> BuildFallbackMail(string templateId, int count, int stackMax, bool fir)
    {
        var items = new List<Dictionary<string, object?>>();
        int index = 0;
        while (count > 0)
        {
            int chunk = Math.Min(count, stackMax);
            var spec = new Dictionary<string, object?>
            {
                ["ref"] = "flea_buy_" + index++,
                ["template"] = templateId,
                ["count"] = chunk
            };
            if (fir)
                spec["attributes"] = new Dictionary<string, object?> { ["foundInRaid"] = true };
            items.Add(spec);
            count -= chunk;
        }
        return items;
    }

    internal static void Draw(Rect area, bool showShortcutSetting)
    {
        float x = area.x, y = area.y, width = area.width;
        const float gap = 8f;
        bool wide = width >= 760f;
        const float shortcutWidth = 248f;
        if (showShortcutSetting && !wide)
        {
            FleaMarketShortcut.DrawSetting(new Rect(x, y, width, 30));
            y += 34;
        }
        bool busy = request != null || buyTask != null || dialogTask != null;
        bool enabled = GUI.enabled;
        try
        {
            GUI.enabled = enabled && !busy;
            float priceWidth = wide ? 324f : width;
            float modeWidth = (priceWidth - 74f) / 3f;
            for (int i = 0; i < modes.Length; i++)
            {
                var mode = modes[i];
                if (InterfaceMenu.Button(new Rect(x + i * (modeWidth + 4), y, modeWidth, 29), mode.Label, mode.Id == prices?.Mode))
                    Begin("fleaPriceMode", fill: true, mode: mode.Id);
            }
            GUI.enabled = enabled && !busy && prices?.Mode is "pvp" or "pve" && prices.Refreshing == false;
            if (InterfaceMenu.Button(new Rect(x + priceWidth - 62, y, 62, 29), "同步"))
                Begin("fleaPriceRefresh");

            GUI.enabled = enabled;
            string status = error.Length > 0 ? error :
                buyTask != null ? I18n.Text("正在直购…") :
                Time.unscaledTime < configMessageUntil ? configMessage :
                prices is { Refreshing: true } ? "同步中…" :
                prices is { Failed: true } ? "同步失败" :
                prices?.Updated?.ToLocalTime().ToString("MM-dd HH:mm") ?? "";
            Color? statusColor = error.Length > 0 ? new Color(1f, 0.45f, 0.35f) :
                Time.unscaledTime < configMessageUntil ? InterfaceMenu.Accent : null;
            if (wide)
            {
                float statusWidth = width - priceWidth - gap - (showShortcutSetting ? shortcutWidth + gap : 0);
                InterfaceMenu.Label(new Rect(x + priceWidth + gap, y, statusWidth, 29), status, statusColor, 12);
                if (showShortcutSetting)
                    FleaMarketShortcut.DrawSetting(new Rect(area.xMax - shortcutWidth, y, shortcutWidth, 29));
                y += 35;
            }
            else
            {
                y += 33;
                if (status.Length > 0)
                {
                    InterfaceMenu.Label(new Rect(x, y, width, 21), status, statusColor, 12);
                    y += 23;
                }
            }

            string[] pages = ["物品列表", "市场规则", "跳蚤方案", "自定义报价"];
            float pageWidth=(width-18)/4;
            for(int i=0;i<pages.Length;i++)
                if(InterfaceMenu.Button(new(x+i*(pageWidth+6),y,pageWidth,28),pages[i],tab==i))tab=i;
            y += 34;

            var body = new Rect(x, y, width, Math.Max(1, area.yMax - y));
            if (tab == 3) { DrawCustomOffers(body, enabled, busy); return; }
            if (tab == 1)
            {
                DrawRules(body, enabled, busy);
                return;
            }
            if (tab == 2)
            {
                DrawPresets(body, enabled, busy);
                return;
            }
            DrawBrowser(body, enabled, busy);
        }
        finally
        {
            GUI.enabled = enabled;
        }
    }

    private static void DrawBrowser(Rect area, bool enabled, bool busy)
    {
        bool sideBySide = selected != null && area.width >= 760f;
        float detailsWidth = Math.Clamp(area.width * .36f, 312f, 410f);
        float x = area.x, y = area.y, width = sideBySide ? area.width - detailsWidth - 12f : area.width;

        search = InterfaceMenu.Field(new Rect(x, y, width - 68, 29), search);
        if (search.Length == 0)
            InterfaceMenu.Label(new Rect(x + 8, y, width - 84, 29), "搜索名称或模板 ID…", Color.Lerp(InterfaceMenu.Text, InterfaceMenu.Background, .55f), 12);
        if (InterfaceMenu.Button(new Rect(x + width - 64, y, 64, 29), "批量", batchOpen))
        {
            batchOpen = !batchOpen;
            choosingCategory = false;
        }
        y += 33;

        float catBtnW = Math.Min(108f, width * 0.28f);
        float chipW = (width - catBtnW - 16f) / 4f;
        for (int i = 0; i < scopeFilters.Length; i++)
        {
            var sf = scopeFilters[i];
            if (InterfaceMenu.Button(new Rect(x + i * (chipW + 4f), y, chipW, 26f), sf.Label, scopeFilter == sf.Id))
                scopeFilter = sf.Id;
        }
        string catText = string.Format(I18n.Text("类别:{0}"), I18n.Text(CategoryLabel(categoryFilter)));
        if (InterfaceMenu.Button(new Rect(x + width - catBtnW, y, catBtnW, 26f), catText, categoryFilter != LootCategory.全部 || choosingCategory))
        {
            choosingCategory = !choosingCategory;
            batchOpen = false;
        }
        y += 30;

        if (choosingCategory)
        {
            const int cols = 5;
            float cw = (width - (cols - 1) * 4f) / cols;
            for (int i = 0; i < categories.Length; i++)
            {
                int col = i % cols, row = i / cols;
                var entry = categories[i];
                if (InterfaceMenu.Button(new Rect(x + col * (cw + 4f), y + row * 28f, cw, 25f), entry.Label, categoryFilter == entry.Cat))
                {
                    categoryFilter = entry.Cat;
                    choosingCategory = false;
                }
            }
            y += 58;
        }

        RefreshResults();

        if (batchOpen)
        {
            InterfaceMenu.Surface(new Rect(x, y, width, 68f), Color.Lerp(InterfaceMenu.Background, InterfaceMenu.Text, .05f), 7);
            GUI.enabled = enabled && !busy && results.Length > 0;
            InterfaceMenu.Label(new Rect(x + 6f, y + 4f, 44f, 26f), "倍率", null, 12);
            batchScaleDraft = InterfaceMenu.Field(new Rect(x + 48f, y + 4f, 58f, 26f), batchScaleDraft);
            bool validScale = double.TryParse(batchScaleDraft.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double scale) && double.IsFinite(scale);
            float b1 = (width - 122f) / 2f;
            GUI.enabled = enabled && !busy && results.Length > 0 && validScale;
            if (InterfaceMenu.Button(new Rect(x + 112f, y + 4f, b1, 26f), "倍率改价"))
                Begin("fleaBatchApply", fill: true, price: scale, batchOp: "scalePrice", batchIds: results.Select(r => r.Id).ToArray());
            GUI.enabled = enabled && !busy && results.Length > 0;
            if (InterfaceMenu.Button(new Rect(x + 116f + b1, y + 4f, b1, 26f), "重置改价"))
                Begin("fleaBatchApply", fill: true, batchOp: "resetPrice", batchIds: results.Select(r => r.Id).ToArray());

            float b2 = (width - 20f) / 3f;
            if (InterfaceMenu.Button(new Rect(x + 6f, y + 36f, b2, 26f), "批量允许"))
                Begin("fleaBatchApply", allowed: true, batchOp: "setListing", batchIds: results.Select(r => r.Id).ToArray());
            if (InterfaceMenu.Button(new Rect(x + 10f + b2, y + 36f, b2, 26f), "批量禁止"))
                Begin("fleaBatchApply", allowed: false, batchOp: "setListing", batchIds: results.Select(r => r.Id).ToArray());
            if (InterfaceMenu.Button(new Rect(x + 14f + b2 * 2f, y + 36f, b2, 26f), "重置权限"))
                Begin("fleaBatchApply", batchOp: "resetListing", batchIds: results.Select(r => r.Id).ToArray());
            GUI.enabled = enabled;
            y += 74f;
        }

        bool inRaid = InLocalRaid();
        float settingsHeight = inRaid ? 157f : 119f;
        float available = Math.Max(0, area.yMax - y);
        float detailsHeight = selected != null && !sideBySide
            ? Math.Min(settingsHeight, Math.Max(0, available - 48f))
            : 0f;
        float listHeight = available - (detailsHeight > 0 ? detailsHeight + 8f : 0);
        GUI.enabled = enabled && !busy;
        if (listHeight > 0)
            DrawItems(new Rect(x, y, width, listHeight));
        GUI.enabled = enabled;
        if (sideBySide)
        {
            InterfaceMenu.Surface(new Rect(x + width + 5f, area.y, 1f, area.height),
                Color.Lerp(InterfaceMenu.Background, InterfaceMenu.Text, .18f), 0);
            DrawItemSettingsView(new Rect(x + width + 12f, area.y, detailsWidth, area.height),
                settingsHeight, enabled, busy, inRaid);
        }
        else if (detailsHeight > 0 && selected != null)
        {
            DrawItemSettingsView(new Rect(x, y + listHeight + 8f, width, detailsHeight),
                settingsHeight, enabled, busy, inRaid);
        }
    }

    private static void DrawItemSettingsView(Rect area, float height, bool enabled, bool busy, bool inRaid)
    {
        if (area.height >= height)
        {
            DrawItemSettings(area, enabled, busy, inRaid);
            return;
        }

        var content = new Rect(0, 0, area.width - 17f, height);
        itemSettingsScroll = GUI.BeginScrollView(area, itemSettingsScroll, content);
        try { DrawItemSettings(content, enabled, busy, inRaid); }
        finally { GUI.EndScrollView(); }
    }

    private static string CategoryLabel(LootCategory cat)
    {
        foreach (var entry in categories)
            if (entry.Cat == cat)
                return entry.Label;
        return "全部";
    }

    private static void RefreshResults()
    {
        var current = ItemSpawner.CommandItems;
        if (catalog == current && searched == search && appliedScope == scopeFilter &&
            appliedCategory == categoryFilter && appliedStamp == filterStamp)
            return;

        bool changedFilter = catalog != current || searched != search || appliedScope != scopeFilter || appliedCategory != categoryFilter;
        catalog = current;
        searched = search;
        appliedScope = scopeFilter;
        appliedCategory = categoryFilter;
        appliedStamp = filterStamp;

        var raw = current.Search(search);
        var overrides = prices?.Overrides;
        var listings = prices?.Listings;

        if (scopeFilter == 0 && categoryFilter == LootCategory.全部)
        {
            results = raw;
        }
        else
        {
            var filtered = new List<ChatItemSearch.Item>(raw.Length);
            foreach (var item in raw)
            {
                if (categoryFilter != LootCategory.全部 && ItemCatalog.Category(item.Id) != categoryFilter)
                    continue;
                bool matchScope = scopeFilter switch
                {
                    1 => overrides?.ContainsKey(item.Id) == true,
                    2 => listings != null && listings.TryGetValue(item.Id, out bool a) && a,
                    3 => listings != null && listings.TryGetValue(item.Id, out bool b) && !b,
                    _ => true
                };
                if (matchScope)
                    filtered.Add(item);
            }
            results = filtered.ToArray();
        }
        if (changedFilter) scroll = Vector2.zero;
    }

    private static void DrawItems(Rect area)
    {
        float width = area.width - 17f;
        var overrides = prices?.Overrides;
        var listings = prices?.Listings;
        scroll = GUI.BeginScrollView(area, scroll, new Rect(0, 0, width, results.Length * RowHeight));
        try
        {
            if (results.Length == 0)
            {
                InterfaceMenu.Label(new Rect(4, 4, width - 8, 26), "没有符合当前筛选的物品", null, 12);
                return;
            }
            int first = Math.Max(0, (int)(scroll.y / RowHeight));
            int end = Math.Min(results.Length, first + (int)Math.Ceiling(area.height / RowHeight) + 1);
            for (int i = first; i < end; i++)
            {
                var item = results[i];
                float rowY = i * RowHeight;
                var name = I18n.English ? item.Title : item.Name;

                bool hasManual = overrides != null && overrides.TryGetValue(item.Id, out int manualVal);
                int mPrice = hasManual ? overrides![item.Id] : 0;
                bool hasListing = listings != null && listings.TryGetValue(item.Id, out _);
                bool allowVal = hasListing && listings![item.Id];

                string badge = "";
                if (hasManual)
                    badge = "₽" + mPrice.ToString("N0", CultureInfo.CurrentCulture);
                if (hasListing)
                    badge += (badge.Length > 0 ? " " : "") + "[" + I18n.Text(allowVal ? "允许" : "禁止") + "]";

                float badgeWidth = badge.Length > 0 ? Math.Min(width * 0.42f, InterfaceMenu.TextWidth(badge, 11) + 12f) : 0f;
                if (InterfaceMenu.Button(new Rect(0, rowY, width - badgeWidth, 29f), name, selected?.Id == item.Id))
                {
                    selected = item;
                    itemSettingsScroll = Vector2.zero;
                    prices = prices is { } prior ? prior with { Price = null, Manual = null, Listing = null } : null;
                    draft = "";
                    Begin("fleaPriceStatus", fill: true);
                }
                if (badgeWidth > 0f)
                {
                    Color badgeColor = hasListing && !allowVal
                        ? new Color(1f, 0.5f, 0.35f)
                        : hasListing && allowVal
                            ? new Color(0.45f, 0.95f, 0.55f)
                            : InterfaceMenu.Accent;
                    InterfaceMenu.Label(new Rect(width - badgeWidth + 4f, rowY, badgeWidth - 4f, 29f), badge, badgeColor, 11, translate: false);
                }
            }
        }
        finally
        {
            GUI.EndScrollView();
        }
    }

    private static void DrawItemSettings(Rect area, bool enabled, bool busy, bool inRaid)
    {
        var item = selected!;
        InterfaceMenu.Label(new Rect(area.x, area.y, area.width, 22), I18n.English ? item.Title : item.Name, InterfaceMenu.Accent, 14, translate: false);

        string current = prices?.Price?.ToString("N0", CultureInfo.CurrentCulture) ?? "—";
        string priceLine = I18n.Text("当前基准价") + " · ₽" + current;
        if (inRaid && prices?.StashRoubles is long stashRub)
            priceLine += "   " + I18n.Text("仓库卢布") + " · ₽" + stashRub.ToString("N0", CultureInfo.CurrentCulture);
        InterfaceMenu.Label(new Rect(area.x, area.y + 20, area.width, 20), priceLine, null, 12, translate: false);

        float y = area.y + 44;
        GUI.enabled = enabled && prices?.Price != null && (!busy || !fillDraft);
        draft = InterfaceMenu.Field(new Rect(area.x, y, area.width - 146, 29), draft);
        bool valid = int.TryParse(draft, NumberStyles.Integer, CultureInfo.InvariantCulture, out int price);
        GUI.enabled = enabled && !busy && prices?.Price != null && valid;
        if (InterfaceMenu.Button(new Rect(area.xMax - 138, y, 64, 29), "保存"))
            Begin("fleaPriceSet", fill: true, price: price);
        GUI.enabled = enabled && !busy && prices?.Manual != null;
        if (InterfaceMenu.Button(new Rect(area.xMax - 67, y, 67, 29), "重置"))
            Begin("fleaPriceReset", fill: true);

        y += 34;
        InterfaceMenu.Label(new Rect(area.x, y, 82, 28), "出售权限", null, 13);
        GUI.enabled = enabled && !busy && prices?.Price != null;
        float optionWidth = (area.width - 90) / 3;
        for (int i = 0; i < listingModes.Length; i++)
        {
            var option = listingModes[i];
            if (InterfaceMenu.Button(new Rect(area.x + 82 + i * (optionWidth + 4), y, optionWidth, 28),
                option.Label, option.Allowed == prices?.Listing) && option.Allowed != prices?.Listing)
                Begin(option.Allowed.HasValue ? "fleaListingSet" : "fleaListingReset", allowed: option.Allowed);
        }

        if (inRaid)
        {
            y += 38;
            GUI.enabled = enabled && !busy;
            InterfaceMenu.Label(new Rect(area.x, y, 42, 29), "数量", null, 12);
            buyCountDraft = InterfaceMenu.Field(new Rect(area.x + 44, y, 56, 29), buyCountDraft);
            bool validQty = int.TryParse(buyCountDraft.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int qty) && qty >= 1;
            double unitRub = MiscPanel.Options.ClearanceMarket ? 1 : (prices?.Price ?? 0);
            string totalLabel = validQty && unitRub > 0
                ? "₽" + ((long)Math.Round(unitRub) * qty).ToString("N0", CultureInfo.CurrentCulture)
                : "—";
            float buyBtnW = 114f;
            InterfaceMenu.Label(new Rect(area.x + 106, y, Math.Max(40, area.width - 114 - buyBtnW), 29), totalLabel, InterfaceMenu.Accent, 12, translate: false);
            GUI.enabled = enabled && !busy && pendingBuy == null && validQty && prices?.Price != null && prices?.Listing != false;
            if (InterfaceMenu.Button(new Rect(area.xMax - buyBtnW, y, buyBtnW, 29), "直购到背包"))
                BeginRaidBuy();
        }
        GUI.enabled = enabled;
    }

    private static void DrawRules(Rect area, bool enabled, bool busy)
    {
        var rules = ActiveRules;
        float contentW = area.width - 18f;
        const int ruleCount = 7;
        const float rowHeight = 62f, gap = 8f;
        int columns = contentW >= 850f ? 2 : 1;
        float ruleWidth = (contentW - gap * (columns - 1)) / columns;
        float rulesHeight = ((ruleCount + columns - 1) / columns) * rowHeight;
        float totalHeight = rulesHeight + 149f;
        rulesScroll = GUI.BeginScrollView(area, rulesScroll, new Rect(0, 0, contentW, totalHeight));
        try
        {
            int ruleIndex = 0;
            GUI.enabled = enabled && !busy && prices != null;

            void ToggleRow(string title, string subtitle, bool active, Func<RulesDto, RulesDto> mutate)
            {
                float x = ruleIndex % columns * (ruleWidth + gap);
                float rowY = ruleIndex++ / columns * rowHeight;
                InterfaceMenu.Surface(new Rect(x, rowY, ruleWidth, 56f), Color.Lerp(InterfaceMenu.Background, InterfaceMenu.Text, .045f), 7);
                InterfaceMenu.Label(new Rect(x + 10f, rowY + 6f, ruleWidth - 96f, 22f), title, active ? InterfaceMenu.Accent : InterfaceMenu.Text, 13);
                InterfaceMenu.Label(new Rect(x + 10f, rowY + 29f, ruleWidth - 96f, 20f), subtitle, Color.Lerp(InterfaceMenu.Text, InterfaceMenu.Background, .38f), 11);
                if (InterfaceMenu.Button(new Rect(x + ruleWidth - 78f, rowY + 13f, 68f, 30f), active ? "开启" : "关闭", active))
                    Begin("fleaRulesSave", rules: mutate(rules));
            }

            ToggleRow("全物品解禁", "解除禁售黑名单与高防插板限制（任务物品除外）",
                rules.UnbanAll, r => r with { UnbanAll = !r.UnbanAll });
            ToggleRow("解除等级限制", "免除 15 级门槛及分类/物品买卖等级锁",
                rules.IgnoreLevelLocks, r => r with { IgnoreLevelLocks = !r.IgnoreLevelLocks });
            ToggleRow("跳蚤购买带战局勾", "从跳蚤购买的物品附带战局内搜刮标记（FIR）",
                rules.PurchasesFir, r => r with { PurchasesFir = !r.PurchasesFir });
            ToggleRow("AI 报盘满状态且保留插板", "新生成的 AI 报盘满耐久/满资源且保留防弹插板",
                rules.FullCondition, r => r with { FullCondition = !r.FullCondition });
            ToggleRow("纯卢布市场", "非玩家外币报盘统一折算为卢布结算",
                rules.RoubleOnly, r => r with { RoubleOnly = !r.RoubleOnly });
            ToggleRow("屏蔽以物易物单", "隐藏并停用非玩家的物品交换报盘",
                rules.NoBarter, r => r with { NoBarter = !r.NoBarter });
            ToggleRow("商人回收比价提示", "原生添加报价窗口显示商人最高回收价与税后净得对比",
                rules.ShowTraderCompare, r => r with { ShowTraderCompare = !r.ShowTraderCompare });

            float y = rulesHeight;
            InterfaceMenu.Surface(new Rect(0, y, contentW, 74f), Color.Lerp(InterfaceMenu.Background, InterfaceMenu.Text, .045f), 7);
            InterfaceMenu.Label(new Rect(10f, y + 6f, contentW - 20f, 22f), "原生上架自动填价", InterfaceMenu.Accent, 13);
            float modeW = (contentW - 32f) / 4f;
            for (int i = 0; i < autoPriceModes.Length; i++)
            {
                var opt = autoPriceModes[i];
                if (InterfaceMenu.Button(new Rect(10f + i * (modeW + 4f), y + 32f, modeW, 28f), opt.Label, rules.AutoPriceMode == opt.Mode)
                    && rules.AutoPriceMode != opt.Mode)
                    Begin("fleaRulesSave", rules: rules with { AutoPriceMode = opt.Mode });
            }
            y += 80f;

            InterfaceMenu.Label(new Rect(4f, y, 106f, 28f), "填价比例 (%)", null, 12);
            autoPercentDraft = InterfaceMenu.Field(new Rect(112f, y, 72f, 28f), autoPercentDraft);
            bool validPct = int.TryParse(autoPercentDraft.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int pct);
            GUI.enabled = enabled && !busy && prices != null && validPct && pct != rules.AutoPricePercent;
            if (InterfaceMenu.Button(new Rect(190f, y, 84f, 28f), "应用比例"))
                Begin("fleaRulesSave", rules: rules with { AutoPricePercent = pct });
            y += 38f;

            GUI.enabled = enabled && !busy && prices != null;
            if (InterfaceMenu.Button(new Rect(0, y, contentW, 31f), "立即重新生成 AI 报盘"))
                Begin("fleaRefreshOffers");
        }
        finally
        {
            GUI.EndScrollView();
            GUI.enabled = enabled;
        }
    }

    private static void DrawPresets(Rect area, bool enabled, bool busy)
    {
        const float saveBtnW = 104f;
        var names = prices?.Presets;
        InterfaceMenu.Label(new Rect(area.x, area.y, area.width, 21), "方案名称（重命名时先在此输入新名称）", null, 12);
        presetName = InterfaceMenu.Field(new Rect(area.x, area.y + 23, area.width - saveBtnW - 6, 29), presetName);
        string name = presetName.Trim();
        bool validName = name.Length is >= 1 and <= 80 && !name.Any(char.IsControl);
        bool exists = names?.Contains(name, StringComparer.OrdinalIgnoreCase) == true;
        GUI.enabled = enabled && !busy && names != null && validName;
        if (InterfaceMenu.Button(new Rect(area.xMax - saveBtnW, area.y + 23, saveBtnW, 29), exists ? "覆盖方案" : "保存方案"))
            Begin("fleaConfigSave", preset: name);

        float halfW = (area.width - 6f) / 2f;
        GUI.enabled = enabled && !busy && prices != null;
        if (InterfaceMenu.Button(new Rect(area.x, area.y + 58, halfW, 28), "导入 JSON"))
        {
            dialogIsImport = true;
            dialogTask = WindowsDialogs.PickOpenFile(I18n.Text("导入跳蚤方案 JSON"));
        }
        if (InterfaceMenu.Button(new Rect(area.x + halfW + 6f, area.y + 58, halfW, 28), "导出当前配置"))
        {
            dialogIsImport = false;
            dialogExportPreset = "*";
            string defaultFile = (validName ? name : "flea-preset") + ".json";
            dialogTask = WindowsDialogs.PickSaveFile(I18n.Text("导出跳蚤方案 JSON"), "json", defaultFile);
        }
        GUI.enabled = enabled;

        names ??= [];
        bool legacy = prices?.HasLegacy == true;
        float y = area.y + 94;
        if (prices is { Presets: null })
        {
            InterfaceMenu.Label(new Rect(area.x, y, area.width, 24), "无法读取跳蚤方案列表。");
            return;
        }
        if (prices != null && names.Length == 0 && !legacy)
        {
            InterfaceMenu.Label(new Rect(area.x, y, area.width, 24), "暂无已保存的跳蚤方案");
            return;
        }

        var list = new Rect(area.x, y, area.width, Math.Max(1, area.yMax - y));
        int count = names.Length + (legacy ? 1 : 0);
        float rowWidth = list.width - 17;
        presetScroll = GUI.BeginScrollView(list, presetScroll, new Rect(0, 0, rowWidth, count * RowHeight));
        try
        {
            int first = Math.Max(0, (int)(presetScroll.y / RowHeight));
            int end = Math.Min(count, first + (int)Math.Ceiling(list.height / RowHeight) + 1);
            for (int i = first; i < end; i++)
            {
                string? entry = i < names.Length ? names[i] : null;
                float rowY = i * RowHeight;
                string label = entry ?? I18n.Text("旧版快照");
                float actionsWidth = entry != null ? 214f : 160f;
                InterfaceMenu.Label(new Rect(0, rowY, Math.Max(40f, rowWidth - actionsWidth - 6f), 29), label, null, 13, translate: false);

                float bx = rowWidth - actionsWidth;
                GUI.enabled = enabled && !busy;
                if (InterfaceMenu.Button(new Rect(bx, rowY, 50f, 28f), "载入"))
                {
                    presetName = entry ?? "";
                    Begin("fleaConfigLoad", fill: true, preset: entry);
                }
                bx += 53f;

                if (entry != null)
                {
                    if (InterfaceMenu.Button(new Rect(bx, rowY, 52f, 28f), "重命名"))
                    {
                        if (!validName || string.Equals(name, entry, StringComparison.Ordinal))
                        {
                            presetName = entry;
                            error = I18n.Text("请先在上方输入新名称，再点重命名。");
                        }
                        else
                        {
                            Begin("fleaConfigRename", preset: entry, targetPreset: name);
                        }
                    }
                    bx += 55f;
                }

                if (InterfaceMenu.Button(new Rect(bx, rowY, 50f, 28f), "导出"))
                {
                    dialogIsImport = false;
                    dialogExportPreset = entry;
                    string safeName = string.Concat((entry ?? "legacy-flea").Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
                    dialogTask = WindowsDialogs.PickSaveFile(I18n.Text("导出跳蚤方案 JSON"), "json", safeName + ".json");
                }
                bx += 53f;

                string deleteKey = entry ?? "<legacy>";
                bool confirming = confirmDeleteTarget == deleteKey && Time.unscaledTime < confirmDeleteUntil;
                if (InterfaceMenu.Button(new Rect(bx, rowY, 53f, 28f), confirming ? "确认?" : "删除", confirming))
                {
                    if (confirming)
                    {
                        confirmDeleteTarget = null;
                        Begin("fleaConfigDelete", preset: entry);
                    }
                    else
                    {
                        confirmDeleteTarget = deleteKey;
                        confirmDeleteUntil = Time.unscaledTime + 4f;
                    }
                }
            }
        }
        finally
        {
            GUI.EndScrollView();
            GUI.enabled = enabled;
        }
    }
}
