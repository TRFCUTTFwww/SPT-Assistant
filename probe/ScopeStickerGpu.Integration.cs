// Opt-in offscreen Unity GPU integration check. Build the core with
// -p:ScopeStickerGpuChecks=true. The default core and offline Probe exclude it.
#if !PROBE
using UnityEngine;
using UnityEngine.Rendering;

namespace LocalSpt5Esp;

internal static partial class ScopeSticker
{
    static partial void GpuSelfCheck()
    {
        const int size = 32;
        var owner = new GameObject("SPT Assistant offscreen sticker check") { hideFlags = HideFlags.HideAndDontSave };
        var testCamera = owner.AddComponent<Camera>(); testCamera.enabled = false;
        testCamera.cullingMask = 0; testCamera.clearFlags = CameraClearFlags.Nothing;
        testCamera.useOcclusionCulling = false; testCamera.allowHDR = false; testCamera.allowMSAA = false;
        var scene = NewTarget(size, size, "SPT Assistant GPU check scene");
        var background = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        var artwork = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        var previousCurrent = current; var previousActive = RenderTexture.active;
        int checkedPixels = 0;
        var seedBackground = new CommandBuffer { name = "SPT Assistant GPU test background" };
        seedBackground.Blit(background, new RenderTargetIdentifier(scene));
        // Seed at the same native event immediately before the production buffer.
        // Camera.Render can overwrite even a Nothing-clear camera target.
        testCamera.AddCommandBuffer(RenderEvent, seedBackground);
        try
        {
            testCamera.targetTexture = scene;
            testCamera.pixelRect = new Rect(0, 0, size, size);
            var input = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Color32>(size * size);
            var maskPixels = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Color32>(size * size);
            // Only the upper-left quadrant is bright, exposing both UV flips.
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                input[y * size + x] = x < size / 2 && y >= size / 2 ? new Color32(240, 220, 180, 137) : new Color32(8, 16, 24, 137);
            background.SetPixels32(input); background.Apply(false, false);
            foreach (byte sourceColor in new byte[] { 0, 255, 82 })
            foreach (float opacityValue in new[] { 1f, .5f })
            {
                for (int i = 0; i < maskPixels.Length; i++) maskPixels[i] = new Color32(sourceColor, (byte)(255 - sourceColor), 57, 255);
                artwork.SetPixels32(maskPixels); artwork.Apply(false, false);
                commands?.Clear(); ClearMask();
                if (texture != null) UnityEngine.Object.Destroy(texture);
                loadedBytes = ImageConversion.EncodeToPNG(artwork); texture = Decode(loadedBytes); loadedImage = "gpu-test";
                current = new ScopeStickerProfile { Width = 100, X = 0, Y = 0, Opacity = opacityValue, AutoContrast = true };
                camera = testCamera; Resources("gpu-test");
                Graphics.Blit(background, scene);
                var before = ReadTarget(scene);
                Build(testCamera);
                testCamera.Render();
                var after = ReadTarget(scene);
                foreach (int y in new[] { 6, 25 }) foreach (int x in new[] { 6, 25 })
                {
                    var a = before[y * size + x]; var b = after[y * size + x];
                    float luminance = (.299f * a.r + .587f * a.g + .114f * a.b) / 255;
                    byte level = ScopeStickerRules.ContrastLevel(luminance, QualitySettings.activeColorSpace == ColorSpace.Linear);
                    CheckChannel(b.r, a.r * (1 - opacityValue) + level * opacityValue);
                    CheckChannel(b.g, a.g * (1 - opacityValue) + level * opacityValue);
                    CheckChannel(b.b, a.b * (1 - opacityValue) + level * opacityValue);
                    CheckChannel(b.a, a.a); checkedPixels++;
                }
            }
            // Transparent and partial alpha keep the same coverage even when
            // every RGB source channel changes. A constant dark scene isolates
            // mask transparency from the background sampling test above.
            for (int i = 0; i < input.Length; i++) input[i] = new Color32(8, 8, 8, 137);
            background.SetPixels32(input); background.Apply(false, false);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                maskPixels[y * size + x] = new Color32(0, 0, 255, (byte)(x < size / 2 ? 0 : 128));
            artwork.SetPixels32(maskPixels); artwork.Apply(false, false);
            commands!.Clear(); ClearMask(); UnityEngine.Object.Destroy(texture);
            loadedBytes = ImageConversion.EncodeToPNG(artwork); texture = Decode(loadedBytes);
            current = current with { Opacity = 1 };
            Graphics.Blit(background, scene); var alphaBefore = ReadTarget(scene);
            Build(testCamera); testCamera.Render(); var alphaAfter = ReadTarget(scene);
            foreach (int x in new[] { 6, 25 })
            {
                var a = alphaBefore[6 * size + x]; var b = alphaAfter[6 * size + x]; float alpha = x < size / 2 ? 0 : 128 / 255f;
                CheckChannel(b.r, a.r * (1 - alpha) + 255 * alpha); CheckChannel(b.a, a.a); checkedPixels++;
            }
            // Flash must replace source RGB, take priority over contrast, and
            // restore contrast after expiry/clear while preserving lens alpha.
            for (int i = 0; i < maskPixels.Length; i++) maskPixels[i] = new Color32(0, 255, 19, 255);
            artwork.SetPixels32(maskPixels); artwork.Apply(false, false);
            commands!.Clear(); ClearMask(); UnityEngine.Object.Destroy(texture);
            loadedBytes = ImageConversion.EncodeToPNG(artwork); texture = Decode(loadedBytes);
            current = current with { HitFlash = true, HitColor = "#1234E8" };
            foreach (float opacityValue in new[] { 1f, .5f })
            {
                current = current with { Opacity = opacityValue }; StartFlash();
                Graphics.Blit(background, scene); var flashBefore = ReadTarget(scene);
                Build(testCamera); testCamera.Render(); var flashed = ReadTarget(scene);
                var a = flashBefore[6 * size + 6]; var b = flashed[6 * size + 6];
                CheckChannel(b.r, a.r * (1 - opacityValue) + 18 * opacityValue);
                CheckChannel(b.g, a.g * (1 - opacityValue) + 52 * opacityValue);
                CheckChannel(b.b, a.b * (1 - opacityValue) + 232 * opacityValue);
                CheckChannel(b.a, a.a); checkedPixels++;
            }
            ClearFlash(); current = current with { Opacity = 1 };
            Build(testCamera); testCamera.Render(); var restored = ReadTarget(scene);
            CheckChannel(restored[6 * size + 6].r, 255); CheckChannel(restored[6 * size + 6].a, 137); checkedPixels++;
            Plugin.Self.Log.LogInfo($"Scope sticker GPU self-check PASS: {checkedPixels} samples, asymmetric bright/dark background, arbitrary source RGB, single opacity application, transparent and partial alpha, lens alpha preserved, chosen hit RGB and opacity, contrast restored after flash");
        }
        catch (Exception error)
        {
            Plugin.Self.Log.LogError("Scope sticker GPU self-check FAILED: " + error);
        }
        finally
        {
            RenderTexture.active = previousActive;
            Detach(); ShutdownEffects();
            foreach (var resource in new UnityEngine.Object?[] { mesh, material, texture, artwork, background })
                if (resource != null) UnityEngine.Object.Destroy(resource);
            testCamera.RemoveCommandBuffer(RenderEvent, seedBackground); seedBackground.Dispose();
            testCamera.targetTexture = null; scene.Release(); UnityEngine.Object.Destroy(scene); UnityEngine.Object.Destroy(owner);
            mesh = null; material = null; texture = null; vertices = null; backgroundUvs = null; vertexColors = null;
            loadedBytes = null; loadedImage = failedImage = ""; current = previousCurrent; reported = false;
        }
    }

    private static Color32[] ReadTarget(RenderTexture target)
    {
        var previous = RenderTexture.active;
        var read = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
        try
        {
            RenderTexture.active = target; read.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); read.Apply(false, false);
            return read.GetPixels32().ToArray();
        }
        finally { RenderTexture.active = previous; UnityEngine.Object.Destroy(read); }
    }

    private static void CheckChannel(byte actual, float expected)
    {
        if (Math.Abs(actual - expected) > 3) throw new InvalidOperationException($"GPU channel mismatch: actual={actual}, expected={expected:F2}");
    }
}
#endif
