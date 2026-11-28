using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using HarmonyLib;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Generators.Ragfair;
using SPTarkov.Server.Core.Helpers.Commerce;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Ragfair;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Eft.Ragfair;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Items;
using SPTarkov.Server.Core.Services.Ragfair;
using SPTarkov.Server.Core.Utils;
using Path = System.IO.Path;

namespace LocalGearMail;

internal static class FleaPrices
{
    private const string RubTpl = "5449016a4bdc2d6f028b456f";
    private const string UsdTpl = "5696686a4bdc2da3298b456a";
    private const string EurTpl = "569668774bdc2da2298b4568";
    private const string RaidBuyReceipts = "localFleaRaidBuyReceipts";
    private sealed record RaidBuyReceipt(string Template, int Count, int UnitPrice, bool Fir);
    private static readonly MongoId RubId = new(RubTpl);
    private static readonly MongoId UsdId = new(UsdTpl);
    private static readonly MongoId EurId = new(EurTpl);
    private static readonly JsonSerializerOptions FlexibleJson = new() { PropertyNameCaseInsensitive = true };

    private static readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
    private static readonly FieldInfo offerService = ControllerField<RagfairOfferService>();
    private static readonly FieldInfo eventOutput = ControllerField<EventOutputHolder>();
    private static readonly FieldInfo responses = ControllerField<HttpResponseUtil>();
    private static readonly FieldInfo globalTableField = ControllerField<GlobalTable>();
    private static readonly FieldInfo serverOfferService = ServerField<RagfairOfferService>();
    private static readonly FieldInfo serverOfferGenerator = ServerField<RagfairOfferGenerator>();
    private static readonly FieldInfo? helperItemHelper = HelperField<ItemHelper>();
    private static readonly FieldInfo? helperItemFilter = HelperField<ItemFilterService>();
    private static readonly HashSet<MongoId> invalidBaseTypes =
    [
        new("566abbb64bdc2d144c8b457d"), // Stash
        new("602543c13fee350cd564d032"), // SortingTable
        new("55d7217a4bdc2d86028b456d"), // Inventory
        new("557596e64bdc2dc2118b4571"), // Pockets
        new("566965d44bdc2d814c8b4571"), // LootContainer
        new("63da6da4784a55176c018dba"), // HideoutAreaContainer
        new("543be5dd4bdc2deb348b4569")  // Money
    ];

    [ThreadStatic] private static int inSingleOffer;
    private static FleaPriceBook book = null!;
    private static TemplateTable? installedTemplates;
    private static RagfairServer? ragfairServer;
    private static (double MinUserLevel, bool ByCategory)? originalRagfairGlobals;

    internal static FleaRules CurrentRules => book?.Current.Rules ?? new FleaRules();

    private static FieldInfo ControllerField<T>() => typeof(RagfairController)
        .GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Single(field => field.FieldType == typeof(T));

    private static FieldInfo ServerField<T>() => typeof(RagfairServer)
        .GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Single(field => field.FieldType == typeof(T));

    private static FieldInfo? HelperField<T>() => typeof(RagfairServerHelper)
        .GetFields(BindingFlags.Instance | BindingFlags.NonPublic).SingleOrDefault(field => field.FieldType == typeof(T));

    internal static void Install(Harmony harmony, FleaPriceBook? source = null, TemplateTable? templates = null)
    {
        book = source ?? new FleaPriceBook(Path.Combine(AppContext.BaseDirectory, "user", "mods", "LocalGearMail", "flea-prices"), client);
        installedTemplates = templates;
        ragfairServer = null;
        Patch(typeof(RagfairServer), nameof(RagfairServer.Update), postfix: nameof(Tick));
        Patch(typeof(RagfairPriceService), nameof(RagfairPriceService.GetFleaPriceForItem), prefix: nameof(Base));
        Patch(typeof(RagfairPriceService), nameof(RagfairPriceService.GetDynamicPriceForItem), prefix: nameof(DynamicBase));
        Patch(typeof(RagfairPriceService), nameof(RagfairPriceService.GetAllFleaPrices), postfix: nameof(AllPrices));
        Patch(typeof(RagfairPriceService), nameof(RagfairPriceService.GetDynamicItemPrice), transpiler: nameof(PriceGuards));
        Patch(typeof(RagfairPriceService), "AdjustPriceIfBelowHandbook", prefix: nameof(HandbookFloor));
        Patch(typeof(RagfairPriceService), "AdjustUnreasonablePrice", prefix: nameof(ModPriceCap));
        Patch(typeof(RagfairController), nameof(RagfairController.GetItemMinAvgMaxFleaPriceValues), postfix: nameof(Quote));
        Patch(typeof(RagfairController), nameof(RagfairController.AddPlayerOffer), prefix: nameof(CheckListing));

        // #1 Unban all items & high-class armor plates
        Patch(typeof(RagfairServerHelper), nameof(RagfairServerHelper.IsItemValidRagfairItem), prefix: nameof(CheckValidRagfairItem));
        Patch(typeof(RagfairOfferGenerator), "RemoveBannedPlatesFromPreset", prefix: nameof(SkipBannedPlateRemoval));

        // #2 Bypass character level & EFT 1.3 category/item level locks
        Patch(typeof(RagfairLevelService), nameof(RagfairLevelService.IsLocked), prefix: nameof(UnlockOfferItems));
        Patch(typeof(RagfairLevelService), nameof(RagfairLevelService.IsTemplateLocked), prefix: nameof(UnlockTemplate));
        Patch(typeof(RagfairCategoriesService), nameof(RagfairCategoriesService.GetCategoriesFromOffers), prefix: nameof(UnlockCategories));

        // #6 Flea purchases Found-In-Raid
        Patch(typeof(TradeHelper), nameof(TradeHelper.BuyItem), prefix: nameof(ApplyPurchaseFir));

        // #7 Full condition AI offers & keep armor plates
        Patch(typeof(RagfairOfferGenerator), "RemoveArmorPlates", prefix: nameof(SkipArmorPlateStrip));
        Patch(typeof(RagfairOfferGenerator), "RandomiseItemCondition", prefix: nameof(SkipConditionDegrade));

        // #8 Rouble-only market & suppress barter offers
        Patch(typeof(RagfairServerHelper), nameof(RagfairServerHelper.GetDynamicOfferCurrency), prefix: nameof(ForceDynamicCurrency));
        Patch(typeof(RagfairOfferGenerator), "CreateSingleOfferForItem", prefix: nameof(BeginSingleOffer), finalizer: nameof(EndSingleOffer));
        Patch(typeof(BarterDetails), "get_ChancePercent", postfix: nameof(SuppressBarterChance));
        Patch(typeof(RagfairOfferHelper), nameof(RagfairOfferHelper.GetValidOffers), postfix: nameof(FilterBarterOffers));
        Patch(typeof(RagfairController), nameof(RagfairController.GetOffers), postfix: nameof(ConvertOffersCurrency));
        Patch(typeof(RagfairController), nameof(RagfairController.GetOfferByInternalId), postfix: nameof(ConvertSingleOfferCurrency));

        void Patch(Type type, string method, string? prefix = null, string? postfix = null, string? transpiler = null, string? finalizer = null)
        {
            var target = AccessTools.DeclaredMethod(type, method) ?? throw new MissingMethodException(type.FullName, method);
            HarmonyMethod? Hook(string? name) => name == null ? null : new HarmonyMethod(typeof(FleaPrices), name);
            harmony.Patch(target, prefix: Hook(prefix), postfix: Hook(postfix), transpiler: Hook(transpiler), finalizer: Hook(finalizer), ilmanipulator: null);
        }
    }

    internal static string Handle(CharacterDataRequest request, TemplateTable templates, HttpResponseUtil http, PmcData? pmc = null)
    {
        MongoId? id = null;
        if (!string.IsNullOrEmpty(request.Template))
        {
            if (!MongoId.IsValidMongoId(request.Template))
                throw new ArgumentException("物品不存在。");
            var template = new MongoId(request.Template);
            if (!templates.Items.TryGetValue(template, out var item) || item.Type != "Item")
                throw new ArgumentException("物品不存在。");
            id = template;
        }

        string? configMessage = null;
        string? exportedJson = null;
        object? offerStatus = null;
        switch (request.Action)
        {
            case "fleaOfferStatus":
            {
                var server = ragfairServer ?? throw new ArgumentException("跳蚤后台尚未就绪，请稍候重试。");
                var service = (RagfairOfferService)serverOfferService.GetValue(server)!;
                CustomFleaOffers.Attach(service,(RagfairOfferGenerator)serverOfferGenerator.GetValue(server)!);
                offerStatus = CustomFleaOffers.Status(request.Id,service);
                break;
            }
            case "fleaOfferEdit":
            case "fleaOfferAdd":
            {
                if (id is not {} tplId || templates.Items[tplId].Properties?.QuestItem == true || IsInvalidTemplate(templates.Items[tplId], templates))
                    throw new ArgumentException("请选择有效的非任务物品。");
                var draft = CustomFleaOffers.Validate(request.FleaOffer, templates.Items[tplId]);
                var server = ragfairServer ?? throw new ArgumentException("跳蚤后台尚未就绪，请稍候重试。");
                var service = (RagfairOfferService)serverOfferService.GetValue(server)!;
                var generator = (RagfairOfferGenerator)serverOfferGenerator.GetValue(server)!;
                CustomFleaOffers.Attach(service,generator);
                if(request.Action=="fleaOfferEdit")CustomFleaOffers.Edit(request.Id,templates.Items[tplId],draft,
                    request.FleaOffer is {} raw&&raw.TryGetProperty("preserveExpiry",out var keep)&&keep.ValueKind==JsonValueKind.True);
                else CustomFleaOffers.Publish(templates.Items[tplId], draft, generator, service);
                configMessage = request.Action=="fleaOfferEdit"?"报价已修改，刷新游戏跳蚤列表即可查看。":"已添加自定义报价，刷新游戏跳蚤列表即可购买。";
                break;
            }
            case "fleaOfferDelete":
            case "fleaOfferRecreate":
            case "fleaOfferWithdraw":
            case "fleaOfferPersist":
            {
                var server = ragfairServer ?? throw new ArgumentException("跳蚤后台尚未就绪，请稍候重试。");
                var service = (RagfairOfferService)serverOfferService.GetValue(server)!;
                CustomFleaOffers.Attach(service,(RagfairOfferGenerator)serverOfferGenerator.GetValue(server)!);
                CustomFleaOffers.Manage(request.Action,request.Id);
                configMessage = request.Action switch{"fleaOfferRecreate"=>"已按原始设置重新创建报价。","fleaOfferWithdraw"=>"已下架报价并保存到 SQLite，可在自定义报价页重新上架。","fleaOfferPersist"=>"报价已保存到 SQLite。",_=>"已移除报价及其持久化记录。"};
                break;
            }
            case "fleaConfigSave":
                book.SavePreset(request.FleaPresetName);
                configMessage = "已保存跳蚤方案";
                break;
            case "fleaConfigLoad":
                book.LoadPreset(request.FleaPresetName);
                SyncGlobalRagfairLevel();
                configMessage = "已载入跳蚤方案";
                break;
            case "fleaConfigDelete":
                book.DeletePreset(request.FleaPresetName);
                configMessage = "已删除跳蚤方案";
                break;
            case "fleaConfigRename":
                book.RenamePreset(request.FleaPresetName, request.FleaTargetName);
                configMessage = "已重命名跳蚤方案";
                break;
            case "fleaConfigExport":
                exportedJson = book.ExportPreset(request.FleaPresetName);
                configMessage = "已生成方案 JSON";
                break;
            case "fleaConfigImport":
            {
                string imported = book.ImportPreset(request.FleaPresetName, request.FleaJson);
                configMessage = $"已导入跳蚤方案：{imported}";
                break;
            }
            case "fleaRulesSave":
            {
                if (request.FleaRules is not { } rawRules)
                    throw new ArgumentException("缺少市场规则参数。");
                var parsed = JsonSerializer.Deserialize<FleaRules>(rawRules.GetRawText(), FlexibleJson)
                    ?? throw new ArgumentException("市场规则格式无效。");
                book.SetRules(parsed);
                SyncGlobalRagfairLevel();
                configMessage = "已更新跳蚤市场规则";
                break;
            }
            case "fleaBatchApply":
            {
                var targets = new List<MongoId>(request.Categories.Length);
                foreach (string rawId in request.Categories)
                {
                    if (!MongoId.IsValidMongoId(rawId))
                        continue;
                    var tplId = new MongoId(rawId);
                    if (!templates.Items.TryGetValue(tplId, out var tpl) || tpl.Type != "Item" || tpl.Properties?.QuestItem == true)
                        continue;
                    if (!IsInvalidTemplate(tpl, templates))
                        targets.Add(tplId);
                }
                int affected = book.ApplyBatch(targets, request.BatchOp, request.Value, request.ListingAllowed,
                    tplId => NativePrice(templates, tplId));
                configMessage = $"批量操作已处理 {affected} 项物品";
                break;
            }
            case "fleaRefreshOffers":
            {
                int removed = RefreshDynamicOffers();
                configMessage = $"已重新生成 AI 报盘（清理旧单 {removed} 条）";
                break;
            }
            case "fleaPriceMode":
                book.SetMode(request.Currency);
                break;
            case "fleaPriceSet":
                if (!id.HasValue || !double.IsFinite(request.Value) || request.Value < int.MinValue || request.Value > int.MaxValue ||
                    request.Value != Math.Truncate(request.Value))
                    throw new ArgumentException("请输入正整数价格。");
                book.SetManual(id.Value, (int)request.Value);
                break;
            case "fleaPriceReset":
                if (!id.HasValue)
                    throw new ArgumentException("物品不存在。");
                book.SetManual(id.Value, null);
                break;
            case "fleaListingSet":
                if (!id.HasValue || !request.ListingAllowed.HasValue)
                    throw new ArgumentException("出售权限无效。");
                book.SetListing(id.Value, request.ListingAllowed.Value);
                break;
            case "fleaListingReset":
                if (!id.HasValue)
                    throw new ArgumentException("物品不存在。");
                book.SetListing(id.Value, null);
                break;
        }
        book.Schedule(force: request.Action == "fleaPriceRefresh");
        SyncGlobalRagfairLevel();
        var (state, refreshing, failed) = book.Status();
        var (presets, hasLegacy) = book.PresetStatus();
        int? manual = null;
        bool? listing = null;
        double? price = null;
        if (id is { } selected)
        {
            if (state.Manual.TryGetValue(selected, out var custom))
                manual = custom;
            price = state.TryGet(selected, out int effective) ? effective : NativePrice(templates, selected);
            if (state.Listings.TryGetValue(selected, out bool allowed))
                listing = allowed;
        }
        long? stashRoubles = pmc != null ? OnlineTradingService.CountRubInStash(pmc) : null;
        return http.GetBody(new
        {
            ok = true,
            mode = state.Mode,
            updated = state.Online?.Updated,
            refreshing,
            failed,
            price,
            manual,
            listing,
            presets,
            hasLegacy,
            configMessage,
            offer = offerStatus,
            customOffers = CustomFleaOffers.List(ragfairServer == null ? null : (RagfairOfferService)serverOfferService.GetValue(ragfairServer)!),
            exportedJson,
            stashRoubles,
            rules = state.Rules,
            overrides = state.Manual.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            listings = state.Listings.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value)
        });
    }

    internal static async ValueTask<string> HandleRaidBuy(
        CharacterDataRequest request,
        SptProfile profile,
        Func<Task> save,
        HttpResponseUtil http,
        TemplateTable templates)
    {
        if (!Guid.TryParseExact(request.Id, "N", out var id))
            throw new ArgumentException("战局直购订单编号无效。");
        string orderId = id.ToString("N");
        var pmc = profile.CharacterData?.PmcData ?? throw new ArgumentException("当前角色尚未创建。");
        var receipts = new Dictionary<string, RaidBuyReceipt>();
        var extension = profile.ExtensionData;
        bool hasReceipts = extension != null && extension.ContainsKey(RaidBuyReceipts);
        if (hasReceipts)
            receipts = JsonSerializer.Deserialize<Dictionary<string, RaidBuyReceipt>>(
                JsonSerializer.Serialize(extension![RaidBuyReceipts])) ?? [];

        if (receipts.TryGetValue(orderId, out var previous))
        {
            if (previous.Template != request.Template || previous.Count != request.Count)
                throw new ArgumentException("战局直购订单与原请求不匹配。");
            return Result(previous, OnlineTradingService.CountRubInStash(pmc));
        }

        if (string.IsNullOrEmpty(request.Template) || !MongoId.IsValidMongoId(request.Template))
            throw new ArgumentException("物品不存在。");
        var tplId = new MongoId(request.Template);
        if (!templates.Items.TryGetValue(tplId, out var tpl) || tpl.Type != "Item" || tpl.Properties?.QuestItem == true || IsInvalidTemplate(tpl, templates))
            throw new ArgumentException("任务物品或无效物品无法从跳蚤直购。");

        var state = book.Current;
        if (state.Listings.TryGetValue(tplId, out bool explicitAllowed))
        {
            if (!explicitAllowed)
                throw new ArgumentException(ListingBlocked);
        }
        else if (!state.Rules.UnbanAll && tpl.Properties?.CanSellOnRagfair == false)
        {
            throw new ArgumentException("该物品当前禁售，可在「市场规则」中开启「全物品解禁」。");
        }

        int count = request.Count;
        if (count < 1)
            throw new ArgumentException("请输入正整数购买数量。");
        int unitPrice = MiscService.Options.ClearanceMarket
            ? 1
            : state.TryGet(tplId, out int configured)
                ? configured
                : (int)Math.Clamp(Math.Round(NativePrice(templates, tplId)), 1, int.MaxValue);
        var receipt = new RaidBuyReceipt(request.Template, count, unitPrice, state.Rules.PurchasesFir);
        var inventory = pmc.Inventory ?? throw new ArgumentException("仓库尚未就绪。");
        var previousItems = inventory.Items ?? throw new ArgumentException("仓库尚未就绪。");
        var previousReceipts = hasReceipts ? extension![RaidBuyReceipts] : null;
        long remaining;
        try
        {
            inventory.Items = previousItems.Select(item => item.Template == RubId
                ? item with { Upd = item.Upd is { } upd ? upd with { } : null }
                : item).ToList();
            remaining = OnlineTradingService.DeductRubFromStash(pmc, (long)unitPrice * count, "战局直购");
            receipts.Add(orderId, receipt);
            profile.ExtensionData ??= [];
            profile.ExtensionData[RaidBuyReceipts] = JsonSerializer.SerializeToElement(receipts);
            await save();
        }
        catch
        {
            inventory.Items = previousItems;
            profile.ExtensionData = extension;
            if (hasReceipts)
                extension![RaidBuyReceipts] = previousReceipts!;
            else
                extension?.Remove(RaidBuyReceipts);
            throw;
        }
        return Result(receipt, remaining);

        string Result(RaidBuyReceipt bought, long balance) => http.GetBody(new
        {
            ok = true,
            template = bought.Template,
            count = bought.Count,
            unitPrice = bought.UnitPrice,
            totalCost = (long)bought.UnitPrice * bought.Count,
            fir = bought.Fir,
            stashRoubles = balance
        });
    }

    private static int RefreshDynamicOffers()
    {
        var server = ragfairServer ?? throw new ArgumentException("跳蚤后台尚未就绪，请稍候重试。");
        var service = (RagfairOfferService)serverOfferService.GetValue(server)!;
        var generator = (RagfairOfferGenerator)serverOfferGenerator.GetValue(server)!;
        var staleAiIds = service.GetOffers()
            .Where(offer => !offer.IsPlayerOffer() && !offer.IsTraderOffer() && !CustomFleaOffers.IsCustom(offer))
            .Select(offer => offer.Id)
            .ToArray();
        foreach (var offerId in staleAiIds)
            service.RemoveOfferById(offerId);
        generator.GenerateDynamicOffers(null);
        server.Update();
        return staleAiIds.Length;
    }

    private static void SyncGlobalRagfairLevel()
    {
        var controller = FleaAutomation.Controller;
        if (controller == null)
            return;
        var globals = globalTableField.GetValue(controller) as GlobalTable;
        var ragfair = globals?.Configuration?.RagFair;
        if (ragfair == null)
            return;
        originalRagfairGlobals ??= (ragfair.MinUserLevel, ragfair.RagfairMinUserLevelByCategory);
        if (book.Current.Rules.IgnoreLevelLocks)
        {
            ragfair.MinUserLevel = 1;
            ragfair.RagfairMinUserLevelByCategory = false;
        }
        else if (originalRagfairGlobals is { } orig)
        {
            ragfair.MinUserLevel = orig.MinUserLevel;
            ragfair.RagfairMinUserLevelByCategory = orig.ByCategory;
        }
    }

    internal static double NativePrice(TemplateTable templates, MongoId id)
    {
        if (templates.Prices != null && templates.Prices.TryGetValue(id, out var price) && price > 0)
            return price;
        return templates.Handbook?.Items?.FirstOrDefault(item => item.Id == id)?.Price is double handbook && handbook > 0 ? handbook : 1;
    }

    private static bool IsInvalidTemplate(TemplateItem item, TemplateTable templates)
    {
        var seen = new HashSet<MongoId>();
        var current = item;
        while (current != null && seen.Add(current.Id))
        {
            if (invalidBaseTypes.Contains(current.Id))
                return true;
            if (current.Parent == default || templates.Items == null || !templates.Items.TryGetValue(current.Parent, out current))
                break;
        }
        return false;
    }

    private static void Tick(RagfairServer __instance)
    {
        ragfairServer = __instance;
        CustomFleaOffers.Attach((RagfairOfferService)serverOfferService.GetValue(__instance)!, (RagfairOfferGenerator)serverOfferGenerator.GetValue(__instance)!);
        book.Schedule();
    }

    private static bool Base(MongoId __0, ref double __result)
    {
        if (!book.Current.TryGet(__0, out int price))
            return true;
        __result = price;
        return false;
    }

    private static bool DynamicBase(MongoId __0, ref double? __result)
    {
        if (!book.Current.TryGet(__0, out int price))
            return true;
        __result = price;
        return false;
    }

    private static void AllPrices(Dictionary<MongoId, double> __result)
    {
        var state = book.Current;
        if (state.Online != null)
            foreach (var (id, price) in state.Online.Prices)
                if (__result.ContainsKey(id))
                    __result[id] = price;
        foreach (var (id, price) in state.Manual)
            __result[id] = price;
    }

    private static bool HandbookFloor(double __0, MongoId __1, ref double __result)
    {
        if (!book.Current.TryGet(__1, out _))
            return true;
        __result = __0;
        return false;
    }

    private static bool ModPriceCap(MongoId __1, double __2, ref double __result) => HandbookFloor(__2, __1, ref __result);

    private static bool UseTraderFloor(Dynamic config, MongoId id) =>
        config.UseTraderPriceForOffersIfHigher && !book.Current.TryGet(id, out _);

    private static bool SumPresetChildren(GenerateFleaPrices config, MongoId id) =>
        config.GeneratePresetPriceByChildren && !book.Current.TryGet(id, out _);

    private static IEnumerable<CodeInstruction> PriceGuards(IEnumerable<CodeInstruction> instructions)
    {
        int replaced = 0;
        var trader = AccessTools.PropertyGetter(typeof(Dynamic), nameof(Dynamic.UseTraderPriceForOffersIfHigher));
        var preset = AccessTools.PropertyGetter(typeof(GenerateFleaPrices), nameof(GenerateFleaPrices.GeneratePresetPriceByChildren));
        foreach (var instruction in instructions)
        {
            string? guard = instruction.Calls(trader) ? nameof(UseTraderFloor) :
                instruction.Calls(preset) ? nameof(SumPresetChildren) : null;
            if (guard == null)
            {
                yield return instruction;
                continue;
            }
            replaced++;
            yield return new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
            yield return CodeInstruction.Call(typeof(FleaPrices), guard);
        }
        if (replaced != 2)
            throw new InvalidOperationException("跳蚤定价方法版本不匹配。");
    }

    private static bool CheckValidRagfairItem(RagfairServerHelper __instance, KeyValuePair<bool, TemplateItem> __0, ref bool __result)
    {
        if (!__0.Key || __0.Value is not { Type: "Item" } tpl || tpl.Properties?.QuestItem == true)
            return true;

        var state = book.Current;
        bool explicitRule = state.Listings.TryGetValue(tpl.Id, out bool explicitAllowed);
        if (explicitRule && !explicitAllowed)
        {
            __result = false;
            return false;
        }
        if (!explicitRule && !state.Rules.UnbanAll)
            return true;

        if (helperItemFilter?.GetValue(__instance) is ItemFilterService filter && filter.IsItemBlacklisted(tpl.Id))
        {
            __result = false;
            return false;
        }
        if (helperItemHelper?.GetValue(__instance) is ItemHelper items && !items.IsValidItem(tpl, invalidBaseTypes))
        {
            __result = false;
            return false;
        }
        if (installedTemplates != null && IsInvalidTemplate(tpl, installedTemplates))
        {
            __result = false;
            return false;
        }
        __result = !invalidBaseTypes.Contains(tpl.Id);
        return false;
    }

    private static bool SkipBannedPlateRemoval(ref bool __result)
    {
        var rules = book.Current.Rules;
        if (!rules.UnbanAll && !rules.FullCondition)
            return true;
        __result = false;
        return false;
    }

    private static bool UnlockOfferItems(ref int __2, ref bool __result)
    {
        if (!book.Current.Rules.IgnoreLevelLocks)
            return true;
        __2 = 0;
        __result = false;
        return false;
    }

    private static bool UnlockTemplate(ref int __2, ref bool __result)
    {
        if (!book.Current.Rules.IgnoreLevelLocks)
            return true;
        __2 = 0;
        __result = false;
        return false;
    }

    private static void UnlockCategories(ref bool __2)
    {
        if (book.Current.Rules.IgnoreLevelLocks)
            __2 = true;
    }

    private static void ApplyPurchaseFir(ref bool __3)
    {
        if (ClearanceSales.InMarketPurchase && book.Current.Rules.PurchasesFir)
            __3 = true;
    }

    private static bool SkipArmorPlateStrip() => !book.Current.Rules.FullCondition;

    private static bool SkipConditionDegrade() => !book.Current.Rules.FullCondition;

    private static bool ForceDynamicCurrency(ref MongoId __result)
    {
        if (!book.Current.Rules.RoubleOnly)
            return true;
        __result = RubId;
        return false;
    }

    private static void BeginSingleOffer() => inSingleOffer++;
    private static void EndSingleOffer() => inSingleOffer--;

    private static void SuppressBarterChance(ref double __result)
    {
        if (inSingleOffer > 0 && book.Current.Rules.NoBarter)
            __result = 0;
    }

    private static bool IsCurrency(MongoId tpl) => tpl == RubId || tpl == UsdId || tpl == EurId;

    internal static bool IsBarterOffer(RagfairOffer offer) =>
        offer.Requirements != null && offer.Requirements.Any(r => !IsCurrency(r.TemplateId));

    private static void FilterBarterOffers(ref List<RagfairOffer>? __result)
    {
        if (!book.Current.Rules.NoBarter || __result is not { Count: > 0 })
            return;
        __result = __result.Where(offer => offer.IsPlayerOffer() || !IsBarterOffer(offer)).ToList();
    }

    internal static RagfairOffer ConvertToRoubleOffer(RagfairOffer offer)
    {
        if (offer.IsPlayerOffer() || offer.Requirements == null)
            return offer;
        var reqs = offer.Requirements as IReadOnlyCollection<OfferRequirement> ?? offer.Requirements.ToArray();
        if (reqs.Count == 0 || !reqs.All(r => IsCurrency(r.TemplateId)) || reqs.All(r => r.TemplateId == RubId))
            return offer;
        double rubCost = offer.RequirementsCost is > 0 ? Math.Max(1, Math.Round(offer.RequirementsCost.Value)) : 1;
        double summary = offer.SummaryCost is > 0 ? Math.Max(1, Math.Round(offer.SummaryCost.Value)) : rubCost;
        OfferRequirement[] converted = [new() { TemplateId = RubId, Count = rubCost, OnlyFunctional = false }];
        return offer with { Requirements = converted, RequirementsCost = rubCost, SummaryCost = summary,
            ExtensionData = offer.ExtensionData == null ? [] : new(offer.ExtensionData) };
    }

    [HarmonyPriority(Priority.High)]
    private static void ConvertOffersCurrency(ref GetOffersResult? __result)
    {
        if (__result?.Offers is not { Count: > 0 } offers)
            return;
        bool roubleOnly = book.Current.Rules.RoubleOnly;
        if (!roubleOnly && !offers.Any(o => CustomFleaOffers.PausedRemaining(o).HasValue))
            return;
        __result = __result with
        {
            Offers = offers.Select(o => CustomFleaOffers.ProjectForClient(roubleOnly ? ConvertToRoubleOffer(o) : o)).ToList()
        };
    }

    [HarmonyPriority(Priority.High)]
    private static void ConvertSingleOfferCurrency(ref RagfairOffer? __result)
    {
        if (__result == null)
            return;
        if (book.Current.Rules.RoubleOnly)
            __result = ConvertToRoubleOffer(__result);
        __result = CustomFleaOffers.ProjectForClient(__result);
    }

    internal const string ListingBlocked = "该物品禁止上架。";

    internal static bool HasBlockedItem(IEnumerable<Item> items) => HasBlockedItem(items, book.Current.Listings);

    private static bool HasBlockedItem(IEnumerable<Item> items, Dictionary<MongoId, bool> listings) =>
        items.Any(item => listings.TryGetValue(item.Template, out bool allowed) && !allowed);

    [HarmonyPriority(Priority.First)]
    private static bool CheckListing(RagfairController __instance, PmcData __0, AddOfferRequestData __1, MongoId __2,
        ref ItemEventRouterResponse __result)
    {
        var listings = book.Current.Listings;
        if (!listings.ContainsValue(false) || __1.Items is not { Count: > 0 } ids || __0.Inventory?.Items is not { } inventory)
            return true;
        var roots = ids.ToHashSet();
        var children = inventory.CreateParentIdLookupCache(out _);
        if (!HasBlockedItem(inventory.Where(item => roots.Contains(item.Id)).SelectMany(item => item.GetItemWithChildren(children)), listings))
            return true;
        var output = ((EventOutputHolder)eventOutput.GetValue(__instance)!).GetOutput(__2);
        __result = ((HttpResponseUtil)responses.GetValue(__instance)!).AppendErrorToOutput(output, ListingBlocked);
        return false;
    }

    private static void Quote(RagfairController __instance, GetMarketPriceRequestData __0, ref GetItemPriceResult __result)
    {
        if (!book.Current.TryGet(__0.TemplateId, out int price))
            return;
        var offers = (RagfairOfferService)offerService.GetValue(__instance)!;
        if (__result.Avg is > 0 && offers.GetOffersOfType(__0.TemplateId)?.Any() == true)
            return;
        __result = new GetItemPriceResult { Min = price, Avg = price, Max = price };
    }
}
