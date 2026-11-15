namespace Scp3X;
public static class DisruptorRules {
 public const string Weapon="a73c00000000000000000001",Magazine="a73c00000000000000000002",Ammo="a73c00000000000000000003";
 public const string DonorWeapon="66015072e9f84d5680039678",DonorMagazine="66015dc4aaad2f54cb04c56a",DonorAmmo="6601546f86889319850bd566";
 public const string RifleCategorySource="5447a9cd4bdc2dbd208b4567";
 public const int Capacity=5,WeaponPrice=500000,MagazinePrice=20000,AmmoPrice=10000;
 public const float SingleChargeSeconds=2.5f,BurstChargeSeconds=1f,RecoverySeconds=5.59f,DissolveSeconds=2.6f,BeamRange=300;
 public static readonly string[] Templates=[Weapon,Magazine,Ammo];
 public static bool IsWeapon(string? id)=>id==Weapon;
 public static bool ConfirmedKill(string? weapon,bool alive,bool ownVictim,string? aggressor,string? local)=>weapon==Weapon&&!alive&&!ownVictim&&!string.IsNullOrWhiteSpace(local)&&aggressor==local;
 public static bool CanFire(bool local,bool alive,bool inRaid,bool ready,bool blocked,bool equipped)=>local&&alive&&inRaid&&ready&&!blocked&&equipped;
 public static bool MayEraseCorpse(long expectedWorld,long currentWorld,string? expectedProfile,string? actualProfile,bool alive)=>expectedWorld!=0&&expectedWorld==currentWorld&&!string.IsNullOrWhiteSpace(expectedProfile)&&expectedProfile==actualProfile&&!alive;
 public static float DissolveProgress(float elapsed)=>float.IsFinite(elapsed)?Math.Clamp(elapsed/DissolveSeconds,0,1):1;
}
