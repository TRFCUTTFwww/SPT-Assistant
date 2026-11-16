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
    internal static ConfigEntry<bool> Enabled = null!,DoorHackingEnabled=null!;
    internal static ConfigEntry<float> Range = null!, Volume = null!;
    internal static ConfigEntry<float> ScannerSpeed = null!;
    internal static ConfigEntry<bool> InfiniteRam = null!;
    internal static ConfigEntry<float> GatewayRange = null!, GatewayHopTime = null!;
    internal static ConfigEntry<int> GatewayTargets = null!;
    internal static void Configure(ConfigFile Config,ManualLogSource log) {
        Logger = log;
        Enabled = Config.Bind("General", "Enabled", true, "启用独立快速破解 Mod。");
        DoorHackingEnabled=Config.Bind("Gameplay","Keycard hacking enabled",true,"允许在原生门卡交互菜单中显示破解；关闭后取消破解入口和尚未完成的授权。");
        ScanKey = Config.Bind("Input", "Scanner key", KeyCode.LeftControl, "按住扫描，鼠标转动视角，滚轮选技能，F 上传。");
        PreviewKey = Config.Bind("Input", "Preview key", KeyCode.F10, "打开界面与无伤害施放预览，支持主菜单。");
        ScannerSpeed = Config.Bind("Gameplay", "Scanner time factor", .15f, new ConfigDescription("快速破解扫描时的时间倍率（相对于打开前的速度），退出后恢复。1 为正常速度。",new AcceptableValueRange<float>(.02f,1)));
        Range = Config.Bind("General", "Range", 60f, new ConfigDescription("AI 扫描与上传距离（米）。", new AcceptableValueRange<float>(5,150)));
        Volume = Config.Bind("Audio", "Volume", .6f, new ConfigDescription("界面和破解音效音量。", new AcceptableValueRange<float>(0,1)));
        InfiniteRam = Config.Bind("Gameplay", "Infinite RAM", false, "无限 RAM：技能不消耗 RAM，也不会因 RAM 不足而禁止上传。");
        GatewayRange = Config.Bind("Gameplay", "Gateway spread range", HackRules.GatewayRange, new ConfigDescription("黑墙每次传染搜索的半径（米），仍检查墙体阻挡。",new AcceptableValueRange<float>(1,150)));
        GatewayTargets = Config.Bind("Gameplay", "Gateway target limit", HackRules.GatewayTargets, new ConfigDescription("一次黑墙连锁的总目标数，包含首个目标。",new AcceptableValueRange<int>(1,30)));
        GatewayHopTime = Config.Bind("Gameplay", "Gateway hop duration", HackRules.GatewayHopTime, new ConfigDescription("黑墙传染后立即播放头顶和身体动画的时长（秒），动画结束立刻结算，无额外等待。",new AcceptableValueRange<float>(.5f,5)));
    }
}

public sealed class RuntimeModule:IQuickhackModule {
    Harmony? patches;
    public int ApiVersion=>1;
    public string Version=>"0.6.2";
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

            log.LogInfo("Quickhacks 0.6.2 runtime ready: eight quickhacks / per-target queues / head markers; Left Ctrl scanner / F10 preview; native keycard list retains cards and adds 破解");
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
        if(Runtime.ScannerLook) {
            if(__0!=null) for(int i=0;i<__0.Length;i++) if(i!=(int)EAxis.TurnX&&i!=(int)EAxis.TurnY&&i!=(int)EAxis.LookX&&i!=(int)EAxis.LookY) __0[i]=0;
            return true;
        }
        if (__0 != null) for (int i=0;i<__0.Length;i++) __0[i]=0;
        return false;
    }
    static bool Command(GamePlayerOwner __instance, ref InputNode.ETranslateResult __result) {
        if (!Runtime.BlockInput || __instance.Player?.IsYourPlayer != true) return true;
        __result = InputNode.ETranslateResult.BlockAll; return false;
    }
    static bool Trigger(Player.FirearmController __instance, bool __0) => !__0 || !Runtime.BlockInput || __instance._player?.IsYourPlayer != true;
    static bool GameInput() { Runtime.CheckScanChord(); return !Runtime.BlockInput||Runtime.ScannerLook; }
}

public static class QuickhackApi {
    public static bool Active=>Runtime.Active;
    public static bool IsHostile(BotOwner owner,IPlayer target)=>Runtime.Active&&AiHacks.IsHostile(owner,target);
    // Shared BepInEx types keep the optional Assistant panel independent of this assembly.
    public static ConfigEntryBase[] GetSettings()=>[Plugin.Enabled,Plugin.DoorHackingEnabled,Plugin.InfiniteRam,Plugin.Range,Plugin.ScannerSpeed,Plugin.GatewayRange,Plugin.GatewayTargets,Plugin.GatewayHopTime,Plugin.Volume];
    public static string Version=>"0.6.2";
    public static int Frames=>Runtime.Frames;
    public static string Diagnostics()=>System.Text.Json.JsonSerializer.Serialize(new { version=Version,frames=Frames,mode=Runtime.Mode.ToString(),status=Runtime.Status,menuAppends=DoorHacking.MenuCount,aiEffects=AiHacks.ActiveCount,uploading=Runtime.Uploading });
}
