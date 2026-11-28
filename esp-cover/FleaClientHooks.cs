using System.Globalization;
using Comfort.Common;
using EFT;
using EFT.HandBook;
using EFT.InventoryLogic;
using EFT.Trading;
using EFT.UI;
using EFT.UI.Ragfair;
using HarmonyLib;
using UnityEngine;

namespace LocalSpt5Esp;

internal static class FleaClientHooks
{
    private const string RubTpl = "5449016a4bdc2d6f028b456f";
    private sealed record OriginalSettings(RagfairSettings Instance, int MinUserLevel, bool ByCategory);
    private sealed record TraderQuote(string TraderName, double RubTotal, bool Valid);

    private static OriginalSettings? savedSettings;
    private static bool appliedIgnoreLevel;
    private static IntPtr autoPricedWindow;
    private static string autoPricedItemId = "";
    private static string cachedQuoteKey = "";
    private static TraderQuote cachedQuote = new("", 0, false);
    private static readonly Vector3[] corners = new Vector3[4];

    internal static bool IgnoreLevelLocks => FleaMarketPanel.ActiveRules.IgnoreLevelLocks;
    internal static int AutoPriceMode => FleaMarketPanel.ActiveRules.AutoPriceMode;
    internal static int AutoPricePercent => FleaMarketPanel.ActiveRules.AutoPricePercent;
    internal static bool ShowTraderCompare => FleaMarketPanel.ActiveRules.ShowTraderCompare;

    internal static void Initialize(Plugin plugin)
    {
        NativeFleaOfferEdit.Initialize(plugin);
        var h = plugin.Patches;
        // #2 Client level locks (entry level 15 + EFT 1.3 category/item buy & sell locks)
        Patch(h, typeof(RagFair), "get_HasMinLevel", prefix: nameof(BypassHasMinLevel));
        PatchExact(h, typeof(RagFair), nameof(RagFair.IsCategoryOrItemLockedByLevelRecursive),
            [typeof(string), typeof(int).MakeByRefType()], prefix: nameof(UnlockByIdShort));
        PatchExact(h, typeof(RagFair), nameof(RagFair.IsCategoryOrItemLockedByLevelRecursive),
            [typeof(string), typeof(int), typeof(Handbook), typeof(int).MakeByRefType()], prefix: nameof(UnlockByIdFull));
        PatchExact(h, typeof(RagFair), nameof(RagFair.IsCategoryOrItemLockedByLevel),
            [typeof(string), typeof(int).MakeByRefType()], prefix: nameof(UnlockByIdShort));
        PatchExact(h, typeof(RagFair), nameof(RagFair.IsCategoryOrItemLockedByLevel),
            [typeof(string), typeof(int), typeof(Handbook), typeof(int).MakeByRefType()], prefix: nameof(UnlockByIdFull));
        PatchExact(h, typeof(RagFair), nameof(RagFair.ShouldShowCategoryOrItemLevelLock),
            [typeof(string), typeof(int).MakeByRefType()], prefix: nameof(UnlockByIdShort));
        PatchExact(h, typeof(RagFair), nameof(RagFair.IsItemOrChildLockedByLevelRecursive),
            [typeof(Item), typeof(int).MakeByRefType()], prefix: nameof(UnlockByItemShort));
        PatchExact(h, typeof(RagFair), nameof(RagFair.IsItemOrChildLockedByLevelRecursive),
            [typeof(Item), typeof(int), typeof(Handbook), typeof(int).MakeByRefType()], prefix: nameof(UnlockByItemFull));
        Patch(h, typeof(RagfairNewOfferContext), nameof(RagfairNewOfferContext.IsLockedByLevel), prefix: nameof(UnlockNewOfferItem));

        // #3 Native AddOfferWindow auto-fill & selection tracking
        Patch(h, typeof(AddOfferWindow), nameof(AddOfferWindow.ItemChangedHandler), prefix: nameof(OnSelectedItemChanging));
        Patch(h, typeof(AddOfferWindow), nameof(AddOfferWindow.RefreshButtonPressed), prefix: nameof(OnRefreshPricesPressed));
        Patch(h, typeof(AddOfferWindow), nameof(AddOfferWindow.Close), postfix: nameof(OnOfferWindowClosed));
        Patch(h, typeof(ItemMarketPricesPanel), nameof(ItemMarketPricesPanel._UpdatePrices_b__22_0), postfix: nameof(OnMarketPricesLoaded));
    }

    private static void Patch(PersistentHarmonyPatcher h, Type type, string method, string? prefix = null, string? postfix = null)
    {
        var target = AccessTools.Method(type, method);
        if (target == null)
        {
            Plugin.Self.Log.LogWarning($"FleaClientHooks: method not found {type.Name}.{method}");
            return;
        }
        h.Patch(target,
            prefix: prefix == null ? null : new HarmonyMethod(typeof(FleaClientHooks), prefix),
            postfix: postfix == null ? null : new HarmonyMethod(typeof(FleaClientHooks), postfix));
    }

    private static void PatchExact(PersistentHarmonyPatcher h, Type type, string method, Type[] parameters, string? prefix = null, string? postfix = null)
    {
        var target = AccessTools.Method(type, method, parameters);
        if (target == null)
        {
            Plugin.Self.Log.LogWarning($"FleaClientHooks: overload not found {type.Name}.{method}");
            return;
        }
        h.Patch(target,
            prefix: prefix == null ? null : new HarmonyMethod(typeof(FleaClientHooks), prefix),
            postfix: postfix == null ? null : new HarmonyMethod(typeof(FleaClientHooks), postfix));
    }

    internal static void Tick()
    {
        SyncLevelSettings(IgnoreLevelLocks);
    }

    private static void SyncLevelSettings(bool unlock)
    {
        try
        {
            var settings = RagFair.Settings;
            if (settings == null || settings.Pointer == IntPtr.Zero)
                return;

            if (savedSettings == null || savedSettings.Instance == null || savedSettings.Instance.Pointer != settings.Pointer)
                savedSettings = new OriginalSettings(settings, settings.minUserLevel, settings.RagfairMinUserLevelByCategory);

            if (unlock == appliedIgnoreLevel && (!unlock || settings.minUserLevel <= 1))
                return;

            appliedIgnoreLevel = unlock;
            if (unlock)
            {
                settings.minUserLevel = 1;
                settings.RagfairMinUserLevelByCategory = false;
            }
            else if (savedSettings != null)
            {
                settings.minUserLevel = savedSettings.MinUserLevel;
                settings.RagfairMinUserLevelByCategory = savedSettings.ByCategory;
            }

            var app = Singleton<AbstractApplication>.Instance?.TryCast<TarkovApplication>();
            var ragfair = app?.Session?.RagFair ?? ItemUiContext.Instance?._ragfair;
            if (ragfair != null && ragfair.Pointer != IntPtr.Zero)
            {
                ragfair.UpdateRagfairStatus();
                FleaOfferSlots.RefreshOpenMarket();
            }
        }
        catch (Exception error)
        {
            Plugin.Self?.Error(error);
        }
    }

    internal static void Reset()
    {
        try
        {
            if (savedSettings?.Instance != null && savedSettings.Instance.Pointer != IntPtr.Zero)
            {
                savedSettings.Instance.minUserLevel = savedSettings.MinUserLevel;
                savedSettings.Instance.RagfairMinUserLevelByCategory = savedSettings.ByCategory;
                var app = Singleton<AbstractApplication>.Instance?.TryCast<TarkovApplication>();
                var ragfair = app?.Session?.RagFair ?? ItemUiContext.Instance?._ragfair;
                if (ragfair != null && ragfair.Pointer != IntPtr.Zero)
                    ragfair.UpdateRagfairStatus();
            }
        }
        catch { }
        savedSettings = null;
        appliedIgnoreLevel = false;
        autoPricedWindow = IntPtr.Zero;
        autoPricedItemId = "";
        cachedQuoteKey = "";
        cachedQuote = new("", 0, false);
    }

    private static bool BypassHasMinLevel(ref bool __result)
    {
        if (!IgnoreLevelLocks)
            return true;
        __result = true;
        return false;
    }

    private static bool UnlockByIdShort(ref int __1, ref bool __result)
    {
        if (!IgnoreLevelLocks)
            return true;
        __1 = 0;
        __result = false;
        return false;
    }

    private static bool UnlockByIdFull(ref int __3, ref bool __result)
    {
        if (!IgnoreLevelLocks)
            return true;
        __3 = 0;
        __result = false;
        return false;
    }

    private static bool UnlockByItemShort(ref int __1, ref bool __result)
    {
        if (!IgnoreLevelLocks)
            return true;
        __1 = 0;
        __result = false;
        return false;
    }

    private static bool UnlockByItemFull(ref int __3, ref bool __result)
    {
        if (!IgnoreLevelLocks)
            return true;
        __3 = 0;
        __result = false;
        return false;
    }

    private static bool UnlockNewOfferItem(ref string __1, ref bool __result)
    {
        if (!IgnoreLevelLocks)
            return true;
        __1 = string.Empty;
        __result = false;
        return false;
    }

    private static void OnSelectedItemChanging(AddOfferWindow __instance, Item __0)
    {
        if (__instance == null)
            return;
        string nextId = __0?.Id ?? "";
        if (__instance.Pointer != autoPricedWindow || nextId != autoPricedItemId)
        {
            autoPricedWindow = IntPtr.Zero;
            autoPricedItemId = "";
        }
    }

    private static void OnRefreshPricesPressed()
    {
        autoPricedWindow = IntPtr.Zero;
        autoPricedItemId = "";
    }

    private static void OnOfferWindowClosed()
    {
        autoPricedWindow = IntPtr.Zero;
        autoPricedItemId = "";
        cachedQuoteKey = "";
    }

    private static void OnMarketPricesLoaded(ItemMarketPricesPanel __instance, ItemMarketPrices __0)
    {
        try
        {
            int mode = AutoPriceMode;
            if (mode is < 1 or > 3 || __instance == null || __0 == null)
                return;

            AddOfferWindow? window = ItemUiContext.Instance?._addOfferWindow;
            if (window == null || !window.gameObject.activeInHierarchy || window._pricesPanel?.Pointer != __instance.Pointer)
            {
                window = null;
                foreach (var candidate in UnityEngine.Object.FindObjectsOfType<AddOfferWindow>())
                {
                    if (candidate != null && candidate.gameObject.activeInHierarchy && candidate._pricesPanel?.Pointer == __instance.Pointer)
                    {
                        window = candidate;
                        break;
                    }
                }
            }
            if (window == null || window._selectedItem == null || window._rubRequirementView == null)
                return;

            string itemId = window._selectedItem.Id;
            if (window.Pointer == autoPricedWindow && itemId == autoPricedItemId)
                return;

            double raw = mode switch
            {
                1 => __0.min > 1f ? __0.min - 1.0 : __0.min,
                2 => __0.avg,
                3 => __0.max,
                _ => 0
            };
            if (!double.IsFinite(raw) || raw <= 0)
                return;

            int pct = Math.Clamp(AutoPricePercent <= 0 ? 100 : AutoPricePercent, 10, 500);
            long suggested = Math.Max(1L, (long)Math.Round(raw * (pct / 100.0), MidpointRounding.AwayFromZero));
            if (suggested > int.MaxValue)
                suggested = int.MaxValue;

            autoPricedWindow = window.Pointer;
            autoPricedItemId = itemId;
            window._rubRequirementView.SetValue(suggested.ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception error)
        {
            Plugin.Self?.Error(error);
        }
    }

    internal static void Draw()
    {
        if (!ShowTraderCompare || InterfaceMenu.Open || ItemInventoryEditor.Open)
            return;

        var window = ItemUiContext.Instance?._addOfferWindow;
        if (window == null || window.Pointer == IntPtr.Zero || !window.isActiveAndEnabled || !window.gameObject.activeInHierarchy)
            return;
        var selected = window._selectedItem;
        if (selected == null || selected.Pointer == IntPtr.Zero)
            return;

        try
        {
            int offerCount = Math.Max(1, window.OfferItemCount);
            string key = $"{selected.Id}:{offerCount}:{window._offerContext?.SelectedItems?.Count ?? 1}";
            if (key != cachedQuoteKey)
            {
                cachedQuoteKey = key;
                cachedQuote = ComputeBestTraderBuyback(window, selected);
            }

            double unitOrPack = window._rubRequirementView != null && window._rubRequirementView._quantity > 0
                ? window._rubRequirementView._quantity
                : window._requirementsCost;
            double grossRub = window._sellInOnePiece ? unitOrPack : unitOrPack * offerCount;
            long taxRub = FleaFees.Free ? 0 : ParseDigits(window._taxPrice?.text);
            double netRub = grossRub - taxRub;

            var rt = window.RectTransform;
            if (rt == null)
                return;
            rt.GetWorldCorners(corners);
            var canvas = rt.GetComponentInParent<Canvas>();
            Camera? uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 minScreen = RectTransformUtility.WorldToScreenPoint(uiCam, corners[0]);
            Vector2 maxScreen = RectTransformUtility.WorldToScreenPoint(uiCam, corners[2]);

            float barWidth = Math.Clamp(maxScreen.x - minScreen.x - 16f, 360f, 760f);
            float barX = Math.Clamp(minScreen.x + 8f, 8f, Math.Max(8f, Screen.width - barWidth - 8f));
            float topY = Screen.height - maxScreen.y;
            float barY = topY >= 40f ? topY - 36f : Math.Min(Screen.height - 38f, Screen.height - minScreen.y + 4f);
            var bar = new Rect(barX, barY, barWidth, 32f);

            string summary;
            Color textColor = InterfaceMenu.Text;
            bool canFillTrader = false;
            long fillUnit = 0;

            if (!cachedQuote.Valid || cachedQuote.RubTotal <= 0)
            {
                summary = grossRub > 0
                    ? string.Format(I18n.Text("商人回收：无可售商人  |  跳蚤税后净得：₽{0}"), Math.Round(netRub).ToString("N0", CultureInfo.CurrentCulture))
                    : I18n.Text("商人回收：无可售商人");
            }
            else
            {
                long traderRub = (long)Math.Round(cachedQuote.RubTotal);
                fillUnit = window._sellInOnePiece
                    ? Math.Max(1L, traderRub)
                    : Math.Max(1L, (long)Math.Ceiling(cachedQuote.RubTotal / offerCount));
                canFillTrader = window._rubRequirementView != null && fillUnit <= int.MaxValue;

                if (grossRub > 0)
                {
                    long diff = (long)Math.Round(netRub) - traderRub;
                    string sign = diff >= 0 ? "+" : "-";
                    summary = string.Format(
                        I18n.Text("商人最高：{0} ₽{1}  |  税后净得：₽{2}（{3}₽{4}）"),
                        cachedQuote.TraderName,
                        traderRub.ToString("N0", CultureInfo.CurrentCulture),
                        ((long)Math.Round(netRub)).ToString("N0", CultureInfo.CurrentCulture),
                        sign,
                        Math.Abs(diff).ToString("N0", CultureInfo.CurrentCulture));
                    textColor = diff < 0 ? new Color(1f, 0.55f, 0.25f) : new Color(0.45f, 0.95f, 0.55f);
                }
                else
                {
                    summary = string.Format(I18n.Text("商人最高回收：{0} ₽{1}"),
                        cachedQuote.TraderName,
                        traderRub.ToString("N0", CultureInfo.CurrentCulture));
                    textColor = InterfaceMenu.Accent;
                }
            }

            int prevDepth = GUI.depth;
            GUI.depth = -600;
            try
            {
                InterfaceMenu.Panel(bar, 0.95f);
                float btnW = canFillTrader ? 96f : 0f;
                InterfaceMenu.Label(new Rect(bar.x + 10f, bar.y + 2f, bar.width - btnW - 20f, 28f), summary, textColor, 12, translate: false);
                if (canFillTrader && InterfaceMenu.Button(new Rect(bar.xMax - btnW - 6f, bar.y + 4f, btnW, 24f), "按商人价"))
                    window._rubRequirementView!.SetValue(fillUnit.ToString(CultureInfo.InvariantCulture));
            }
            finally
            {
                GUI.depth = prevDepth;
            }
        }
        catch (Exception error)
        {
            Plugin.Self?.Error(error);
        }
    }

    private static TraderQuote ComputeBestTraderBuyback(AddOfferWindow window, Item primary)
    {
        var app = Singleton<AbstractApplication>.Instance?.TryCast<TarkovApplication>();
        var session = app?.GetClientBackEndSession()?.TryCast<ITradingSession>();
        if (session?.Traders == null)
            return new("", 0, false);

        var items = new List<Item>();
        if (window._offerContext?.SelectedItems is { Count: > 0 } selectedSet)
        {
            foreach (var it in selectedSet)
                if (it != null && it.Pointer != IntPtr.Zero)
                    items.Add(it);
        }
        if (items.Count == 0)
            items.Add(primary);

        string bestName = "";
        double bestRub = 0;
        bool any = false;

        foreach (var trader in session.Traders)
        {
            if (trader == null || trader.Pointer == IntPtr.Zero || !trader.PricesLoaded)
                continue;
            double sumRub = 0;
            bool buysAll = true;
            foreach (var it in items)
            {
                var quote = trader.GetUserItemPrice(it);
                if (!quote.HasValue || !quote.Value.CurrencyId.HasValue || quote.Value.Amount <= 0)
                {
                    buysAll = false;
                    break;
                }
                string currency = quote.Value.CurrencyId.Value.ToString();
                double rate = currency == RubTpl ? 1.0 : trader._supplyData?.GetBasePrice(quote.Value.CurrencyId.Value) ?? 0;
                if (!double.IsFinite(rate) || rate <= 0)
                {
                    buysAll = false;
                    break;
                }
                sumRub += quote.Value.Amount * rate;
            }
            if (buysAll && sumRub > bestRub)
            {
                bestRub = sumRub;
                bestName = string.IsNullOrWhiteSpace(trader.LocalizedName) ? trader.Id : trader.LocalizedName;
                any = true;
            }
        }
        return new(bestName, bestRub, any);
    }

    private static long ParseDigits(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return 0;
        long value = 0;
        foreach (char c in raw)
        {
            if (c is >= '0' and <= '9')
            {
                value = value * 10 + (c - '0');
                if (value > 1_000_000_000_000L)
                    break;
            }
        }
        return value;
    }
}
