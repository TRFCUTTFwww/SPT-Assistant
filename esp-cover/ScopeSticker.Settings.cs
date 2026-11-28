using System;
using System.Globalization;
using UnityEngine;

namespace LocalSpt5Esp;

internal static partial class ScopeSticker
{
    private static readonly (string Label, string Hex)[] QuickSwatches =
    [
        ("红", "#FF3B30"),
        ("绿", "#00FF66"),
        ("青", "#00F0FF"),
        ("黄", "#FFD60A"),
        ("橙", "#FF9500"),
        ("粉", "#FF38B8"),
        ("紫", "#AF52DE"),
        ("白", "#FFFFFF")
    ];

    private static readonly (int Mode, string Label)[] ColorModes =
    [
        (0, "原图颜色"),
        (4, "固定颜色"),
        (1, "彩虹渐变"),
        (2, "双色渐变"),
        (3, "呼吸脉冲")
    ];

    private static Vector2 controlsScroll;
    private static string colorKey = "", colorObserved = "", hitHex = "", hitRed = "", hitGreen = "", hitBlue = "", colorNotice = "";
    private static string dynKey = "", dynPrimaryObs = "", dynSecondaryObs = "", dynPrimaryHex = "", dynSecondaryHex = "", dynSpeedDraft = "", dynNotice = "";
    private static string dynPriR = "", dynPriG = "", dynPriB = "";
    private static float dynSpeedObs = -1f;

    private static string menuPrimaryObs = "", menuSecondaryObs = "", menuPrimaryHex = "", menuSecondaryHex = "", menuSpeedDraft = "", menuColorNotice = "";
    private static string menuPriR = "", menuPriG = "", menuPriB = "";
    private static float menuSpeedObs = -1f;

    private static float ComputeColorPanelHeight(int mode, bool hasNotice)
    {
        float h = 64; // header + 2 rows of mode buttons
        if (mode == 0) return h;
        h += 34; // mask toggle
        if (mode is 1 or 2 or 3) h += 62; // speed slider + exact speed field
        if (mode is 4 or 2 or 3) h += 160; // swatches + RGB sliders + RGB fields + HEX field
        if (mode == 2) h += 34; // secondary HEX
        if (hasNotice) h += 28;
        return h;
    }

    private static void DrawEditingControls(Rect rect)
    {
        if (draft == null) return;
        float dynExtra = ComputeColorPanelHeight(draft.ColorMode, dynNotice.Length > 0);
        float height = 442 + dynExtra + (draft.AutoContrast ? 40 : 0) + (draft.HitFlash ? 190 : 0) + (notice.Length > 0 ? 44 : 0) + (choosingImage ? 184 : 0);
        controlsScroll = GUI.BeginScrollView(rect, controlsScroll, new Rect(0, 0, rect.width - 20, height));
        try
        {
            float xPos = 0, yPos = 0, w = rect.width - 24;
            if (InterfaceMenu.Button(new Rect(0, yPos, w - 90, 30), ImageName(draft.Image), choosingImage)) choosingImage = !choosingImage;
            bool oldEnabled = GUI.enabled; GUI.enabled = oldEnabled && importTask == null;
            if (InterfaceMenu.Button(new Rect(w - 82, yPos, 82, 30), "导入图片")) BeginImport();
            GUI.enabled = oldEnabled; yPos += 38;
            draft = draft with
            {
                Width = EditorSlider(xPos, ref yPos, w, "宽度", draft.Width, 1, 150),
                X = EditorSlider(xPos, ref yPos, w, "水平位置", draft.X, -150, 150),
                Y = EditorSlider(xPos, ref yPos, w, "垂直位置", draft.Y, -150, 150),
                Opacity = EditorSlider(xPos, ref yPos, w, "透明度", draft.Opacity * 100, 0, 100) / 100,
                Rotation = EditorSlider(xPos, ref yPos, w, "旋转角度", draft.Rotation, -180, 180, "°")
            };
            float turnWidth = (w - 12) / 3;
            if (InterfaceMenu.Button(new Rect(0, yPos, turnWidth, 28), "旋转 -90°")) draft = (draft with { Rotation = draft.Rotation - 90 }).Normalized();
            if (InterfaceMenu.Button(new Rect(turnWidth + 6, yPos, turnWidth, 28), "角度归零")) draft = draft with { Rotation = 0 };
            if (InterfaceMenu.Button(new Rect((turnWidth + 6) * 2, yPos, turnWidth, 28), "旋转 +90°")) draft = (draft with { Rotation = draft.Rotation + 90 }).Normalized();
            yPos += 36;
            DrawDynamicColorControls(w, ref yPos);
            if (InterfaceMenu.Button(new Rect(0, yPos, w, 30), draft.AutoContrast ? "自动高对比度 · 开启" : "自动高对比度 · 关闭", draft.AutoContrast)) draft = draft with { AutoContrast = !draft.AutoContrast };
            yPos += 38;
            if (draft.AutoContrast)
            {
                InterfaceMenu.Paragraph(new Rect(0, yPos, w, 38), "按背景逐处变黑或变白；保留原图透明度。", null, 12); yPos += 40;
            }
            if (InterfaceMenu.Button(new Rect(0, yPos, w, 30), draft.HitFlash ? "命中闪色 · 开启" : "命中闪色 · 关闭", draft.HitFlash))
            {
                draft = draft with { HitFlash = !draft.HitFlash }; if (!draft.HitFlash) ClearFlash();
            }
            yPos += 38;
            if (draft.HitFlash) DrawHitColor(w, ref yPos);
            InterfaceMenu.Paragraph(new Rect(0, yPos, w, 44), "拖动镜片内贴纸；滚轮或右上角方块缩放。Enter 保存，Esc 取消。", null, 12); yPos += 48;
            InterfaceMenu.Label(new Rect(0, yPos, w, 24), "同倍镜的每张图片分别保存布局", InterfaceMenu.Accent, 12); yPos += 28;
            if (notice.Length > 0) { InterfaceMenu.Paragraph(new Rect(0, yPos, w, 42), notice, null, 12); yPos += 44; }
            if (choosingImage) DrawImageChoices(new Rect(0, yPos, w, 176));
        }
        finally { GUI.EndScrollView(); }
    }

    private static void SyncPrimaryFields(ScopeStickerRules.Rgb rgb, ref string obs, ref string hex, ref string r, ref string g, ref string b)
    {
        obs = hex = rgb.Hex;
        r = rgb.R.ToString(CultureInfo.InvariantCulture);
        g = rgb.G.ToString(CultureInfo.InvariantCulture);
        b = rgb.B.ToString(CultureInfo.InvariantCulture);
    }

    private static void DrawDynamicColorControls(float w, ref float yPos)
    {
        if (draft == null) return;
        string sessionKey = editorSession + "/" + editScopeKey + "/" + draft.Image;
        if (sessionKey != dynKey || dynPrimaryObs != draft.ColorPrimary || dynSecondaryObs != draft.ColorSecondary)
        {
            dynKey = sessionKey;
            if (!ScopeStickerRules.TryHex(draft.ColorPrimary, out var pInit)) pInit = new(0, 240, 255);
            SyncPrimaryFields(pInit, ref dynPrimaryObs, ref dynPrimaryHex, ref dynPriR, ref dynPriG, ref dynPriB);
            dynSecondaryObs = dynSecondaryHex = draft.ColorSecondary;
            dynNotice = "";
        }
        if (Math.Abs(dynSpeedObs - draft.ColorSpeed) > .0001f)
        {
            dynSpeedObs = draft.ColorSpeed;
            dynSpeedDraft = draft.ColorSpeed.ToString("0.##", CultureInfo.InvariantCulture);
        }

        int mode = draft.ColorMode;
        string primary = draft.ColorPrimary;
        string secondary = draft.ColorSecondary;
        float speed = draft.ColorSpeed;
        bool useMask = draft.ColorUseMask;

        DrawSharedColorPicker(0, ref yPos, w, draft,
            ref mode, ref primary, ref secondary, ref speed, ref useMask,
            ref dynPrimaryObs, ref dynPrimaryHex, ref dynPriR, ref dynPriG, ref dynPriB,
            ref dynSecondaryObs, ref dynSecondaryHex, ref dynSpeedObs, ref dynSpeedDraft, ref dynNotice);

        if (mode != draft.ColorMode || primary != draft.ColorPrimary || secondary != draft.ColorSecondary ||
            Math.Abs(speed - draft.ColorSpeed) > .0001f || useMask != draft.ColorUseMask)
        {
            draft = (draft with
            {
                ColorMode = mode,
                ColorPrimary = primary,
                ColorSecondary = secondary,
                ColorSpeed = speed,
                ColorUseMask = useMask
            }).Normalized();
        }
    }

    internal static float MenuColorSettingsHeight() =>
        ComputeColorPanelHeight(defaultColorMode?.Value ?? 0, menuColorNotice.Length > 0) + 46;

    internal static void DrawMenuColorSettings(float xPos, ref float yPos, float w)
    {
        int mode = Math.Clamp(defaultColorMode.Value, 0, 4);
        string primary = ScopeStickerRules.TryHex(defaultColorPrimary.Value, out var pValid) ? pValid.Hex : "#00F0FF";
        string secondary = ScopeStickerRules.TryHex(defaultColorSecondary.Value, out var sValid) ? sValid.Hex : "#FF38B8";
        float speed = float.IsFinite(defaultColorSpeed.Value) && defaultColorSpeed.Value > 0 ? Math.Clamp(defaultColorSpeed.Value, .01f, 50f) : 1f;
        bool useMask = defaultColorUseMask.Value;

        if (menuPrimaryObs != primary || menuSecondaryObs != secondary)
        {
            ScopeStickerRules.TryHex(primary, out var pInit);
            SyncPrimaryFields(pInit, ref menuPrimaryObs, ref menuPrimaryHex, ref menuPriR, ref menuPriG, ref menuPriB);
            menuSecondaryObs = menuSecondaryHex = secondary;
        }
        if (Math.Abs(menuSpeedObs - speed) > .0001f)
        {
            menuSpeedObs = speed;
            menuSpeedDraft = speed.ToString("0.##", CultureInfo.InvariantCulture);
        }

        var sample = new ScopeStickerProfile
        {
            ColorMode = mode,
            ColorPrimary = primary,
            ColorSecondary = secondary,
            ColorSpeed = speed,
            ColorUseMask = useMask
        };

        DrawSharedColorPicker(xPos, ref yPos, w, sample,
            ref mode, ref primary, ref secondary, ref speed, ref useMask,
            ref menuPrimaryObs, ref menuPrimaryHex, ref menuPriR, ref menuPriG, ref menuPriB,
            ref menuSecondaryObs, ref menuSecondaryHex, ref menuSpeedObs, ref menuSpeedDraft, ref menuColorNotice);

        if (mode != defaultColorMode.Value) defaultColorMode.Value = mode;
        if (primary != defaultColorPrimary.Value) defaultColorPrimary.Value = primary;
        if (secondary != defaultColorSecondary.Value) defaultColorSecondary.Value = secondary;
        if (Math.Abs(speed - defaultColorSpeed.Value) > .0001f) defaultColorSpeed.Value = speed;
        if (useMask != defaultColorUseMask.Value) defaultColorUseMask.Value = useMask;

        bool canSync = store != null && store.Profiles.Count > 0;
        bool prevGui = GUI.enabled; GUI.enabled = prevGui && canSync;
        if (InterfaceMenu.Button(new Rect(xPos, yPos, w, 30), "应用当前颜色到全部已存倍镜"))
        {
            store!.ApplyColorToAll(mode, primary, secondary, speed, useMask);
            if (currentKey.Length > 0)
                current = store.Get(currentKey, Defaults);
            notice = "已将当前颜色设置同步到全部已保存倍镜方案。";
        }
        GUI.enabled = prevGui;
        yPos += 38;
    }

    private static void DrawSharedColorPicker(
        float xPos,
        ref float yPos,
        float w,
        ScopeStickerProfile previewSource,
        ref int mode,
        ref string primary,
        ref string secondary,
        ref float speed,
        ref bool useMask,
        ref string priObs,
        ref string priHex,
        ref string priR,
        ref string priG,
        ref string priB,
        ref string secObs,
        ref string secHex,
        ref float speedObs,
        ref string speedDraft,
        ref string localNotice)
    {
        InterfaceMenu.Label(new Rect(xPos, yPos, w - 42, 22), "贴纸颜色与变色效果", InterfaceMenu.Accent, 13);
        if (mode != 0)
        {
            var preview = ScopeStickerRules.EvaluateDynamicColor(previewSource, Time.unscaledTime);
            byte alpha = (byte)Math.Clamp(MathF.Round(preview.AlphaFactor * 255f), 32, 255);
            InterfaceMenu.Surface(new Rect(xPos + w - 36, yPos + 1, 36, 20), new Color32(preview.R, preview.G, preview.B, alpha), 4);
        }
        yPos += 24;

        float row1Cell = (w - 8) / 3f;
        for (int i = 0; i < 3; i++)
        {
            var item = ColorModes[i];
            if (InterfaceMenu.Button(new Rect(xPos + i * (row1Cell + 4), yPos, row1Cell, 28), item.Label, mode == item.Mode))
            {
                mode = item.Mode;
                localNotice = "";
            }
        }
        yPos += 32;
        float row2Cell = (w - 4) / 2f;
        for (int i = 3; i < 5; i++)
        {
            var item = ColorModes[i];
            if (InterfaceMenu.Button(new Rect(xPos + (i - 3) * (row2Cell + 4), yPos, row2Cell, 28), item.Label, mode == item.Mode))
            {
                mode = item.Mode;
                localNotice = "";
            }
        }
        yPos += 34;

        if (mode == 0) return;

        if (InterfaceMenu.Button(new Rect(xPos, yPos, w, 28), useMask ? "着色方式 · 纯色剪影（适合黑线稿）" : "着色方式 · 原图叠色（保留明暗）", useMask))
            useMask = !useMask;
        yPos += 34;

        const float applyW = 56;
        if (mode is 1 or 2 or 3)
        {
            float clampedSpeed = Math.Clamp(speed, .05f, 10f);
            InterfaceMenu.Label(new Rect(xPos, yPos, 120, 24), I18n.Text("变色速度") + $"  {clampedSpeed:F1}x", null, 12, translate: false);
            float sliderSpeed = InterfaceMenu.Slider(new Rect(xPos + 126, yPos + 5, Math.Max(30, w - 126), 20), clampedSpeed, .05f, 10f);
            yPos += 29;
            if (Math.Abs(sliderSpeed - clampedSpeed) > .001f)
            {
                speed = Math.Clamp(sliderSpeed, .01f, 50f);
                speedObs = speed;
                speedDraft = speed.ToString("0.##", CultureInfo.InvariantCulture);
            }

            const float speedLabelW = 72;
            float speedFieldW = Math.Max(50, w - speedLabelW - applyW - 8);
            InterfaceMenu.Label(new Rect(xPos, yPos, speedLabelW, 26), "精确速度", size: 12);
            speedDraft = InterfaceMenu.Field(new Rect(xPos + speedLabelW, yPos, speedFieldW, 26), speedDraft);
            if (InterfaceMenu.Button(new Rect(xPos + w - applyW, yPos, applyW, 26), "应用"))
            {
                if (float.TryParse(speedDraft.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) && float.IsFinite(parsed) && parsed > 0)
                {
                    speed = Math.Clamp(parsed, .01f, 50f);
                    speedObs = speed;
                    speedDraft = speed.ToString("0.##", CultureInfo.InvariantCulture);
                    localNotice = "";
                }
                else localNotice = "变色速度请输入大于 0 的有限数值（如 0.25～50）。";
            }
            yPos += 32;
        }

        if (mode is 4 or 2 or 3)
        {
            if (!ScopeStickerRules.TryHex(primary, out var pRgb)) pRgb = new(0, 240, 255);

            // Quick preset color swatches
            float swatchW = (w - 7 * 4) / 8f;
            for (int i = 0; i < QuickSwatches.Length; i++)
            {
                var sw = QuickSwatches[i];
                ScopeStickerRules.TryHex(sw.Hex, out var swRgb);
                var swRect = new Rect(xPos + i * (swatchW + 4), yPos, swatchW, 24);
                InterfaceMenu.Surface(new Rect(swRect.x, swRect.y - 3, swRect.width, 3), new Color32(swRgb.R, swRgb.G, swRgb.B, 255), 1);
                if (InterfaceMenu.Button(swRect, sw.Label, string.Equals(primary, sw.Hex, StringComparison.OrdinalIgnoreCase)))
                {
                    primary = sw.Hex;
                    SyncPrimaryFields(swRgb, ref priObs, ref priHex, ref priR, ref priG, ref priB);
                    localNotice = "";
                }
            }
            yPos += 30;

            // Live R / G / B sliders
            byte nr = (byte)Math.Clamp(MathF.Round(DrawChannelSlider(xPos, ref yPos, w, "R", pRgb.R)), 0, 255);
            byte ng = (byte)Math.Clamp(MathF.Round(DrawChannelSlider(xPos, ref yPos, w, "G", pRgb.G)), 0, 255);
            byte nb = (byte)Math.Clamp(MathF.Round(DrawChannelSlider(xPos, ref yPos, w, "B", pRgb.B)), 0, 255);
            if (nr != pRgb.R || ng != pRgb.G || nb != pRgb.B)
            {
                var updated = new ScopeStickerRules.Rgb(nr, ng, nb);
                primary = updated.Hex;
                SyncPrimaryFields(updated, ref priObs, ref priHex, ref priR, ref priG, ref priB);
                localNotice = "";
                pRgb = updated;
            }

            // Exact RGB numeric fields
            const float rgbLabel = 36;
            float rgbInputW = Math.Max(60, w - rgbLabel - applyW - 6), rgbCell = rgbInputW / 3f;
            InterfaceMenu.Label(new Rect(xPos, yPos, rgbLabel, 26), "RGB", size: 12);
            priR = InterfaceMenu.Field(new Rect(xPos + rgbLabel, yPos, rgbCell - 4, 26), priR);
            priG = InterfaceMenu.Field(new Rect(xPos + rgbLabel + rgbCell, yPos, rgbCell - 4, 26), priG);
            priB = InterfaceMenu.Field(new Rect(xPos + rgbLabel + 2 * rgbCell, yPos, rgbCell, 26), priB);
            if (InterfaceMenu.Button(new Rect(xPos + w - applyW, yPos, applyW, 26), "应用"))
            {
                if (ScopeStickerRules.TryRgb(priR, priG, priB, out var nextRgb))
                {
                    primary = nextRgb.Hex;
                    SyncPrimaryFields(nextRgb, ref priObs, ref priHex, ref priR, ref priG, ref priB);
                    localNotice = "";
                }
                else localNotice = "RGB 各通道须为 0–255 的整数。";
            }
            yPos += 30;

            // Exact HEX input
            InterfaceMenu.Label(new Rect(xPos, yPos, 54, 26), mode == 2 ? "颜色 A" : "贴纸颜色", size: 12);
            InterfaceMenu.Surface(new Rect(xPos + 56, yPos + 2, 24, 22), new Color32(pRgb.R, pRgb.G, pRgb.B, 255), 4);
            priHex = InterfaceMenu.Field(new Rect(xPos + 86, yPos, Math.Max(50, w - 86 - applyW - 6), 26), priHex);
            if (InterfaceMenu.Button(new Rect(xPos + w - applyW, yPos, applyW, 26), "应用"))
            {
                if (ScopeStickerRules.TryHex(priHex, out var nextA))
                {
                    primary = nextA.Hex;
                    SyncPrimaryFields(nextA, ref priObs, ref priHex, ref priR, ref priG, ref priB);
                    localNotice = "";
                }
                else localNotice = "HEX 请输入六位颜色值，例如 #00F0FF。";
            }
            yPos += 32;
        }

        if (mode == 2)
        {
            if (!ScopeStickerRules.TryHex(secondary, out var sRgb)) sRgb = new(255, 56, 184);
            InterfaceMenu.Label(new Rect(xPos, yPos, 54, 26), "颜色 B", size: 12);
            InterfaceMenu.Surface(new Rect(xPos + 56, yPos + 2, 24, 22), new Color32(sRgb.R, sRgb.G, sRgb.B, 255), 4);
            secHex = InterfaceMenu.Field(new Rect(xPos + 86, yPos, Math.Max(50, w - 86 - applyW - 6), 26), secHex);
            if (InterfaceMenu.Button(new Rect(xPos + w - applyW, yPos, applyW, 26), "应用"))
            {
                if (ScopeStickerRules.TryHex(secHex, out var nextB)) { secondary = nextB.Hex; secObs = secHex = nextB.Hex; localNotice = ""; }
                else localNotice = "HEX 请输入六位颜色值，例如 #FF38B8。";
            }
            yPos += 32;
        }

        if (localNotice.Length > 0)
        {
            InterfaceMenu.Paragraph(new Rect(xPos, yPos, w, 26), localNotice, null, 12);
            yPos += 28;
        }
    }

    private static float DrawChannelSlider(float xPos, ref float yPos, float w, string channel, byte currentVal)
    {
        InterfaceMenu.Label(new Rect(xPos, yPos, 58, 22), $"{channel}  {currentVal}", null, 12, translate: false);
        float res = InterfaceMenu.Slider(new Rect(xPos + 64, yPos + 3, Math.Max(30, w - 64), 18), currentVal, 0, 255);
        yPos += 23;
        return res;
    }

    private static void SyncHitColor(ScopeStickerRules.Rgb value)
    {
        colorObserved = hitHex = value.Hex;
        hitRed = value.R.ToString(CultureInfo.InvariantCulture); hitGreen = value.G.ToString(CultureInfo.InvariantCulture); hitBlue = value.B.ToString(CultureInfo.InvariantCulture);
        colorNotice = "";
    }

    private static void DrawHitColor(float w, ref float yPos)
    {
        string key = editorSession + "/" + editScopeKey + "/" + draft!.Image;
        if (key != colorKey || colorObserved != draft.HitColor)
        {
            colorKey = key;
            if (!ScopeStickerRules.TryHex(draft.HitColor, out var value)) value = new(255, 64, 64);
            SyncHitColor(value);
        }
        ScopeStickerRules.TryHex(draft.HitColor, out var selected);
        InterfaceMenu.Label(new Rect(0, yPos, w - 40, 24), "命中闪烁颜色", InterfaceMenu.Accent, 13);
        InterfaceMenu.Surface(new Rect(w - 32, yPos, 32, 24), new Color32(selected.R, selected.G, selected.B, 255), 4); yPos += 28;
        const float label = 32, apply = 56;
        float inputWidth = Math.Max(60, w - label - apply - 6), cell = inputWidth / 3;
        InterfaceMenu.Label(new Rect(0, yPos, label, 26), "RGB", size: 12);
        hitRed = InterfaceMenu.Field(new Rect(label, yPos, cell - 4, 26), hitRed);
        hitGreen = InterfaceMenu.Field(new Rect(label + cell, yPos, cell - 4, 26), hitGreen);
        hitBlue = InterfaceMenu.Field(new Rect(label + 2 * cell, yPos, cell, 26), hitBlue);
        if (InterfaceMenu.Button(new Rect(w - apply, yPos, apply, 26), "应用"))
        {
            if (ScopeStickerRules.TryRgb(hitRed, hitGreen, hitBlue, out var rgb)) { draft = draft with { HitColor = rgb.Hex }; SyncHitColor(rgb); }
            else colorNotice = "RGB 各通道须为 0–255 的整数。";
        }
        yPos += 32;
        InterfaceMenu.Label(new Rect(0, yPos, label, 26), "HEX", size: 12);
        hitHex = InterfaceMenu.Field(new Rect(label, yPos, inputWidth, 26), hitHex);
        if (InterfaceMenu.Button(new Rect(w - apply, yPos, apply, 26), "应用"))
        {
            if (ScopeStickerRules.TryHex(hitHex, out var rgb)) { draft = draft with { HitColor = rgb.Hex }; SyncHitColor(rgb); }
            else colorNotice = "HEX 请输入六位颜色值，例如 #C41528。";
        }
        yPos += 32;
        InterfaceMenu.Paragraph(new Rect(0, yPos, w, 32), colorNotice.Length > 0 ? colorNotice : "确认命中角色时闪烁，随后恢复当前贴纸模式。", null, 12); yPos += 36;
        draft = draft with { HitSeconds = EditorSlider(0, ref yPos, w, "闪烁时长", draft.HitSeconds * 1000, 50, 1000, "ms") / 1000 };
        if (InterfaceMenu.Button(new Rect(0, yPos, w, 28), "预览命中闪烁")) StartFlash();
        yPos += 34;
    }
}
