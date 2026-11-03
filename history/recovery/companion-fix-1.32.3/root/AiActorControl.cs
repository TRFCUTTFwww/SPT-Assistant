using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.HealthSystem;
using HarmonyLib;
using UnityEngine;
using Object=UnityEngine.Object;
namespace LocalSpt5Esp;
internal static class AiActorControl {
 sealed class Settings {internal Player Actor=null!;internal IntPtr Health;internal bool IgnorePlayer,Invincible;internal float Damage=1;}
 static readonly Dictionary<IntPtr,Settings> actors=new();static readonly Dictionary<IntPtr,Settings> healths=new();static readonly HashSet<IntPtr> neutralOwners=new();static readonly AiRelationTable relations=new();
 static readonly Dictionary<IntPtr,string> identities=new();
 [ThreadStatic] static BotOwner? evaluating;
 static readonly HashSet<IntPtr> forcedActive=new();
 static readonly Dictionary<IntPtr,(LiveActorReference Actor,IntPtr Memory,bool Attack)> combatOriginals=new();
 sealed record CombatActor(LiveActorReference Reference,BotOwner Owner,int? Team);
 sealed class CombatPass {internal CombatActor[] Bots=Array.Empty<CombatActor>();internal AiCombatSchedule Schedule=new(0);internal EnemyInfo? Chosen;internal Player? Target;internal float Nearest=float.MaxValue;internal readonly HashSet<(IntPtr Group,IntPtr Target,bool Hostile)> NativeSynced=new();}
 static CombatPass? combatPass;
 static readonly List<Player> targets=new(),sideA=new(),sideB=new();static IntPtr world;static Player? local;static string localProfile="",notice="",multiplier="1",damage="10";static bool teamMode=true;static float nextTick,nextWarning;static Vector2 scroll;
 internal static bool Open=>targets.Count>0;
 internal static Player[] PoseSelection=>targets.ToArray();
 internal static void EquipmentNotice(string text)=>notice=text;
 internal static void Reset(){combatPass=null;RestoreRelations(false);AiTeamControl.Reset();actors.Clear();healths.Clear();neutralOwners.Clear();identities.Clear();targets.Clear();sideA.Clear();sideB.Clear();forcedActive.Clear();evaluating=null;world=IntPtr.Zero;local=null;localProfile="";nextTick=0;}
 internal static void Close()=>targets.Clear();
 internal static void Select(GameWorld current,IEnumerable<Player> selected){targets.Clear();targets.AddRange(selected.Where(p=>p!=null&&p.IsAI&&!p.IsYourPlayer));foreach(var target in targets){if(identities.TryGetValue(target.Pointer,out var original)&&original!=target.ProfileId)throw new ArgumentException("AI 实体已被替换，请重新进入管理列表。");identities[target.Pointer]=target.ProfileId;}world=current.Pointer;local=current.MainPlayer;localProfile=local?.ProfileId??"";AiPairTransfer.Prefill(current,targets);AiTeamControl.Prefill(current,targets);notice="修改仅作用于当前战局；可单选或多选 AI。";scroll=Vector2.zero;if(targets.Count==1&&actors.TryGetValue(targets[0].Pointer,out var settings))multiplier=settings.Damage.ToString(CultureInfo.InvariantCulture);}
 internal static string Team(BotOwner bot)=>bot.BotsGroup is BotsGroup group?"队伍 #"+group.Id+" · "+group.MembersCount+" AI"+(AiTeamControl.HasPlayer(group)?" + 玩家":""):"未分队";
 static Player Require(Player p){var current=Singleton<GameWorld>.Instance;bool present=false;var list=current?.AllAlivePlayersList;if(list!=null)for(int i=0;i<list.Count;i++)if(list[i]?.Pointer==p.Pointer){present=true;break;}if(!present||!AiControlRules.SameActor(world.ToInt64(),p.Pointer.ToInt64(),identities.GetValueOrDefault(p.Pointer,""),current?.Pointer.ToInt64()??0,p.Pointer.ToInt64(),p.ProfileId,p.IsAI,p.IsYourPlayer,p.HealthController?.IsAlive==true))throw new ArgumentException("AI 已离开当前战局或已死亡，请重新选择。");return p;}
 static Settings For(Player p){Require(p);if(!actors.TryGetValue(p.Pointer,out var settings)){settings=new(){Actor=p,Health=p.ActiveHealthController?.Pointer??IntPtr.Zero};actors[p.Pointer]=settings;if(settings.Health!=IntPtr.Zero)healths[settings.Health]=settings;}return settings;}
 internal static bool Ignored(BotOwner? owner,IPlayer? target){if(owner==null||target==null)return false;if(neutralOwners.Count>0&&neutralOwners.Contains(owner.Pointer)&&target.ProfileId==localProfile&&owner.GetPlayer is Player actor&&identities.GetValueOrDefault(actor.Pointer)==actor.ProfileId&&actors.TryGetValue(actor.Pointer,out var entry)&&entry.IgnorePlayer)return true;return Relation(owner,target)==false;}
 internal static bool? Relation(BotOwner owner,IPlayer target){var p=owner.GetPlayer;if(p==null)return null;var companionRule=CompanionSquad.Relation(owner,target);if(companionRule.HasValue)return companionRule;var rule=relations.Resolve(p.ProfileId,target.ProfileId,owner.BotsGroup?.Id,AiTeamControl.Group(target)?.Id);return rule??(AiTeamControl.Same(owner.BotsGroup,target)?false:null);}
 internal static bool ForcedEnemy(BotOwner owner,IPlayer target)=>Relation(owner,target)==true;
 internal static bool ForcedFrom(object source,IPlayer target)=>source switch{BotEnemiesController enemies=>enemies._owner!=null&&ForcedEnemy(enemies._owner,target),BotMemory memory=>memory._owner!=null&&ForcedEnemy(memory._owner,target),BotsGroup group=>GroupRelation(group,target)==true,_=>false};
 internal static bool IndividuallyProtected(ActiveHealthController health)=>healths.TryGetValue(health.Pointer,out var settings)&&settings.Invincible&&settings.Actor?.ActiveHealthController?.Pointer==health.Pointer&&identities.GetValueOrDefault(settings.Actor.Pointer)==settings.Actor.ProfileId;
 internal static void ApplyBirthState(GameWorld current,Player actor,bool invincible,int playerRelation,int aiRelation){
  new LiveActorReference(current,actor).Resolve();if(world!=IntPtr.Zero&&world!=current.Pointer)Reset();world=current.Pointer;local=current.MainPlayer;localProfile=local?.ProfileId??"";identities[actor.Pointer]=actor.ProfileId;
  if(actor.ActiveHealthController==null)throw new ArgumentException("AI 生命控制器尚未就绪。");var settings=For(actor);settings.Invincible=invincible;settings.IgnorePlayer=false;var owner=actor.AIData?.BotOwner??throw new ArgumentException("AI 控制器尚未就绪。");neutralOwners.Remove(owner.Pointer);
  if(playerRelation!=0&&local!=null){relations.ActorPair(actor.ProfileId,local.ProfileId,playerRelation==2);if(playerRelation==1)Forget(owner,local);}
  if(aiRelation!=0)foreach(var other in LiveBots(current))if(other.Pointer!=actor.Pointer)relations.ActorPair(actor.ProfileId,other.ProfileId,aiRelation==2);
  nextTick=0;
 }
 internal static bool ForcedCombatActive(Player actor)=>forcedActive.Contains(actor.Pointer);
 internal static void RestoreSceneRelations(IEnumerable<(Player First,Player Second,bool Hostile)> pairs){foreach(var pair in pairs)relations.ActorPair(pair.First.ProfileId,pair.Second.ProfileId,pair.Hostile);nextTick=0;}
 static bool? GroupRelation(BotsGroup group,IPlayer target){if(evaluating?.BotsGroup?.Pointer==group.Pointer)return Relation(evaluating,target);var other=AiTeamControl.Group(target);return other!=null&&relations.Teams.TryGetValue((group.Id,other.Id),out bool value)?value:AiTeamControl.Same(group,target)?false:null;}
 internal static bool EnemyOverride(BotEnemiesController controller,IPlayer target,ref bool result){if(Ignored(controller._owner,target)){result=false;return false;}if(controller._owner!=null&&Relation(controller._owner,target) is bool value){result=value;return false;}return true;}
 static bool Ally(BotsGroup __instance,IPlayer __0,ref bool __result){if(GroupRelation(__instance,__0) is bool rule){__result=!rule&&AiTeamControl.Same(__instance,__0);return false;}return true;}
 internal static bool GroupOverride(BotsGroup group,IPlayer target,ref bool result){if(GroupRelation(group,target) is bool value){result=value;return false;}return true;}
 internal static double PreviewDamageFactor(Player victim){double factor=PlayerDamageControl.Factor;var health=victim.ActiveHealthController;if(health!=null&&healths.TryGetValue(health.Pointer,out var settings)&&settings.Actor?.Pointer==victim.Pointer&&identities.GetValueOrDefault(victim.Pointer)==victim.ProfileId)factor*=settings.Damage;return factor;}
 internal static bool ScaleDamage(ActiveHealthController controller,DamageInfo info,ref float amount,ref float result){double factor=PlayerDamageControl.Factor;if(factor==1&&healths.Count==0)return true;if(info==null||info.OverDamageFrom.HasValue)return true;var victim=controller.Player;if(victim==null||victim.IsYourPlayer)return true;string localId=localProfile.Length>0?localProfile:Singleton<GameWorld>.Instance?.MainPlayer?.ProfileId??"";if(localId.Length==0||info.Player?.iPlayer?.ProfileId!=localId)return true;if(healths.TryGetValue(controller.Pointer,out var settings)&&settings.Actor?.IsYourPlayer==false&&settings.Actor.ActiveHealthController?.Pointer==controller.Pointer&&identities.GetValueOrDefault(settings.Actor.Pointer)==settings.Actor.ProfileId)factor*=settings.Damage;if(factor==1)return true;if(factor==0){result=0;return false;}float available=controller.GetBodyPartHealth(EBodyPart.Common,false).Current;amount=AiControlRules.Damage(amount,factor,available);return true;}

 internal static void Initialize(Plugin plugin){
  plugin.Patches.Patch(AccessTools.Method(typeof(BotsGroup),"IsAlly"),prefix:new HarmonyMethod(typeof(AiActorControl),nameof(Ally)));
  plugin.Patches.Patch(AccessTools.PropertySetter(typeof(BotMemory),"GoalEnemy"),prefix:new HarmonyMethod(typeof(AiActorControl),nameof(Goal)));
  plugin.Patches.Patch(AccessTools.Method(typeof(BotGroupHostility),"UpdatePlayerHostilityCollection"),prefix:new HarmonyMethod(typeof(AiActorControl),nameof(GroupStatus)));
  plugin.Patches.Patch(typeof(BotOwner).GetMethod("UpdateManual",Type.EmptyTypes),prefix:new HarmonyMethod(typeof(AiActorControl),nameof(BrainContext)),postfix:new HarmonyMethod(typeof(CompanionSquad),nameof(CompanionSquad.AfterBrain)),finalizer:new HarmonyMethod(typeof(AiActorControl),nameof(EndBrainContext)));
  foreach(var method in new[]{"ShallKnowEnemy","ShallKnowEnemyLate"})plugin.Patches.Patch(AccessTools.Method(typeof(EnemyInfo),method),prefix:new HarmonyMethod(typeof(AiActorControl),nameof(KnowEnemy)));
  plugin.Log.LogInfo("Forced AI hostility ready: native team collections, per-owner ally checks, enemy memory, attack goals and reversible originals");
 }
 static void BrainContext(BotOwner __instance,out BotOwner? __state){__state=evaluating;evaluating=__instance;}
 static void EndBrainContext(BotOwner? __state){evaluating=__state;}
 static bool KnowEnemy(EnemyInfo __instance,ref bool __result){if(__instance.Owner!=null&&__instance.Person!=null&&Relation(__instance.Owner,__instance.Person) is bool rule){__result=rule&&__instance.Person.TryCast<Player>()?.HealthController?.IsAlive==true;return false;}return true;}
 static void Goal(BotMemory __instance,ref EnemyInfo __0){if(__0!=null&&Ignored(__instance._owner,__0.Person))__0=null!;}
 static void GroupStatus(BotGroupHostility __instance,IPlayer __0,ref HostilityEvaluationResult __1){var from=__instance.OwnedGroup;if(AiTeamControl.Same(from,__0)){__1=new HostilityEvaluationResult(BotHostilityStatus.Ally,"SPT Assistant team");return;}var to=AiTeamControl.Group(__0);if(from!=null&&to!=null&&relations.Teams.TryGetValue((from.Id,to.Id),out bool hostile))__1=new HostilityEvaluationResult(hostile?BotHostilityStatus.Enemy:BotHostilityStatus.Neutral,"Local AI team control");}
 internal static void Forget(BotOwner owner,IPlayer target){var memory=owner.Memory;if(memory==null)return;bool aiming=memory.GoalEnemy?.Person?.ProfileId==target.ProfileId;if(aiming){owner.GetPlayer?.HandsController?.TryCast<Player.FirearmController>()?.SetTriggerPressed(false);memory.GoalEnemy=null!;}if(memory.LastEnemy?.Person?.ProfileId==target.ProfileId)memory.LastEnemy=null!;if(owner.EnemiesController?.EnemyInfos?.ContainsKey(target)==true)owner.EnemiesController.Remove(target);}
 static EnemyInfo EnsureEnemy(BotOwner owner,Player target){
  var enemies=owner.EnemiesController??throw new ArgumentException("AI 敌人控制器尚未就绪。");
  var group=owner.BotsGroup??throw new ArgumentException("AI 没有队伍。");
  AiNativeRelations.EnsureEnemyEntry(group,target);
  if(!enemies.EnemyInfos.ContainsKey(target)){
   var shared=AiNativeRelations.EnsureEnemyEntry(group,target);
   owner.Memory?.AddEnemyInfo(target,shared,false);
   if(!enemies.EnemyInfos.ContainsKey(target))enemies.AddNew(group,target,shared);
  }
  if(!enemies.EnemyInfos.ContainsKey(target))throw new InvalidOperationException("AI 敌人记忆创建失败。");
  var info=enemies.EnemyInfos[target];info.IgnoreUntilAggression=false;info.SetIgnoreState(false);
  // Supply the commanded target's location for native search; vision and firing still use native checks.
  info.PersonalLastPos=target.Position;info.GroupInfo.EnemyLastPosition=target.Position;
  return info;
 }
 internal static void Tick(GameWorld? current,Player? player){
  AiTeamControl.Tick(current);
  if(current==null||player==null){combatPass=null;if(world!=IntPtr.Zero)Reset();return;}
  if(world!=IntPtr.Zero&&world!=current.Pointer){combatPass=null;Reset();}world=current.Pointer;local=player;localProfile=player.ProfileId;
  if(actors.Count==0&&relations.Actors.Count==0&&relations.Teams.Count==0&&!CompanionSquad.HasHunting){combatPass=null;return;}
  if(combatPass==null){
   if(Time.unscaledTime<nextTick)return;nextTick=Time.unscaledTime+.5f;forcedActive.Clear();
   foreach(var settings in actors.Values)try{if(settings.IgnorePlayer&&settings.Actor?.HealthController?.IsAlive==true&&settings.Actor.AIData?.BotOwner is BotOwner owner)Forget(owner,player);}catch(Exception error){WarnRelation(error);}
   if(relations.Actors.Count==0&&relations.Teams.Count==0&&!CompanionSquad.HasHunting)return;
   var bots=LiveBots(current).Select(p=>new CombatActor(new(current,p),p.AIData.BotOwner,p.AIData.BotOwner.BotsGroup?.Id)).ToArray();combatPass=new(){Bots=bots,Schedule=new(bots.Length)};
  }
  var pass=combatPass;long stamp=RuntimePerformance.Start();int processed=0,steps=0;
  try{
   while(!pass.Schedule.Done&&processed<12&&steps<64&&(steps==0||(System.Diagnostics.Stopwatch.GetTimestamp()-stamp)*1000d/System.Diagnostics.Stopwatch.Frequency<2)){
    steps++;
    var source=pass.Bots[pass.Schedule.Source];Player actor;try{actor=source.Reference.Resolve();if(actor.AIData?.BotOwner?.Pointer!=source.Owner.Pointer){NextCombatSource(pass);continue;}}catch{NextCombatSource(pass);continue;}
    bool paused=AiTestControl.IsPaused(actor)||ModelPoseLock.Locked(actor);var previous=evaluating;evaluating=source.Owner;
    try{
     if(pass.Schedule.SourceComplete){
      var playerRule=Relation(source.Owner,player);
      if(playerRule==false)Forget(source.Owner,player);
      else if(playerRule==true&&!paused&&player.HealthController?.IsAlive==true){var info=EnsureEnemy(source.Owner,player);float distance=(actor.Position-player.Position).sqrMagnitude;if(distance<pass.Nearest){pass.Nearest=distance;pass.Chosen=info;pass.Target=player;}}
      if(pass.Chosen!=null&&pass.Target?.HealthController?.IsAlive==true&&!paused){CommandAttack(source.Owner,pass.Target);forcedActive.Add(actor.Pointer);}else if(pass.Chosen==null)RestoreCombat(actor.Pointer);
      NextCombatSource(pass);processed++;continue;
     }
     var target=pass.Bots[pass.Schedule.TakeTarget()];if(source.Reference.Profile==target.Reference.Profile)continue;Player other;try{other=target.Reference.Resolve();}catch{continue;}
     var rule=Relation(source.Owner,other);if(rule==null||rule==true&&paused)continue;processed++;
     var group=source.Owner.BotsGroup;if(source.Team.HasValue&&target.Team.HasValue&&relations.Teams.TryGetValue((source.Team.Value,target.Team.Value),out bool teamRule)&&group!=null&&pass.NativeSynced.Add((group.Pointer,other.Pointer,teamRule)))AiNativeRelations.Set(group,other,teamRule);
     if(rule==false){Forget(source.Owner,other);continue;}
     var enemy=EnsureEnemy(source.Owner,other);float score=(actor.Position-other.Position).sqrMagnitude;if(source.Owner.Memory?.GoalEnemy?.Person?.ProfileId==other.ProfileId)score*=.8f;
     if(score<pass.Nearest){pass.Nearest=score;pass.Chosen=enemy;pass.Target=other;}
    }catch(Exception error){processed++;WarnRelation(error);}finally{evaluating=previous;}
   }
   if(pass.Schedule.Done)combatPass=null;
  }finally{RuntimePerformance.AiCombatDone(stamp,processed);}
 }
 static void NextCombatSource(CombatPass pass){pass.Schedule.NextSource();pass.Chosen=null;pass.Target=null;pass.Nearest=float.MaxValue;}
 internal static void CommandAttack(BotOwner owner,Player target){var actor=owner.GetPlayer??throw new ArgumentException("AI 尚未就绪。");if(AiTestControl.IsPaused(actor)||ModelPoseLock.Locked(actor)||CompanionSquad.Friendly(actor,target))return;var previous=evaluating;evaluating=owner;try{var info=EnsureEnemy(owner,target);var memory=owner.Memory??throw new ArgumentException("AI 战斗记忆尚未就绪。");if(!combatOriginals.ContainsKey(actor.Pointer))combatOriginals[actor.Pointer]=(new(Singleton<GameWorld>.Instance,actor),memory.Pointer,memory.AttackImmediately);memory.IsPeace=false;memory.AttackImmediately=true;if(memory.GoalEnemy?.Pointer!=info.Pointer)memory.GoalEnemy=info;AiLimitCompatibility.Wake(actor);}finally{evaluating=previous;}}
 static void WarnRelation(Exception error){if(Time.unscaledTime<nextWarning)return;nextWarning=Time.unscaledTime+10;Plugin.Self.Log.LogWarning("AI relation update: "+error.Message);}
 static void RestoreCombat(IntPtr pointer){if(!combatOriginals.Remove(pointer,out var saved))return;try{var owner=saved.Actor.Resolve().AIData?.BotOwner;var memory=owner?.Memory;if(memory?.Pointer==saved.Memory){memory.AttackImmediately=saved.Attack;memory.CheckIsPeace();}}catch{}}
 static Player[] LiveBots(GameWorld current){var result=new List<Player>();var all=current.AllAlivePlayersList;if(all!=null)for(int i=0;i<all.Count;i++){var actor=all[i];if(actor?.IsAI==true&&!actor.IsYourPlayer&&actor.HealthController?.IsAlive==true&&actor.AIData?.BotOwner?.BotsGroup!=null)result.Add(actor);}return result.ToArray();}
 static void ApplyRelationsNow(bool hostile,string scope){
  var current=Singleton<GameWorld>.Instance??throw new ArgumentException("请进入战局。");var all=LiveBots(current);
  var participants=all.Where(actor=>all.Any(other=>actor.Pointer!=other.Pointer&&ForcedEnemy(actor.AIData.BotOwner,other))).ToArray();
  // Hostility changes preserve pause and model-lock ownership.
  nextTick=0;Tick(current,current.MainPlayer);
  if(hostile){
   int paused=participants.Count(actor=>AiTestControl.IsPaused(actor)||ModelPoseLock.Locked(actor));int ready=participants.Count(actor=>actor.AIData.BotOwner.Memory?.GoalEnemy is {} goal&&ForcedEnemy(actor.AIData.BotOwner,goal.Person)&&actor.AIData.BotOwner.EnemiesController.EnemyInfos.ContainsKey(goal.Person));
   notice=$"{scope}已设置敌对：{participants.Length} 名 AI，保持暂停／锁定 {paused} 名，交战目标已就绪 {ready} 名。";
   Plugin.Self.Log.LogInfo($"Forced combat applied: participants={participants.Length}, nativeEnemyGoals={ready}; "+scope);
   if(ready<participants.Length-paused)notice+="部分 AI 控制器尚未就绪，将继续同步。";
  }else notice=scope+"已互相停战。";
  FeatureToasts.Show(notice);
 }
 internal static void ForceSelected(GameWorld current,IEnumerable<Player> selected){
  var picked=selected.GroupBy(actor=>actor.Pointer).Select(group=>group.First()).ToArray();
  if(picked.Length<2)throw new ArgumentException("至少选择两名 AI。");
  var live=picked.Select(actor=>new LiveActorReference(current,actor).Resolve()).ToArray();
  var groups=live.Select(actor=>actor.AIData?.BotOwner?.BotsGroup??throw new ArgumentException("所选 AI 没有队伍。")).GroupBy(group=>group.Pointer).Select(group=>group.First()).ToArray();
  if(groups.Length<2)throw new ArgumentException("所选 AI 属于同一队；请在关系页使用“只作用于所选 AI”指定 A、B 双方。");
  Select(current,live);
  for(int i=0;i<groups.Length;i++)for(int j=i+1;j<groups.Length;j++)relations.TeamPair(groups[i].Id,groups[j].Id,true);
  viewPage=1;ApplyRelationsNow(true,"所选 "+groups.Length+" 支队伍");
 }
 static void Ignore(bool value){int changed=0;foreach(var target in targets){var p=Require(target);var settings=For(p);settings.IgnorePlayer=value;var owner=p.AIData?.BotOwner;if(owner==null)continue;if(value){neutralOwners.Add(owner.Pointer);if(local!=null)Forget(owner,local);}else{neutralOwners.Remove(owner.Pointer);if(local!=null&&owner.BotsGroup?.Enemies.ContainsKey(local)==true)owner.Memory?.AddEnemyInfo(local,owner.BotsGroup.Enemies[local],false);}changed++;}notice=$"已将 {changed} 名 AI 的对玩家敌对设置为 "+(value?"关闭。":"默认判断。");FeatureToasts.Show(notice);}
 static void Multiplier(){float value=ItemSpawnRules.Number(multiplier,0,float.MaxValue,"承受玩家伤害倍率");foreach(var target in targets)For(target).Damage=value;notice="已设置承受玩家伤害倍率："+value.ToString("G6");FeatureToasts.Show(notice);}
 static void Hurt(){var current=Singleton<GameWorld>.Instance;if(current?.Pointer!=world||local?.HealthController?.IsAlive!=true)throw new ArgumentException("需要存活的本地玩家作为伤害来源。");float value=ItemSpawnRules.Number(damage,0,float.MaxValue,"施加伤害");int affected=0;float lost=0;var bridge=new PlayerBridge(local).Cast<IObserverToPlayerBridge>();foreach(var target in targets.ToArray()){var p=Require(target);var health=p.ActiveHealthController??throw new ArgumentException("AI 生命控制器尚未就绪。");float before=health.GetBodyPartHealth(EBodyPart.Common,false).Current;float effective=Math.Min(value,before);var info=new DamageInfo{DamageType=EDamageType.Bullet,Damage=effective,Player=bridge,MasterOrigin=local.Position,HitPoint=p.Position+Vector3.up,Direction=(p.Position-local.Position).normalized,SourceId="local-ai-control"};p.LastAggressor=local;health.ApplyDamage(EBodyPart.Chest,effective,info);lost+=Math.Max(0,before-health.GetBodyPartHealth(EBodyPart.Common,false).Current);affected++;}notice=$"已对 {affected} 名 AI 提交玩家来源伤害；实际扣血合计 {lost:G6}。受伤害倍率与 AI 无敌设置影响。";FeatureToasts.Show("玩家来源伤害已执行");}
 static void Side(List<Player> destination){destination.Clear();foreach(var target in targets)destination.Add(Require(target));notice="已选定 "+destination.Count+" 名 AI 作为关系一方。";}
 static void SetRelations(bool hostile){if(sideA.Count==0||sideB.Count==0)throw new ArgumentException("请先从 AI 列表选择双方，并分别设为 A、B。");var a=sideA.Select(Require).ToArray();var b=sideB.Select(Require).ToArray();foreach(var left in a)foreach(var right in b){if(left.Pointer==right.Pointer)throw new ArgumentException("关系双方不能包含同一个 AI。");if(teamMode){var first=left.AIData?.BotOwner?.BotsGroup??throw new ArgumentException("A 方 AI 没有队伍。");var second=right.AIData?.BotOwner?.BotsGroup??throw new ArgumentException("B 方 AI 没有队伍。");if(first.Id==second.Id)throw new ArgumentException("双方属于同一队伍。");}}
  foreach(var left in a)foreach(var right in b){if(teamMode){var first=left.AIData.BotOwner.BotsGroup;var second=right.AIData.BotOwner.BotsGroup;relations.TeamPair(first.Id,second.Id,hostile);}else relations.ActorPair(left.ProfileId,right.ProfileId,hostile);}ApplyRelationsNow(hostile,(teamMode?"双方整队":"所选 AI 双方"));
 }
 static void RestoreRelations()=>RestoreRelations(true);
 static void RestoreRelations(bool notify){combatPass=null;
  var current=Singleton<GameWorld>.Instance;var live=current!=null?LiveBots(current):Array.Empty<Player>();
  foreach(var actor in live){var owner=actor.AIData.BotOwner;foreach(var target in live)if(target.Pointer!=actor.Pointer&&relations.Resolve(actor.ProfileId,target.ProfileId,owner.BotsGroup?.Id,target.AIData.BotOwner.BotsGroup?.Id).HasValue)try{Forget(owner,target);}catch(Exception error){WarnRelation(error);}}
  if(current?.MainPlayer is {} human)foreach(var actor in live){var owner=actor.AIData.BotOwner;if(relations.Resolve(actor.ProfileId,human.ProfileId,owner.BotsGroup?.Id,AiTeamControl.Group(human)?.Id).HasValue)try{Forget(owner,human);}catch(Exception error){WarnRelation(error);}}
  relations.Clear();forcedActive.Clear();AiNativeRelations.Restore();
  foreach(var pointer in combatOriginals.Keys.ToArray())RestoreCombat(pointer);
  foreach(var group in live.Select(actor=>actor.AIData.BotOwner.BotsGroup).Where(group=>group!=null).GroupBy(group=>group.Pointer).Select(group=>group.First()))try{group.BotGroupHostility?.OwnedGroupCalcActionNextFrame();}catch{}
  if(notify){notice="已恢复默认关系与原生友军／敌人数据。";FeatureToasts.Show(notice);}
 }

 internal static string CleanupIdentity(IntPtr actor,IntPtr currentWorld)=>world==currentWorld?identities.GetValueOrDefault(actor,""):"";
 internal static void CleanupTracking(IntPtr actor,string profile,IntPtr owner){if(actors.Remove(actor,out var settings))healths.Remove(settings.Health);neutralOwners.Remove(owner);identities.Remove(actor);forcedActive.Remove(actor);combatOriginals.Remove(actor);targets.RemoveAll(p=>p.Pointer==actor);sideA.RemoveAll(p=>p.Pointer==actor);sideB.RemoveAll(p=>p.Pointer==actor);}
 internal static int Delete(IEnumerable<Player> selected)=>AiRemoval.Delete(Singleton<GameWorld>.Instance??throw new ArgumentException("请进入战局。"),selected);
 internal static Player[] SelectedTargets()=>targets.Select(Require).ToArray();
 static void Action(Action action){try{action();}catch(Exception e){notice=e.Message;Plugin.Self.Log.LogWarning("AI actor control: "+e.Message);}}
 static int viewPage;static readonly string[] pages={"行为与伤害","AI／队伍关系","位置传送","装备参数","组队"};
 static Rect Card(float width,ref float y,string title,float height){var rect=new Rect(0,y,width,height);InterfaceMenu.Surface(rect,Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.04f),10);InterfaceMenu.Label(new Rect(16,y+12,width-32,28),title,InterfaceMenu.Accent,16);var body=new Rect(16,y+54,width-32,height-66);y+=height+18;return body;}
 internal static void Draw(Rect rect){float x=rect.x,y=rect.y,w=rect.width;if(InterfaceMenu.Button(new Rect(x,y,128,32),"返回 AI 列表")){Close();return;}InterfaceMenu.Label(new Rect(x+146,y,w-146,32),targets.Count==1?(targets[0].Profile?.Nickname??"AI")+" · "+(targets[0].AIData?.BotOwner is BotOwner bot?Team(bot):"未分队"):"批量管理 · "+targets.Count+" 名 AI",InterfaceMenu.Accent,19);y+=50;float cell=(w-8*(pages.Length-1))/pages.Length;for(int i=0;i<pages.Length;i++)if(InterfaceMenu.Button(new Rect(x+i*(cell+8),y,cell,36),pages[i],viewPage==i)){viewPage=i;scroll=Vector2.zero;}y+=54;var body=new Rect(x,y,w,Math.Max(100,rect.yMax-y-76));if(viewPage==4){AiTeamControl.Draw(new Rect(x,y,w,rect.yMax-y),SelectedTargets);return;}if(viewPage==2){TeleportControl.DrawActorTools(new Rect(x,y,w,rect.yMax-y),targets);return;}else if(viewPage==3){DrawEquipment(body);}else{float height=viewPage==0?UiPageLayout.CardStack(new[]{142f,170f,178f,128f}):UiPageLayout.CardStack(new[]{246f,138f,246f},tail:82);scroll=GUI.BeginScrollView(body,scroll,new Rect(0,0,w-20,Math.Max(body.height-1,height)));if(viewPage==0)DrawBehavior(w-30);else DrawRelations(w-30);GUI.EndScrollView();}InterfaceMenu.Paragraph(new Rect(x,rect.yMax-62,w,58),notice,InterfaceMenu.Accent,13);}
 static void DrawBehavior(float width){float top=4;var card=Card(width,ref top,"对玩家的行为",142);float half=(card.width-12)/2;if(InterfaceMenu.Button(new Rect(card.x,card.y,half,40),"停止对玩家敌对"))Action(()=>Ignore(true));if(InterfaceMenu.Button(new Rect(card.x+half+12,card.y,half,40),"恢复默认判断"))Action(()=>Ignore(false));
  card=Card(width,ref top,"承受玩家伤害",170);InterfaceMenu.Label(new Rect(card.x,card.y,card.width,26),"倍率：0 免疫 · 1 正常 · 2 双倍",null,13);multiplier=InterfaceMenu.Field(new Rect(card.x,card.y+40,Math.Min(180,card.width*.36f),36),multiplier);if(InterfaceMenu.Button(new Rect(card.x+Math.Min(180,card.width*.36f)+14,card.y+40,Math.Min(220,card.width*.52f),36),"应用到所选 AI"))Action(Multiplier);
  card=Card(width,ref top,"直接施加玩家来源伤害",178);InterfaceMenu.Label(new Rect(card.x,card.y,card.width,26),"输入伤害点数，作用于胸部",null,13);damage=InterfaceMenu.Field(new Rect(card.x,card.y+42,Math.Min(180,card.width*.36f),36),damage);if(InterfaceMenu.Button(new Rect(card.x+Math.Min(180,card.width*.36f)+14,card.y+42,Math.Min(220,card.width*.52f),36),"施加伤害"))Action(Hurt);
  card=Card(width,ref top,"管理操作",128);float third=(card.width-20)/3;if(InterfaceMenu.Button(new Rect(card.x,card.y,third,38),"暂停")){AiTestControl.SetPaused(targets,true);notice="所选 AI 已暂停";}if(InterfaceMenu.Button(new Rect(card.x+third+10,card.y,third,38),"恢复")){AiTestControl.ResumeActors(targets.ToArray());}if(InterfaceMenu.Button(new Rect(card.x+2*(third+10),card.y,third,38),"删除所选"))Action(()=>{Delete(targets.ToArray());notice=AiRemoval.Status;FeatureToasts.Show(notice);});}
 static void DrawRelations(float width){float top=4;var card=Card(width,ref top,"关系双方",246);float half=(card.width-16)/2;InterfaceMenu.Label(new Rect(card.x,card.y,half,24),"A 方",null,14);InterfaceMenu.Label(new Rect(card.x+half+16,card.y,half,24),"B 方",null,14);InterfaceMenu.Paragraph(new Rect(card.x,card.y+34,half,66),sideA.Count==0?"未选择":string.Join("、",sideA.Take(6).Select(actor=>actor.Profile?.Nickname??actor.ProfileId)),null,13);InterfaceMenu.Paragraph(new Rect(card.x+half+16,card.y+34,half,66),sideB.Count==0?"未选择":string.Join("、",sideB.Take(6).Select(actor=>actor.Profile?.Nickname??actor.ProfileId)),null,13);if(InterfaceMenu.Button(new Rect(card.x,card.y+116,half,36),"将当前所选设为 A 方"))Action(()=>Side(sideA));if(InterfaceMenu.Button(new Rect(card.x+half+16,card.y+116,half,36),"将当前所选设为 B 方"))Action(()=>Side(sideB));
  card=Card(width,ref top,"影响范围",138);half=(card.width-12)/2;if(InterfaceMenu.Button(new Rect(card.x,card.y,half,38),"只作用于所选 AI",!teamMode))teamMode=false;if(InterfaceMenu.Button(new Rect(card.x+half+12,card.y,half,38),"作用于双方整支队伍",teamMode))teamMode=true;
  card=Card(width,ref top,"设置双方关系",246);half=(card.width-12)/2;if(InterfaceMenu.Button(new Rect(card.x,card.y,half,40),"A ↔ B 强制交战"))Action(()=>SetRelations(true));if(InterfaceMenu.Button(new Rect(card.x+half+12,card.y,half,40),"A ↔ B 停战"))Action(()=>SetRelations(false));if(InterfaceMenu.Button(new Rect(card.x,card.y+56,card.width,34),"所选队伍强制互相敌对"))Action(()=>ForceSelected(Singleton<GameWorld>.Instance!,SelectedTargets()));if(InterfaceMenu.Button(new Rect(card.x,card.y+102,card.width,34),"恢复所有自定义关系"))Action(RestoreRelations);InterfaceMenu.Paragraph(new Rect(8,top+6,width-16,70),"已设置：AI 关系 "+relations.Actors.Count/2+" 对 · 队伍关系 "+relations.Teams.Count/2+" 对。可返回列表改选另一方，再进入本页设置。",null,13);}
 static void DrawEquipment(Rect rect){bool allowed=GUI.enabled;GUI.enabled=allowed&&targets.Count>0&&!AiEquipmentRemoval.Busy;if(InterfaceMenu.Button(new Rect(rect.x,rect.y,rect.width,36),AiEquipmentRemoval.Busy?"正在删除装备…":"一键删除所选 AI 全部武器（含近战）"))Action(()=>{AiEquipmentRemoval.RemoveWeapons(Singleton<GameWorld>.Instance!,targets.ToArray());notice=AiEquipmentRemoval.Status;});GUI.enabled=allowed;InterfaceMenu.Paragraph(new Rect(rect.x,rect.y+44,rect.width,52),AiEquipmentRemoval.Busy?AiEquipmentRemoval.Status:"删除主武器、副武器、手枪与近战武器及其配件。",null,12);rect=new Rect(rect.x,rect.y+108,rect.width,Math.Max(80,rect.height-108));if(targets.Count==1){InterfaceMenu.Label(new Rect(rect.x,rect.y,rect.width,30),"护甲、弹挂、口袋与武器",InterfaceMenu.Accent,17);InterfaceMenu.Paragraph(new Rect(rect.x,rect.y+46,rect.width,48),"编辑这名 AI 当前装备的参数；口袋和弹挂也可进入内部物品编辑。",null,14);if(InterfaceMenu.Button(new Rect(rect.x,rect.y+112,Math.Min(360,rect.width),42),"打开装备编辑器"))Action(()=>AiEquipmentEditor.Select(Singleton<GameWorld>.Instance!,Require(targets[0])));return;}InterfaceMenu.Paragraph(new Rect(rect.x,rect.y,rect.width,48),"选择一名 AI 查看其装备。",null,14);var view=new Rect(rect.x,rect.y+64,rect.width,Math.Max(80,rect.height-64));scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,rect.width-20,targets.Count*62));for(int i=0;i<targets.Count;i++){var actor=targets[i];InterfaceMenu.Label(new Rect(12,i*62,rect.width-210,42),actor.Profile?.Nickname??actor.ProfileId,null,14);if(InterfaceMenu.Button(new Rect(rect.width-188,i*62+4,160,36),"编辑装备"))Action(()=>AiEquipmentEditor.Select(Singleton<GameWorld>.Instance!,Require(actor)));}GUI.EndScrollView();}
}
