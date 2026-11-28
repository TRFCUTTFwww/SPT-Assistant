using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using Mono.Cecil;
using LocalSpt5Esp;

internal static class ScopeStickerChecks
{
    internal static void Run(string sourceRoot)
    {
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        byte[] artwork = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLttAAAAABJRU5ErkJggg==");
        int imageWidth = BinaryPrimitives.ReadInt32BigEndian(artwork.AsSpan(16, 4));
        int imageHeight = BinaryPrimitives.ReadInt32BigEndian(artwork.AsSpan(20, 4));
        Check(imageWidth == 1 && imageHeight == 1 && artwork[25] == 6,
            "the import fixture is an RGBA PNG");
        foreach (var target in new[] { (1024, 1024), (1920, 1080), (768, 1024) })
        {
            Check(ScopeStickerRules.TryLayout(imageWidth, imageHeight, target.Item1, target.Item2, 32, 8, 0, out var layout),
                "default sticker intersects the lens texture");
            float pixelAspect = layout.Width * target.Item1 / (layout.Height * target.Item2);
            Check(Math.Abs(pixelAspect - (float)imageWidth / imageHeight) < .00001f && layout.X + layout.Width < .5f,
                "portrait proportions survive non-square optic textures and leave the center clear");
        }
        Check(!ScopeStickerRules.TryLayout(0, imageHeight, 1024, 1024, 32, 8, 0, out _) &&
            !ScopeStickerRules.TryLayout(imageWidth, imageHeight, 1024, 1024, float.NaN, 8, 0, out _) &&
            !ScopeStickerRules.TryLayout(imageWidth, imageHeight, 1024, 1024, 32, -100, -100, out _),
            "invalid dimensions, non-finite settings and fully offscreen art are skipped");

        var fixedLayout = new ScopeStickerRules.Layout(.08f, 0, .32f, .407f);
        Check(ScopeStickerRules.Vertex(fixedLayout, 0) == new Vector3(.08f, 0, 0) &&
            Vector3.Distance(ScopeStickerRules.Vertex(fixedLayout, 2), new Vector3(.4f, .407f, 0)) < .00001f,
            "quad uses fixed optic UV coordinates without scene projection");
        var original = new ScopeStickerProfile { Width = 30, X = 10, Y = 20 };
        var scaled = ScopeStickerRules.Scale(original, 2, 25, 35);
        Check(scaled.Width == 60 && scaled.X == -5 && scaled.Y == 5 && original.Width == 30,
            "pointer zoom preserves anchor and leaves original draft untouched");
        Check(ScopeStickerRules.Scale(original, float.NaN, 0, 0) == original &&
            ScopeStickerRules.Scale(original, 100, 25, 35).Width == 150, "wheel input and maximum scale are bounded");
        Console.WriteLine("PASS scope sticker portrait aspect, fixed UV vertices, anchored scaling and layout bounds");
        foreach (var target in new[] { (1024, 1024), (1920, 1080), (768, 1024) })
        foreach (float angle in new[] { -180f, -90, -37, 0, 45, 90, 180 })
        {
            Check(ScopeStickerRules.TryLayout(imageWidth, imageHeight, target.Item1, target.Item2, 32, 8, 0, out var layout, angle), "rotated artwork remains visible");
            float aspect = (float)target.Item1 / target.Item2;
            var a = ScopeStickerRules.Vertex(layout, 0, angle, aspect); var b = ScopeStickerRules.Vertex(layout, 1, angle, aspect);
            var c = ScopeStickerRules.Vertex(layout, 2, angle, aspect); var d = ScopeStickerRules.Vertex(layout, 3, angle, aspect);
            Vector2 Pixels(Vector3 delta) => new(delta.X * target.Item1, delta.Y * target.Item2);
            var horizontal = Pixels(b - a); var vertical = Pixels(d - a);
            Check(Math.Abs(horizontal.Length() - layout.Width * target.Item1) < .001f &&
                Math.Abs(vertical.Length() - layout.Height * target.Item2) < .001f &&
                Math.Abs(Vector2.Dot(Vector2.Normalize(horizontal), Vector2.Normalize(vertical))) < .00001f,
                "rotation preserves lengths and right angles in target pixels on non-square textures");
            Check(Vector3.Distance((a + b + c + d) / 4, new Vector3(layout.X + layout.Width / 2, layout.Y + layout.Height / 2, 0)) < .00001f,
                "rotation preserves artwork center");
        }
        var quarter = ScopeStickerRules.Vertex(new ScopeStickerRules.Layout(0, 0, .2f, .4f), 2, 90);
        Check(Math.Abs(quarter.X - .3f) < .00001f && Math.Abs(quarter.Y - .1f) < .00001f, "positive rotation is clockwise");
        Check(!ScopeStickerRules.TryLayout(100, 400, 1024, 1024, 20, -45, 10, out _) &&
            ScopeStickerRules.TryLayout(100, 400, 1024, 1024, 20, -45, 10, out _, 90), "visibility uses rotated bounds");
        Check(new ScopeStickerProfile { Rotation = 540 }.Normalized().Rotation == -180 &&
            new ScopeStickerProfile { Rotation = -450 }.Normalized().Rotation == -90 &&
            new ScopeStickerProfile { Rotation = float.NaN }.Normalized().Rotation == 0, "saved rotation wraps and rejects non-finite input");
        var turned = original with { Rotation = 37 };
        var turnedScale = ScopeStickerRules.Scale(turned, 1.5f, 25, 35);
        Check(turnedScale.Rotation == 37 && Math.Abs(turnedScale.X - 2.5f) < .00001f && Math.Abs(turnedScale.Y - 12.5f) < .00001f,
            "anchored scaling retains rotation");
        Console.WriteLine("PASS scope sticker pixel-correct rotation, clockwise direction, rotated clipping and normalized angles");
        EffectRules(Check);
        StoreChecks(artwork, Check);

        using var assembly = AssemblyDefinition.ReadAssembly(Path.Combine(sourceRoot, "esp-cover/bin/Release/net10.0/Local.Spt5.ESP.Core.dll"));
        Check(!assembly.MainModule.Resources.Any(r => r.Name == "LocalSpt5Esp.Assets.ScopeSticker.png") &&
            !File.Exists(Path.Combine(sourceRoot, "esp-cover/Assets/ScopeSticker.png")),
            "the core and source contain no built-in scope sticker artwork");
        Check(MenuSections.Group("73. 倍镜贴纸") == "镜头与画面" && MenuSections.Pages("镜头与画面").Contains("瞄准镜贴纸") &&
            MenuSections.OnPage("镜头与画面", "瞄准镜贴纸", "73. 倍镜贴纸", "Enabled") &&
            !MenuSections.OnPage("镜头与画面", "人物显示", "73. 倍镜贴纸", "Enabled"),
            "sticker settings share the optics page");
        Console.WriteLine("PASS imported-only scope stickers and shared optics settings page");
        var sticker = assembly.MainModule.Types.Single(t => t.Name == "ScopeSticker");
        using var unity = AssemblyDefinition.ReadAssembly(Path.Combine(Environment.GetEnvironmentVariable("SPT_GAME_ROOT")!, "BepInEx/interop/UnityEngine.CoreModule.dll"));
        var cameraEvent = unity.MainModule.Types.Single(t => t.FullName == "UnityEngine.Rendering.CameraEvent");
        Check(Convert.ToInt32(sticker.Fields.Single(f => f.Name == "RenderEvent").Constant) ==
            Convert.ToInt32(cameraEvent.Fields.Single(f => f.Name == "AfterEverything").Constant), "render after temporal effects");
        MethodReference[] Calls(TypeDefinition type, string method) => type.Methods.Single(m => m.Name == method).Body.Instructions
            .Select(i => i.Operand).OfType<MethodReference>().ToArray();
        var renderCalls = Calls(sticker, "Build");
        Check(renderCalls.Count(m => m.Name == "GetGPUProjectionMatrix") == 1,
            "overlay does not apply the render-texture Y flip a second time");
        var instructions = sticker.Methods.Single(m => m.Name == "Build").Body.Instructions;
        int drawIndex = instructions.ToList().FindIndex(i => i.Operand is MethodReference m && m.Name == "DrawMesh");
        int conversionIndex = instructions.ToList().FindIndex(i => i.Operand is MethodReference m && m.Name == "GetGPUProjectionMatrix");
        Check(conversionIndex > drawIndex, "GPU projection conversion is restricted to restoring scene matrices after drawing");
        Check(renderCalls.Any(m => m.Name == "Ortho") && renderCalls.Count(m => m.Name == "SetViewProjectionMatrices") == 2 &&
            !renderCalls.Any(m => m.Name is "ViewportToWorldPoint" or "get_cameraToWorldMatrix" or "get_fieldOfView"),
            "orthographic UV placement restores native matrices without world-space placement");
        var menu = assembly.MainModule.Types.Single(t => t.Name == "InterfaceMenu");
        Check(Calls(menu, "AllowGameInput").First().DeclaringType.Name == "ScopeSticker" &&
            Calls(menu, "get_InputBlocked").Any(m => m.DeclaringType.Name == "ScopeSticker" && m.Name == "get_InputBlocked"),
            "optic shortcut is captured before native input and gates trigger and axes");
        Check(Calls(assembly.MainModule.Types.Single(t => t.Name == "ReloadInputRecovery"), "BeforeReload").First().Name == "CancelEdit",
            "optic editor releases its cursor before host reload captures input state");
        Console.WriteLine("PASS scope sticker after-effects drawing and editor input-gate integration");
        Check(!sticker.Methods.Any(m => m.Name is "GpuSelfCheck" or "ReadTarget"), "release excludes opt-in GPU readback checks");
        Check(!sticker.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Select(i => i.Operand).OfType<MethodReference>()
            .Any(m => m.Name is "ReadPixels" or "AsyncGPUReadback"), "release never reads the scene back to the CPU");
        Check(renderCalls.Count(m => m.Name == "Blit") == 2 && instructions.Take(drawIndex).Count(i => i.Operand is MethodReference m && m.Name == "Blit") == 2,
            "contrast captures the clean background before drawing the sticker");
        Check(Calls(sticker, "Detach").Any(m => m.Name == "ReleaseContrastTargets") && Calls(sticker, "Shutdown").Any(m => m.Name == "ShutdownEffects") &&
            Calls(sticker, "LoadImage").Any(m => m.Name == "ClearMask"), "optic detach, shutdown and image switches release effect resources");
        var hit = assembly.MainModule.Types.Single(t => t.Name == "HitDisplay");
        foreach (string before in new[] { "BeforeShot", "BeforeDamage" })
            Check(Calls(hit, before).Any(m => m.DeclaringType.Name == "ScopeSticker" && m.Name == "get_WantsHits"), "sticker hit collection is independent of display and sound toggles");
        foreach (string after in new[] { "AfterShot", "AfterDamage" })
            Check(Calls(hit, after).Any(m => m.DeclaringType.Name == "ScopeSticker" && m.Name == "ConfirmedHit"), "confirmed native hits notify sticker effects");
        var smuggler = assembly.MainModule.Types.Single(t => t.Name == "Smuggler");
        Check(smuggler.Methods.Single(m => m.Name == "Initialize").Body.Instructions.Any(i => Equals(i.Operand, "GetItemsOverDiscardLimit")),
            "Smuggler hooks the destruction list separately from the discard UI check");
        var keep = Calls(smuggler, "KeepDiscardedItems");
        Check(keep.Any(m => m.Name == "LocalController") && keep.Any(m => m.Name == ".ctor" && m.DeclaringType.Name == "List`1") &&
            !keep.Any(m => m.Name is "GetItemsOverDiscardLimit" or "set_SpawnedInSession" or "get_SpawnedInSession"),
            "local discard skips native destructive enumeration for root items and nested children without changing FIR");
        var local = Calls(smuggler, "LocalController");
        Check(local.Any(m => m.Name == "get_Enabled") && local.Any(m => m.Name == "InRaid") && local.Any(m => m.Name == "get_MainPlayer") &&
            local.Count(m => m.Name == "get_Pointer") == 2, "disabled, menu, Fika and foreign-controller boundaries are retained");
        Console.WriteLine("PASS scope sticker GPU-only lifecycle, independent confirmed hits and Smuggler native destruction-list regression");
    }

    private static void EffectRules(Action<bool, string> Check)
    {
        Check(ScopeStickerRules.TryHex("  #aBcD01  ", out var hex) && hex == new ScopeStickerRules.Rgb(171, 205, 1) && hex.Hex == "#ABCD01" &&
            ScopeStickerRules.TryHex("000000", out _) && ScopeStickerRules.TryHex("FFFFFF", out _), "six-digit HEX accepts optional prefix and canonicalizes case");
        foreach (string? text in new string?[] { null, "", "#123", "#12345678", "0x123456", "#GG0000", "12 456" })
            Check(!ScopeStickerRules.TryHex(text, out _), "invalid HEX cannot replace selected color");
        Check(ScopeStickerRules.TryRgb("0", " 128 ", "255", out var rgb) && rgb.Hex == "#0080FF", "RGB bounds and channels are exact");
        foreach (string text in new[] { "-1", "256", "1.5", "1e2", "", "red" })
            Check(!ScopeStickerRules.TryRgb(text, "0", "0", out _) && !ScopeStickerRules.TryRgb("0", text, "0", out _) && !ScopeStickerRules.TryRgb("0", "0", text, out _), "invalid RGB channels rejected");
        Check(ScopeStickerRules.ContrastLevel(0, false) == 255 && ScopeStickerRules.ContrastLevel(.5f, false) == 255 && ScopeStickerRules.ContrastLevel(.501f, false) == 0 &&
            ScopeStickerRules.ContrastLevel(.18f, true) == 255 && ScopeStickerRules.ContrastLevel(.181f, true) == 0 && ScopeStickerRules.ContrastLevel(1, true) == 0, "black/white thresholds match gamma and linear scenes");
        Check(ScopeStickerRules.FlashActive(10, 10, .25f) && ScopeStickerRules.FlashActive(10.125f, 10, .25f) &&
            !ScopeStickerRules.FlashActive(10.25f, 10, .25f) && !ScopeStickerRules.FlashActive(9, 10, .25f) &&
            !ScopeStickerRules.FlashActive(10, float.NegativeInfinity, .25f) && !ScopeStickerRules.FlashActive(float.NaN, 10, .25f) &&
            !ScopeStickerRules.FlashActive(10, 10, 0) && !ScopeStickerRules.FlashActive(10, 10, float.NaN), "flash interval includes start, excludes end and rejects invalid clocks");
        var defaults = new ScopeStickerProfile();
        Check(!defaults.AutoContrast && !defaults.HitFlash && defaults.HitColor == "#FF4040" && defaults.HitSeconds == .18f &&
            defaults.ColorMode == 0 && defaults.ColorSpeed == 1f && defaults.ColorPrimary == "#00F0FF" && defaults.ColorSecondary == "#FF38B8" && !defaults.ColorUseMask &&
            new ScopeStickerProfile { HitColor = "abcd12", HitSeconds = .001f }.Normalized() is { HitColor: "#ABCD12", HitSeconds: .05f } &&
            new ScopeStickerProfile { HitColor = "invalid", HitSeconds = float.NaN }.Normalized() is { HitColor: "#FF4040", HitSeconds: .18f } &&
            new ScopeStickerProfile { HitSeconds = 5 }.Normalized().HitSeconds == 1, "effects default off and normalize colors and duration safely");

        // Dynamic color animation & speed checks
        var rainbowSlow = new ScopeStickerProfile { ColorMode = 1, ColorSpeed = 0.5f };
        var rainbowFast = new ScopeStickerProfile { ColorMode = 1, ColorSpeed = 2.0f };
        var c0 = ScopeStickerRules.EvaluateDynamicColor(rainbowSlow, 0f);
        var cHalfSlow = ScopeStickerRules.EvaluateDynamicColor(rainbowSlow, 0.5f);
        var cQuarterFast = ScopeStickerRules.EvaluateDynamicColor(rainbowFast, 0.125f);
        Check((c0.R, c0.G, c0.B) != (cHalfSlow.R, cHalfSlow.G, cHalfSlow.B) && cHalfSlow == cQuarterFast,
            "rainbow HSV animation scales phase proportionally to ColorSpeed");
        var gradient = new ScopeStickerProfile { ColorMode = 2, ColorSpeed = 1f, ColorPrimary = "#FF0000", ColorSecondary = "#0000FF" };
        var gStart = ScopeStickerRules.EvaluateDynamicColor(gradient, 0f);
        var gPeak = ScopeStickerRules.EvaluateDynamicColor(gradient, 0.5f);
        Check(gStart.R == 255 && gStart.B == 0 && gPeak.R == 0 && gPeak.B == 255,
            "two-color gradient smoothly oscillates between primary and secondary colors");
        var breath = new ScopeStickerProfile { ColorMode = 3, ColorSpeed = 2f, ColorPrimary = "#00FF80" };
        var bLow = ScopeStickerRules.EvaluateDynamicColor(breath, 0f);
        var bHigh = ScopeStickerRules.EvaluateDynamicColor(breath, 0.25f);
        Check(bLow.AlphaFactor < 0.35f && bHigh.AlphaFactor > 0.99f && bHigh.G == 255,
            "breathing pulse modulates alpha factor at configured speed");
        var solid = new ScopeStickerProfile { ColorMode = 4, ColorPrimary = "#FF3B30" };
        var s0 = ScopeStickerRules.EvaluateDynamicColor(solid, 0f);
        var sLater = ScopeStickerRules.EvaluateDynamicColor(solid, 1.37f);
        Check(s0 == new ScopeStickerRules.AnimatedTint(255, 59, 48, 1f) && s0 == sLater &&
            (solid.Normalized() is { ColorMode: 4, ColorPrimary: "#FF3B30" }),
            "static custom color mode holds exact RGB and unit alpha across time");
        Check(new ScopeStickerProfile { ColorMode = 9, ColorSpeed = 999f, ColorPrimary = "bad", ColorSecondary = "112233" }.Normalized()
            is { ColorMode: 0, ColorSpeed: 50f, ColorPrimary: "#00F0FF", ColorSecondary: "#112233" } &&
            new ScopeStickerProfile { ColorSpeed = .001f }.Normalized().ColorSpeed == .01f &&
            new ScopeStickerProfile { ColorSpeed = 0f }.Normalized().ColorSpeed == 1f,
            "dynamic color mode, speed bounds and hex fallbacks normalize cleanly");

        Console.WriteLine("PASS scope sticker RGB/HEX parsing, dynamic color animation & speed, contrast thresholds, flash boundaries and safe profile defaults");
    }

    private static void StoreChecks(byte[] artwork, Action<bool, string> Check)
    {
        Check(ScopeStickerImage.TrySize(artwork, out int iw, out int ih, out string ext) && iw == 1 && ih == 1 && ext == ".png",
            "PNG header dimensions are recognized");
        byte[] oversized = (byte[])artwork.Clone(); BinaryPrimitives.WriteInt32BigEndian(oversized.AsSpan(16, 4), 4097);
        Check(!ScopeStickerImage.TrySize(oversized, out _, out _, out _) &&
            !ScopeStickerImage.TrySize(artwork.AsSpan(0, 24), out _, out _, out _) &&
            !ScopeStickerImage.TrySize(new byte[ScopeStickerStore.MaximumImageBytes + 1], out _, out _, out _),
            "oversized and truncated PNG headers are rejected before texture allocation");
        byte[] jpeg = { 255,216,255,224,0,4,0,0,255,192,0,17,8,0,240,1,64,3,1,17,0,2,17,1,3,17,1,255,217 };
        Check(ScopeStickerImage.TrySize(jpeg, out int jw, out int jh, out string je) && jw == 320 && jh == 240 && je == ".jpg",
            "JPEG reader skips metadata before SOF dimensions");
        jpeg[10] = 127;
        Check(!ScopeStickerImage.TrySize(jpeg, out _, out _, out _) && !ScopeStickerStore.ValidImageId("../outside.png") &&
            !ScopeStickerStore.ValidImageId("builtin.png") && !ScopeStickerStore.ValidImageId(null), "malformed JPEG and path traversal rejected");
        Console.WriteLine("PASS scope sticker PNG/JPEG size limits, truncated headers and image paths");
        string directory = Path.Combine(Path.GetTempPath(), "scope-sticker-probe-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ScopeStickerStore(directory); var defaults = new ScopeStickerProfile();
            Check(defaults.Image == "" && store.Images.Count == 0 && store.CycleImage("", 1) == "" &&
                store.CycleImage("builtin", -1) == "" && !ScopeStickerStore.ValidImageId("builtin"),
                "empty libraries have no default artwork and safely ignore cycling");
            string a = ScopeStickerStore.Key("optic-A", 0), b = ScopeStickerStore.Key("optic-B", 0), sub = ScopeStickerStore.Key("optic-A", 1);
            Check(a != b && a != sub && ScopeStickerStore.Key("", 0) == "" && ScopeStickerStore.Key("optic-A", -1) == "", "independent optic identities");
            var untouched = store.Get(b, defaults);
            string imageA = store.Import(artwork, "picture A.png");
            // A PNG may contain trailing data after IEND. Preserve all original bytes.
            string imageB = store.Import(artwork.Concat(new byte[] { 0 }).ToArray(), "picture B.png");
            Check(imageA != imageB && store.Import(artwork, "picture A.png") == imageA && store.Images.Count == 2 &&
                File.ReadAllBytes(store.ImagePath(imageA)).SequenceEqual(artwork), "copied original images and content deduplication");
            Check(store.CycleImage("builtin", -1) == imageB && store.CycleImage(imageB, 1) == imageA &&
                store.CycleImage(imageA, -1) == imageB && store.CycleImage(imageA, 1) == imageB &&
                store.CycleImage("missing", 1) == imageA && store.CycleImage("missing", -1) == imageB &&
                store.CycleImage(imageA, 0) == imageA, "image shortcuts follow library order, wrap in both directions and recover missing selections");
            var a1 = new ScopeStickerProfile { Name = "A", Image = imageA, Width = 30, X = 10, Y = 20, Opacity = .2f, Rotation = 12, AutoContrast = true, HitFlash = true, HitColor = "#00FF80", HitSeconds = .5f, ColorMode = 2, ColorSpeed = 3.5f, ColorPrimary = "#123456", ColorSecondary = "#654321", ColorUseMask = true };
            var a2 = a1 with { Image = imageB, Width = 48, X = 24, Opacity = .8f, Rotation = -45, AutoContrast = false, HitColor = "#112233", HitSeconds = .1f, ColorMode = 1, ColorSpeed = .75f, ColorUseMask = false };
            var b1 = a1 with { Name = "B", Width = 15, Y = 12, Rotation = 90, HitFlash = false, ColorMode = 3, ColorSpeed = 2f };
            store.Save(a, a1); store.Save(b, b1); store.Save(sub, defaults); store.Save(a, a2);
            var reloaded = new ScopeStickerStore(directory);
            Check(reloaded.Get(a, defaults) == a2 && reloaded.Get(b, defaults) == b1 && reloaded.Get(sub, defaults) == defaults,
                "active image, dynamic color settings and layout survive reload independently per scope and sub-sight");
            Check(reloaded.ForImage(a, imageA, defaults) == a1 && reloaded.ForImage(a, imageB, defaults) == a2 &&
                reloaded.ForImage(b, imageA, defaults) == b1 && reloaded.ForImage(b, imageB, defaults) == (defaults with { Image = imageB }),
                "same scope restores image-specific layout without borrowing another scope's variant");
            reloaded.Save(a, reloaded.ForImage(a, imageA, defaults)); var again = new ScopeStickerStore(directory);
            Check(again.Get(a, defaults) == a1 && again.ForImage(a, imageB, defaults) == a2 && again.Get(b, defaults) == b1 &&
                untouched == defaults && defaults.Image == "" && defaults.Width == 32,
                "changing active image retains other variants and global defaults");
            Console.WriteLine("PASS scope sticker independent scopes, sub-sights, image variants, active selection and reload");
            string path = Path.Combine(directory, "profiles.json"); byte[] before = File.ReadAllBytes(path); bool failed = false;
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                try { again.Save(a, a2); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { failed = true; }
            Check(failed && File.ReadAllBytes(path).SequenceEqual(before) && again.Get(a, defaults) == a1 &&
                again.ForImage(a, imageB, defaults) == a2 && !Directory.EnumerateFiles(directory, "*.tmp").Any(),
                "failed save retains disk and memory state and removes temporary files");
            File.WriteAllText(path, "{broken"); failed = false;
            try { _ = new ScopeStickerStore(directory); } catch (System.Text.Json.JsonException) { failed = true; }
            Check(failed && File.ReadAllText(path) == "{broken", "malformed profiles are preserved");
            Console.WriteLine("PASS scope sticker failed-save rollback, temporary cleanup and corrupt-file preservation");
            string legacy = Path.Combine(directory, "legacy"); Directory.CreateDirectory(legacy);
            string legacyFile = Path.Combine(legacy, "profiles.json");
            const string oldJson = "{\"Version\":1,\"Profiles\":{\"legacy:0\":{\"Width\":41,\"X\":9,\"Y\":-4,\"Opacity\":0.4}},\"Images\":{}}";
            File.WriteAllText(legacyFile, oldJson);
            var legacyStore = new ScopeStickerStore(legacy); var legacyValue = legacyStore.Get("legacy:0", defaults);
            Check((legacyValue with { Image = "builtin" }).Normalized().Image == "" &&
                (legacyValue with { Image = "builtin" }).Normalized().Width == legacyValue.Width,
                "retired built-in selections become empty without discarding their layout");
            Check(legacyValue.Rotation == 0 && !legacyValue.AutoContrast && !legacyValue.HitFlash && legacyValue.HitColor == "#FF4040" && legacyValue.HitSeconds == .18f && legacyValue.ColorMode == 0 && legacyValue.ColorSpeed == 1f && legacyValue.Width == 41 && legacyValue.X == 9 && legacyValue.Y == -4 &&
                legacyValue.Opacity == .4f && File.ReadAllText(legacyFile) == oldJson, "old profiles default to upright without rewriting the file");
            legacyStore.Save("legacy:0", legacyValue with { Rotation = 75 });
            Check(new ScopeStickerStore(legacy).Get("legacy:0", defaults) == (legacyValue with { Rotation = 75 }),
                "adding rotation preserves all existing settings");
            Console.WriteLine("PASS scope sticker legacy profile compatibility and independent persisted rotation");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
