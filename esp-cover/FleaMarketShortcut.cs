using BepInEx.Configuration;
using EFT.UI;
using EFT.UI.Screens;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace LocalSpt5Esp;

internal static class FleaMarketShortcut
{
    private const string Setting = "显示原生跳蚤按钮";
    private static ConfigEntry<bool> enabled = null!;
    private static TradingScreen? tradingScreen;
    private static float nextLookup;
    private static GameObject? blocker;
    private static RectTransform? hitArea;
    private static bool visible, open;
    internal static bool Expanded => visible && open;
    private static Rect Bounds => open
        ? new Rect(Screen.width - Math.Min(462, Screen.width - 16), 72, Math.Min(450, Screen.width - 28), Screen.height - 88)
        : new Rect(Screen.width - 144, 72, 132, 34);

    internal static void Initialize(Plugin plugin)
    {
        enabled = plugin.Config.Bind("Flea Market", "Show native shortcut", false,
            new ConfigDescription("", null, new CnConfig.ConfigurationManagerAttributes { DispName = Setting, Category = MenuSections.FleaMarket }));
    }

    internal static void DrawSetting(Rect area)
    {
        InterfaceMenu.Label(new Rect(area.x, area.y, area.width - 84, area.height), Setting);
        if (InterfaceMenu.Button(new Rect(area.xMax - 76, area.y, 76, area.height), enabled.Value ? "开启" : "关闭", enabled.Value))
            enabled.Value = !enabled.Value;
    }

    internal static void Tick()
    {
        bool market = enabled.Value && IsMarketOpen();
        if (!market)
            open = false;
        visible = market && !InterfaceMenu.Open && !ItemInventoryEditor.Open && Screen.width >= 440 &&
            Screen.height >= 440 && ItemUiContext.Instance?._addOfferWindow?.isActiveAndEnabled != true;
        UpdateBlocker();
    }

    private static void UpdateBlocker()
    {
        if (visible)
        {
            if (blocker == null)
                CreateBlocker();
            var bounds = Bounds;
            hitArea!.anchoredPosition = new Vector2(bounds.x, -bounds.y);
            hitArea.sizeDelta = bounds.size;
        }
        if (blocker != null && blocker.activeSelf != visible)
            blocker.SetActive(visible);
    }

    private static bool IsMarketOpen()
    {
        var screen = EftScreenManager.Instance?.CurrentScreenController?.ScreenType;
        if (screen != EEftScreenType.Traders)
        {
            tradingScreen = null;
            nextLookup = 0;
            return screen == EEftScreenType.FleaMarket;
        }
        if ((tradingScreen == null || !tradingScreen.isActiveAndEnabled) && Time.unscaledTime >= nextLookup)
        {
            tradingScreen = UnityEngine.Object.FindObjectOfType<TradingScreen>();
            nextLookup = Time.unscaledTime + .5f;
        }
        return tradingScreen != null && tradingScreen.isActiveAndEnabled &&
            tradingScreen._ragfairScreen?.isActiveAndEnabled == true;
    }

    private static void CreateBlocker()
    {
        blocker = new GameObject("Assistant flea shortcut", new[] { Il2CppType.Of<RectTransform>() });
        blocker.hideFlags = HideFlags.HideAndDontSave;
        var canvas = blocker.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        blocker.AddComponent<GraphicRaycaster>();
        var target = new GameObject("Shortcut hit area", new[] { Il2CppType.Of<RectTransform>() });
        target.transform.SetParent(blocker.transform, false);
        hitArea = target.GetComponent<RectTransform>();
        hitArea.anchorMin = hitArea.anchorMax = hitArea.pivot = new Vector2(0, 1);
        var image = target.AddComponent<Image>();
        image.color = Color.clear;
        image.canvasRenderer.cullTransparentMesh = false;
    }

    internal static void Draw()
    {
        if (!visible || InterfaceMenu.Open)
            return;
        var bounds = Bounds;
        int depth = GUI.depth;
        GUI.depth = -500;
        try
        {
            InterfaceMenu.Panel(bounds);
            if (!open)
            {
                if (InterfaceMenu.Button(bounds, "跳蚤设置"))
                {
                    open = true;
                    UpdateBlocker();
                }
                return;
            }

            InterfaceMenu.Label(new Rect(bounds.x + 12, bounds.y + 8, bounds.width - 64, 30), MenuSections.FleaMarket, InterfaceMenu.Accent, 18);
            if (InterfaceMenu.Button(new Rect(bounds.xMax - 44, bounds.y + 8, 32, 30), "×"))
            {
                GUI.FocusControl(null);
                open = false;
                UpdateBlocker();
                return;
            }
            FleaMarketPanel.Draw(new Rect(bounds.x + 12, bounds.y + 48, bounds.width - 24, bounds.height - 60), showShortcutSetting: false);
        }
        finally
        {
            GUI.depth = depth;
        }
    }

    internal static void Reset()
    {
        if (blocker != null)
            UnityEngine.Object.Destroy(blocker);
        blocker = null;
        hitArea = null;
        tradingScreen = null;
        visible = open = false;
    }
}
