using System;
using System.IO;
using System.Linq;
using Mono.Cecil;

internal static class RecoilAssistChecks
{
    internal static void Run(string sourceRoot, string gameRoot)
    {
        void Check(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
        MethodReference[] Calls(MethodDefinition method) => method.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>().ToArray();
        using var core = AssemblyDefinition.ReadAssembly(Path.Combine(sourceRoot, "esp-cover/bin/Release/net10.0/Local.Spt5.ESP.Core.dll"));
        var recoil = core.MainModule.Types.Single(t => t.Name == "RecoilAssist");
        var initialize = recoil.Methods.Single(m => m.Name == "Initialize");
        var hooks = initialize.Body.Instructions.Select(i => i.Operand).OfType<string>().ToArray();
        Check(hooks.Count(s => s == "AddRecoilForce") == 2 && hooks.Contains("ProcessEffectors") && hooks.Contains("FinishEffectors") &&
            hooks.Contains("AddHandRecoilRotateToCamera"), "retained development build hooks shot impulses and current-local camera recoil with scoped steady aim");
        foreach (string name in new[] { "OldForce", "NewForce" })
        {
            var calls = Calls(recoil.Methods.Single(m => m.Name == name));
            Check(calls.Any(m => m.Name == "get_Active") && calls.Any(m => m.Name == "get__firearmController") && calls.Any(m => m.Name == "Local"),
                name + " checks the effect's current owner on the actual shot");
        }
        var camera = Calls(recoil.Methods.Single(m => m.Name == "CameraRecoil"));
        Check(camera.Any(m => m.Name == "get_Active") && camera.Any(m => m.Name == "get__firearmController") &&
            camera.Any(m => m.Name == "Local"), "retained camera suppression uses the currently held local controller");
        var local = Calls(recoil.Methods.Single(m => m.Name == "Local"));
        Check(local.Count(m => m.Name == "get_Destroyed") == 2 && local.Any(m => m.Name == "get_IsYourPlayer") && local.Any(m => m.Name == "get_HandsController") &&
            local.Any(m => m.Name == "get_Pointer"), "outgoing, disposed and foreign controllers cannot inherit local no-recoil state");
        Check(!recoil.Fields.Any(f => f.FieldType.FullName is "System.IntPtr" or "System.Action" ||
            f.FieldType.Namespace == "EFT.Animations") && !recoil.Methods.Any(m => m.Name is "Tick" or "UpdateSteadyAim"),
            "no cached weapon identities, animation objects or delayed restore delegates survive between frames");
        var before = recoil.Methods.Single(m => m.Name == "BeforeEffectors");
        var finish = recoil.Methods.Single(m => m.Name == "FinishEffectors");
        Check(before.ReturnType.FullName == "System.Void" && before.Parameters.Last().IsOut &&
            recoil.NestedTypes.Single(t => t.Name == "SteadyFrame").IsValueType && finish.ReturnType.FullName == "System.Void",
            "steady aim always runs native processing and keeps one value-type restoration state per invocation");
        var beforeCalls = Calls(before);
        Check(beforeCalls.Any(m => m.Name == "Local") && beforeCalls.Any(m => m.Name == "get_IsAiming") &&
            beforeCalls.Count(m => m.Name == "IsFinite") == 2, "steady aim requires the current local aiming rig and finite intensities");
        var finishCalls = Calls(finish);
        Check(finishCalls.Count(m => m.Name == "set_Intensity") == 1 && finishCalls.Count(m => m.Name == "set_InputIntensity") == 1 &&
            finish.Body.ExceptionHandlers.Count == 2, "finalizer independently restores breath and rotation even if one restoration fails");
        Check(!recoil.Methods.SelectMany(Calls).Any(m => m.Name is "ResetSpring" or "ResetCameraRotation" or "set_Current" or "set_Velocity" or "set_localRotation"),
            "the fix does not clamp or reset native camera, spring physics or draw/stow transforms");
        Console.WriteLine("PASS recoil current-controller isolation, native camera interpolation and per-invocation steady-aim restoration contracts");

        using var native = AssemblyDefinition.ReadAssembly(Path.Combine(gameRoot, "BepInEx/interop/Assembly-CSharp.dll"));
        foreach (var name in new[] { "EFT.Animations.OldRecoilShotEffect", "EFT.Animations.NewRecoil.NewRecoilShotEffect" })
        {
            var type = native.MainModule.Types.Single(t => t.FullName == name);
            var force = type.Methods.Single(m => m.Name == "AddRecoilForce");
            Check(force.ReturnType.FullName == "System.Void" && force.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(new[] { "System.Single" }) &&
                type.Properties.Single(p => p.Name == "_firearmController").PropertyType.FullName == "EFT.Player/FirearmController", "installed recoil impulse/ownership contract: " + name);
        }
        var animation = native.MainModule.Types.Single(t => t.FullName == "EFT.Animations.ProceduralWeaponAnimation");
        Check(animation.Methods.Single(m => m.Name == "ProcessEffectors").Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(
            new[] { "System.Single", "System.Int32", "UnityEngine.Vector3", "UnityEngine.Vector3", "System.Boolean" }) &&
            animation.Properties.Single(p => p.Name == "_firearmController").PropertyType.FullName == "EFT.Player/FirearmController",
            "installed procedural processing/ownership contract matches the scoped hook");
        Console.WriteLine("PASS installed EFT recoil impulse signatures, owner properties and procedural processing signature");
    }
}
