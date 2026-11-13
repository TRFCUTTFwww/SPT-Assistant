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
Console.WriteLine($"PASS: {checks} enemy-memory regression checks (native contract model, not an in-game test)");
