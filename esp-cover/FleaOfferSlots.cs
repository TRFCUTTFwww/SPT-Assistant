using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.Ragfair;
using HarmonyLib;

namespace LocalSpt5Esp;

internal static class FleaOfferSlots
{
    internal static void Initialize(Plugin plugin)
    {
        // EFT 48558 inlines the MaxOffersCount getter into its callers.
        plugin.Patches.Patch(
            AccessTools.Method(typeof(RagFair), nameof(RagFair.GetMaxOffersCount)),
            postfix: new HarmonyMethod(typeof(FleaOfferSlots), nameof(ApplyLimit)));

        plugin.Patches.Patch(
            AccessTools.Method(typeof(RagfairNewOfferContext), nameof(RagfairNewOfferContext.GetMaxAvailableItemsCount)),
            postfix: new HarmonyMethod(typeof(FleaOfferSlots), nameof(ApplySelectionLimit)));
        plugin.Patches.Patch(
            AccessTools.Method(typeof(AddOfferWindow), nameof(AddOfferWindow.CheckTemplateRestrictions)),
            prefix: new HarmonyMethod(typeof(FleaOfferSlots), nameof(RemoveTemplateLimit)),
            finalizer: new HarmonyMethod(typeof(FleaOfferSlots), nameof(RestoreTemplateLimit)));
    }

    static void ApplyLimit(ref int __result)
    {
        if (MiscPanel.Options.FreeTrade)
            __result = int.MaxValue;
    }

    static void ApplySelectionLimit(Item __0, ref int __result)
    {
        if (MiscPanel.Options.BatchFleaListings && __0 != null)
            __result = int.MaxValue;
    }

    static void RemoveTemplateLimit(RagfairItemRestrictions __1, out (int Single, int Stack)? __state)
    {
        __state = null;
        if (!MiscPanel.Options.BatchFleaListings || __1 == null)
            return;

        // The native check reads these fields directly for individual and pack listings.
        __state = (__1.MaxFlea, __1.MaxFleaStacked);
        __1.MaxFlea = int.MaxValue;
        __1.MaxFleaStacked = int.MaxValue;
    }

    static void RestoreTemplateLimit(RagfairItemRestrictions __1, (int Single, int Stack)? __state)
    {
        if (__state is not { } original)
            return;

        __1.MaxFlea = original.Single;
        __1.MaxFleaStacked = original.Stack;
    }

    internal static void RefreshOpenMarket()
    {
        foreach (var screen in UnityEngine.Object.FindObjectsOfType<RagfairScreen>())
        {
            if (screen != null && screen.gameObject.activeInHierarchy &&
                screen._ragfair != null && screen._addOfferButton != null)
                screen.UpdateAddOfferButton();
        }
    }
}
