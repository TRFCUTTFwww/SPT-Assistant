using Comfort.Common;
using EFT;
using EFT.Ballistics;
using HarmonyLib;
using UnityEngine;

namespace LocalQuickhacks;

internal static class AiHacks {
    sealed class Madness {
        internal ActorRef Ref=null!;
        internal BotOwner Owner=null!;
        internal float Until;
        internal bool Attack,Peace;
        internal EnemyInfo? Goal,Last;
        internal readonly List<(Player Target,IntPtr Info)> Created=[];
    }
    sealed class Chain {
        internal ActorRef Next=null!;
        internal IntPtr World;
        internal float At;
        internal readonly HashSet<string> Visited=[];
    }
    static readonly Dictionary<IntPtr,Madness> madness=[];
    static readonly Dictionary<IntPtr,(ActorRef Ref,float Until)> blind=[];
    static readonly Dictionary<IntPtr,(ActorRef Ref,float Until)> jammed=[],wiped=[];
    sealed class MovementLock {
        internal ActorRef Ref=null!;
        internal BotMover Mover=null!;
        internal bool Pause;
        internal float PauseUntil,Until;
    }
    sealed record Backup(ActorRef Caller,ActorRef Recipient,GoToPointRequest Request,float Until);
    static readonly Dictionary<IntPtr,MovementLock> movement=[];
    static readonly List<Backup> backups=[];
    static readonly List<Chain> chains=[];
    [ThreadStatic] static BotOwner? evaluating;
    static float nextMadness;
    internal static int ActiveCount=>madness.Count+blind.Count+jammed.Count+wiped.Count+movement.Count+backups.Count+chains.Count;
    internal static void Install(Harmony h) {
        void Patch(Type t,string method,string? before=null,string? after=null,string? final=null) {
            var m=AccessTools.Method(t,method); if(m==null) return;
            h.Patch(m,prefix:before==null?null:new HarmonyMethod(typeof(AiHacks),before),postfix:after==null?null:new HarmonyMethod(typeof(AiHacks),after),finalizer:final==null?null:new HarmonyMethod(typeof(AiHacks),final));
        }
        Patch(typeof(BotEnemiesController),"IsEnemy",after:nameof(IsEnemy));
        Patch(typeof(BotsGroup),"IsEnemy",after:nameof(GroupEnemy));
        Patch(typeof(BotsGroup),"IsAlly",after:nameof(GroupAlly));
        Patch(typeof(EnemyInfo),"CheckLookEnemy",before:nameof(Vision));
        foreach(string m in new[]{"ShallKnowEnemy","ShallKnowEnemyLate"}) Patch(typeof(EnemyInfo),m,after:nameof(Know));
        // Scope same-squad overrides to the currently evaluating bot.
        Patch(typeof(BotOwner),"UpdateManual",before:nameof(BeforeOwner),final:nameof(AfterOwner));
        h.Patch(AccessTools.Method(typeof(Player.FirearmController),"SetTriggerPressed",[typeof(bool)]),prefix:new HarmonyMethod(typeof(AiHacks),nameof(AllowTrigger)));
        Patch(typeof(Player.FirearmController),"CanPressTrigger",before:nameof(CanFire));
        foreach(string m in new[]{"ManualUpdate","ManualFixedUpdate"}) Patch(typeof(BotMover),m,before:nameof(KeepMovementLocked));
    }
    static bool Mad(BotOwner? owner)=>owner!=null&&madness.TryGetValue(owner.Pointer,out var m)&&m.Ref.Resolve()!=null&&Time.unscaledTime<m.Until;
    static bool IsAi(IPlayer? player)=>player!=null&&!player.IsYourPlayer&&player.AIData?.BotOwner!=null;
    static bool MadTarget(IPlayer? target)=>target!=null&&Mad(target.AIData?.BotOwner);
    static void IsEnemy(BotEnemiesController __instance,IPlayer __0,ref bool __result) { if(IsAi(__0)&&(Mad(__instance._owner)||MadTarget(__0))) __result=true; }
    static bool GroupRule(BotsGroup group,IPlayer target)=>IsAi(target)&&(MadTarget(target)||evaluating?.BotsGroup?.Pointer==group.Pointer&&Mad(evaluating));
    static void GroupEnemy(BotsGroup __instance,IPlayer __0,ref bool __result) { if(GroupRule(__instance,__0)) __result=true; }
    static void GroupAlly(BotsGroup __instance,IPlayer __0,ref bool __result) { if(GroupRule(__instance,__0)) __result=false; }
    static void Know(EnemyInfo __instance,ref bool __result) { if(Timed(wiped,__instance.Owner)) __result=false; else if(IsAi(__instance.Person)&&Mad(__instance.Owner)) __result=true; }
    static bool Timed(Dictionary<IntPtr,(ActorRef Ref,float Until)> effects,BotOwner? owner)=>owner!=null&&effects.TryGetValue(owner.Pointer,out var state)&&Time.unscaledTime<state.Until&&state.Ref.Resolve()!=null;
    static bool CannotFire(Player? actor)=>actor!=null&&!actor.IsYourPlayer&&(Timed(jammed,actor.AIData?.BotOwner)||Timed(wiped,actor.AIData?.BotOwner));
    static bool AllowTrigger(Player.FirearmController __instance,bool __0)=>!__0||!CannotFire(__instance._player);
    static bool CanFire(Player.FirearmController __instance,ref bool __result) { if(!CannotFire(__instance._player)) return true; __result=false; return false; }
    static void KeepMovementLocked(BotMover __instance) {
        foreach(var state in movement.Values) if(state.Mover.Pointer==__instance.Pointer&&Time.unscaledTime<state.Until&&state.Ref.Resolve()!=null) { __instance.Pause=true; __instance._pauseEndTime=float.MaxValue; break; }
    }
    static void BeforeOwner(BotOwner __instance,out BotOwner? __state) { __state=evaluating; evaluating=__instance; }
    static void AfterOwner(BotOwner? __state) { evaluating=__state; }
    static bool Vision(EnemyInfo __instance) {
        var owner=__instance.Owner;
        if(!Timed(blind,owner)&&!Timed(wiped,owner)) return true;
        __instance.SetVisible(false); __instance.SetCanShoot(false); return false;
    }
    internal static string Apply(ActorRef reference,HackKind kind) {
        var actor=reference.Resolve()??throw new InvalidOperationException("破解目标已离开。");
        var owner=actor.AIData?.BotOwner??throw new InvalidOperationException("目标没有本地 AI 控制器。");
        string result=HackRules.Name(kind)+" · 已上传";
        if(kind==HackKind.RebootOptics) {
            blind[owner.Pointer]=(reference,Time.unscaledTime+10);
            owner.Memory?.LoseVisionCurrentEnemy();
            if(owner.EnemiesController?.EnemyInfos!=null) foreach(var pair in owner.EnemiesController.EnemyInfos) pair.Value.SetVisible(false);
        } else if(kind==HackKind.WeaponGlitch) {
            jammed[owner.Pointer]=(reference,Time.unscaledTime+HackRules.Duration(kind));
            actor.HandsController?.TryCast<Player.FirearmController>()?.SetTriggerPressed(false);
        } else if(kind==HackKind.CrippleMovement) {
            var mover=owner.Mover??throw new InvalidOperationException("目标移动控制器未就绪。");
            if(!movement.TryGetValue(owner.Pointer,out var state)) movement[owner.Pointer]=state=new MovementLock { Ref=reference,Mover=mover,Pause=mover.Pause,PauseUntil=mover._pauseEndTime };
            state.Until=Time.unscaledTime+HackRules.Duration(kind); owner.StopMove(); mover.Pause=true; mover._pauseEndTime=float.MaxValue;
        } else if(kind==HackKind.MemoryWipe) {
            if(madness.Remove(owner.Pointer,out var active)) { Restore(active); Effects.Forget(reference,HackKind.Cyberpsychosis); }
            wiped[owner.Pointer]=(reference,Time.unscaledTime+HackRules.Duration(kind)); Forget(owner);
        } else if(kind==HackKind.RequestBackup) {
            result=RequestBackup(reference,actor,owner);
        } else if(kind==HackKind.Cyberpsychosis) {
            if(wiped.Remove(owner.Pointer)) Effects.Forget(reference,HackKind.MemoryWipe);
            if(madness.TryGetValue(owner.Pointer,out var active)) active.Until=Time.unscaledTime+20;
            else madness[owner.Pointer]=new Madness { Ref=reference,Owner=owner,Until=Time.unscaledTime+20,Attack=owner.Memory.AttackImmediately,Peace=owner.Memory.IsPeace,Goal=owner.Memory.GoalEnemy };
            nextMadness=0;
        } else {
            chains.Add(new Chain { Next=reference,World=reference.World,At=Time.unscaledTime+.5f });
        }
        Assets.Play(HackRules.Icon(kind)); Effects.Target(actor,kind); return result;
    }
    static void Forget(BotOwner owner) {
        var memory=owner.Memory; if(memory==null) return;
        owner.GetPlayer?.HandsController?.TryCast<Player.FirearmController>()?.SetTriggerPressed(false);
        memory.LoseVisionCurrentEnemy(); memory.GoalEnemy=null!; memory.LastEnemy=null!;
        if(owner.EnemiesController?.EnemyInfos is {} infos) {
            var targets=new List<IPlayer>(); foreach(var pair in infos) targets.Add(pair.Key);
            foreach(var target in targets) { memory.DeleteInfoAboutEnemy(target); if(infos.ContainsKey(target)) owner.EnemiesController.Remove(target); }
        }
        memory.IsPeace=true;
    }
    static string RequestBackup(ActorRef reference,Player actor,BotOwner owner) {
        if(backups.Any(b=>b.Caller==reference)) throw new InvalidOperationException("目标已发出支援请求。");
        var group=owner.BotsGroup?.RequestsController??throw new InvalidOperationException("目标没有可用的同队支援系统。");
        var mates=Bots().Where(p=>p.Pointer!=actor.Pointer&&p.AIData.BotOwner.BotsGroup?.Pointer==owner.BotsGroup.Pointer&&Vector3.Distance(p.Position,actor.Position)<=40)
            .Where(p=>p.AIData.BotOwner.Memory?.GoalEnemy==null&&p.AIData.BotOwner.BotRequestController?.CurRequest==null&&!movement.ContainsKey(p.AIData.BotOwner.Pointer)&&!Mad(p.AIData.BotOwner))
            .OrderBy(p=>(p.Position-actor.Position).sqrMagnitude).ToArray();
        foreach(var mate in mates) {
            var request=new GoToPointRequest(actor,actor.Position,null,null);
            if(!group.TryAddRequestByPlayer(actor,request,mate.AIData.BotOwner)) { request.Dispose(); continue; }
            backups.Add(new Backup(reference,ActorRef.Of(mate),request,Time.unscaledTime+15));
            return "请求支援 · 已呼叫 "+mate.Profile.Nickname;
        }
        throw new InvalidOperationException("附近没有可响应的空闲同队 AI。");
    }
    static void ReleaseMovement(MovementLock state) {
        try { if(state.Ref.Resolve()==null||state.Mover._pauseEndTime!=float.MaxValue) return; state.Mover._pauseEndTime=state.PauseUntil; state.Mover.Pause=state.Pause&&state.PauseUntil>Time.time; }
        catch(Exception e) { Runtime.Warn("Restore movement",e); }
    }
    static void EndBackup(Backup state) {
        try { if(state.Recipient.Resolve()!=null&&!state.Request.Removed) state.Request.Complete(); }
        catch(Exception e) { Runtime.Warn("End backup",e); }
        Effects.Forget(state.Caller,HackKind.RequestBackup);
    }
    internal static void Tick() {
        foreach(var p in blind.ToArray()) if(Time.unscaledTime>=p.Value.Until||p.Value.Ref.Resolve()==null) blind.Remove(p.Key);
        foreach(var effects in new[]{jammed,wiped}) foreach(var p in effects.ToArray()) if(Time.unscaledTime>=p.Value.Until||p.Value.Ref.Resolve()==null) effects.Remove(p.Key);
        foreach(var state in wiped.Values) if(state.Ref.Resolve()?.AIData?.BotOwner is {} owner) Forget(owner);
        foreach(var p in movement.ToArray()) {
            if(Time.unscaledTime>=p.Value.Until||p.Value.Ref.Resolve()==null) { ReleaseMovement(p.Value); movement.Remove(p.Key); }
            else KeepMovementLocked(p.Value.Mover);
        }
        foreach(var state in backups.ToArray()) if(Time.unscaledTime>=state.Until||state.Caller.Resolve()==null||state.Recipient.Resolve()==null||state.Request.Removed) { EndBackup(state); backups.Remove(state); }
        foreach(var p in madness.ToArray()) if(Time.unscaledTime>=p.Value.Until||p.Value.Ref.Resolve()==null) { Restore(p.Value); madness.Remove(p.Key); }
        if(Time.unscaledTime>=nextMadness) {
            nextMadness=Time.unscaledTime+.3f;
            foreach(var state in madness.Values.ToArray()) try { Direct(state); } catch(Exception e) { Runtime.Warn("Cyberpsychosis",e); }
        }
        foreach(var chain in chains.ToArray()) if(Time.unscaledTime>=chain.At) {
            try { if(!Step(chain)) chains.Remove(chain); } catch(Exception e) { chains.Remove(chain); Runtime.Warn("Gateway",e); }
        }
    }
    static IEnumerable<Player> Bots() {
        var list=Singleton<GameWorld>.Instance?.AllAlivePlayersList;
        if(list==null) yield break;
        for(int i=0;i<list.Count;i++) { var p=list[i]; if(p!=null&&!p.IsYourPlayer&&p.IsAI&&p.HealthController?.IsAlive==true&&p.AIData?.BotOwner!=null) yield return p; }
    }
    static void Direct(Madness state) {
        var actor=state.Ref.Resolve(); var owner=state.Owner;
        if(actor==null||owner.Memory==null||owner.BotsGroup==null||owner.EnemiesController==null) return;
        var target=Bots().Where(p=>p.Pointer!=actor.Pointer).OrderBy(p=>(p.Position-actor.Position).sqrMagnitude).FirstOrDefault();
        if(target==null||Vector3.Distance(target.Position,actor.Position)>80) return;
        var previous=evaluating; evaluating=owner;
        try {
            EnemyInfo info;
            if(owner.EnemiesController.EnemyInfos.ContainsKey(target)) info=owner.EnemiesController.EnemyInfos[target];
            else {
                info=owner.EnemiesController.AddNew(owner.BotsGroup,target,new BotGroupEnemyInfo(target,owner.BotsGroup));
                state.Created.Add((target,info.Pointer));
            }
            info.SetIgnoreState(false); info.IgnoreUntilAggression=false;
            info.PersonalLastPos=target.Position;
            owner.Memory.IsPeace=false; owner.Memory.AttackImmediately=true; owner.Memory.GoalEnemy=info; state.Last=info;
        } finally { evaluating=previous; }
    }
    static void Restore(Madness state) {
        try {
            if(state.Ref.Resolve()==null) return;
            var owner=state.Owner;
            if(owner.Memory is {} memory&&state.Last!=null&&memory.GoalEnemy?.Pointer==state.Last.Pointer) memory.GoalEnemy=state.Goal!;
            if(owner.Memory!=null) { owner.Memory.AttackImmediately=state.Attack; if(state.Peace&&owner.Memory.GoalEnemy==null) owner.Memory.IsPeace=true; }
            foreach(var entry in state.Created) if(entry.Target!=null&&owner.EnemiesController?.EnemyInfos?.ContainsKey(entry.Target)==true&&owner.EnemiesController.EnemyInfos[entry.Target]?.Pointer==entry.Info) owner.EnemiesController.Remove(entry.Target);
        } catch(Exception e) { Runtime.Warn("Restore cyberpsychosis",e); }
    }
    static bool Protected(Player actor) {
        // Optional coexistence with the Assistant's explicit AI invincibility.
        foreach(var asm in AppDomain.CurrentDomain.GetAssemblies()) {
            var type=asm.GetType("LocalSpt5Esp.Plugin"); if(type==null) continue;
            var method=AccessTools.Method(type,"DamageBlocked");
            if(method!=null) try { if(method.Invoke(null,[actor.ActiveHealthController]) is true) return true; } catch { return true; }
        }
        return false;
    }
    static bool Step(Chain chain) {
        var world=Singleton<GameWorld>.Instance;
        var actor=chain.Next.Resolve(); var local=world?.MainPlayer;
        if(world?.Pointer!=chain.World||actor==null||local?.HealthController?.IsAlive!=true||Protected(actor)||!chain.Visited.Add(actor.ProfileId)) return false;
        var origin=actor.PlayerBones?.Ribcage?.Original?.position??actor.Position+Vector3.up;
        Effects.Target(actor,HackKind.BlackwallGateway);
        float damage=Math.Max(10000,actor.ActiveHealthController.GetBodyPartHealth(EBodyPart.Common,false).Current*10);
        var info=new DamageInfo { DamageType=EDamageType.Bullet,Damage=damage,Player=new PlayerBridge(local).Cast<IObserverToPlayerBridge>(),MasterOrigin=local.Position,HitPoint=origin,Direction=(origin-local.Position).normalized,SourceId="cp2077-quickhack" };
        actor.ActiveHealthController.ApplyDamage(EBodyPart.Head,damage,info);
        if(actor.HealthController.IsAlive) return false;
        Assets.Play("death");
        if(chain.Visited.Count>=5) return false;
        int mask=LayerMask.GetMask("HighPolyCollider","Terrain","LowPolyCollider"); if(mask==0) return false;
        var next=Bots().Where(p=>!chain.Visited.Contains(p.ProfileId)&&Vector3.Distance(p.Position,origin)<8).OrderBy(p=>(p.Position-origin).sqrMagnitude).FirstOrDefault(p=>{
            var delta=(p.PlayerBones?.Ribcage?.Original?.position??p.Position+Vector3.up)-origin;
            return !Physics.Raycast(origin,delta.normalized,Math.Max(0,delta.magnitude-.2f),mask,QueryTriggerInteraction.Ignore);
        });
        if(next==null) return false;
        chain.Next=ActorRef.Of(next); chain.At=Time.unscaledTime+.9f;
        return true;
    }
    internal static void Reset() {
        foreach(var state in madness.Values.ToArray()) Restore(state);
        foreach(var state in movement.Values.ToArray()) ReleaseMovement(state);
        foreach(var state in backups.ToArray()) EndBackup(state);
        madness.Clear(); blind.Clear(); jammed.Clear(); wiped.Clear(); movement.Clear(); backups.Clear(); chains.Clear(); evaluating=null;
        Effects.ForgetAll();
    }
}
