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
    public static Sword? Find(string id)=>Array.Find(All,s=>s.Id==id);
}
