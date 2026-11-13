using EFT;
using LocalAiCombat;

int checks=0;
void Check(bool value,string message) { if(!value)throw new Exception(message); checks++; }
foreach(bool sameSquad in new[]{true,false}) {
    var owner=new BotOwner(); owner.Memory.RejectSquad=sameSquad;
    var target=new Player();
    var shared=NativeEnemyMemory.EnsureGroup(owner.BotsGroup,target);
    Check(ReferenceEquals(shared,NativeEnemyMemory.EnsureGroup(owner.BotsGroup,target)),"Group enemy is reused");
    var info=NativeEnemyMemory.EnsurePersonal(owner,target,shared);
    Check(ReferenceEquals(owner.EnemiesController.EnemyInfos[target],info),"Enemy is registered even when the native memory entry point rejects squadmates");
    Check(owner.EnemiesController.SortedInfos.Count==1&&ReferenceEquals(owner.EnemiesController.SortedInfos[0],info),"Enemy participates in native observation, not only dictionary lookup");
    Check(!info.Ignored&&!info.IgnoreUntilAggression&&info.PersonalLastPos==target.Position,"Commanded enemy is eligible and has a search position");
    for(int i=0;i<20;i++)Check(ReferenceEquals(info,NativeEnemyMemory.EnsurePersonal(owner,target,shared)),"Repeated ticks reuse the observation entry");
    Check(owner.EnemiesController.Created==1&&owner.EnemiesController.Registered==1,"No duplicate native registration");
    var replacement=new BotGroupEnemyInfo(target,owner.BotsGroup);
    NativeEnemyMemory.EnsurePersonal(owner,target,replacement);
    Check(ReferenceEquals(info.GroupInfo,replacement),"Updated shared registry propagates to existing memory");
    NativeEnemyMemory.Command(owner,info); NativeEnemyMemory.Command(owner,info);
    Check(owner.Memory.AttackImmediately&&!owner.Memory.IsPeace&&owner.Memory.Assignments==1,"Repeated commands do not restart aiming");
}
var rejected=new BotOwner(); rejected.EnemiesController.RejectRegistration=true;
bool failedClearly=false;
try { var target=new Player(); NativeEnemyMemory.EnsurePersonal(rejected,target,NativeEnemyMemory.EnsureGroup(rejected.BotsGroup,target)); }
catch(InvalidOperationException) { failedClearly=true; }
Check(failedClearly,"Failed registration is diagnosed before indexing missing memory");
var shooter=new BotOwner(); var victim=new BotOwner();
var miss=new BotEnemiesMissController{_botEnemies=shooter.EnemiesController};
bool forced=true,result=false;
ForcedCombatDamage.Install(new HarmonyLib.Harmony(),(_,_)=>forced);
Check(!ForcedCombatDamage.Before(miss,victim.GetPlayer,ref result)&&result,"Forced AI collisions continue to native armor and health");
forced=false;result=false;
Check(ForcedCombatDamage.Before(miss,victim.GetPlayer,ref result)&&!result,"Ordinary friendly AI keep their native miss policy");
forced=true;victim.GetPlayer.IsYourPlayer=true;
Check(ForcedCombatDamage.Before(miss,victim.GetPlayer,ref result),"Local human target cannot bypass miss policy");
victim.GetPlayer.IsYourPlayer=false;victim.GetPlayer.IsAI=false;
Check(ForcedCombatDamage.Before(miss,victim.GetPlayer,ref result),"Remote human target cannot bypass miss policy");
victim.GetPlayer.IsAI=true;shooter.GetPlayer.IsYourPlayer=true;
Check(ForcedCombatDamage.Before(miss,victim.GetPlayer,ref result),"Local human attacker remains unchanged");
shooter.GetPlayer.IsYourPlayer=false;
Check(ForcedCombatDamage.Before(miss,shooter.GetPlayer,ref result),"Self damage is not forced combat");
victim.GetPlayer.AIData=null;
Check(ForcedCombatDamage.Before(miss,victim.GetPlayer,ref result),"Unowned AI target remains unchanged");
var random=new Random(42);
for(int i=0;i<10000;i++) {
    var original=new UnityEngine.Color32((byte)random.Next(256),(byte)random.Next(256),(byte)random.Next(256),(byte)random.Next(256));
    var pixels=new[]{original};LocalQuickhacks.BlackwallPixels.Normalize(pixels);var actual=pixels[0];
    Check(Math.Abs(actual.r*actual.a/255f-original.r*original.a/255f)<=2&&Math.Abs(actual.g*actual.a/255f-original.g*original.a/255f)<=2&&Math.Abs(actual.b*actual.a/255f-original.b*original.a/255f)<=2,"Atlas conversion preserves premultiplied color within byte rounding");
}
var black=new[]{new UnityEngine.Color32(0,0,0,255)};LocalQuickhacks.BlackwallPixels.Normalize(black);
Check(black[0].a==0,"Black atlas background becomes transparent");
Console.WriteLine($"PASS: {checks} combat/atlas regression checks (native contract model, not an in-game test)");
