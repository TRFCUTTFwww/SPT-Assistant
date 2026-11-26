using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using LocalSpt5Esp;

internal static class OpticZoomChecks
{
    internal static void Run(string sourceRoot, string gameRoot)
    {
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        float zoomed = OpticZoomRules.Step(6, 40);
        Check(zoomed < 1 && zoomed >= OpticZoomRules.MinimumFov, "Alt-wheel can exceed a native six-degree optic limit");
        float forward = OpticZoomRules.Step(20, 1), restored = OpticZoomRules.Step(forward, -1);
        Check(Math.Abs(restored - 20) < .0001f &&
            Math.Abs(Math.Tan(20 * Math.PI / 360) / Math.Tan(forward * Math.PI / 360) - 1.1) < .00001,
            "wheel steps change magnification consistently and reverse without drift");
        Check(OpticZoomRules.Step(.001f, 128) == OpticZoomRules.MinimumFov &&
            OpticZoomRules.Step(170, -128) == OpticZoomRules.MaximumFov &&
            OpticZoomRules.Step(6, 128, .5f, 120) == .5f &&
            OpticZoomRules.Step(6, -128, .5f, 120) == 120, "engine-accepted FOV limits bound both wheel directions");
        Check(OpticZoomRules.Step(6, float.NaN) == 6 && OpticZoomRules.Step(6, float.PositiveInfinity) == 6 &&
            float.IsNaN(OpticZoomRules.Step(float.NaN, 1)), "invalid zoom inputs cannot produce a new projection");
        Check(OpticZoomRules.SensitivityScale(12, 6, 24) == 1 &&
            OpticZoomRules.SensitivityScale(6, 24, 6) == 1 &&
            OpticZoomRules.SensitivityScale(24, 24, 6) == 1,
            "supported zoom retains native sensitivity at both endpoints and between them");
        Check(Math.Abs(OpticZoomRules.SensitivityScale(OpticZoomRules.Step(6, 1), 6, 24) - 1 / 1.1f) < .00001f &&
            Math.Abs(OpticZoomRules.SensitivityScale(OpticZoomRules.Step(24, -1), 6, 24) - 1.1f) < .00001f &&
            OpticZoomRules.SensitivityScale(float.NaN, 6, 24) == 1,
            "extended sensitivity follows optical magnification continuously in either direction");
        using var native = AssemblyDefinition.ReadAssembly(Path.Combine(gameRoot, "BepInEx/interop/Assembly-CSharp.dll"));
        var handler = native.MainModule.Types.Single(t => t.Name == "ScopeZoomHandler");
        Check(handler.Methods.Single(m => m.Name == "GetZoomInput").ReturnType.FullName == "System.Single" &&
            handler.Methods.Single(m => m.Name == "UpdateFieldOfView").Parameters.Count == 0 &&
            handler.Methods.Single(m => m.Name == "ForceChangeScopeState").Parameters.Count == 0 &&
            handler.Properties.Single(p => p.Name == "_sightComponent").PropertyType.FullName == "EFT.InventoryLogic.SightComponent",
            "native zoom input, state update, endpoint switch and sight ownership signatures match");
        var updater = native.MainModule.Types.Single(t => t.FullName == "EFT.CameraControl.OpticComponentUpdater");
        Check(updater.Methods.Single(m => m.Name == "LateUpdate").Parameters.Count == 0 &&
            updater.Methods.Single(m => m.Name == "CopyComponentFromOptic").Parameters.Single().ParameterType.FullName == "EFT.CameraControl.OpticSight" &&
            updater.Properties.Single(p => p.Name == "_scopeZoomHandler").PropertyType.FullName == "ScopeZoomHandler",
            "native adjustable and fixed optic application contracts match");
        var firearm = native.MainModule.Types.Single(t => t.FullName == "EFT.Player").NestedTypes.Single(t => t.Name == "FirearmController");
        Check(firearm.Methods.Single(m => m.Name == "UpdateSensitivity").Parameters.Count == 0 &&
            firearm.Properties.Single(p => p.Name == "_aimingSens").PropertyType.FullName == "System.Single" &&
            firearm.Properties.Single(p => p.Name == "_isAiming").PropertyType.FullName == "System.Boolean",
            "native firearm sensitivity application and aiming guard contracts match");
        Check(MenuSections.Pages("镜头与画面").Contains("瞄准镜") &&
            !MenuSections.Pages("镜头与画面").Contains("倍镜变倍") &&
            MenuSections.Group("74. 倍镜变倍") == "镜头与画面" &&
            MenuSections.OnPage("镜头与画面", "瞄准镜", "74. 倍镜变倍", "Extended optical zoom") &&
            MenuSections.OnPage("镜头与画面", "瞄准镜", "73. 倍镜贴纸", "Enabled"),
            "zoom and sticker settings share one optics page");
        var sight = native.MainModule.Types.Single(t => t.FullName == "EFT.InventoryLogic.SightComponent");
        Check(sight.Methods.Single(m => m.Name == "SetScopeMode").Parameters.All(p => p.ParameterType.FullName == "System.Int32") &&
            sight.Properties.Any(p => p.Name == "SelectedScopeMode"), "native scope mode switching remains available");
        Console.WriteLine("PASS optic zoom limits, native camera pipeline contracts and independent settings switch");
    }
}
