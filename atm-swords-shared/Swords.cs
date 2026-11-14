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
        new("a7e000000000000000000004","quantum","MI Quantum Sword","MI 量子剑",0,999999998,16777216),
        new("a7e000000000000000000101","tablet","Portable Transmutation Tablet","便携转化桌",0,-1,262144),
        new("a7e000000000000000000102","wireless_terminal","AE2 Wireless Terminal","AE2 无线访问终端",0,-1,65536),
        new("a7e000000000000000000103","totem","Totem of Undying","不死图腾",0,-1,131072),
        new("a7e000000000000000000104","white_bag","White Alchemical Bag","白色炼金术之袋",0,-1,1032),
        new("a7e000000000000000000110","body_stone","Body Stone","身之宝石",0,-1,131072),
        new("a7e000000000000000000111","soul_stone","Soul Stone","灵魂宝石",0,-1,131072),
        new("a7e000000000000000000112","life_stone","Life Stone","生命宝石",0,-1,262144),
        new("a7e000000000000000000113","time_watch","Watch of Flowing Time","时间洪流怀表",0,-1,568320),
        new("a7e000000000000000000114","repair_talisman","Talisman of Repair","修复护符",0,-1,4660),
        new("a7e000000000000000000301","orange_bag","Orange Alchemical Bag","橙色炼金术之袋",0,-1,1032),
        new("a7e000000000000000000302","magenta_bag","Magenta Alchemical Bag","品红色炼金术之袋",0,-1,1032),
        new("a7e000000000000000000303","light_blue_bag","Light Blue Alchemical Bag","淡蓝色炼金术之袋",0,-1,1032),
        new("a7e000000000000000000304","yellow_bag","Yellow Alchemical Bag","黄色炼金术之袋",0,-1,1032),
        new("a7e000000000000000000305","lime_bag","Lime Alchemical Bag","黄绿色炼金术之袋",0,-1,1032),
        new("a7e000000000000000000306","pink_bag","Pink Alchemical Bag","粉红色炼金术之袋",0,-1,1032),
        new("a7e000000000000000000307","gray_bag","Gray Alchemical Bag","灰色炼金术之袋",0,-1,1032),
        new("a7e000000000000000000308","light_gray_bag","Light Gray Alchemical Bag","淡灰色炼金术之袋",0,-1,1032),
        new("a7e000000000000000000309","cyan_bag","Cyan Alchemical Bag","青色炼金术之袋",0,-1,1032),
        new("a7e000000000000000000310","purple_bag","Purple Alchemical Bag","紫色炼金术之袋",0,-1,1032),
        new("a7e000000000000000000311","blue_bag","Blue Alchemical Bag","蓝色炼金术之袋",0,-1,1032),
        new("a7e000000000000000000312","brown_bag","Brown Alchemical Bag","棕色炼金术之袋",0,-1,1032),
        new("a7e000000000000000000313","green_bag","Green Alchemical Bag","绿色炼金术之袋",0,-1,1032),
        new("a7e000000000000000000314","red_bag","Red Alchemical Bag","红色炼金术之袋",0,-1,1032),
        new("a7e000000000000000000315","black_bag","Black Alchemical Bag","黑色炼金术之袋",0,-1,1032)
    ];
    public static Sword? Find(string id)=>Array.Find(Renderable,s=>s.Id==id);
    public static bool Generic(string id)=>IsUtility(id)&&id is not ("a7e000000000000000000101" or "a7e000000000000000000102");
    public static bool Original(string id)=>id is BaseTemplate or "590c621186f774138d11ea29" or "590c60fc86f77412b13fddcf";
    public static bool IsUtility(string id)=>Find(id)?.SwordBonus==-1;
}
