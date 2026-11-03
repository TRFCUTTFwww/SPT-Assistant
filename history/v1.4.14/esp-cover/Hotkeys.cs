using UnityEngine;
namespace LocalSpt5Esp;
internal static class Hotkeys {
 internal static bool Pressed(KeyCode key)=>Input.GetKeyDown(key)&&(Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl));
 internal static string Label(KeyCode key)=>"Ctrl+"+key;
}
