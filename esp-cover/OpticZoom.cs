using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.CameraControl;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace LocalSpt5Esp;

internal static class OpticZoom
{
    internal static ConfigEntry<bool> Enabled = null!;
    static OpticSight? optic;
    static SightComponent? sight;
    static ScopeZoomHandler? handler;
    static OpticComponentUpdater? updater;
    static ScopeCameraData? fixedData;
    static int scopeIndex, scopeMode, inputFrame = -1;
    static bool custom;
    static float fov, nativeFov, nextError;
    static float minimum = OpticZoomRules.MinimumFov, maximum = OpticZoomRules.MaximumFov;

    internal static void Initialize(Plugin plugin)
    {
        Enabled = FeatureConfig.Bind("74. 倍镜变倍", "Extended optical zoom", "Alt+滚轮扩展倍率", true);
        Enabled.SettingChanged += Changed;
        plugin.Patches.Patch(AccessTools.Method(typeof(ScopeZoomHandler), "UpdateFieldOfView"),
            prefix: new HarmonyMethod(typeof(OpticZoom), nameof(BeforeFieldOfView)));
        plugin.Patches.Patch(AccessTools.Method(typeof(OpticComponentUpdater), "LateUpdate"),
            prefix: new HarmonyMethod(typeof(OpticZoom), nameof(BeforeOpticUpdate)),
            postfix: new HarmonyMethod(typeof(OpticZoom), nameof(AfterOpticUpdate)));
        plugin.Patches.Patch(AccessTools.Method(typeof(ScopeZoomHandler), "ForceChangeScopeState"),
            prefix: new HarmonyMethod(typeof(OpticZoom), nameof(NativeSwitch)));
        plugin.Patches.Patch(AccessTools.Method(typeof(SightComponent), "SetScopeMode"),
            prefix: new HarmonyMethod(typeof(OpticZoom), nameof(NativeMode)));
        plugin.Patches.Patch(AccessTools.Method(typeof(Player.FirearmController), "UpdateSensitivity"),
            postfix: new HarmonyMethod(typeof(OpticZoom), nameof(AfterSensitivity)));
    }

    static void Changed(object? sender, EventArgs args) { if (!Enabled.Value) Reset(); }

    static bool Context()
    {
        var player = Singleton<GameWorld>.Instance?.MainPlayer;
        var animation = player?.ProceduralWeaponAnimation;
        var aiming = animation?.CurrentAimingMod;
        var manager = CameraManager.Instance?.OpticCameraManager;
        var lens = manager?.CurrentOpticSight;
        var target = manager?.Updater;
        if (!Enabled.Value || player?.IsYourPlayer != true || player.HealthController?.IsAlive != true ||
            animation?.IsAiming != true || aiming == null || lens == null || target == null ||
            manager?.Camera == null || target._opticSight?.Pointer != lens.Pointer || DirectorStage.FreeActive)
        {
            Reset();
            return false;
        }
        if (optic?.Pointer != lens.Pointer || sight?.Pointer != aiming.Pointer || updater?.Pointer != target.Pointer ||
            scopeIndex != aiming.SelectedScopeIndex || scopeMode != aiming.SelectedScopeMode || handler == null && fixedData == null)
        {
            Reset();
            optic = lens;
            sight = aiming;
            updater = target;
            scopeIndex = aiming.SelectedScopeIndex;
            scopeMode = aiming.SelectedScopeMode;
            var zoom = lens.CameraData?.TryCast<ScopeSmoothCameraData>()?.ScopeZoomHandler;
            if (zoom?._isInited == true && zoom._sightComponent?.Pointer == aiming.Pointer)
                handler = zoom;
            else
                fixedData = lens.CameraData?.TryCast<ScopeCameraData>();
            minimum = OpticZoomRules.MinimumFov;
            maximum = OpticZoomRules.MaximumFov;
        }
        return handler != null || fixedData != null;
    }

    static bool Capture()
    {
        float wheel = Input.mouseScrollDelta.y;
        if (!Hotkeys.Ready || InterfaceMenu.InputBlocked ||
            !(Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) ||
            Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
            Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ||
            !float.IsFinite(wheel) || Math.Abs(wheel) < .001f)
            return false;
        if (inputFrame != Time.frameCount)
        {
            inputFrame = Time.frameCount;
            if (!custom) nativeFov = handler?._currentFieldOfView ?? fixedData!.FieldOfView;
            if (!float.IsFinite(nativeFov) || nativeFov <= 0 || nativeFov >= 180) return false;
            fov = OpticZoomRules.Step(custom ? fov : nativeFov, wheel, minimum, maximum);
            custom = true;
            if (handler != null) ApplyNative(fov);
            else ApplyFixed(fov);
            RefreshSensitivity();
        }
        return true;
    }

    static void ApplyNative(float value)
    {
        var zoom = handler!;
        bool changed = zoom._currentFieldOfView != value || sight!.ScopeZoomValue != value;
        zoom._currentFieldOfView = zoom._targetFieldOfView = zoom._zoomAmount = value;
        zoom._speed = zoom._scroll = zoom._previousScroll = 0;
        zoom._isFieldOfViewEquals = true;
        zoom._normalizedZoom = Mathf.InverseLerp(zoom.MaxFov, zoom.MinFov, value);
        sight!.ScopeZoomValue = value;
        if (changed)
        {
            zoom.ValidateCurrentSmoothScopeState();
            zoom.SetSwitchRotation();
            zoom.OnSmoothScopeZoomValueChangedField?.Invoke(sight, value);
        }
    }

    static void ApplyFixed(float value)
    {
        var data = fixedData!;
        float original = data.FieldOfView;
        try
        {
            data.FieldOfView = value;
            updater!.CopyComponentFromOptic(optic);
        }
        finally { data.FieldOfView = original; }
    }

    internal static void Tick()
    {
        try { if (Context()) Capture(); }
        catch (Exception error) { Failed(error); }
    }

    static bool Local(ScopeZoomHandler value) => handler?.Pointer == value.Pointer && sight?.Pointer == value._sightComponent?.Pointer;

    static void AfterSensitivity(Player.FirearmController __instance)
    {
        if (!custom || !Enabled.Value || !__instance._isAiming) return;
        try
        {
            var player = Singleton<GameWorld>.Instance?.MainPlayer;
            if (player?.HandsController?.Pointer != __instance.Pointer || !Context() || !custom) return;
            float scale = handler != null
                ? OpticZoomRules.SensitivityScale(fov, handler.MinFov, handler.MaxFov)
                : OpticZoomRules.SensitivityScale(fov, nativeFov, nativeFov);
            float value = __instance._aimingSens * scale;
            if (float.IsFinite(value) && value >= 0) __instance._aimingSens = value;
        }
        catch (Exception error) { Failed(error); }
    }

    static void RefreshSensitivity()
    {
        if (handler != null) handler.UpdateSensitivity();
        else Singleton<GameWorld>.Instance?.MainPlayer?.HandsController?.TryCast<Player.FirearmController>()?.UpdateSensitivity();
    }

    static bool BeforeFieldOfView(ScopeZoomHandler __instance)
    {
        try
        {
            if (!Context() || !Local(__instance)) return true;
            bool wheel = Capture();
            if (!custom) return true;
            if (!wheel && Hotkeys.Ready && !InterfaceMenu.InputBlocked && __instance.GetZoomInput() != 0)
            {
                ClearZoom();
                return true;
            }
            ApplyNative(fov);
            return false;
        }
        catch (Exception error) { Failed(error); return true; }
    }

    static void BeforeOpticUpdate(OpticComponentUpdater __instance)
    {
        try { if (Context() && updater?.Pointer == __instance.Pointer) Capture(); }
        catch (Exception error) { Failed(error); }
    }

    static void AfterOpticUpdate(OpticComponentUpdater __instance)
    {
        if (!custom || updater?.Pointer != __instance.Pointer) return;
        try
        {
            var manager = CameraManager.Instance?.OpticCameraManager;
            if (manager?.CurrentOpticSight?.Pointer != optic?.Pointer || manager?.Camera == null) return;
            float accepted = manager.Camera.fieldOfView;
            if (!float.IsFinite(accepted) || accepted <= 0 || accepted >= 180) return;
            if (!Mathf.Approximately(accepted, fov))
            {
                if (accepted > fov) minimum = Math.Max(minimum, accepted);
                else maximum = Math.Min(maximum, accepted);
                fov = accepted;
                if (handler != null) ApplyNative(fov);
                RefreshSensitivity();
            }
        }
        catch (Exception error) { Failed(error); }
    }

    static void NativeSwitch(ScopeZoomHandler __instance) { if (Local(__instance)) ClearZoom(); }
    static void NativeMode(SightComponent __instance) { if (sight?.Pointer == __instance.Pointer) ClearZoom(); }

    static void ClearZoom()
    {
        if (!custom) return;
        custom = false;
        if (handler != null)
            ApplyNative(Math.Clamp(nativeFov, Math.Min(handler.MinFov, handler.MaxFov), Math.Max(handler.MinFov, handler.MaxFov)));
        else if (fixedData != null && CameraManager.Instance?.OpticCameraManager?.CurrentOpticSight?.Pointer == optic?.Pointer)
            ApplyFixed(nativeFov);
        RefreshSensitivity();
    }

    static void Reset()
    {
        ClearZoom();
        optic = null;
        sight = null;
        handler = null;
        updater = null;
        fixedData = null;
    }

    static void Failed(Exception error)
    {
        try { Reset(); } catch { custom = false; }
        if (Time.unscaledTime < nextError) return;
        nextError = Time.unscaledTime + 10;
        Plugin.Self.Log.LogWarning("Optic zoom: " + error.Message);
    }

    internal static float CurrentMagnification(SightComponent? mod)
    {
        float baseZoom = mod != null ? Math.Max(1f, mod.GetCurrentOpticZoom()) : 1f;
        if (!custom || !Enabled.Value || !float.IsFinite(fov) || fov <= 0 || fov >= 180) return baseZoom;
        float scale = handler != null
            ? OpticZoomRules.SensitivityScale(fov, handler.MinFov, handler.MaxFov)
            : OpticZoomRules.SensitivityScale(fov, nativeFov, nativeFov);
        return scale > .0001f && float.IsFinite(scale) ? Math.Clamp(baseZoom / scale, .1f, 999f) : baseZoom;
    }

    internal static void Shutdown()
    {
        if (Enabled != null) Enabled.SettingChanged -= Changed;
        Reset();
    }
}
