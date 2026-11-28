using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HarmonyLib;
using LocalGearMail;
using LocalMiscShared;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Generators.Ragfair;
using SPTarkov.Server.Core.Helpers.Commerce;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Ragfair;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Eft.Ragfair;
using SPTarkov.Server.Core.Models.Eft.Trade;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Ragfair;
using SPTarkov.Server.Core.Services.Items;
using SPTarkov.Server.Core.Utils;
using Item = SPTarkov.Server.Core.Models.Eft.Common.Tables.Item;
using Path = System.IO.Path;
using TemplateItem = SPTarkov.Server.Core.Models.Eft.Common.Tables.TemplateItem;

internal static class FleaPriceChecks
{
    private sealed class PriceFeed : HttpMessageHandler
    {
        internal string Pvp = "";
        internal string Pve = "";
        internal int Calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            string content = request.RequestUri!.AbsolutePath.EndsWith("prices-pve.json") ? Pve : Pvp;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) });
        }
    }

    internal static async Task Run(string gameRoot, JsonUtil json)
    {
        var item = new MongoId("5447a9cd4bdc2dbd208b4567");
        var missing = new MongoId("5447ac644bdc2d6c208b4567");
        string path = Path.Combine(Path.GetTempPath(), "spt-flea-prices-" + Guid.NewGuid().ToString("N"));
        using var handler = new PriceFeed
        {
            Pvp = JsonSerializer.Serialize(new Dictionary<string, int> { [item.ToString()] = 8000, ["invalid"] = 500 }),
            Pve = JsonSerializer.Serialize(new Dictionary<string, int> { [item.ToString()] = 4000 })
        };
        using var http = new HttpClient(handler);
        var harmony = new Harmony("local.flea-prices-test");

        try
        {
            var book = new FleaPriceBook(path, http);
            await book.Schedule()!;
            Check(book.Current.TryGet(item, out int price) && price == 8000, "PvP prices load");
            Check(book.Current.Online!.Prices.Count == 1 && !book.Current.TryGet(missing, out _), "invalid and absent entries are excluded");
            Check(book.Schedule() == null && handler.Calls == 1, "fresh prices skip scheduled download");

            handler.Pvp = "{}";
            await book.Schedule(force: true)!;
            Check(book.Status().Failed && book.Current.TryGet(item, out price) && price == 8000, "failed download retains good prices");

            book.SetManual(item, 700);
            book.SetListing(item, false);
            book.SetListing(missing, true);
            book.SetMode(FleaPriceBook.Pve);
            await book.Schedule()!;
            Check(book.Current.TryGet(item, out price) && price == 700 && book.Current.Online!.Prices[item] == 4000,
                "manual price overrides the independent PvE feed");

            var reloaded = new FleaPriceBook(path, http);
            Check(reloaded.Current.Mode == FleaPriceBook.Pve && reloaded.Current.TryGet(item, out price) && price == 700,
                "mode and override survive a reload");
            Check(reloaded.Current.Listings[item] == false && reloaded.Current.Listings[missing], "listing choices survive a reload");
            Check(reloaded.Current.Online!.Prices[item] == 4000, "last good feed survives a reload");

            var templates = Activator.CreateInstance<TemplateTable>();
            var original = new Dictionary<MongoId, double> { [item] = 9000, [missing] = 5000 };
            typeof(TemplateTable).GetProperty(nameof(TemplateTable.Prices))!.SetValue(templates, original);
            var config = json.Deserialize<RagfairConfig>(File.ReadAllText(Path.Combine(gameRoot, "SPT_Runtime", "SPT_Data", "configs", "ragfair.json")))!;
            config.Dynamic.UseTraderPriceForOffersIfHigher = true;
            config.Dynamic.OfferAdjustment.AdjustPriceWhenBelowHandbookPrice = true;
            config.Dynamic.ItemPriceMultiplier?.Clear();
            config.Dynamic.UnreasonableModPrices.Clear();
            config.Dynamic.PriceRanges.Default.Min = config.Dynamic.PriceRanges.Default.Max = 1;
            var native = new RagfairPriceService(null!, templates, null!, new RandomUtil(null!, null!), null!, null!, null!, null!, null!, config);
            AccessTools.Field(typeof(RagfairPriceService), "StaticPrices").SetValue(native, new Dictionary<MongoId, double>());
            FleaAutomation.Install(harmony);
            ClearanceSales.Install(harmony);
            FleaPrices.Install(harmony, reloaded);
            CheckListings(templates, reloaded, item, missing, json);
            Check(native.GetFleaPriceForItem(item) == 700 && native.GetDynamicPriceForItem(item) == 700, "native flea lookups use custom price");
            Check(native.GetDynamicPriceForItem(missing) == 5000, "native fallback remains available");
            Check(native.GetAllFleaPrices()[item] == 700 && original[item] == 9000, "bulk flea prices preserve the database");
            Check(native.GetDynamicItemPrice(item, new MongoId("5449016a4bdc2d6f028b456f")) == 700, "native generator honors the price below native floors");
            Check(config.Dynamic.UseTraderPriceForOffersIfHigher && config.Dynamic.OfferAdjustment.AdjustPriceWhenBelowHandbookPrice,
                "native floor settings remain enabled");

            reloaded.SetManual(item, null);
            Check(native.GetFleaPriceForItem(item) == 4000, "reset uses the active feed");
            reloaded.SetMode(FleaPriceBook.Local);
            Check(reloaded.Schedule(force: true) == null && native.GetDynamicPriceForItem(item) == 9000,
                "local mode restores original prices");
            reloaded.SetMode(FleaPriceBook.Pvp);
            Check(native.GetFleaPriceForItem(item) == 8000, "switching feed restores the matching cache");

            CheckMarketRulesAndHooks(templates, reloaded, config, json);
            await CheckBatchAndRaidBuy(templates, reloaded, item, missing, json);
            CheckPresets(path, templates, reloaded, item, missing, http, json);
            Console.WriteLine("PASS flea feed cache, per-item/batch pricing & listing, market rules (unban/levels/FIR/condition/RUB-only/no-barter), in-raid direct buy, named preset CRUD & JSON import/export");
        }
        finally
        {
            harmony.UnpatchSelf();
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
    }

    private static void CheckMarketRulesAndHooks(TemplateTable templates, FleaPriceBook book, RagfairConfig config, JsonUtil json)
    {
        var bannedId = new MongoId("5c0530ee86f774697952d952");
        var questId = new MongoId("593965cf86f774087a77e1b6");
        var bannedTpl = new TemplateItem { Id = bannedId, Type = "Item", Properties = new() { CanSellOnRagfair = false, QuestItem = false } };
        var questTpl = new TemplateItem { Id = questId, Type = "Item", Properties = new() { CanSellOnRagfair = false, QuestItem = true } };
        templates.Items[bannedId] = bannedTpl;
        templates.Items[questId] = questTpl;

        // #1 UnbanAll & plate removal bypass
        typeof(TemplateTable).GetProperty(nameof(TemplateTable.Handbook))!.SetValue(templates,
            new HandbookBase { Items = [new HandbookItem { Id = bannedId, Price = 1000 }], Categories = [] });
        var itemConfig = new ItemConfig
        {
            HandbookPriceOverride = [], Blacklist = [], LootableItemBlacklist = [],
            RewardItemBlacklist = [], RewardItemTypeBlacklist = [], BossItems = [], CustomItemGlobalPresets = []
        };
        var filter = new ItemFilterService(itemConfig);
        AccessTools.Field(typeof(ItemFilterService), "ItemBlacklistCache").SetValue(filter, new HashSet<MongoId>());
        var baseClasses = new ItemBaseClassService(null!, templates, null!);
        AccessTools.Field(typeof(ItemBaseClassService), "_itemBaseClassesCache").SetValue(baseClasses,
            new Dictionary<MongoId, HashSet<MongoId>> { [bannedId] = [] });
        var handbook = new HandbookHelper(null!, templates, itemConfig, new SPTarkov.Server.Core.Utils.Cloners.FastCloner());
        var items = new ItemHelper(null!, templates, null!, handbook, baseClasses, filter, null!, null!, null!);
        var serverHelper = new RagfairServerHelper(null!, null!, null!, null!, items, null!, null!, null!, config, null!);
        config.Dynamic.Blacklist.EnableBsgList = true;
        book.SetListing(bannedId, null);
        book.SetRules(new FleaRules(UnbanAll: false));
        Check(!serverHelper.IsItemValidRagfairItem(new(true, bannedTpl)), "banned item rejected when UnbanAll is off");

        book.SetRules(new FleaRules(UnbanAll: true));
        Check(serverHelper.IsItemValidRagfairItem(new(true, bannedTpl)), "banned non-quest item allowed when UnbanAll is on");
        Check(!serverHelper.IsItemValidRagfairItem(new(true, questTpl)), "quest item stays blocked even when UnbanAll is on");

        book.SetListing(bannedId, false);
        Check(!serverHelper.IsItemValidRagfairItem(new(true, bannedTpl)), "explicit per-item block overrides UnbanAll");
        book.SetListing(bannedId, null);

        var offerGen = (RagfairOfferGenerator)RuntimeHelpers.GetUninitializedObject(typeof(RagfairOfferGenerator));
        var removeBannedPlates = AccessTools.Method(typeof(RagfairOfferGenerator), "RemoveBannedPlatesFromPreset")!;
        Check(!(bool)removeBannedPlates.Invoke(offerGen, [new List<Item>(), new ArmorPlateBlacklistSettings { MaxProtectionLevel = 4, IgnoreSlots = [] }])!,
            "RemoveBannedPlatesFromPreset returns false (no plates stripped) when UnbanAll is on");

        // #2 Level lock bypass
        var levelService = (RagfairLevelService)RuntimeHelpers.GetUninitializedObject(typeof(RagfairLevelService));
        AccessTools.GetDeclaredFields(typeof(RagfairLevelService)).Single(f => f.FieldType == typeof(TemplateTable)).SetValue(levelService, templates);
        book.SetRules(new FleaRules(IgnoreLevelLocks: true));
        int reqLvl = 99;
        Check(!levelService.IsTemplateLocked(bannedId, 1, out reqLvl) && reqLvl == 0, "IsTemplateLocked bypassed when IgnoreLevelLocks is on");
        reqLvl = 99;
        Check(!levelService.IsLocked([new Item { Id = new MongoId(), Template = bannedId }], 1, out reqLvl) && reqLvl == 0,
            "IsLocked bypassed when IgnoreLevelLocks is on");

        // #7 Full condition AI offers & keep armor plates
        var armorRoot = new Item { Id = new MongoId(), Template = bannedId };
        var plateChild = new Item { Id = new MongoId(), Template = bannedId, ParentId = armorRoot.Id.ToString(), SlotId = "Front_plate", Upd = new() { Repairable = new() { Durability = 50, MaxDurability = 50 } } };
        var armorTree = new List<Item> { armorRoot, plateChild };
        var removePlates = AccessTools.Method(typeof(RagfairOfferGenerator), "RemoveArmorPlates")!;
        var randomiseCond = AccessTools.Method(typeof(RagfairOfferGenerator), "RandomiseItemCondition")!;
        book.SetRules(new FleaRules(FullCondition: true));
        removePlates.Invoke(offerGen, [armorTree, armorRoot]);
        randomiseCond.Invoke(offerGen, [bannedId, armorTree, bannedTpl]);
        Check(armorTree.Count == 2 && plateChild.Upd.Repairable.Durability == 50,
            "FullCondition preserves armor plates and full durability");

        // #8 Rouble-only currency & barter filtering
        var rubId = new MongoId("5449016a4bdc2d6f028b456f");
        var usdId = new MongoId("5696686a4bdc2da3298b456a");
        book.SetRules(new FleaRules(RoubleOnly: true, NoBarter: true));
        Check(serverHelper.GetDynamicOfferCurrency() == rubId, "GetDynamicOfferCurrency forced to Roubles when RoubleOnly is on");

        var usdOffer = new RagfairOffer
        {
            Id = new MongoId(),
            CreatedBy = OfferCreator.FakePlayer,
            User = new RagfairOfferUser { Id = new MongoId(), MemberType = MemberCategory.Default },
            Items = [new Item { Id = new MongoId(), Template = bannedId }],
            Requirements = [new OfferRequirement { TemplateId = usdId, Count = 100 }],
            RequirementsCost = 14500,
            SummaryCost = 14500,
            Quantity = 1
        };
        var converted = FleaPrices.ConvertToRoubleOffer(usdOffer);
        Check(converted.Requirements!.Single().TemplateId == rubId && converted.Requirements!.Single().Count == 14500,
            "non-player USD offer converts to Rouble requirement");

        var playerUsdOffer = usdOffer with
        {
            CreatedBy = OfferCreator.Player,
            User = new RagfairOfferUser { Id = new MongoId(), MemberType = MemberCategory.UniqueId }
        };
        var barterOffer = usdOffer with
        {
            Id = new MongoId(),
            Requirements = [new OfferRequirement { TemplateId = bannedId, Count = 2 }]
        };
        Check(FleaPrices.IsBarterOffer(barterOffer) && !FleaPrices.IsBarterOffer(usdOffer), "barter offer detection distinguishes items vs currency");
        var filterMethod = AccessTools.Method(typeof(FleaPrices), "FilterBarterOffers")!;
        object?[] filterArgs = [new List<RagfairOffer> { usdOffer, barterOffer, playerUsdOffer }];
        filterMethod.Invoke(null, filterArgs);
        var filtered = (List<RagfairOffer>)filterArgs[0]!;
        Check(filtered.Count == 2 && !filtered.Any(o => o.Id == barterOffer.Id), "NoBarter removes AI barter offers while preserving currency and player offers");

        // #6 Flea purchases FIR
        var applyFir = AccessTools.Method(typeof(FleaPrices), "ApplyPurchaseFir")!;
        var beginBuy = AccessTools.Method(typeof(ClearanceSales), "BeginBuy")!;
        var endBuy = AccessTools.Method(typeof(ClearanceSales), "EndBuy")!;
        book.SetRules(new FleaRules(PurchasesFir: true));
        object?[] firOutside = [false];
        applyFir.Invoke(null, firOutside);
        Check(!(bool)firOutside[0]!, "PurchasesFir does not alter non-ragfair trader purchases");

        object?[] buyState = [false];
        beginBuy.Invoke(null, buyState);
        try
        {
            object?[] firInside = [false];
            applyFir.Invoke(null, firInside);
            Check((bool)firInside[0]!, "PurchasesFir marks ragfair purchases as Found-In-Raid");
        }
        finally
        {
            endBuy.Invoke(null, buyState);
        }
        book.SetRules(new FleaRules());
    }

    private static async Task CheckBatchAndRaidBuy(TemplateTable templates, FleaPriceBook book, MongoId item, MongoId other, JsonUtil json)
    {
        var http = new HttpResponseUtil(json, null!);
        templates.Items[item] = new TemplateItem { Id = item, Type = "Item", Properties = new() { CanSellOnRagfair = true } };
        templates.Items[other] = new TemplateItem { Id = other, Type = "Item", Properties = new() { CanSellOnRagfair = false } };

        // #10 Batch price scaling & listing permissions
        var batchReq = new CharacterDataRequest
        {
            Action = "fleaBatchApply",
            BatchOp = "scalePrice",
            Value = 1.5,
            Categories = [item.ToString(), other.ToString()]
        };
        using (var doc = JsonDocument.Parse(FleaPrices.Handle(batchReq, templates, http)))
        {
            var overrides = doc.RootElement.GetProperty("data").GetProperty("overrides");
            Check(overrides.GetProperty(item.ToString()).GetInt32() == 12000 && overrides.GetProperty(other.ToString()).GetInt32() == 7500,
                "batch scalePrice scales both feed and native fallback prices");
        }
        batchReq.BatchOp = "setListing";
        batchReq.ListingAllowed = true;
        FleaPrices.Handle(batchReq, templates, http);
        Check(book.Current.Listings[item] && book.Current.Listings[other], "batch setListing applies to all filtered items");

        batchReq.BatchOp = "resetPrice";
        FleaPrices.Handle(batchReq, templates, http);
        batchReq.BatchOp = "resetListing";
        FleaPrices.Handle(batchReq, templates, http);
        Check(!book.Current.Manual.ContainsKey(item) && !book.Current.Listings.ContainsKey(other), "batch reset clears manual prices and listings");

        // #9 In-raid direct buy
        var rubTpl = new MongoId("5449016a4bdc2d6f028b456f");
        var session = new MongoId();
        var pmc = new PmcData
        {
            Id = session,
            Inventory = new()
            {
                Items = [new Item { Id = new MongoId(), Template = rubTpl, Upd = new() { StackObjectsCount = 20000 } }]
            }
        };
        var profile = new SptProfile { CharacterData = new() { PmcData = pmc } };
        int writes = 0;
        Task Save()
        {
            writes++;
            return Task.CompletedTask;
        }
        ValueTask<string> Buy(CharacterDataRequest request) => FleaPrices.HandleRaidBuy(request, profile, Save, http, templates);

        book.SetRules(new FleaRules(PurchasesFir: true, UnbanAll: false));
        var buyReq = new CharacterDataRequest { Action = "fleaRaidBuy", Id = Guid.NewGuid().ToString("N"), Template = item.ToString(), Count = 2 };
        using (var buyDoc = JsonDocument.Parse(await Buy(buyReq)))
        {
            var data = buyDoc.RootElement.GetProperty("data");
            Check(data.GetProperty("ok").GetBoolean() && data.GetProperty("totalCost").GetInt64() == 16000 &&
                  data.GetProperty("fir").GetBoolean() && data.GetProperty("stashRoubles").GetInt64() == 4000,
                "fleaRaidBuy deducts exact stash Roubles and returns FIR + remaining balance");
        }

        using (var retry = JsonDocument.Parse(await Buy(buyReq)))
            Check(retry.RootElement.GetProperty("data").GetProperty("totalCost").GetInt64() == 16000 &&
                writes == 1 && pmc.Inventory.Items.Single().Upd!.StackObjectsCount == 4000, "same order retries without charging twice");
        var reopened = json.Deserialize<SptProfile>(json.Serialize(profile))!;
        using (var retry = JsonDocument.Parse(await FleaPrices.HandleRaidBuy(buyReq, reopened, Save, http, templates)))
            Check(retry.RootElement.GetProperty("data").GetProperty("stashRoubles").GetInt64() == 4000 && writes == 1,
                "saved receipts prevent duplicate charges after a profile reload");
        buyReq.Count = 1;
        try
        {
            await Buy(buyReq);
            throw new InvalidOperationException("fleaRaidBuy should reject a changed order");
        }
        catch (ArgumentException) { }
        buyReq.Count = 2;
        buyReq.Id = Guid.NewGuid().ToString("N");
        // Insufficient funds preserves remaining 4,000 RUB
        try
        {
            await Buy(buyReq);
            throw new InvalidOperationException("fleaRaidBuy should reject insufficient stash Roubles");
        }
        catch (ArgumentException) { }
        Check(pmc.Inventory.Items.Single().Upd!.StackObjectsCount == 4000, "rejected raid buy preserves stash Roubles");

        // Banned item rejected unless UnbanAll is enabled
        var buyBanned = new CharacterDataRequest { Action = "fleaRaidBuy", Id = Guid.NewGuid().ToString("N"), Template = other.ToString(), Count = 1 };
        try
        {
            await Buy(buyBanned);
            throw new InvalidOperationException("fleaRaidBuy should reject banned item when UnbanAll is off");
        }
        catch (ArgumentException) { }

        // ClearanceMarket overrides unit price to 1 RUB
        var miscProp = typeof(MiscService).GetProperty(nameof(MiscService.Options))!;
        var prevMisc = MiscService.Options;
        try
        {
            miscProp.SetValue(null, new MiscSettings { ClearanceMarket = true });
            book.SetRules(new FleaRules(UnbanAll: true));
            var largeBuy = new CharacterDataRequest { Action = "fleaRaidBuy", Id = Guid.NewGuid().ToString("N"), Template = item.ToString(), Count = 120 };
            using (var largeDoc = JsonDocument.Parse(await Buy(largeBuy)))
            {
                Check(largeDoc.RootElement.GetProperty("data").GetProperty("totalCost").GetInt64() == 120 &&
                      pmc.Inventory.Items.Single().Upd!.StackObjectsCount == 3880,
                    "unrestricted fleaRaidBuy supports large multi-stack orders");
            }
            try
            {
                await FleaPrices.HandleRaidBuy(buyBanned, profile, () => Task.FromException(new IOException()), http, templates);
                throw new InvalidOperationException("fleaRaidBuy should surface save failure");
            }
            catch (IOException) { }
            Check(pmc.Inventory.Items.Single().Upd!.StackObjectsCount == 3880, "failed save rolls back the charge");
            using var cheapDoc = JsonDocument.Parse(await Buy(buyBanned));
            Check(cheapDoc.RootElement.GetProperty("data").GetProperty("totalCost").GetInt64() == 1 &&
                  pmc.Inventory.Items.Single().Upd!.StackObjectsCount == 3879,
                "fleaRaidBuy honors UnbanAll and ClearanceMarket 1 RUB price");
        }
        finally
        {
            miscProp.SetValue(null, prevMisc);
            book.SetRules(new FleaRules());
        }
    }

    private static void CheckPresets(string path, TemplateTable templates, FleaPriceBook book,
        MongoId item, MongoId other, HttpClient client, JsonUtil json)
    {
        var response = new HttpResponseUtil(json, null!);
        var request = new CharacterDataRequest { Template = item.ToString() };
        JsonElement Send(string action, string? preset = null, string? target = null, string? rawJson = null)
        {
            request.Action = action;
            request.FleaPresetName = preset;
            request.FleaTargetName = target;
            request.FleaJson = rawJson;
            using var result = JsonDocument.Parse(FleaPrices.Handle(request, templates, response));
            return result.RootElement.GetProperty("data").Clone();
        }

        void Reject(string action, string? preset, string? target = null, string? rawJson = null)
        {
            try
            {
                Send(action, preset, target, rawJson);
                throw new InvalidOperationException("Flea preset request should fail.");
            }
            catch (ArgumentException) { }
        }

        Check(Send("fleaPriceStatus").GetProperty("presets").GetArrayLength() == 0, "a new settings book has no named presets");
        Reject("fleaConfigLoad", "Missing");
        Reject("fleaConfigSave", " ");
        book.SetManual(item, 1234);
        book.SetListing(item, false);
        book.SetListing(other, true);
        book.SetRules(new FleaRules(UnbanAll: true, AutoPriceMode: 2, AutoPricePercent: 95));
        var active = book.Current;
        Check(Send("fleaConfigSave", " Market ").GetProperty("presets")[0].GetString() == "Market"
            && ReferenceEquals(active, book.Current), "saving a named preset preserves active settings");
        string legacy = Path.Combine(path, "settings-snapshot.json");
        File.Copy(Path.Combine(path, "settings.json"), legacy);
        book.SetMode(FleaPriceBook.Local);
        book.SetManual(item, null);
        book.SetManual(other, 6789);
        book.SetListing(item, true);
        book.SetListing(other, null);
        book.SetRules(new FleaRules(RoubleOnly: true));
        var saved = Send("fleaConfigSave", "Local");
        Check(saved.GetProperty("presets").EnumerateArray().Select(name => name.GetString()).SequenceEqual(["Local", "Market"])
            && saved.GetProperty("hasLegacy").GetBoolean(), "both named presets and the existing legacy snapshot are available");

        var status = Send("fleaConfigLoad", "Market");
        Check(status.GetProperty("mode").GetString() == FleaPriceBook.Pvp && status.GetProperty("price").GetDouble() == 1234
            && !status.GetProperty("listing").GetBoolean()
            && status.GetProperty("rules").GetProperty("unbanAll").GetBoolean()
            && status.GetProperty("rules").GetProperty("autoPricePercent").GetInt32() == 95,
            "loading Market restores its price, mode, listing and rules");
        var restarted = new FleaPriceBook(path, client);
        Check(restarted.Current.Manual.Count == 1 && restarted.Current.Manual[item] == 1234
            && restarted.Current.Listings[other] && restarted.Current.Rules.UnbanAll && restarted.PresetStatus().Names!.Length == 2,
            "the active settings, rules and both named presets survive restart");
        status = Send("fleaConfigLoad", "Local");
        Check(status.GetProperty("mode").GetString() == FleaPriceBook.Local &&
            book.Current.Manual.Count == 1 && book.Current.Manual[other] == 6789 && book.Current.Listings[item] && book.Current.Rules.RoubleOnly,
            "loading Local restores its independent copy");
        book.SetManual(item, 4321);
        Check(Send("fleaConfigSave", "market").GetProperty("presets").GetArrayLength() == 2,
            "a case-insensitive name match overwrites only that preset");
        Check(Send("fleaConfigLoad", "Market").GetProperty("price").GetDouble() == 4321, "overwritten value loads");
        Check(Send("fleaConfigLoad").GetProperty("price").GetDouble() == 1234, "the legacy snapshot loads independently");

        // #11 Rename, Export, Import, Delete
        Reject("fleaConfigRename", "Market", "Local");
        var renamed = Send("fleaConfigRename", "Market", "MarketPro");
        Check(renamed.GetProperty("presets").EnumerateArray().Select(n => n.GetString()).SequenceEqual(["Local", "MarketPro"]),
            "renaming updates the preset list cleanly");
        string exportedJson = Send("fleaConfigExport", "MarketPro").GetProperty("exportedJson").GetString()!;
        Check(!string.IsNullOrWhiteSpace(exportedJson), "export returns serialized JSON");
        Reject("fleaConfigImport", "BadImport", rawJson: "{invalid");
        Send("fleaConfigImport", "ImportedCopy", rawJson: exportedJson);
        Check(Send("fleaConfigLoad", "ImportedCopy").GetProperty("price").GetDouble() == 4321, "imported preset restores exported state");
        Send("fleaConfigDelete", "ImportedCopy");
        Check(!Send("fleaPriceStatus").GetProperty("presets").EnumerateArray().Any(n => n.GetString() == "ImportedCopy"),
            "deleted preset is removed");

        File.WriteAllText(legacy, "{");
        string presetsFile = Path.Combine(path, "settings-presets.json");
        File.WriteAllText(presetsFile, "{");
        var current = book.Current;
        Reject("fleaConfigLoad", null);
        Reject("fleaConfigLoad", "MarketPro");
        Reject("fleaConfigSave", "New");
        Check(ReferenceEquals(current, book.Current) && File.ReadAllText(presetsFile) == "{",
            "unreadable presets preserve active settings and the existing file");
    }

    private static void CheckListings(TemplateTable templates, FleaPriceBook book,
        MongoId restricted, MongoId allowed, JsonUtil json)
    {
        var http = new HttpResponseUtil(json, null!);
        typeof(TemplateTable).GetProperty(nameof(TemplateTable.Items))!.SetValue(templates,
            new Dictionary<MongoId, TemplateItem>
            {
                [restricted] = new() { Id = restricted, Type = "Item", Properties = new() },
                [allowed] = new() { Id = allowed, Type = "Item", Properties = new() }
            });

        var root = new Item { Id = new MongoId(), Template = allowed };
        var attachment = new Item { Id = new MongoId(), Template = restricted, ParentId = root.Id.ToString() };
        var session = new MongoId();
        var pmc = new PmcData { Id = session, Inventory = new() { Items = [root, attachment] } };
        var output = new EventOutputHolder(null!, null!, null!);
        var responses = (Dictionary<MongoId, ItemEventRouterResponse>)AccessTools.Field(typeof(EventOutputHolder), "_outputStore").GetValue(output)!;
        var controller = (RagfairController)RuntimeHelpers.GetUninitializedObject(typeof(RagfairController));
        AccessTools.GetDeclaredFields(typeof(RagfairController)).Single(field => field.FieldType == typeof(EventOutputHolder)).SetValue(controller, output);
        AccessTools.GetDeclaredFields(typeof(RagfairController)).Single(field => field.FieldType == typeof(HttpResponseUtil)).SetValue(controller, http);
        bool Rejects(MongoId id)
        {
            responses[session] = new() { Warnings = [], ProfileChanges = [] };
            var request = new AddOfferRequestData { Items = [id], Requirements = [] };
            return controller.AddPlayerOffer(pmc, request, session).Warnings?.SingleOrDefault()?.ErrorMessage == FleaPrices.ListingBlocked;
        }

        Check(FleaPrices.HasBlockedItem([root, attachment]), "raid listings reject bundles containing restricted items");
        Check(Rejects(attachment.Id), "native listing rejects restricted items");
        Check(Rejects(root.Id), "native listing rejects bundles containing restricted items");
        var request = new CharacterDataRequest { Template = restricted.ToString(), Action = "fleaListingSet", ListingAllowed = true };
        FleaPrices.Handle(request, templates, http);
        Check(book.Current.Listings[restricted] && !FleaPrices.HasBlockedItem([root, attachment]),
            "explicitly allowing an item removes the block");
        request.Action = "fleaListingReset";
        FleaPrices.Handle(request, templates, http);
        Check(!book.Current.Listings.ContainsKey(restricted) && !FleaPrices.HasBlockedItem([attachment]),
            "reset restores native handling");
        request.Action = "fleaListingSet";
        request.ListingAllowed = false;
        FleaPrices.Handle(request, templates, http);
        Check(!book.Current.Listings[restricted] && Rejects(root.Id), "setting a block applies immediately");
    }

    private static void Check(bool condition, string operation)
    {
        if (!condition)
            throw new InvalidOperationException("Flea price check failed: " + operation);
    }
}
