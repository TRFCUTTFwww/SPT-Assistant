using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;
using Shortcut=BepInEx.Unity.IL2CPP.Configuration.KeyboardShortcut;
namespace LocalSpt5Esp;
internal static class ShortcutBindings {
 static readonly Dictionary<ConfigDefinition,int> frames=new();
 internal static bool IsBinding(object value)=>value is KeyCode or Shortcut;
 internal static bool Assigned(object value)=>value is KeyCode key?key!=KeyCode.None:value is Shortcut shortcut&&shortcut.MainKey!=KeyCode.None;
 internal static object Empty(object value)=>value is Shortcut?Shortcut.Empty:KeyCode.None;
 internal static string Label(object value)=>value is Shortcut shortcut?Label(shortcut):value.ToString()??"";
 internal static bool LegacyControl(ConfigEntryBase entry)=>HotkeyRules.LegacyControl(MenuSections.Number(entry.Definition.Section),entry.Definition.Key);
 internal static string Label(ConfigEntryBase entry)=>entry.BoxedValue is KeyCode key&&key!=KeyCode.None&&LegacyControl(entry)?"Ctrl+"+Label(entry.BoxedValue):Label(entry.BoxedValue);
 internal static string Label(Shortcut value)=>value.MainKey==KeyCode.None?"None":string.Join("+",value.Modifiers.OrderBy(key=>Modifier(key)==HotkeyModifiers.None?8:(int)Modifier(key)).Select(KeyLabel).Append(KeyLabel(value.MainKey)).Distinct());
 static string KeyLabel(KeyCode key)=>key switch{KeyCode.LeftControl or KeyCode.RightControl=>"Ctrl",KeyCode.LeftShift or KeyCode.RightShift=>"Shift",KeyCode.LeftAlt or KeyCode.RightAlt=>"Alt",KeyCode.Delete=>"Del",_=>key.ToString()};
 static HotkeyModifiers Modifier(KeyCode key)=>key switch{KeyCode.LeftControl or KeyCode.RightControl=>HotkeyModifiers.Control,KeyCode.LeftShift or KeyCode.RightShift=>HotkeyModifiers.Shift,KeyCode.LeftAlt or KeyCode.RightAlt=>HotkeyModifiers.Alt,_=>HotkeyModifiers.None};
 static HotkeyModifiers Held=>(Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl)?HotkeyModifiers.Control:0)|(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)?HotkeyModifiers.Shift:0)|(Input.GetKey(KeyCode.LeftAlt)||Input.GetKey(KeyCode.RightAlt)?HotkeyModifiers.Alt:0);
 internal static bool Down(Shortcut shortcut,bool editorAlt=false){
  if(shortcut.MainKey==KeyCode.None||!Input.GetKeyDown(shortcut.MainKey))return false;
  var required=shortcut.Modifiers.Aggregate(HotkeyModifiers.None,(value,key)=>value|Modifier(key));
  return HotkeyRules.Matches(required,Held,editorAlt)&&shortcut.Modifiers.All(key=>Modifier(key)!=HotkeyModifiers.None||Input.GetKey(key));
 }
 internal static bool Take(ConfigEntry<Shortcut> binding,bool editorAlt=false){if(!Down(binding.Value,editorAlt)||frames.TryGetValue(binding.Definition,out int frame)&&frame==Time.frameCount)return false;frames[binding.Definition]=Time.frameCount;return true;}
 internal static bool IsModifier(KeyCode key)=>Modifier(key)!=HotkeyModifiers.None;
 internal static Shortcut Capture(KeyCode key){var mods=new List<KeyCode>();if((Held&HotkeyModifiers.Control)!=0)mods.Add(KeyCode.LeftControl);if((Held&HotkeyModifiers.Shift)!=0)mods.Add(KeyCode.LeftShift);if((Held&HotkeyModifiers.Alt)!=0)mods.Add(KeyCode.LeftAlt);return new(key,mods.ToArray());}
}
