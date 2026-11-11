using UnityEngine;
using BepInEx.Configuration;
using Shortcut=BepInEx.Unity.IL2CPP.Configuration.KeyboardShortcut;
namespace LocalSpt5Esp;
internal static class Hotkeys {
 internal static ConfigEntry<Shortcut> DetailNext=null!,DetailPrevious=null!;
 internal static void Initialize(){DetailNext=FeatureConfig.Bind("1. ESP","Detail next page shortcut","详情下一页热键",new Shortcut(KeyCode.PageDown),"物品详情与准星 AI Buff 详情共用；支持组合键。");DetailPrevious=FeatureConfig.Bind("1. ESP","Detail previous page shortcut","详情上一页热键",new Shortcut(KeyCode.PageUp),"物品详情与准星 AI Buff 详情共用；支持组合键。");}
 internal static bool Ready=>!InterfaceMenu.Open&&!RaidChat.InputBlocked&&!WorldEditor.InputBlocked&&!FloatingStatus.Capturing&&Application.isFocused&&!Cursor.visible;
 internal static bool Pressed(KeyCode key)=>key!=KeyCode.None&&Ready&&Input.GetKeyDown(key)&&(Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl));
 internal static bool Pressed(ConfigEntry<Shortcut> binding)=>Ready&&ShortcutBindings.Take(binding);
 internal static string Label(KeyCode key)=>"Ctrl+"+key;
}
