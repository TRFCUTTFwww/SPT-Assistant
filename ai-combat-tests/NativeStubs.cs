// Model the verified EFT 48558 contract: AddNew only constructs; SetInfo
// registers both the dictionary and the sorted observation collection.
namespace EFT;
internal interface IPlayer { }
internal class AIData { public BotOwner? BotOwner; }
internal class Player:IPlayer { static int next; public IntPtr Pointer=new(++next); public int Position=7; public bool IsAI=true,IsYourPlayer; public AIData? AIData; }
internal enum EBodyPart { Head }
internal class BotEnemiesMissController { public BotEnemiesController _botEnemies=null!; public bool ShouldApplyDamage(Player target,EFT.Ballistics.DamageInfo damage,EBodyPart part)=>false; }
internal class BotsGroup { public BotGroupHostility BotGroupHostility=new(); }
internal class BotGroupHostility {
    public Dictionary<Player,BotGroupEnemyInfo> Enemies=new();
    public void AddEnemyAction(Player target,BotGroupEnemyInfo info)=>Enemies.Add(target,info);
}
internal class BotGroupEnemyInfo(Player target,BotsGroup group) {
    static int next;
    public Player Target=target;
    public BotsGroup Group=group;
    public IntPtr Pointer=new(++next);
    public int EnemyLastPosition;
}
internal class EnemyInfo {
    static int next;
    public IntPtr Pointer=new(++next);
    public bool IgnoreUntilAggression=true,Ignored=true;
    public BotGroupEnemyInfo GroupInfo=null!;
    public int PersonalLastPos;
    public void SetIgnoreState(bool value)=>Ignored=value;
}
internal class BotEnemiesController {
    public BotOwner? _owner;
    public Dictionary<Player,EnemyInfo> EnemyInfos=new();
    public List<EnemyInfo> SortedInfos=new();
    public int Created,Registered;
    public bool RejectRegistration;
    public EnemyInfo AddNew(BotsGroup group,Player target,BotGroupEnemyInfo shared) { Created++; return new(){GroupInfo=shared}; }
    public void SetInfo(Player target,EnemyInfo info) { Registered++; if(RejectRegistration)return; EnemyInfos.Add(target,info); SortedInfos.Add(info); }
}
internal class BotMemory(BotOwner owner) {
    public bool RejectSquad=true,IsPeace=true,AttackImmediately;
    public int Assignments;
    EnemyInfo? goal;
    public EnemyInfo? GoalEnemy { get=>goal; set { goal=value; Assignments++; } }
    public void AddEnemyInfo(Player target,BotGroupEnemyInfo shared,bool activation) {
        if(RejectSquad)return;
        owner.EnemiesController.SetInfo(target,owner.EnemiesController.AddNew(owner.BotsGroup,target,shared));
    }
}
internal class BotOwner {
    public Player GetPlayer=new();
    public BotsGroup BotsGroup=new();
    public BotEnemiesController EnemiesController=new();
    public BotMemory Memory;
    public BotOwner(){Memory=new(this);EnemiesController._owner=this;GetPlayer.AIData=new(){BotOwner=this};}
}
