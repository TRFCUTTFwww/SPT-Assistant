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
