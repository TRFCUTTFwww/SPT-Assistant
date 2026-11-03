namespace LocalSpt5Esp;
internal static class AiEquipmentRemovalRules {
 internal static bool Allowed(string slot)=>slot is "Headwear" or "FirstPrimaryWeapon" or "SecondPrimaryWeapon" or "Holster";
 internal static bool Matches(string expected,long pointer,string actual,long actualPointer)=>expected.Length>0&&expected==actual&&pointer!=0&&pointer==actualPointer;
}
