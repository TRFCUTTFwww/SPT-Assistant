namespace HarmonyLib {
    internal class Harmony { public void Patch(System.Reflection.MethodInfo method,HarmonyMethod prefix){} }
    internal class HarmonyMethod { public HarmonyMethod(System.Type type,string name){} }
}
namespace EFT.Ballistics { internal class DamageInfo { } }
namespace UnityEngine {
    internal struct Color32(byte red,byte green,byte blue,byte alpha) { public byte r=red,g=green,b=blue,a=alpha; }
}
