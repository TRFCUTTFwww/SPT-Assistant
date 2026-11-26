using System;
namespace LocalSpt5Esp;
// Keep the host-owned config type stable across hot reloads. Numeric enum values round-trip in BepInEx.
internal static class AimModes {
 internal const AimMode Soft=(AimMode)3;
 internal static readonly AimMode[] All={AimMode.Off,AimMode.Snap,AimMode.Silent,Soft};
 internal static AimMode Next(AimMode value)=>All[(Array.IndexOf(All,value)+1)%All.Length];
}
