using System;
namespace LocalSpt5Esp;
[Flags] internal enum HotkeyModifiers { None=0,Control=1,Shift=2,Alt=4 }
internal static class HotkeyRules {
 internal static bool LegacyControl(int section,string key)=>section switch{3=>true,8=>key is "Cycle mode key" or "Always active toggle key",10=>key=="Toggle key",14=>key is "Force toggle key" or "Ignore requirements toggle key",16=>key=="Night vision toggle",29=>key=="Heal key",33=>key=="Toggle key",_=>false};
 internal static bool Matches(HotkeyModifiers required,HotkeyModifiers held,bool editorAlt=false)=>
  required==held||editorAlt&&(held&~HotkeyModifiers.Alt)==required;
}
