using System;
using EFT;

namespace LocalAiCombat;

// Shared by Assistant's AI relationship controls and standalone quickhacks.
// Native observation enumerates the group enemy registry as well as personal memory.
internal static class NativeEnemyMemory {
    internal static BotGroupEnemyInfo EnsureGroup(BotsGroup group,Player target) {
        var state=group.BotGroupHostility??throw new ArgumentException("AI 队伍仇恨控制器尚未就绪。");
        if(state.Enemies.ContainsKey(target)) return state.Enemies[target];
        var created=new BotGroupEnemyInfo(target,group);
        state.AddEnemyAction(target,created);
        if(!state.Enemies.ContainsKey(target)) state.Enemies[target]=created;
        if(!state.Enemies.ContainsKey(target)) throw new InvalidOperationException("AI 队伍敌人登记失败。");
        return state.Enemies[target];
    }
    internal static EnemyInfo EnsurePersonal(BotOwner owner,Player target,BotGroupEnemyInfo shared) {
        var group=owner.BotsGroup??throw new ArgumentException("AI 没有队伍。");
        var enemies=owner.EnemiesController??throw new ArgumentException("AI 敌人控制器尚未就绪。");
        if(!enemies.EnemyInfos.ContainsKey(target)) {
            owner.Memory?.AddEnemyInfo(target,shared,false);
            if(!enemies.EnemyInfos.ContainsKey(target)) {
                // EFT 48558 AddEnemyInfo rejects members of the owner's native squad.
                // AddNew is only a factory: SetInfo registers BOTH EnemyInfos and
                // SortedInfos (plus the native index/count used by observation).
                var created=enemies.AddNew(group,target,shared)
                    ??throw new InvalidOperationException("AI 敌人记忆创建失败。");
                enemies.SetInfo(target,created);
            }
        }
        if(!enemies.EnemyInfos.ContainsKey(target)) throw new InvalidOperationException("AI 敌人记忆登记失败。");
        var info=enemies.EnemyInfos[target]; info.SetIgnoreState(false); info.IgnoreUntilAggression=false;
        if(info.GroupInfo?.Pointer!=shared.Pointer) info.GroupInfo=shared;
        info.PersonalLastPos=target.Position; info.GroupInfo.EnemyLastPosition=target.Position;
        return info;
    }
    internal static void Command(BotOwner owner,EnemyInfo info) {
        var memory=owner.Memory??throw new ArgumentException("AI 战斗记忆尚未就绪。");
        memory.IsPeace=false; memory.AttackImmediately=true;
        // Reassigning the same goal restarts native target/aim bookkeeping.
        if(memory.GoalEnemy?.Pointer!=info.Pointer) memory.GoalEnemy=info;
    }
}
