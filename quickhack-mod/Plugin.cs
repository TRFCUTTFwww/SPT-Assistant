using LocalQuickhacks.Hosting;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using EFT;
using EFT.InputSystem;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LocalQuickhacks;

internal static class Plugin {
    internal static ManualLogSource Logger = null!;
    internal static ConfigEntry<KeyCode> ScanKey = null!, PreviewKey = null!;
    internal static ConfigEntry<bool> Enabled = null!;
    internal static ConfigEntry<float> Range = null!, Volume = null!;
    internal static void Configure(ConfigFile Config,ManualLogSource log) {
        Logger = log;
        Enabled = Config.Bind("General", "Enabled", true, "启用独立快速破解 Mod。");
        ScanKey = Config.Bind("Input", "Scanner key", KeyCode.F9, "按住扫描，滚轮选技能，F 上传；不减慢游戏时间。");
        PreviewKey = Config.Bind("Input", "Preview key", KeyCode.F10, "打开界面与无伤害施放预览，支持主菜单。");
        Range = Config.Bind("General", "Range", 60f, new ConfigDescription("AI 扫描与上传距离（米）。", new AcceptableValueRange<float>(5,150)));
        Volume = Config.Bind("Audio", "Volume", .6f, new ConfigDescription("界面和破解音效音量。", new AcceptableValueRange<float>(0,1)));
    }
}

public sealed class RuntimeModule:IQuickhackModule {
    Harmony? patches;
    public int ApiVersion=>1;
    public string Version=>"0.3.3";
    public bool CanReload=>!Runtime.Uploading&&Runtime.Mode!=UiMode.Breach;
    public void Initialize(ConfigFile config,ManualLogSource log,string directory) {
        Plugin.Configure(config,log);
        Assets.Home=directory;
        patches = new Harmony("local.cp2077.quickhacks.runtime");
        try {
            Runtime.Start();
            DoorHacking.Install(patches);
            InputHooks.Install(patches);
            AiHacks.Install(patches);
            log.LogInfo("Quickhacks 0.3.3 runtime ready: independent overlapping breach programs / animated terminal; F9 scanner / F10 preview; native keycard list retains cards and adds 破解");
        } catch { patches.UnpatchSelf(); Runtime.Stop(); throw; }
    }
    public void Shutdown() { try { Runtime.Stop(); } finally { patches?.UnpatchSelf(); patches=null; } }
    public void Tick()=>Runtime.Tick();
    public void LateTick() { try { Runtime.KeepCursor(); Effects.Tick(); } catch(Exception e) { Runtime.Warn("Effects",e); Effects.Clear(); } }
    public void Draw()=>HackUi.Draw();
}

internal static class InputHooks {
    internal static void Install(Harmony h) {
        h.Patch(AccessTools.Method(typeof(GamePlayerOwner), "TranslateAxes"), prefix: new HarmonyMethod(typeof(InputHooks),nameof(Axes)));
        h.Patch(AccessTools.Method(typeof(GamePlayerOwner), "TranslateCommand"), prefix: new HarmonyMethod(typeof(InputHooks),nameof(Command)));
        h.Patch(AccessTools.Method(typeof(Player.FirearmController), "SetTriggerPressed", [typeof(bool)]), prefix: new HarmonyMethod(typeof(InputHooks),nameof(Trigger)));
        h.Patch(AccessTools.Method(typeof(InputManager), "Update"), prefix: new HarmonyMethod(typeof(InputHooks),nameof(GameInput)));
        h.Patch(AccessTools.Method(typeof(EventSystem), "Update"), prefix: new HarmonyMethod(typeof(InputHooks),nameof(GameInput)));
    }
    static bool Axes(GamePlayerOwner __instance, ref Il2CppStructArray<float> __0) {
        if (!Runtime.BlockInput || __instance.Player?.IsYourPlayer != true) return true;
        if (__0 != null) for (int i=0;i<__0.Length;i++) __0[i]=0;
        return false;
    }
    static bool Command(GamePlayerOwner __instance, ref InputNode.ETranslateResult __result) {
        if (!Runtime.BlockInput || __instance.Player?.IsYourPlayer != true) return true;
        __result = InputNode.ETranslateResult.BlockAll; return false;
    }
    static bool Trigger(Player.FirearmController __instance, bool __0) => !__0 || !Runtime.BlockInput || __instance._player?.IsYourPlayer != true;
    static bool GameInput()=>!Runtime.BlockInput;
}

public static class QuickhackApi {
    public static string Version=>"0.3.3";
    public static int Frames=>Runtime.Frames;
    public static string Diagnostics()=>System.Text.Json.JsonSerializer.Serialize(new { version=Version,frames=Frames,mode=Runtime.Mode.ToString(),status=Runtime.Status,menuAppends=DoorHacking.MenuCount,aiEffects=AiHacks.ActiveCount,uploading=Runtime.Uploading });
}
