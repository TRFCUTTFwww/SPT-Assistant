using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.CameraControl;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
using Shortcut = BepInEx.Unity.IL2CPP.Configuration.KeyboardShortcut;

namespace LocalSpt5Esp;

internal static partial class ScopeSticker
{
    static partial void GpuSelfCheck();
    private const CameraEvent RenderEvent = CameraEvent.AfterEverything;
    private static ConfigEntry<bool> enabled = null!;
    private static ConfigEntry<Shortcut> editKey = null!, previousKey = null!, nextKey = null!;
    private static ConfigEntry<float> width = null!, x = null!, y = null!, opacity = null!, defaultColorSpeed = null!;
    private static ConfigEntry<int> defaultColorMode = null!;
    private static ConfigEntry<string> defaultColorPrimary = null!, defaultColorSecondary = null!;
    private static ConfigEntry<bool> defaultColorUseMask = null!;
    private static ScopeStickerStore? store;
    private static Camera? camera;
    private static Camera.CameraCallback? callback;
    private static CommandBuffer? commands;
    private static Texture2D? texture;
    private static byte[]? loadedBytes;
    private static Material? material;
    private static Mesh? mesh;
    private static Il2CppStructArray<Vector3>? vertices;
    private static Il2CppStructArray<Vector2>? backgroundUvs;
    private static Il2CppStructArray<Color>? vertexColors;
    private static string currentKey = "", currentName = "", loadedImage = "";
    private static IntPtr currentSight;
    private static ScopeStickerProfile current = new();
    private static int lastRenderFrame = -10;
    private static float nextAttempt;
    private static bool reported;
    private static ScopeStickerProfile Defaults => new ScopeStickerProfile
    {
        Image = store?.Images.Keys.FirstOrDefault() ?? "",
        Width = width.Value,
        X = x.Value,
        Y = y.Value,
        Opacity = opacity.Value,
        ColorMode = defaultColorMode.Value,
        ColorPrimary = defaultColorPrimary.Value,
        ColorSecondary = defaultColorSecondary.Value,
        ColorSpeed = defaultColorSpeed.Value,
        ColorUseMask = defaultColorUseMask.Value
    }.Normalized();
    private static ScopeStickerProfile Value => draft ?? current;

    internal static void Initialize()
    {
        const string section = "73. 倍镜贴纸";
        enabled = FeatureConfig.Bind(section, "Enabled", "启用倍镜贴纸", false,
            "每种倍镜分别保存图片、位置、大小和透明度。开镜后按编辑快捷键调整。");
        rangefinder = FeatureConfig.Bind(section, "Crosshair rangefinder", "准星测距悬浮窗", false);
        rangeReadouts = FeatureConfig.Bind(section, "Rangefinder zoom and zero", "测距窗显示倍率与归零", true);
        rangeIronSights = FeatureConfig.Bind(section, "Rangefinder on 1x sights", "机瞄与红点开镜测距", false);
        autoZeroLive = FeatureConfig.Bind(section, "Continuous auto-zero", "开镜测距自动匹配归零", false);
        autoZeroKey = FeatureConfig.Bind(section, "Auto-zero shortcut", "测距一键归零快捷键", new Shortcut(KeyCode.Z, KeyCode.LeftAlt));
        rangeOffsetX = FeatureConfig.Bind(section, "Rangefinder X offset", "测距窗水平偏移", 18f, "", new AcceptableValueRange<float>(-600, 600));
        rangeOffsetY = FeatureConfig.Bind(section, "Rangefinder Y offset", "测距窗垂直偏移", 14f, "", new AcceptableValueRange<float>(-400, 400));
        editKey = FeatureConfig.Bind(section, "Edit shortcut", "倍镜贴纸编辑快捷键", new Shortcut(KeyCode.F10, KeyCode.LeftControl));
        previousKey = FeatureConfig.Bind(section, "Previous image shortcut", "上一张贴纸快捷键", new Shortcut(KeyCode.UpArrow));
        nextKey = FeatureConfig.Bind(section, "Next image shortcut", "下一张贴纸快捷键", new Shortcut(KeyCode.DownArrow));
        width = FeatureConfig.Bind(section, "Width percent", "新倍镜默认贴纸宽度（%）", 32f, "", new AcceptableValueRange<float>(1, 150));
        x = FeatureConfig.Bind(section, "Horizontal position", "新倍镜默认水平位置（%）", 8f, "", new AcceptableValueRange<float>(-150, 150));
        y = FeatureConfig.Bind(section, "Vertical position", "新倍镜默认垂直位置（%）", 0f, "", new AcceptableValueRange<float>(-150, 150));
        opacity = FeatureConfig.Bind(section, "Opacity", "新倍镜默认透明度", .7f, "", new AcceptableValueRange<float>(0, 1));
        defaultColorMode = FeatureConfig.Bind(section, "Default color mode", "新倍镜默认颜色模式", 0, "", new AcceptableValueRange<int>(0, 4));
        defaultColorPrimary = FeatureConfig.Bind(section, "Default primary color", "新倍镜默认主颜色", "#00F0FF");
        defaultColorSecondary = FeatureConfig.Bind(section, "Default secondary color", "新倍镜默认副颜色", "#FF38B8");
        defaultColorSpeed = FeatureConfig.Bind(section, "Default color speed", "新倍镜默认变色速度", 1f, "", new AcceptableValueRange<float>(.01f, 50f));
        defaultColorUseMask = FeatureConfig.Bind(section, "Default mask tint", "新倍镜默认纯色剪影", false);
        try
        {
            var host = (LocalSpt5Esp.HotReload.IHostServices)Plugin.Self.Host;
            store = new ScopeStickerStore(Path.Combine(host.PluginDirectory, "scope-stickers"));
        }
        catch (Exception error) { notice = "无法读取倍镜贴纸方案：" + error.Message; Plugin.Self.Log.LogWarning(notice); }
        GpuSelfCheck();
    }

    private static Camera? Target()
    {
        if (DirectorStage.HudHidden) return null;
        var local = Singleton<GameWorld>.Instance?.MainPlayer;
        if (local?.IsYourPlayer != true || local.ProceduralWeaponAnimation?.IsAiming != true) return null;
        var optic = CameraManager.Instance?.OpticCameraManager;
        var target = optic?.CurrentOpticSight != null ? optic.Camera : null;
        return target != null && target.isActiveAndEnabled && !target.orthographic ? target : null;
    }

    private static bool Context()
    {
        var local = Singleton<GameWorld>.Instance?.MainPlayer;
        var animation = local?.ProceduralWeaponAnimation;
        var mod = animation?.CurrentAimingMod;
        var item = mod?.Item;
        var sight = CameraManager.Instance?.OpticCameraManager?.CurrentOpticSight;
        if (item == null || sight == null) return false;
        string key = ScopeStickerStore.Key(item.StringTemplateId, mod!.SelectedScopeIndex);
        if (key.Length == 0) return false;
        if (key != currentKey || sight.Pointer != currentSight)
        {
            if (Editing) EndEdit(false);
            ClearFlash();
            currentKey = key; currentSight = sight.Pointer;
            currentName = LootDisplay.ItemName(item, false);
            current = store?.Get(key, Defaults) ?? Defaults;
            reported = false;
        }
        return true;
    }

    internal static void Tick()
    {
        try
        {
            CompleteImport();
            TickRange();
            var target = Target();
            if (target == null || !Context())
            {
                if (Editing) EndEdit(false);
                currentKey = ""; currentSight = IntPtr.Zero; Detach(); return;
            }
            if ((!enabled.Value && !Editing) || !Value.Enabled || Value.Opacity <= 0) { Detach(); return; }
            if (Time.unscaledTime < nextAttempt) return;
            if (camera == null || camera.Pointer != target.Pointer) { Detach(); camera = target; }
            if (!Resources(Value.Image)) { Detach(); return; }
            if (callback == null)
            {
                callback = DelegateSupport.ConvertDelegate<Camera.CameraCallback>(new Action<Camera>(BeforeRender));
                Camera.onPreRender += callback;
            }
            if (lastRenderFrame < Time.frameCount - 1) Build(target);
        }
        catch (Exception error) { Failed(error); }
    }

    private static void BeforeRender(Camera target)
    {
        if (camera == null || target == null || target.Pointer != camera.Pointer) return;
        long stamp = RuntimePerformance.Start();
        try
        {
            if ((!enabled.Value && !Editing) || !Value.Enabled || Target()?.Pointer != target.Pointer ||
                CameraManager.Instance?.OpticCameraManager?.CurrentOpticSight?.Pointer != currentSight)
            { commands?.Clear(); return; }
            Build(target); lastRenderFrame = Time.frameCount;
        }
        catch (Exception error) { Failed(error); }
        finally { RuntimePerformance.RenderDone(stamp); }
    }

    private static Texture2D Decode(byte[] bytes, bool readable = false)
    {
        if (!ScopeStickerImage.TrySize(bytes, out _, out _, out _))
            throw new InvalidDataException("请选择不超过 4096 × 4096、16 MB 的 PNG 或 JPG 图片。");
        var loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            name = "SPT Assistant scope sticker", hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
        };
        try
        {
            if (!ImageConversion.LoadImage(loaded, bytes, !readable)) throw new InvalidDataException("图片解码失败。");
            return loaded;
        }
        catch { UnityEngine.Object.Destroy(loaded); throw; }
    }

    private static bool LoadImage(string id)
    {
        if (texture != null && loadedImage == id) return true;
        commands?.Clear();
        ClearMask();
        if (texture != null) UnityEngine.Object.Destroy(texture);
        texture = null; loadedImage = ""; loadedBytes = null;
        if (!ScopeStickerStore.ValidImageId(id)) return false;
        string path = store?.ImagePath(id) ?? throw new InvalidOperationException("图片库未就绪。");
        if (new FileInfo(path).Length > ScopeStickerStore.MaximumImageBytes) throw new InvalidDataException("图片过大。");
        byte[] bytes = File.ReadAllBytes(path);
        var loaded = Decode(bytes);
        if (material != null) material.mainTexture = loaded;
        texture = loaded; loadedImage = id; loadedBytes = bytes;
        return true;
    }

    private static bool Resources(string image)
    {
        if (!LoadImage(image)) return false;
        if (material == null)
        {
            var shader = Shader.Find("UI/Default");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("Scope sticker UI shader is unavailable");
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, mainTexture = texture };
            material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            material.SetInt("_StencilComp", (int)CompareFunction.Always);
            material.SetInt("_ColorMask", 14);
        }
        if (mesh == null)
        {
            vertices = new(4);
            backgroundUvs = new(4); vertexColors = new(4);
            mesh = new Mesh { name = "SPT Assistant optic sticker quad", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic(); mesh.vertices = vertices;
            mesh.uv = new Il2CppStructArray<Vector2>(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            mesh.colors = new Il2CppStructArray<Color>(new[] { Color.white, Color.white, Color.white, Color.white });
            mesh.triangles = new Il2CppStructArray<int>(new[] { 0, 1, 2, 0, 2, 3 });
        }
        if (commands == null)
        {
            commands = new CommandBuffer { name = "SPT Assistant optic sticker after effects" };
            camera!.AddCommandBuffer(RenderEvent, commands);
        }
        if (Value.AutoContrast || Value.HitFlash || (Value.ColorMode != 0 && Value.ColorUseMask)) WhiteMask();
        return true;
    }

    private static void Build(Camera target)
    {
        commands!.Clear();
        var value = Value;
        if (!ScopeStickerRules.TryLayout(texture!.width, texture.height, target.pixelWidth, target.pixelHeight,
                value.Width, value.X, value.Y, out var layout, value.Rotation)) return;
        for (int i = 0; i < 4; i++)
        {
            var point = ScopeStickerRules.Vertex(layout, i, value.Rotation, (float)target.pixelWidth / target.pixelHeight);
            vertices![i] = new Vector3(point.X, point.Y, point.Z);
            backgroundUvs![i] = new Vector2(point.X * target.pixelWidth, point.Y * target.pixelHeight);
        }
        mesh!.vertices = vertices!; mesh.RecalculateBounds();
        bool flashing = IsFlashing(value);
        Color tint;
        if (flashing && ScopeStickerRules.TryHex(value.HitColor, out var rgb))
            tint = new Color(rgb.R / 255f, rgb.G / 255f, rgb.B / 255f, value.Opacity);
        else if (value.ColorMode != 0)
        {
            var dyn = ScopeStickerRules.EvaluateDynamicColor(value, Time.unscaledTime);
            tint = new Color(dyn.R / 255f, dyn.G / 255f, dyn.B / 255f, value.Opacity * dyn.AlphaFactor);
        }
        else tint = new Color(1, 1, 1, value.Opacity);
        for (int i = 0; i < 4; i++) vertexColors![i] = tint;
        mesh.colors = vertexColors!;
        material!.color = Color.white;
        material.mainTexture = flashing || (value.ColorMode != 0 && value.ColorUseMask) ? WhiteMask() : texture;
        // Draw after TAA/postprocessing in optic UV space. Neither weapon sway,
        // camera movement nor jittered scene matrices participate in placement.
        var color = target.targetTexture != null ? new RenderTargetIdentifier(target.targetTexture) : new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);
        var draw = material;
        if (!flashing && value.AutoContrast && ContrastResources(target))
        {
            // Sample the original optic before drawing any sticker. Only GPU
            // blits run per frame; the mask and lookup table are built once.
            commands.Blit(color, new RenderTargetIdentifier(grayTarget!), grayscale!, 0);
            commands.Blit(new RenderTargetIdentifier(grayTarget!), new RenderTargetIdentifier(contrastTarget!), curves!, 0);
            mesh.uv2 = backgroundUvs!; draw = contrastDraw!;
        }
        commands.SetRenderTarget(color, new RenderTargetIdentifier(BuiltinRenderTextureType.None));
        commands.SetViewport(target.pixelRect);
        // These vertices already address the final lens texture directly.
        // A render-to-texture GPU projection flip would invert the artwork
        // vertically a second time relative to the lens's UV sampling.
        commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.Ortho(0, 1, 0, 1, -1, 1));
        commands.DrawMesh(mesh, Matrix4x4.identity, draw, 0, 0);
        commands.SetRenderTarget(color);
        commands.SetViewProjectionMatrices(target.worldToCameraMatrix,
            GL.GetGPUProjectionMatrix(target.projectionMatrix, target.targetTexture != null));
        if (!reported)
        {
            reported = true;
            Plugin.Self.Log.LogInfo($"Optic sticker ready: optic={currentKey}, image={loadedImage}, target={target.pixelWidth}x{target.pixelHeight}, upright UV overlay, rotation={value.Rotation:F1}, after effects");
        }
    }

    private static void Failed(Exception error)
    {
        Detach();
        ShutdownEffects();
        if (Time.unscaledTime < nextAttempt) return;
        nextAttempt = Time.unscaledTime + 20;
        notice = "倍镜贴纸：" + error.Message; Plugin.Self.Log.LogWarning(notice);
    }

    private static void Detach()
    {
        if (commands != null)
        {
            try { if (camera != null) camera.RemoveCommandBuffer(RenderEvent, commands); }
            finally { commands.Clear(); commands.Dispose(); commands = null; }
        }
        camera = null; lastRenderFrame = -10;
        ReleaseContrastTargets(); ClearFlash();
    }

    internal static void Shutdown()
    {
        ResetRange();
        if (Editing) EndEdit(false);
        editGate.Reset();
        if (callback != null) { Camera.onPreRender -= callback; callback = null; }
        Detach();
        ShutdownEffects();
        if (mesh != null) UnityEngine.Object.Destroy(mesh);
        if (material != null) UnityEngine.Object.Destroy(material);
        if (texture != null) UnityEngine.Object.Destroy(texture);
        mesh = null; material = null; texture = null; vertices = null; backgroundUvs = null; vertexColors = null;
        loadedBytes = null; store = null; importTask = null;
        currentKey = loadedImage = ""; currentSight = IntPtr.Zero;
        reported = false; nextAttempt = 0;
    }
}
