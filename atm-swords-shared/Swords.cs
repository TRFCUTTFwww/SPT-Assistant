namespace AllTheModium;
public sealed record Sword(string Id,string Key,string English,string Chinese,int TierBonus,int SwordBonus,int Price) {
    // Minecraft Player base attack (1) + SwordItem.createAttributes(tier, bonus, speed).
    public int Damage=>1+TierBonus+SwordBonus;
}
public static class Swords {
    public const string BaseTemplate="664a5428d5e33a713b622379";
    public const string KnifeParent="5447e1d04bdc2dff2f8b4567";
    public static readonly Sword[] All=[
        new("a7a000000000000000000001","allthemodium","Allthemodium Sword","ATM 剑",12,5,18000),
        new("a7a000000000000000000002","vibranium","Vibranium Sword","振金剑",25,10,36000),
        new("a7a000000000000000000003","unobtainium","Unobtainium Sword","难得素剑",35,15,51000),
        new("a7a000000000000000000004","alloy","Alloy Sword","合金剑",65,3,69000)
    ];
    // One renderer owns all pixel melee meshes, so pooled gladius instances
    // cannot retain another mod's replacement when switching between swords.
    public static readonly Sword[] Renderable=[..All,
        new("a7e000000000000000000001","dm","Dark Matter Sword","暗物质剑",3,9,131072),
        new("a7e000000000000000000002","rm","Red Matter Sword","红物质剑",4,12,786432),
        new("a7e000000000000000000003","katar","Red Matter Katar","红物质拳剑",4,19,16777216),
        new("a7e000000000000000000101","tablet","Portable Transmutation Tablet","便携转化桌",0,-1,262144),
        new("a7e000000000000000000102","wireless_terminal","AE2 Wireless Terminal","AE2 无线访问终端",0,-1,65536),
        new("a7e000000000000000000103","totem","Totem of Undying","不死图腾",0,-1,131072)
    ];
    public static Sword? Find(string id)=>Array.Find(Renderable,s=>s.Id==id);
    public static bool IsUtility(string id)=>Find(id)?.SwordBonus==-1;
}
