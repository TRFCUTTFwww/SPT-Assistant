using UnityEngine;
using UnityEngine.Rendering;

namespace LocalSpt5Esp;

internal static partial class ScopeSticker
{
    private static Texture2D? whiteMask, contrastLut;
    private static Material? grayscale, curves, contrastDraw;
    private static RenderTexture? grayTarget, contrastTarget;
    private static bool contrastUnavailable, contrastReported;
    private static string flashKey = "", flashImage = "";
    private static float flashStarted = float.NegativeInfinity;

    internal static bool WantsHits => enabled?.Value == true && currentKey.Length > 0 && Value.Enabled && Value.HitFlash;

    internal static void ConfirmedHit()
    {
        try
        {
            if (!WantsHits || Target() == null || !Context()) return;
            StartFlash();
        }
        catch (Exception error) { Plugin.Self.Log.LogWarning("Scope sticker hit flash: " + error.Message); }
    }

    private static void StartFlash()
    {
        flashKey = currentKey; flashImage = Value.Image; flashStarted = Time.unscaledTime;
    }

    private static void ClearFlash() { flashKey = flashImage = ""; flashStarted = float.NegativeInfinity; }
    private static bool IsFlashing(ScopeStickerProfile value) => value.HitFlash && flashKey == currentKey && flashImage == value.Image &&
        ScopeStickerRules.FlashActive(Time.unscaledTime, flashStarted, value.HitSeconds);

    private static Texture2D WhiteMask()
    {
        if (whiteMask != null) return whiteMask;
        var mask = Decode(loadedBytes ?? throw new InvalidOperationException("Sticker image is not loaded"), true);
        try
        {
            var pixels = mask.GetPixels32();
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, pixels[i].a);
            mask.SetPixels32(pixels); mask.Apply(false, true);
            mask.name = "SPT Assistant sticker alpha mask";
            whiteMask = mask; return mask;
        }
        catch { UnityEngine.Object.Destroy(mask); throw; }
    }

    private static void ClearMask()
    {
        if (contrastDraw != null) contrastDraw.mainTexture = null;
        if (whiteMask != null) UnityEngine.Object.Destroy(whiteMask);
        whiteMask = null;
    }

    private static Material EffectMaterial(Shader shader) => new(shader) { hideFlags = HideFlags.HideAndDontSave };

    private static bool ContrastResources(Camera target)
    {
        if (contrastUnavailable) return false;
        if (contrastDraw == null)
        {
            var grayShader = Shader.Find("Hidden/Grayscale");
            var curveShader = Shader.Find("Hidden/ColorCorrectionCurves");
            var detailShader = Shader.Find("UI/Unlit/Detail");
            if (grayShader == null || !grayShader.isSupported || curveShader == null || !curveShader.isSupported || detailShader == null || !detailShader.isSupported)
            {
                contrastUnavailable = true; notice = "自动高对比度材质不可用，暂时显示原图。";
                Plugin.Self.Log.LogWarning(notice); return false;
            }
            grayscale = EffectMaterial(grayShader); grayscale.SetFloat("_EffectAmount", 1);
            curves = EffectMaterial(curveShader);
            contrastDraw = EffectMaterial(detailShader);
            // The detail shader multiplies its color twice; leave it white
            // and supply opacity through vertex colors exactly once.
            contrastDraw.color = Color.white;
            contrastDraw.SetFloat("_Strength", 1);
            contrastDraw.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            contrastDraw.SetInt("_StencilComp", (int)CompareFunction.Always);
            contrastDraw.SetInt("_ColorMask", 14); // Preserve the native lens alpha.
            contrastDraw.SetVector("_DetailTex_ST", new Vector4(1, 1, 0, 0));
            contrastDraw.SetVector("_MainTex_ST", new Vector4(1, 1, 0, 0));
            const int width = 256;
            contrastLut = new Texture2D(width, 4, TextureFormat.RGBA32, false, true)
            {
                name = "SPT Assistant inverse contrast curve", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            var colors = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Color32>(width * 4);
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            for (int y = 0; y < 4; y++) for (int x = 0; x < width; x++)
            {
                byte level = ScopeStickerRules.ContrastLevel(x / 255f, linear);
                colors[y * width + x] = new Color32(level, level, level, 255);
            }
            contrastLut.SetPixels32(colors); contrastLut.Apply(false, true);
            curves.SetTexture("_RgbTex", contrastLut); curves.SetTexture("_RgbDepthTex", contrastLut);
            curves.SetTexture("_ZCurve", Texture2D.blackTexture);
            curves.SetTexture("_CameraDepthTexture", Texture2D.whiteTexture);
        }
        contrastDraw.mainTexture = WhiteMask();
        if (contrastTarget == null || contrastTarget.width != target.pixelWidth || contrastTarget.height != target.pixelHeight)
        {
            ReleaseContrastTargets();
            grayTarget = NewTarget(target.pixelWidth, target.pixelHeight, "SPT Assistant optic luminance");
            contrastTarget = NewTarget(target.pixelWidth, target.pixelHeight, "SPT Assistant optic contrast");
            contrastDraw.SetTexture("_DetailTex", contrastTarget);
            contrastDraw.SetVector("_DetailTex_TexelSize", new Vector4(1f / target.pixelWidth, 1f / target.pixelHeight, target.pixelWidth, target.pixelHeight));
        }
        if (!contrastReported)
        {
            contrastReported = true;
            Plugin.Self.Log.LogInfo($"Scope sticker contrast ready: GPU grayscale + inverse LUT + alpha mask, target={target.pixelWidth}x{target.pixelHeight}, no frame readback");
        }
        return true;
    }

    private static RenderTexture NewTarget(int width, int height, string name)
    {
        var result = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
        {
            name = name, hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            useMipMap = false, autoGenerateMips = false
        };
        if (result.Create()) return result;
        UnityEngine.Object.Destroy(result); throw new InvalidOperationException("Cannot allocate scope contrast render target");
    }

    private static void ReleaseContrastTargets()
    {
        if (contrastDraw != null) contrastDraw.SetTexture("_DetailTex", null);
        if (grayTarget != null) { grayTarget.Release(); UnityEngine.Object.Destroy(grayTarget); }
        if (contrastTarget != null) { contrastTarget.Release(); UnityEngine.Object.Destroy(contrastTarget); }
        grayTarget = contrastTarget = null; contrastReported = false;
    }

    private static void ShutdownEffects()
    {
        ReleaseContrastTargets(); ClearMask(); ClearFlash();
        foreach (var value in new UnityEngine.Object?[] { grayscale, curves, contrastDraw, contrastLut })
            if (value != null) UnityEngine.Object.Destroy(value);
        grayscale = curves = contrastDraw = null; contrastLut = null; contrastUnavailable = false;
    }
}
