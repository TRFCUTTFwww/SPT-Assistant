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
        internal float NextDiagnostic;
        internal readonly List<(Player Target,IntPtr Info)> Created=[];
    }
    sealed class Chain {
        internal ActorRef Next=null!;
        internal IntPtr World;
        internal float At;
        internal float Started;
        internal float Completed=-1;
        internal readonly HashSet<string> Visited=[];
    }
    static readonly Dictionary<IntPtr,Madness> madness=[];
    static readonly Dictionary<IntPtr,(ActorRef Ref,float Until)> blind=[];
    static readonly Dictionary<IntPtr,(ActorRef Ref,float Until)> jammed=[],wiped=[];
    sealed class Heat(ActorRef reference,float start) {
        internal ActorRef Ref=reference;
        internal float Started=start,Applied,Next;
    }
    static readonly Dictionary<IntPtr,Heat> heat=[];
    sealed class MovementLock {
        internal ActorRef Ref=null!;
        internal BotMover Mover=null!;
        internal bool Pause;
        internal float PauseUntil,Until;
    }
    static readonly Dictionary<IntPtr,MovementLock> movement=[];
    static readonly List<Chain> chains=[];
    [ThreadStatic] static BotOwner? evaluating;
    static float nextMadness;
    internal static int ActiveCount=>madness.Count+blind.Count+jammed.Count+wiped.Count+movement.Count+chains.Count+heat.Count;
    internal static IEnumerable<(ActorRef Ref,float Started,float At,float Completed)> GatewayUploads=>chains.Select(c=>(c.Next,c.Started,c.At,c.Completed));
    internal static bool HasGatewayUploads=>chains.Count>0;
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
        // Run after optional AI management prefixes that reject same-squad goals.
        h.Patch(AccessTools.PropertySetter(typeof(BotMemory),"GoalEnemy"),prefix:new HarmonyMethod(typeof(AiHacks),nameof(KeepMadGoal)) { priority=Priority.Last });
        h.Patch(AccessTools.Method(typeof(Player.FirearmController),"SetTriggerPressed",[typeof(bool)]),prefix:new HarmonyMethod(typeof(AiHacks),nameof(AllowTrigger)));
        // CanPressTrigger is an IL2CPP shared constant-return body in EFT 48558.
        // Detouring it also intercepts hundreds of unrelated object types.
        // SetTriggerPressed has the dedicated native entry point we need.
        foreach(string m in new[]{"ManualUpdate","ManualFixedUpdate"}) Patch(typeof(BotMover),m,before:nameof(KeepMovementLocked));
    }
    static bool Mad(BotOwner? owner)=>owner!=null&&madness.TryGetValue(owner.Pointer,out var m)&&m.Ref.Resolve()!=null&&Time.unscaledTime<m.Until;
    static bool IsAi(IPlayer? player)=>player!=null&&!player.IsYourPlayer&&player.AIData?.BotOwner!=null;
    static bool MadTarget(IPlayer? target)=>target!=null&&Mad(target.AIData?.BotOwner);
    internal static bool IsHostile(BotOwner? owner,IPlayer? target)=>IsAi(target)&&owner?.GetPlayer is {} actor&&!actor.IsYourPlayer&&actor.Pointer!=target!.Pointer&&(Mad(owner)||MadTarget(target));
    static void IsEnemy(BotEnemiesController __instance,IPlayer __0,ref bool __result) { if(IsHostile(__instance._owner,__0)) __result=true; }
    static bool GroupRule(BotsGroup group,IPlayer target)=>IsAi(target)&&target.Pointer!=evaluating?.GetPlayer?.Pointer&&(MadTarget(target)||evaluating?.BotsGroup?.Pointer==group.Pointer&&Mad(evaluating));
    static void GroupEnemy(BotsGroup __instance,IPlayer __0,ref bool __result) { if(GroupRule(__instance,__0)) __result=true; }
    static void GroupAlly(BotsGroup __instance,IPlayer __0,ref bool __result) { if(GroupRule(__instance,__0)) __result=false; }
    static void Know(EnemyInfo __instance,ref bool __result) { if(Timed(wiped,__instance.Owner)) __result=false; else if(IsHostile(__instance.Owner,__instance.Person)) __result=true; }
    static bool Timed(Dictionary<IntPtr,(ActorRef Ref,float Until)> effects,BotOwner? owner)=>owner!=null&&effects.TryGetValue(owner.Pointer,out var state)&&Time.unscaledTime<state.Until&&state.Ref.Resolve()!=null;
    static bool CannotFire(Player? actor)=>actor!=null&&!actor.IsYourPlayer&&(Timed(jammed,actor.AIData?.BotOwner)||Timed(wiped,actor.AIData?.BotOwner));
    static bool AllowTrigger(Player.FirearmController __instance,bool __0)=>!__0||!CannotFire(__instance._player);
    static void KeepMovementLocked(BotMover __instance) {
        foreach(var state in movement.Values) if(state.Mover.Pointer==__instance.Pointer&&Time.unscaledTime<state.Until&&state.Ref.Resolve()!=null) { __instance.Pause=true; __instance._pauseEndTime=float.MaxValue; break; }
    }
    static void BeforeOwner(BotOwner __instance,out BotOwner? __state) { __state=evaluating; evaluating=__instance; }
    static void AfterOwner(BotOwner? __state) { evaluating=__state; }
    static void KeepMadGoal(BotMemory __instance,ref EnemyInfo __0) {
        var owner=__instance._owner;
        if(!Mad(owner)||!madness.TryGetValue(owner!.Pointer,out var state)||state.Last?.Person?.TryCast<Player>() is not {} target||target.HealthController?.IsAlive!=true) return;
        __0=state.Last;
    }
    static bool Vision(EnemyInfo __instance) {
        var owner=__instance.Owner;
        if(!Timed(blind,owner)&&!Timed(wiped,owner)) return true;
        __instance.SetVisible(false); __instance.SetCanShoot(false); return false;
    }
    internal static string Apply(ActorRef reference,HackKind kind,float missingRam=0) {
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
        } else if(kind==HackKind.Cyberpsychosis) {
            if(wiped.Remove(owner.Pointer)) Effects.Forget(reference,HackKind.MemoryWipe);
            if(madness.TryGetValue(owner.Pointer,out var active)) active.Until=Time.unscaledTime+20;
            else madness[owner.Pointer]=new Madness { Ref=reference,Owner=owner,Until=Time.unscaledTime+20,Attack=owner.Memory.AttackImmediately,Peace=owner.Memory.IsPeace,Goal=owner.Memory.GoalEnemy };
            nextMadness=0;
        } else if(kind==HackKind.Overheat) {
            heat[actor.Pointer]=new(reference,Time.unscaledTime);
        } else if(kind==HackKind.SynapseBurnout) {
            Effects.Target(actor,kind,1);
            Hit(actor,EBodyPart.Chest,CombatHackRules.BurnoutDamage(missingRam),EDamageType.Bullet);
        } else if(kind==HackKind.BlackwallGateway) {
            chains.Add(new Chain { Next=reference,World=reference.World,Started=Time.unscaledTime,At=Time.unscaledTime+.5f });
        }
        Assets.Play(HackRules.Icon(kind));
        if(kind is not HackKind.BlackwallGateway and not HackKind.SynapseBurnout) Effects.Target(actor,kind,kind==HackKind.Overheat?CombatHackRules.OverheatDuration:3);
        return result;
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
    static void ReleaseMovement(MovementLock state) {
        try { if(state.Ref.Resolve()==null||state.Mover._pauseEndTime!=float.MaxValue) return; state.Mover._pauseEndTime=state.PauseUntil; state.Mover.Pause=state.Pause&&state.PauseUntil>Time.time; }
        catch(Exception e) { Runtime.Warn("Restore movement",e); }
    }
    internal static void Tick() {
        foreach(var pair in heat.ToArray()) {
            var state=pair.Value; var actor=state.Ref.Resolve();
            if(actor==null) { heat.Remove(pair.Key); continue; }
            if(Time.unscaledTime<state.Next) continue;
            state.Next=Time.unscaledTime+.25f;
            float elapsed=Time.unscaledTime-state.Started,damage=CombatHackRules.HeatDamage(elapsed,state.Applied);
            state.Applied=elapsed;
            try { if(damage>0) Hit(actor,EBodyPart.Chest,damage,EDamageType.Flame); }
            catch(Exception e) { heat.Remove(pair.Key); Runtime.Warn("Overheat",e); }
            if(elapsed>=CombatHackRules.OverheatDuration) heat.Remove(pair.Key);
        }
        foreach(var p in blind.ToArray()) if(Time.unscaledTime>=p.Value.Until||p.Value.Ref.Resolve()==null) blind.Remove(p.Key);
        foreach(var effects in new[]{jammed,wiped}) foreach(var p in effects.ToArray()) if(Time.unscaledTime>=p.Value.Until||p.Value.Ref.Resolve()==null) effects.Remove(p.Key);
        foreach(var state in wiped.Values) if(state.Ref.Resolve()?.AIData?.BotOwner is {} owner) Forget(owner);
        foreach(var p in movement.ToArray()) {
            if(Time.unscaledTime>=p.Value.Until||p.Value.Ref.Resolve()==null) { ReleaseMovement(p.Value); movement.Remove(p.Key); }
            else KeepMovementLocked(p.Value.Mover);
        }
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
        if(owner.StandBy is {} standby&&owner.BotState is not EBotState.Disposed and not EBotState.ActiveFail and not EBotState.PreActive&&(!actor.gameObject.activeSelf||standby.StandByType==BotStandByType.paused||owner.BotState==EBotState.NonActive)) {
            standby.Activate(); standby._nextCheckTime=Time.time+10; actor.gameObject.SetActive(true); owner.BotState=EBotState.Active;
        }
        var target=Bots().Where(p=>p.Pointer!=actor.Pointer&&(p.Position-actor.Position).sqrMagnitude<=80*80)
            .OrderByDescending(p=>p.AIData?.BotOwner?.BotsGroup?.Pointer==owner.BotsGroup.Pointer)
            .ThenBy(p=>(p.Position-actor.Position).sqrMagnitude*(state.Last?.Person?.Pointer==p.Pointer?.8f:1f)).FirstOrDefault();
        if(target==null) { state.Last=null; Report(state,"no-nearby-ai"); return; }
        var previous=evaluating; evaluating=owner;
        try {
            EnemyInfo info;
            if(owner.EnemiesController.EnemyInfos.ContainsKey(target)) info=owner.EnemiesController.EnemyInfos[target];
            else {
                var shared=new BotGroupEnemyInfo(target,owner.BotsGroup);
                owner.Memory.AddEnemyInfo(target,shared,false);
                info=owner.EnemiesController.EnemyInfos.ContainsKey(target)?owner.EnemiesController.EnemyInfos[target]:owner.EnemiesController.AddNew(owner.BotsGroup,target,shared);
                state.Created.Add((target,info.Pointer));
            }
            info.SetIgnoreState(false); info.IgnoreUntilAggression=false;
            info.PersonalLastPos=target.Position;
            info.GroupInfo.EnemyLastPosition=target.Position;
            state.Last=info;
            owner.EnemiesController.UpdateFor(info,true);
            owner.Memory.IsPeace=false; owner.Memory.AttackImmediately=true; owner.Memory.GoalEnemy=info;
            Report(state,"commanded");
        } finally { evaluating=previous; }
    }
    static void Report(Madness state,string stage) {
        if(Time.unscaledTime<state.NextDiagnostic) return;
        state.NextDiagnostic=Time.unscaledTime+2;
        CombatDiagnostics.Record(state.Owner,state.Last,stage);
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
    static void Hit(Player actor,EBodyPart part,float damage,EDamageType type) {
        var local=Singleton<GameWorld>.Instance?.MainPlayer;
        if(local?.HealthController?.IsAlive!=true||Protected(actor)||actor.HealthController?.IsAlive!=true) return;
        var origin=actor.PlayerBones?.Ribcage?.Original?.position??actor.Position+Vector3.up;
        var info=new DamageInfo { DamageType=type,Damage=damage,Player=new PlayerBridge(local).Cast<IObserverToPlayerBridge>(),MasterOrigin=local.Position,HitPoint=origin,Direction=(origin-local.Position).normalized,SourceId="cp2077-quickhack" };
        actor.ActiveHealthController.ApplyDamage(part,damage,info);
    }
    static bool Step(Chain chain) {
        var world=Singleton<GameWorld>.Instance;
        var actor=chain.Next.Resolve(); var local=world?.MainPlayer;
        if(world?.Pointer!=chain.World||actor==null||local?.HealthController?.IsAlive!=true||Protected(actor)) return false;
        if(chain.Completed<0) {
            chain.Completed=Time.unscaledTime;
            chain.At=chain.Completed+GatewayTiming.ExecutionDuration;
            Effects.Target(actor,HackKind.BlackwallGateway,GatewayTiming.ExecutionDuration);
            return true;
        }
        if(!GatewayTiming.CanExecute(chain.Completed,Time.unscaledTime)) return true;
        if(!chain.Visited.Add(actor.ProfileId)) return false;
        var origin=actor.PlayerBones?.Ribcage?.Original?.position??actor.Position+Vector3.up;
        float damage=Math.Max(10000,actor.ActiveHealthController.GetBodyPartHealth(EBodyPart.Common,false).Current*10);
        var info=new DamageInfo { DamageType=EDamageType.Bullet,Damage=damage,Player=new PlayerBridge(local).Cast<IObserverToPlayerBridge>(),MasterOrigin=local.Position,HitPoint=origin,Direction=(origin-local.Position).normalized,SourceId="cp2077-quickhack" };
        actor.ActiveHealthController.ApplyDamage(EBodyPart.Head,damage,info);
        if(actor.HealthController.IsAlive) return false;
        Assets.Play("death");
        if(chain.Visited.Count>=Plugin.GatewayTargets.Value) return false;
        int mask=LayerMask.GetMask("HighPolyCollider","Terrain","LowPolyCollider"); if(mask==0) return false;
        var next=Bots().Where(p=>!chain.Visited.Contains(p.ProfileId)&&!Protected(p)&&!chains.Any(c=>c!=chain&&c.Next.Pointer==p.Pointer)&&Vector3.Distance(p.Position,origin)<=Plugin.GatewayRange.Value).OrderBy(p=>(p.Position-origin).sqrMagnitude).FirstOrDefault(p=>{
            var delta=(p.PlayerBones?.Ribcage?.Original?.position??p.Position+Vector3.up)-origin;
            return !Physics.Raycast(origin,delta.normalized,Math.Max(0,delta.magnitude-.2f),mask,QueryTriggerInteraction.Ignore);
        });
        if(next==null) return false;
        chain.Next=ActorRef.Of(next); chain.Started=Time.unscaledTime; chain.At=chain.Started+Plugin.GatewayHopTime.Value; chain.Completed=-1;
        return true;
    }
    internal static void Reset() {
        var originals=madness.Values.ToArray(); madness.Clear();
        foreach(var state in originals) Restore(state);
        foreach(var state in movement.Values.ToArray()) ReleaseMovement(state);
        madness.Clear(); blind.Clear(); jammed.Clear(); wiped.Clear(); movement.Clear(); chains.Clear(); heat.Clear(); evaluating=null;
        Effects.ForgetAll();
    }
}
