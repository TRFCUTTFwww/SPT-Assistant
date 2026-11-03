using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
namespace LocalSpt5Esp;
internal static class AiNativeRelations {
 sealed class Saved {
  internal BotsGroup Group=null!;internal LiveActorReference Target=null!;
  internal BotGroupEnemyInfo? Ally,Enemy,Warn,Neutral;internal BotHostilityStatus? Status;
 }
 static readonly Dictionary<(IntPtr Group,IntPtr Target),Saved> saved=new();
 internal static void Set(BotsGroup group,Player target,bool hostile){
  var state=group.BotGroupHostility??throw new ArgumentException("AI 队伍仇恨控制器尚未就绪。");
  var key=(group.Pointer,target.Pointer);
  if(!saved.ContainsKey(key))saved[key]=new(){Group=group,Target=new(Singleton<GameWorld>.Instance,target),Ally=state.Allies.ContainsKey(target)?state.Allies[target]:null,Enemy=state.Enemies.ContainsKey(target)?state.Enemies[target]:null,Warn=state.Warns.ContainsKey(target)?state.Warns[target]:null,Neutral=state.Neutrals.ContainsKey(target)?state.Neutrals[target]:null,Status=state.LastHostilityStatus.ContainsKey(target.RaidId)?state.LastHostilityStatus[target.RaidId]:null};
  var desired=hostile?BotHostilityStatus.Enemy:BotHostilityStatus.Neutral;
  bool correct=state.LastHostilityStatus.ContainsKey(target.RaidId)&&state.LastHostilityStatus[target.RaidId]==desired&&!state.Allies.ContainsKey(target)&&!state.Warns.ContainsKey(target)&&(hostile?state.Enemies.ContainsKey(target)&&!state.Neutrals.ContainsKey(target):!state.Enemies.ContainsKey(target)&&state.Neutrals.ContainsKey(target));
  if(correct)return;
  // Native updates can early-return for an unchanged status while stale ally data remains.
  state.LastHostilityStatus.Remove(target.RaidId);
  state.UpdatePlayerHostilityCollection(target,new(desired,"SPT Assistant forced relation"));
  state.Allies.Remove(target);state.Warns.Remove(target);
  if(hostile){state.Neutrals.Remove(target);if(!state.Enemies.ContainsKey(target))state.AddEnemyAction(target,new BotGroupEnemyInfo(target,group));}
  else{if(state.Enemies.ContainsKey(target))state.RemoveEnemyAction(target);if(!state.Neutrals.ContainsKey(target))state.AddNeutralAction(target);}
  state.LastHostilityStatus[target.RaidId]=desired;
  if(hostile&&(!state.Enemies.ContainsKey(target)||state.Allies.ContainsKey(target)))throw new InvalidOperationException("AI 原生敌对关系回读失败。");
 }
 internal static void Restore(){
  foreach(var entry in saved.Values)try{
   if(!RaidClock.Active||Singleton<GameWorld>.Instance?.Pointer!=entry.Target.World)continue;
   var target=entry.Target.Resolve();var group=entry.Group;
   var state=group.BotGroupHostility;if(state==null)continue;
   if(state.Enemies.ContainsKey(target))state.RemoveEnemyAction(target);
   state.Allies.Remove(target);state.Warns.Remove(target);state.Neutrals.Remove(target);state.LastHostilityStatus.Remove(target.RaidId);
   if(entry.Ally!=null)state.Allies[target]=entry.Ally;if(entry.Warn!=null)state.Warns[target]=entry.Warn;if(entry.Neutral!=null)state.Neutrals[target]=entry.Neutral;
   if(entry.Enemy!=null)state.AddEnemyAction(target,entry.Enemy);if(entry.Status.HasValue)state.LastHostilityStatus[target.RaidId]=entry.Status.Value;
   state.OwnedGroupCalcActionNextFrame();
  }catch(ArgumentException){}catch(Exception error){Plugin.Self.Log.LogWarning("AI relation restore: "+error.Message);}
  saved.Clear();
 }
}
