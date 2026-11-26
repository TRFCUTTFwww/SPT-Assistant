using System;
using System.Globalization;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace LocalSpt5Esp;

internal static class SkyTint
{
    internal const float PanelHeight = 152;
    private const string DefaultHex = "#C41528";
    private static readonly string[] Presets =
        { DefaultHex, "#FF7443", "#FFB740", "#A6DE77", "#53ADFF", "#956AE8", "#FFFFFF" };

    private static ConfigEntry<bool> enabled = null!;
    private static ConfigEntry<string> colorSetting = null!;
    private static string? currentSetting;
    private static string hex = "", red = "", green = "", blue = "", notice = "";
    private static Color tint;
    private static Renderer? atmosphere;
    private static MaterialPropertyBlock? originalBlock, tintBlock;
    private static float nextError;

    internal static void Initialize(Plugin plugin)
    {
        enabled = FeatureConfig.Bind("31. 环境控制", "Sky tint", "天空染色", false);
        colorSetting = FeatureConfig.Bind("31. 环境控制", "Sky color", "天空颜色", DefaultHex);
        var update = AccessTools.DeclaredMethod(typeof(TOD_Sky), nameof(TOD_Sky.UpdateShaderProperties));
        plugin.Patches.Patch(update, postfix: new HarmonyMethod(typeof(SkyTint), nameof(AfterShaderUpdate)));
    }

    private static void AfterShaderUpdate(TOD_Sky __instance)
    {
        try
        {
            if (!enabled.Value)
            {
                Reset();
                return;
            }

            if (!RaidClock.Active || __instance.Components?.AtmosphereRenderer is not { } renderer ||
                !renderer || __instance.Resources is not { } resources)
                return;

            ReadSetting();
            if (!atmosphere || atmosphere!.Pointer != renderer.Pointer)
            {
                Reset();
                originalBlock ??= new MaterialPropertyBlock();
                tintBlock ??= new MaterialPropertyBlock();
                renderer.GetPropertyBlock(originalBlock);
                atmosphere = renderer;
            }

            // Keep the override on the sky dome. Native global colors still feed fog and lighting.
            renderer.GetPropertyBlock(tintBlock!);
            tintBlock!.SetColor(resources.ID_SunSkyColor, Recolor(__instance.SunSkyColor));
            tintBlock.SetColor(resources.ID_MoonSkyColor, Recolor(__instance.MoonSkyColor));
            renderer.SetPropertyBlock(tintBlock);
        }
        catch (Exception error)
        {
            if (Time.unscaledTime >= nextError)
            {
                nextError = Time.unscaledTime + 20;
                Plugin.Self.Error(error);
            }
        }
    }

    private static Color Recolor(Color original)
    {
        float brightness = original.maxColorComponent;
        return new Color(tint.r * brightness, tint.g * brightness, tint.b * brightness, original.a);
    }

    internal static void Reset()
    {
        var renderer = atmosphere;
        atmosphere = null;
        if (renderer)
            renderer!.SetPropertyBlock(originalBlock!.isEmpty ? null : originalBlock);
    }

    private static bool TryHex(string value, out Color color)
    {
        ReadOnlySpan<char> digits = value.AsSpan().Trim();
        if (!digits.IsEmpty && digits[0] == '#')
            digits = digits[1..];
        if (digits.Length == 6 && int.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int rgb))
        {
            color = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
            return true;
        }
        color = default;
        return false;
    }

    private static bool TryChannel(string value, out byte channel) =>
        byte.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out channel);

    private static void ReadSetting()
    {
        if (currentSetting == colorSetting.Value)
            return;
        currentSetting = colorSetting.Value;
        if (!TryHex(currentSetting, out tint))
            TryHex(DefaultHex, out tint);
        SyncFields();
    }

    private static void SyncFields()
    {
        Color32 bytes = tint;
        hex = "#" + ColorUtility.ToHtmlStringRGB(tint);
        red = bytes.r.ToString(CultureInfo.InvariantCulture);
        green = bytes.g.ToString(CultureInfo.InvariantCulture);
        blue = bytes.b.ToString(CultureInfo.InvariantCulture);
    }

    private static void Apply(Color color)
    {
        tint = color;
        SyncFields();
        currentSetting = hex;
        colorSetting.Value = hex;
        notice = "";
    }

    internal static void Draw(Rect rect)
    {
        ReadSetting();
        float x = rect.x, y = rect.y, w = Math.Min(rect.width, 440);
        if (InterfaceMenu.Button(new Rect(x, y, w - 42, 28), enabled.Value ? "天空染色 · 开启" : "天空染色 · 关闭", enabled.Value))
        {
            enabled.Value = !enabled.Value;
            if (!enabled.Value)
                Reset();
        }
        InterfaceMenu.Surface(new Rect(x + w - 34, y, 34, 28), tint, 5);

        float swatchWidth = w / Presets.Length;
        for (int i = 0; i < Presets.Length; i++)
        {
            var swatch = new Rect(x + i * swatchWidth, y + 34, swatchWidth - 4, 22);
            TryHex(Presets[i], out var preset);
            InterfaceMenu.Surface(swatch, preset, 4);
            if (GUI.Button(swatch, GUIContent.none, GUIStyle.none))
                Apply(preset);
        }

        const float labelWidth = 32, applyWidth = 56;
        float inputWidth = w - labelWidth - applyWidth - 6;
        InterfaceMenu.Label(new Rect(x, y + 62, labelWidth, 26), "RGB", size: 12);
        float cell = inputWidth / 3;
        red = InterfaceMenu.Field(new Rect(x + labelWidth, y + 62, cell - 4, 26), red);
        green = InterfaceMenu.Field(new Rect(x + labelWidth + cell, y + 62, cell - 4, 26), green);
        blue = InterfaceMenu.Field(new Rect(x + labelWidth + 2 * cell, y + 62, cell, 26), blue);
        if (InterfaceMenu.Button(new Rect(x + w - applyWidth, y + 62, applyWidth, 26), "应用"))
        {
            if (TryChannel(red, out byte r) && TryChannel(green, out byte g) && TryChannel(blue, out byte b))
                Apply(new Color32(r, g, b, 255));
            else
                notice = "RGB 各通道须为 0–255 的整数。";
        }

        InterfaceMenu.Label(new Rect(x, y + 94, labelWidth, 26), "HEX", size: 12);
        hex = InterfaceMenu.Field(new Rect(x + labelWidth, y + 94, inputWidth, 26), hex);
        if (InterfaceMenu.Button(new Rect(x + w - applyWidth, y + 94, applyWidth, 26), "应用"))
        {
            if (TryHex(hex, out var selected))
                Apply(selected);
            else
                notice = "HEX 请输入六位颜色值，例如 #C41528。";
        }
        InterfaceMenu.Label(new Rect(x, y + 122, w, 26), notice, size: 12);
    }
}
