namespace ProjectE;
public sealed record Sword(string Id,string Key,string English,string Chinese,int TierBonus,int SwordBonus,int Price) {
 public int Damage=>1+TierBonus+SwordBonus;
}
public static class Swords {
 public const string BaseTemplate="664a5428d5e33a713b622379",KnifeParent="5447e1d04bdc2dff2f8b4567";
 public const string Quantum="a7e000000000000000000004";
 public static readonly Sword[] All=[
  new("a7e000000000000000000001","dm","Dark Matter Sword","暗物质剑",3,9,131072),
  new("a7e000000000000000000002","rm","Red Matter Sword","红物质剑",4,12,786432),
  new("a7e000000000000000000003","katar","Red Matter Katar","红物质拳剑",4,19,16777216),
  new(Quantum,"quantum","MI Quantum Sword","MI 量子剑",0,999999998,16777216)
 ];
 public static Sword? Find(string id)=>Array.Find(All,s=>s.Id==id);
}
public sealed record Gadget(string Id,string Key,string English,string Chinese,int Price);
public static class Gadgets {
 public const string Tablet="a7e000000000000000000101",Terminal="a7e000000000000000000102",Totem="a7e000000000000000000103",Bag="a7e000000000000000000104";
 public const string Body="a7e000000000000000000110",Soul="a7e000000000000000000111",Life="a7e000000000000000000112",Watch="a7e000000000000000000113",Repair="a7e000000000000000000114";
 public const string PassiveBase="590c621186f774138d11ea29",BagBase="590c60fc86f77412b13fddcf";
 public static readonly HashSet<string> BagIds=[Bag,"a7e000000000000000000301","a7e000000000000000000302","a7e000000000000000000303","a7e000000000000000000304","a7e000000000000000000305","a7e000000000000000000306","a7e000000000000000000307","a7e000000000000000000308","a7e000000000000000000309","a7e000000000000000000310","a7e000000000000000000311","a7e000000000000000000312","a7e000000000000000000313","a7e000000000000000000314","a7e000000000000000000315"];
 public static bool IsBag(string id)=>BagIds.Contains(id);
 public static readonly Gadget[] All=[new(Tablet,"tablet","Portable Transmutation Tablet","便携转化桌",262144),new(Terminal,"wireless_terminal","AE2 Wireless Terminal","AE2 无线访问终端",65536),new(Totem,"totem","Totem of Undying","不死图腾",131072),new(Bag,"white_bag","White Alchemical Bag","白色炼金术之袋",1032),new(Body,"body_stone","Body Stone","身之宝石",131072),new(Soul,"soul_stone","Soul Stone","灵魂宝石",131072),new(Life,"life_stone","Life Stone","生命宝石",262144),new(Watch,"time_watch","Watch of Flowing Time","时间洪流怀表",568320),new(Repair,"repair_talisman","Talisman of Repair","修复护符",4660),new("a7e000000000000000000301","orange_bag","Orange Alchemical Bag","橙色炼金术之袋",1032),new("a7e000000000000000000302","magenta_bag","Magenta Alchemical Bag","品红色炼金术之袋",1032),new("a7e000000000000000000303","light_blue_bag","Light Blue Alchemical Bag","淡蓝色炼金术之袋",1032),new("a7e000000000000000000304","yellow_bag","Yellow Alchemical Bag","黄色炼金术之袋",1032),new("a7e000000000000000000305","lime_bag","Lime Alchemical Bag","黄绿色炼金术之袋",1032),new("a7e000000000000000000306","pink_bag","Pink Alchemical Bag","粉红色炼金术之袋",1032),new("a7e000000000000000000307","gray_bag","Gray Alchemical Bag","灰色炼金术之袋",1032),new("a7e000000000000000000308","light_gray_bag","Light Gray Alchemical Bag","淡灰色炼金术之袋",1032),new("a7e000000000000000000309","cyan_bag","Cyan Alchemical Bag","青色炼金术之袋",1032),new("a7e000000000000000000310","purple_bag","Purple Alchemical Bag","紫色炼金术之袋",1032),new("a7e000000000000000000311","blue_bag","Blue Alchemical Bag","蓝色炼金术之袋",1032),new("a7e000000000000000000312","brown_bag","Brown Alchemical Bag","棕色炼金术之袋",1032),new("a7e000000000000000000313","green_bag","Green Alchemical Bag","绿色炼金术之袋",1032),new("a7e000000000000000000314","red_bag","Red Alchemical Bag","红色炼金术之袋",1032),new("a7e000000000000000000315","black_bag","Black Alchemical Bag","黑色炼金术之袋",1032)];
 public static Gadget? Find(string id)=>Array.Find(All,x=>x.Id==id);
 public static bool Handheld(string id)=>id is Tablet or Terminal or Watch;
 public static string Description(string id)=>IsBag(id)?"原生独立容器，外部 1×1、内部 13×8。每个袋子各有自己的空间，同色也不共享，禁止袋子互套。":id switch {
  Tablet=>"装备近战槽并手持右键，打开转化桌。",
  Terminal=>"装备近战槽并手持右键，打开独立无线战局仓库，无需辅助 MOD。",
  Totem=>"随身携带即可抵挡一次致命伤并消耗一枚，保留当前武器，触发图腾动画、再生及火焰抵抗。放在大厅仓库不生效。",
  Bag=>"白色炼金术之袋，外部 1×1 格，内部 13×8 格。每个袋子独立保存内容，双击或右键打开；不可套袋。",
  Body=>"随身携带，在缺少能量时每秒消耗 64 EMC，恢复 2 点能量。",
  Soul=>"随身携带，在受伤时每秒消耗 64 EMC，恢复 2 点生命，不复活损毁部位。",
  Life=>"随身携带，兼具身之宝石与灵魂宝石，每个实际恢复效果消耗 64 EMC；同类效果不叠加。",
  Watch=>"可放入近战槽并拿在手上，也可随身携带。消耗转化桌 EMC，在 8 米内减慢 AI 水平移动；充能 0/1/2 级分别为原速的 25%/16%/12%，每秒消耗 20/30/40 EMC。可在 ProjectE 配置中关闭或调级。",
  Repair=>"随身携带，每秒为身上及随身容器中的可维修装备恢复 1 点当前耐久，不提高耐久上限，不消耗 EMC。",
  _=>""
 };
}
public static class MatterRules {
 public static int MaxCharge(string template)=>Swords.Find(template) is {} sword?sword.Key=="quantum"?0:sword.Key=="dm"?2:sword.Key=="katar"?4:3:0;
 public static int Step(int charge,int maximum,bool decrease)=>Math.Clamp(charge+(decrease?-1:1),0,Math.Max(0,maximum));
 public static float Radius(int charge)=>2.5f*Math.Clamp(charge,0,4);
 public static float MeleeBonus(int charge)=>Math.Clamp(charge,0,4);
 public static bool EligibleTarget(bool local,bool human,bool alive,bool friendly,bool all)=>!local&&!human&&alive&&(all||!friendly);
 public static bool CanSave(bool raid,bool local,bool carried,bool consumed,bool alive)=>raid&&local&&carried&&!consumed&&alive;
 public static float RevivalHealth(float maximum)=>Math.Clamp(maximum*.2f,1,Math.Max(1,maximum));
}
