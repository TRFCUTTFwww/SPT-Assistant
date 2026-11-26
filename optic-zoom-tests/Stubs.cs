using System.Reflection;

internal class NativeObject
{
    static long next;
    public IntPtr Pointer { get; } = new(++next);
    internal T? TryCast<T>() where T : class => this as T;
}

namespace BepInEx.Configuration
{
    internal sealed class ConfigEntry<T>(T value)
    {
        T current = value;
        internal event EventHandler? SettingChanged;
        internal T Value { get => current; set { current = value; SettingChanged?.Invoke(this, EventArgs.Empty); } }
    }
}
namespace UnityEngine
{
    internal enum KeyCode { LeftAlt, RightAlt, LeftControl, RightControl, LeftShift, RightShift }
    internal static class Input
    {
        internal static readonly HashSet<KeyCode> Keys = new();
        internal static Vector2 mouseScrollDelta;
        internal static bool GetKey(KeyCode key) => Keys.Contains(key);
    }
    internal record struct Vector2(float x, float y);
    internal static class Time { internal static int frameCount; internal static float unscaledTime => frameCount / 60f; }
    internal static class Mathf
    {
        internal static bool Approximately(float a, float b) => Math.Abs(a - b) <= Math.Max(1e-7f, Math.Max(Math.Abs(a), Math.Abs(b)) * 1e-6f);
        internal static float InverseLerp(float a, float b, float value) => a == b ? 0 : Math.Clamp((value - a) / (b - a), 0, 1);
    }
    internal sealed class Camera : NativeObject
    {
        float value = 12;
        internal float Minimum = .00001f, Maximum = 179, LastRequested;
        internal float fieldOfView { get => value; set { LastRequested = value; this.value = Math.Clamp(value, Minimum, Maximum); } }
    }
}
namespace Comfort.Common { internal static class Singleton<T> { internal static T Instance = default!; } }
namespace EFT
{
    internal sealed class Health { internal bool IsAlive = true; }
    internal sealed class Animation { internal bool IsAiming = true; internal InventoryLogic.SightComponent CurrentAimingMod = new(); }
    internal sealed class Player
    {
        internal bool IsYourPlayer = true;
        internal Health HealthController = new();
        internal Animation ProceduralWeaponAnimation = new();
        internal FirearmController HandsController = new();
        internal sealed class FirearmController : NativeObject
        {
            internal bool _isAiming = true;
            internal float _aimingSens, UserSensitivity = 1.7f, SmoothFactor = .7f, FixedScopeSensitivity = .55f;
            internal void UpdateSensitivity()
            {
                if (_isAiming)
                    _aimingSens = UserSensitivity * (CameraControl.CameraManager.Instance.OpticCameraManager.Updater._scopeZoomHandler != null ? SmoothFactor : FixedScopeSensitivity);
                HarmonyLib.Harmony.After(typeof(FirearmController), nameof(UpdateSensitivity), this);
            }
        }
    }
    internal sealed class GameWorld { internal Player MainPlayer = new(); }
}
namespace EFT.InventoryLogic
{
    internal sealed class SightComponent : NativeObject
    {
        internal int SelectedScopeIndex, SelectedScopeMode;
        internal float ScopeZoomValue;
        internal void SetScopeMode(int index, int mode)
        {
            HarmonyLib.Harmony.Before(typeof(SightComponent), nameof(SetScopeMode), this);
            SelectedScopeIndex = index;
            SelectedScopeMode = mode;
        }
    }
}
namespace EFT.CameraControl
{
    internal sealed class ScopeCameraData : NativeObject { internal float FieldOfView; }
    internal sealed class ScopeSmoothCameraData : NativeObject { internal ScopeZoomHandler ScopeZoomHandler = null!; }
    internal sealed class OpticSight : NativeObject { internal NativeObject CameraData = new ScopeCameraData { FieldOfView = 12 }; }
    internal sealed class OpticCameraManager { internal UnityEngine.Camera Camera = new(); internal OpticSight CurrentOpticSight = new(); internal OpticComponentUpdater Updater = new(); }
    internal sealed class CameraManager { internal static CameraManager Instance = new(); internal OpticCameraManager OpticCameraManager = new(); }
    internal sealed class OpticComponentUpdater : NativeObject
    {
        internal OpticSight? _opticSight;
        internal ScopeZoomHandler? _scopeZoomHandler;
        internal void CopyComponentFromOptic(OpticSight? optic) => CameraManager.Instance.OpticCameraManager.Camera.fieldOfView = ((ScopeCameraData)optic!.CameraData).FieldOfView;
        internal void LateUpdate()
        {
            HarmonyLib.Harmony.Before(typeof(OpticComponentUpdater), nameof(LateUpdate), this);
            if (_scopeZoomHandler != null)
            {
                _scopeZoomHandler.UpdateFieldOfView();
                _scopeZoomHandler.ReticleUpdates++;
                _scopeZoomHandler.UpdateSensitivity();
                CameraManager.Instance.OpticCameraManager.Camera.fieldOfView = _scopeZoomHandler._currentFieldOfView;
            }
            HarmonyLib.Harmony.After(typeof(OpticComponentUpdater), nameof(LateUpdate), this);
        }
    }
}
internal sealed class ScopeZoomHandler : NativeObject
{
    internal EFT.InventoryLogic.SightComponent? _sightComponent;
    internal bool _isInited = true, _isFieldOfViewEquals, OriginalRan;
    internal float _currentFieldOfView = 12, _targetFieldOfView = 12, _zoomAmount = 12, _normalizedZoom, _speed, _scroll, _previousScroll;
    internal float MinFov = 24, MaxFov = 6;
    internal int ReticleUpdates, SensitivityUpdates, ZoomEvents;
    internal Action<EFT.InventoryLogic.SightComponent, float> OnSmoothScopeZoomValueChangedField;
    internal Action<float>? OnSmoothSensetivityChangeField;
    internal ScopeZoomHandler() => OnSmoothScopeZoomValueChangedField = (_, _) => ZoomEvents++;
    internal float GetZoomInput() => UnityEngine.Input.mouseScrollDelta.y;
    internal void ValidateCurrentSmoothScopeState() { }
    internal void SetSwitchRotation() { }
    internal void UpdateSensitivity()
    {
        SensitivityUpdates++;
        float factor = UnityEngine.Mathf.InverseLerp(MinFov, MaxFov, _currentFieldOfView);
        OnSmoothSensetivityChangeField?.Invoke(.7f + (.3f - .7f) * factor);
    }
    internal void UpdateFieldOfView()
    {
        OriginalRan = HarmonyLib.Harmony.Before(typeof(ScopeZoomHandler), nameof(UpdateFieldOfView), this);
        if (!OriginalRan) return;
        _currentFieldOfView = Math.Clamp(_currentFieldOfView - GetZoomInput(), MaxFov, MinFov);
        if (_sightComponent != null) _sightComponent.ScopeZoomValue = _currentFieldOfView;
    }
    internal void ForceChangeScopeState()
    {
        HarmonyLib.Harmony.Before(typeof(ScopeZoomHandler), nameof(ForceChangeScopeState), this);
        _currentFieldOfView = Math.Abs(_currentFieldOfView - MinFov) <= Math.Abs(_currentFieldOfView - MaxFov) ? MaxFov : MinFov;
        _targetFieldOfView = _zoomAmount = _currentFieldOfView;
        if (_sightComponent != null) _sightComponent.ScopeZoomValue = _currentFieldOfView;
    }
}
namespace HarmonyLib
{
    internal sealed class HarmonyMethod(Type type, string name)
    {
        internal MethodInfo Method { get; } = type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;
    }
    internal sealed class Harmony
    {
        static readonly Dictionary<(Type, string), (HarmonyMethod? Prefix, HarmonyMethod? Postfix)> hooks = new();
        internal void Patch(MethodInfo target, HarmonyMethod? prefix = null, HarmonyMethod? postfix = null)
        {
            if (target == null) throw new Exception("Missing native method");
            hooks[(target.DeclaringType!, target.Name)] = (prefix, postfix);
        }
        internal static bool Before(Type type, string name, object instance) => !hooks.TryGetValue((type, name), out var hook) || hook.Prefix?.Method.Invoke(null, new[] { instance }) is not false;
        internal static void After(Type type, string name, object instance)
        {
            if (hooks.TryGetValue((type, name), out var hook)) hook.Postfix?.Method.Invoke(null, new[] { instance });
        }
    }
    internal static class AccessTools
    {
        internal static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
    }
}
namespace LocalSpt5Esp
{
    internal static class FeatureConfig
    {
        internal static BepInEx.Configuration.ConfigEntry<T> Bind<T>(string section, string key, string name, T value) => new(value);
    }
    internal static class Hotkeys { internal static bool Ready = true; }
    internal static class InterfaceMenu { internal static bool InputBlocked; }
    internal static class DirectorStage { internal static bool FreeActive = false; }
    internal sealed class Log { internal List<string> Warnings = new(); internal void LogWarning(string value) => Warnings.Add(value); }
    internal sealed class Plugin
    {
        internal static Plugin Self = new();
        internal HarmonyLib.Harmony Patches = new();
        internal Log Log = new();
    }
}
