namespace LocalSpt5Esp;
internal static class AiWeaponSlotRules {
 internal static readonly string[] Slots={"FirstPrimaryWeapon","SecondPrimaryWeapon","Holster"};
 internal static bool Allowed(string slot)=>slot is "FirstPrimaryWeapon" or "SecondPrimaryWeapon" or "Holster";
 internal static string Resolve(int choice,string heldSlot)=>choice switch{0=>Allowed(heldSlot)?heldSlot:"FirstPrimaryWeapon",1=>"FirstPrimaryWeapon",2=>"SecondPrimaryWeapon",3=>"Holster",_=>throw new System.ArgumentException("武器槽位无效。")};
}
