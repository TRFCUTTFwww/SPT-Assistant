using System.Globalization;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using UnityEngine;
using Shortcut = BepInEx.Unity.IL2CPP.Configuration.KeyboardShortcut;

namespace LocalSpt5Esp;

internal static partial class ScopeSticker
{
    private static ConfigEntry<bool> rangefinder = null!, rangeReadouts = null!, rangeIronSights = null!, autoZeroLive = null!;
    private static ConfigEntry<Shortcut> autoZeroKey = null!;
    private static ConfigEntry<float> rangeOffsetX = null!, rangeOffsetY = null!;
    private static bool rangeVisible, rangeOnTarget, rangeOptical, draggingRange;
    private static string rangeText = "— m";
    private static float nextRange, nextRangeError, lastDistance, nextAutoZero;
    private static IntPtr rangeCamera, rangeWorld;
    private static Vector2 rangeDragStart, rangeDragBase;
    private static GUIStyle? rangeStyle;

    private static bool RangeBlocked => Editing || InterfaceMenu.Open || RaidChat.Open || WorldEditor.ModalOpen ||
        DirectorStage.FreeActive || DirectorStage.HudHidden || Cursor.visible || !Application.isFocused;

    private static Camera? ActiveRangeCamera(Player local, out bool optical)
    {
        var optic = Target();
        if (optic != null) { optical = true; return optic; }
        optical = false;
        if (!rangeIronSights.Value || DirectorStage.HudHidden || local.IsYourPlayer != true || local.ProceduralWeaponAnimation?.IsAiming != true)
            return null;
        var main = OverlayProjection.MainCamera;
        return main != null && main.isActiveAndEnabled ? main : null;
    }

    private static void TickRange()
    {
        try
        {
            var world = Singleton<GameWorld>.Instance;
            var local = world?.MainPlayer;
            if (world == null || local?.HealthController?.IsAlive != true) { HideRange(); return; }
            bool manualZero = Hotkeys.Ready && !InterfaceMenu.InputBlocked && !RangeBlocked && ShortcutBindings.Take(autoZeroKey);
            var activeCam = ActiveRangeCamera(local, out bool optical);
            if (manualZero && activeCam != null)
            {
                var probeHit = CrosshairProbe.Read(world, local, activeCam, 5000);
                float dist = Vector3.Distance(local.Position, probeHit.Point);
                if (probeHit.State is CrosshairState.Target or CrosshairState.Blocked && float.IsFinite(dist) && dist > 0)
                    ApplyZeroing(local, dist, toast: true);
                else
                    FeatureToasts.Show("当前准星未测得有效距离。");
            }
            if (!rangefinder.Value || RangeBlocked || activeCam == null) { HideRange(); return; }
            if (rangeCamera != activeCam.Pointer || rangeWorld != world.Pointer || rangeOptical != optical)
            {
                rangeVisible = false; nextRange = 0;
                rangeCamera = activeCam.Pointer; rangeWorld = world.Pointer; rangeOptical = optical;
            }
            if (Time.unscaledTime < nextRange) return;
            nextRange = Time.unscaledTime + .1f;
            var hit = CrosshairProbe.Read(world, local, activeCam, 5000);
            float distance = Vector3.Distance(local.Position, hit.Point);
            bool valid = hit.State is CrosshairState.Target or CrosshairState.Blocked && float.IsFinite(distance);
            rangeOnTarget = hit.State == CrosshairState.Target;
            lastDistance = valid ? distance : 0;
            if (valid && autoZeroLive.Value && Time.unscaledTime >= nextAutoZero)
            {
                nextAutoZero = Time.unscaledTime + .35f;
                ApplyZeroing(local, distance, toast: false);
            }
            string text = valid ? distance.ToString("0.0", CultureInfo.InvariantCulture) + " m" : "— m";
            if (rangeReadouts.Value)
            {
                var mod = local.ProceduralWeaponAnimation?.CurrentAimingMod;
                float zoom = optical ? OpticZoom.CurrentMagnification(mod) : 1f;
                if (optical || zoom > 1.05f)
                    text += " · " + zoom.ToString("0.#", CultureInfo.InvariantCulture) + "×";
                int zero = 0;
                try { if (mod != null && mod.ScopesCount > 0) zero = mod.GetCurrentOpticCalibrationDistance(); } catch { }
                if (zero > 0) text += " · Z" + zero.ToString(CultureInfo.InvariantCulture);
            }
            rangeText = text;
            rangeVisible = true;
        }
        catch (Exception error) { RangeFailed(error); }
    }

    private static void ApplyZeroing(Player local, float distance, bool toast)
    {
        var pwa = local.ProceduralWeaponAnimation;
        var mod = pwa?.CurrentAimingMod;
        if (pwa == null || mod == null || mod.ScopesCount <= 0)
        {
            if (toast) FeatureToasts.Show("当前瞄具不支持归零调节。");
            return;
        }
        int scope = Math.Clamp(mod.SelectedScopeIndex, 0, mod.ScopesCount - 1);
        if (!mod.HasOpticCalibrationPoints(scope))
        {
            if (toast) FeatureToasts.Show("当前瞄具没有可用归零档位。");
            return;
        }
        var points = mod.GetScopeCalibrationDistances(scope);
        if (points == null || points.Length == 0) return;
        int best = 0;
        float bestDiff = float.MaxValue;
        for (int i = 0; i < points.Length; i++)
        {
            float diff = Math.Abs(points[i] - distance);
            if (diff < bestDiff) { bestDiff = diff; best = i; }
        }
        int currentIdx = mod.GetCurrentOpticCalibrationPointIndex(scope);
        if (currentIdx != best)
        {
            for (int step = 0; step < points.Length && mod.GetCurrentOpticCalibrationPointIndex(scope) != best; step++)
            {
                if (best > mod.GetCurrentOpticCalibrationPointIndex(scope)) pwa.OpticCalibrationSwitchUp();
                else pwa.OpticCalibrationSwitchDown();
            }
            if (mod.GetCurrentOpticCalibrationPointIndex(scope) != best)
            {
                mod.SetSelectedOpticCalibrationPoint(scope, best);
                pwa.CalibrateSight();
            }
        }
        if (toast)
            FeatureToasts.Show($"已匹配归零：{points[best]} m（测距 {distance:F1} m）");
    }

    internal static Rect RangePanelRect(Vector2 center, string sample)
    {
        float w = Math.Max(88, InterfaceMenu.TextWidth(sample, 14) + 22);
        return new Rect(
            Math.Clamp(center.x + rangeOffsetX.Value, 4, Math.Max(4, Screen.width - w - 4)),
            Math.Clamp(center.y + rangeOffsetY.Value, 4, Math.Max(4, Screen.height - 30)),
            w, 26);
    }

    internal static void DrawRangeEditorPreview(Rect lens, Rect editorPanel)
    {
        if (!rangefinder.Value) return;
        string sample = rangeReadouts.Value ? "125.0 m · 4× · Z100" : "125.0 m";
        var box = RangePanelRect(lens.center, sample);
        var e = Event.current;
        if (e != null)
        {
            if (e.type == EventType.MouseDown && e.button == 0 && !editorPanel.Contains(e.mousePosition) && box.Contains(e.mousePosition))
            {
                draggingRange = true; rangeDragStart = e.mousePosition;
                rangeDragBase = new Vector2(rangeOffsetX.Value, rangeOffsetY.Value);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && draggingRange)
            {
                var delta = e.mousePosition - rangeDragStart;
                rangeOffsetX.Value = (float)Math.Round(Math.Clamp(rangeDragBase.x + delta.x, -600, 600), 1);
                rangeOffsetY.Value = (float)Math.Round(Math.Clamp(rangeDragBase.y + delta.y, -400, 400), 1);
                e.Use();
            }
            else if (e.type == EventType.MouseUp) draggingRange = false;
        }
        EnsureRangeStyle();
        rangeStyle!.normal.textColor = new Color(.65f, 1, .82f);
        InterfaceMenu.Surface(new Rect(box.x - 1, box.y - 1, box.width + 2, box.height + 2), InterfaceMenu.Accent, 5);
        InterfaceMenu.Surface(box, new Color(.025f, .04f, .035f, .9f), 4);
        GUI.Label(box, sample, rangeStyle);
    }

    private static void EnsureRangeStyle()
    {
        rangeStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 14, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter, wordWrap = false, richText = false
        };
    }

    internal static void DrawRange()
    {
        try { DrawRangeCore(); }
        catch (Exception error) { RangeFailed(error); }
    }

    private static void DrawRangeCore()
    {
        if (!rangeVisible || !rangefinder.Value || RangeBlocked || Event.current.type != EventType.Repaint) return;
        var local = Singleton<GameWorld>.Instance?.MainPlayer;
        if (local == null || ActiveRangeCamera(local, out bool optical)?.Pointer != rangeCamera) return;
        var center = optical && LensRect(out var lens) ? lens.center : new Vector2(Screen.width * .5f, Screen.height * .5f);
        var panel = RangePanelRect(center, rangeText);
        EnsureRangeStyle();
        rangeStyle!.normal.textColor = rangeOnTarget ? new Color(1f, .56f, .34f) : new Color(.65f, 1f, .82f);
        InterfaceMenu.Surface(panel, new Color(.025f, .04f, .035f, .85f), 4);
        GUI.Label(panel, rangeText, rangeStyle);
    }

    private static void RangeFailed(Exception error)
    {
        rangeVisible = false; nextRange = Time.unscaledTime + 1;
        if (Time.unscaledTime < nextRangeError) return;
        nextRangeError = Time.unscaledTime + 20;
        Plugin.Self.Log.LogWarning("Scope rangefinder: " + error.Message);
    }

    private static void HideRange()
    {
        rangeVisible = false; rangeOnTarget = false; lastDistance = 0;
        nextRange = 0; rangeCamera = rangeWorld = IntPtr.Zero;
    }

    private static void ResetRange()
    {
        rangeVisible = rangeOnTarget = draggingRange = false; lastDistance = 0;
        rangeText = "— m"; nextRange = nextRangeError = nextAutoZero = 0;
        rangeCamera = rangeWorld = IntPtr.Zero; rangeStyle = null;
    }
}
