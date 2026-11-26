using System.Globalization;
using UnityEngine;

namespace LocalSpt5Esp;

internal static partial class ScopeSticker
{
    private static Vector2 controlsScroll;
    private static string colorKey = "", colorObserved = "", hitHex = "", hitRed = "", hitGreen = "", hitBlue = "", colorNotice = "";

    private static void DrawEditingControls(Rect rect)
    {
        if (draft == null) return;
        float height = 400 + (draft.AutoContrast ? 40 : 0) + (draft.HitFlash ? 190 : 0) + (notice.Length > 0 ? 44 : 0) + (choosingImage ? 184 : 0);
        controlsScroll = GUI.BeginScrollView(rect, controlsScroll, new Rect(0, 0, rect.width - 20, height));
        try
        {
            float xPos = 0, yPos = 0, w = rect.width - 24;
            if (InterfaceMenu.Button(new Rect(0, yPos, w - 90, 30), ImageName(draft.Image), choosingImage)) choosingImage = !choosingImage;
            bool oldEnabled = GUI.enabled; GUI.enabled = oldEnabled && importTask == null;
            if (InterfaceMenu.Button(new Rect(w - 82, yPos, 82, 30), "导入图片")) BeginImport();
            GUI.enabled = oldEnabled; yPos += 38;
            draft = draft with { Width = EditorSlider(xPos, ref yPos, w, "宽度", draft.Width, 1, 150),
                X = EditorSlider(xPos, ref yPos, w, "水平位置", draft.X, -150, 150),
                Y = EditorSlider(xPos, ref yPos, w, "垂直位置", draft.Y, -150, 150),
                Opacity = EditorSlider(xPos, ref yPos, w, "透明度", draft.Opacity * 100, 0, 100) / 100,
                Rotation = EditorSlider(xPos, ref yPos, w, "旋转角度", draft.Rotation, -180, 180, "°") };
            float turnWidth = (w - 12) / 3;
            if (InterfaceMenu.Button(new Rect(0, yPos, turnWidth, 28), "旋转 -90°")) draft = (draft with { Rotation = draft.Rotation - 90 }).Normalized();
            if (InterfaceMenu.Button(new Rect(turnWidth + 6, yPos, turnWidth, 28), "角度归零")) draft = draft with { Rotation = 0 };
            if (InterfaceMenu.Button(new Rect((turnWidth + 6) * 2, yPos, turnWidth, 28), "旋转 +90°")) draft = (draft with { Rotation = draft.Rotation + 90 }).Normalized();
            yPos += 36;
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
