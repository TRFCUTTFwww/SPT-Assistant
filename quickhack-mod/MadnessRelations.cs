using Comfort.Common;
using EFT;
using LocalAiCombat;

namespace LocalQuickhacks;

internal static class MadnessRelations {
    sealed class Saved(BotsGroup group,Player target) {
        internal BotsGroup Group=group;
        internal ActorRef Target=ActorRef.Of(target);
        internal BotGroupEnemyInfo? Enemy=group.BotGroupHostility.Enemies.ContainsKey(target)?group.BotGroupHostility.Enemies[target]:null;
        internal BotGroupEnemyInfo? Ally=group.BotGroupHostility.Allies.ContainsKey(target)?group.BotGroupHostility.Allies[target]:null;
        internal BotGroupEnemyInfo? Warn=group.BotGroupHostility.Warns.ContainsKey(target)?group.BotGroupHostility.Warns[target]:null;
        internal BotGroupEnemyInfo? Neutral=group.BotGroupHostility.Neutrals.ContainsKey(target)?group.BotGroupHostility.Neutrals[target]:null;
        internal BotHostilityStatus? Status=group.BotGroupHostility.LastHostilityStatus.ContainsKey(target.RaidId)?group.BotGroupHostility.LastHostilityStatus[target.RaidId]:null;
        internal IntPtr Owned;
        internal HashSet<IntPtr> Users=[];
    }
    static readonly Dictionary<(IntPtr Group,IntPtr Target),Saved> saved=[];
    internal static bool Contains(BotsGroup group,IPlayer target)=>saved.ContainsKey((group.Pointer,target.Pointer));
    internal static BotGroupEnemyInfo Acquire(BotOwner owner,Player target) {
        var group=owner.BotsGroup;
        var key=(group.Pointer,target.Pointer);
        if(!saved.TryGetValue(key,out var entry)) saved[key]=entry=new(group,target);
        entry.Users.Add(owner.Pointer);
        var state=group.BotGroupHostility;
        // Match Assistant's native relation transition, including stale ally entries.
        if(!state.Enemies.ContainsKey(target)||state.Allies.ContainsKey(target)||state.Warns.ContainsKey(target)||state.Neutrals.ContainsKey(target)||!state.LastHostilityStatus.ContainsKey(target.RaidId)||state.LastHostilityStatus[target.RaidId]!=BotHostilityStatus.Enemy) {
            state.LastHostilityStatus.Remove(target.RaidId);
            state.UpdatePlayerHostilityCollection(target,new(BotHostilityStatus.Enemy,"Cyberpsychosis temporary relation"));
            state.Allies.Remove(target); state.Warns.Remove(target); state.Neutrals.Remove(target);
            state.LastHostilityStatus[target.RaidId]=BotHostilityStatus.Enemy;
        }
        var shared=NativeEnemyMemory.EnsureGroup(group,target); entry.Owned=shared.Pointer;
        return shared;
    }
    internal static void Release(IntPtr owner) {
        foreach(var pair in saved.ToArray()) {
            var entry=pair.Value; entry.Users.Remove(owner);
            if(entry.Users.Count>0) continue;
            saved.Remove(pair.Key);
            try {
                var target=entry.Target.Resolve();
                if(target==null||Singleton<GameWorld>.Instance?.Pointer!=entry.Target.World) continue;
                var state=entry.Group.BotGroupHostility;
                // Preserve a newer enemy entry installed by another controller.
                if(state.Enemies.ContainsKey(target)&&state.Enemies[target].Pointer!=entry.Owned) continue;
                if(state.Enemies.ContainsKey(target)) state.RemoveEnemyAction(target);
                state.Allies.Remove(target); state.Warns.Remove(target); state.Neutrals.Remove(target); state.LastHostilityStatus.Remove(target.RaidId);
                if(entry.Ally!=null) state.Allies[target]=entry.Ally;
                if(entry.Warn!=null) state.Warns[target]=entry.Warn;
                if(entry.Neutral!=null) state.Neutrals[target]=entry.Neutral;
                if(entry.Enemy!=null) state.AddEnemyAction(target,entry.Enemy);
                if(entry.Status.HasValue) state.LastHostilityStatus[target.RaidId]=entry.Status.Value;
                state.OwnedGroupCalcActionNextFrame();
            } catch(Exception e) { Runtime.Warn("Restore cyberpsychosis group registry",e); }
        }
    }
}
