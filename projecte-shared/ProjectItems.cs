namespace ProjectE;
public sealed record Sword(string Id,string Key,string English,string Chinese,int TierBonus,int SwordBonus,int Price) {
 public int Damage=>1+TierBonus+SwordBonus;
}
public static class Swords {
 public const string BaseTemplate="664a5428d5e33a713b622379",KnifeParent="5447e1d04bdc2dff2f8b4567";
 public static readonly Sword[] All=[
  new("a7e000000000000000000001","dm","Dark Matter Sword","暗物质剑",3,9,131072),
  new("a7e000000000000000000002","rm","Red Matter Sword","红物质剑",4,12,786432),
  new("a7e000000000000000000003","katar","Red Matter Katar","红物质拳剑",4,19,16777216)
 ];
 public static Sword? Find(string id)=>Array.Find(All,s=>s.Id==id);
}
public sealed record Gadget(string Id,string Key,string English,string Chinese,int Price);
public static class Gadgets {
 public const string Tablet="a7e000000000000000000101",Terminal="a7e000000000000000000102",Totem="a7e000000000000000000103";
 public static readonly Gadget[] All=[new(Tablet,"tablet","Portable Transmutation Tablet","便携转化桌",262144),new(Terminal,"wireless_terminal","AE2 Wireless Terminal","AE2 无线访问终端",65536),new(Totem,"totem","Totem of Undying","不死图腾",131072)];
 public static Gadget? Find(string id)=>Array.Find(All,x=>x.Id==id);
}
public static class MatterRules {
 public static int MaxCharge(string template)=>Swords.Find(template) is {} sword?sword.Key=="dm"?2:sword.Key=="katar"?4:3:0;
 public static int Step(int charge,int maximum,bool decrease)=>Math.Clamp(charge+(decrease?-1:1),0,Math.Max(0,maximum));
 public static float Radius(int charge)=>2.5f*Math.Clamp(charge,0,4);
 public static float MeleeBonus(int charge)=>Math.Clamp(charge,0,4);
 public static bool EligibleTarget(bool local,bool human,bool alive,bool friendly,bool all)=>!local&&!human&&alive&&(all||!friendly);
 public static bool CanSave(bool raid,bool local,bool held,bool consumed,bool alive)=>raid&&local&&held&&!consumed&&alive;
 public static float RevivalHealth(float maximum)=>Math.Clamp(maximum*.2f,1,Math.Max(1,maximum));
}
