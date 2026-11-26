using BepInEx.Configuration;
using EFT;
using HarmonyLib;
using NativeTask=Il2CppSystem.Threading.Tasks.Task;
namespace LocalSpt5Esp;
internal static class MenuIdleControl {
 static ConfigEntry<bool> keepOpen=null!;
 internal static void Initialize(Plugin plugin){
  keepOpen=FeatureConfig.Bind("47. 主菜单挂机","Keep menu open","主菜单挂机不自动退出",true,"阻止主菜单长时间无操作自动退出游戏。");
  plugin.Patches.Patch(AccessTools.Method(typeof(AFKMonitor),"StartWaiting"),prefix:new HarmonyMethod(typeof(MenuIdleControl),nameof(BeforeWaiting)));
  plugin.Patches.Patch(AccessTools.Method(typeof(AFKMonitor),"TryQuitApplication"),prefix:new HarmonyMethod(typeof(MenuIdleControl),nameof(AllowIdleQuit)));
  plugin.Log.LogInfo("Main-menu AFK guard ready: idle monitor waits and idle-only quit suppressed; manual exit unchanged");
 }
 static bool BeforeWaiting(ref NativeTask __result){if(!keepOpen.Value)return true;__result=NativeTask.CompletedTask;return false;}
 static bool AllowIdleQuit()=>!keepOpen.Value;
}
