using BepInEx.Configuration;
using EFT;
using HarmonyLib;
using NativeTask=Il2CppSystem.Threading.Tasks.Task;
namespace LocalSpt5Esp;
internal static class MenuIdleControl {
 static ConfigEntry<bool> keepOpen=null!;
 internal static void Initialize(Plugin plugin){
  keepOpen=FeatureConfig.Bind("47. 主菜单挂机","Keep menu open","主菜单挂机不自动退出",true,"阻止游戏因主菜单长时间无输入而自动退出；手动退出游戏仍然有效。关闭后下次进入主菜单恢复原有闲置检测。");
  plugin.Patches.Patch(AccessTools.Method(typeof(AFKMonitor),"StartWaiting"),prefix:new HarmonyMethod(typeof(MenuIdleControl),nameof(BeforeWaiting)));
  plugin.Patches.Patch(AccessTools.Method(typeof(AFKMonitor),"TryQuitApplication"),prefix:new HarmonyMethod(typeof(MenuIdleControl),nameof(AllowIdleQuit)));
  plugin.Log.LogInfo("Main-menu AFK guard ready: idle monitor waits and idle-only quit suppressed; manual exit unchanged");
 }
 static bool BeforeWaiting(ref NativeTask __result){if(!keepOpen.Value)return true;__result=NativeTask.CompletedTask;return false;}
 static bool AllowIdleQuit()=>!keepOpen.Value;
}
