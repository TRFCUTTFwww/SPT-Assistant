using System;
using EFT;
using HarmonyLib;
namespace LocalSpt5Esp;
// Supply native requests and widen native pursuit decisions, never write motion.
internal static class CompanionNativeControl {
 internal sealed class State {
  internal BaseBrain? Brain;internal PeacefulRequestLayer? Requests;internal PursuitLayer? Pursuit;internal int LayerIndex,PursuitIndex;
  internal BotSettings? Original,Extended;internal BotPriorityAxeTarget? Priority;
  internal bool AllPursuit,HadPursuitableEnemy;internal Il2CppSystem.Collections.Generic.HashSet<WildSpawnType>? Types,ExtendedTypes;
 }
 internal static void Initialize(Plugin plugin){
  plugin.Patches.Patch(AccessTools.Method(typeof(BossBlackDivision),"CanDoPersuit"),postfix:new HarmonyMethod(typeof(CompanionNativeControl),nameof(AllowPursuit)));
  plugin.Patches.Patch(AccessTools.Method(typeof(BossBlackDivision),"ShouldControlBuilding"),postfix:new HarmonyMethod(typeof(CompanionNativeControl),nameof(ReleaseBuilding)));
  plugin.Patches.Patch(AccessTools.Method(typeof(BlackDivisionPersuit),"ShallUseNow"),postfix:new HarmonyMethod(typeof(CompanionNativeControl),nameof(UsePursuit)));
  plugin.Patches.Patch(AccessTools.Method(typeof(PursuitLayer),"ShallUseNow"),postfix:new HarmonyMethod(typeof(CompanionNativeControl),nameof(UseGenericPursuit)));
  plugin.Patches.Patch(AccessTools.Method(typeof(BotPriorityAxeTarget),"IsInPossibleRadius"),postfix:new HarmonyMethod(typeof(CompanionNativeControl),nameof(PursuitRadius)));
 }
 internal static void EnsureRequests(CompanionSquad.Member member,Player actor){
  var brain=actor.AIData?.BotOwner?.Brain?.BaseBrain??throw new ArgumentException("队友原生行为控制器尚未就绪。");var state=member.Native;
  if(state.Brain?.Pointer==brain.Pointer)return;
  RemoveRequests(state);
  // Ordinary roles already include this native layer. Black Division does not.
  state.Brain=brain;
  if(!brain.HaveLayer<PeacefulRequestLayer>(out _)){
   int index=10000;while(brain._layers.ContainsKey(index))index++;
   var layer=new PeacefulRequestLayer(actor.AIData!.BotOwner,99);
   if(!brain.TryAddLayer(index,layer,true)){state.Brain=null;throw new ArgumentException("未能添加原生队伍指令层。");}
   state.Requests=layer;state.LayerIndex=index;
   Plugin.Self.Log.LogInfo($"Companion native request layer added: profile={actor.ProfileId}; priority=99; index={index}");
  }
  if(!brain.HaveLayer<PursuitLayer>(out _)){
   int index=10001;while(brain._layers.ContainsKey(index))index++;
   var layer=new PursuitLayer(actor.AIData!.BotOwner,98);
   if(!brain.TryAddLayer(index,layer,true)){RemoveRequests(state);throw new ArgumentException("未能添加原生追击层。");}
   state.Pursuit=layer;state.PursuitIndex=index;
  }
 }
 static void RemoveRequests(State state){
  if(state.Brain!=null&&state.Requests!=null){state.Brain.DeactivateLayer(state.LayerIndex);state.Brain._layers.Remove(state.LayerIndex);}
  if(state.Brain!=null&&state.Pursuit!=null){state.Brain.DeactivateLayer(state.PursuitIndex);state.Brain._layers.Remove(state.PursuitIndex);}
  state.Brain=null;state.Requests=null;state.Pursuit=null;
 }
 internal static void ExtendPursuit(CompanionSquad.Member member,Player actor,Player target){
  var owner=actor.AIData!.BotOwner;var state=member.Native;
  if(state.Extended==null){
   var original=owner.Settings??throw new ArgumentException("队友原生设置尚未就绪。");
   // The game's Copy() shares Mind. Clone every branch that is changed.
   var extended=original.MemberwiseClone().Cast<BotSettings>();
   var file=original.FileSettings.MemberwiseClone().Cast<BotSettingsComponents>();
   var mind=original.FileSettings.Mind.MemberwiseClone().Cast<BotGlobalsMindSettings>();
   float range=CompanionRules.PursuitDistance;mind.MAX_AGGRO_BOT_DIST=range;mind.MAX_AGGRO_BOT_DIST_SQR=range*range;
   mind.MAX_AGGRO_BOT_DIST_UPPER_LIMIT=range;mind.MAX_AGGRO_BOT_DIST_SQR_UPPER_LIMIT=range*range;
   mind.TIME_TO_FORGOR_ABOUT_ENEMY_SEC=Math.Max(mind.TIME_TO_FORGOR_ABOUT_ENEMY_SEC,7200);mind.SEARCH_TARGET=true;
   mind.WILL_PERSUE_AXEMAN=true;mind.MAX_DIST_TO_RUN_PERSUE_AXEMAN=range;mind.MAX_DIST_TO_PERSUE_AXEMAN=range;mind.MAX_DIST_TO_PERSUE_AXEMAN_FORCE=range;
   file.Mind=mind;extended.FileSettings=file;state.Original=original;state.Extended=extended;state.HadPursuitableEnemy=owner.EnemiesController.HavePursuitableEnemy;owner.Settings=extended;
   if(owner.PriorityAxeTarget is {} priority){
    state.Priority=priority;state.AllPursuit=priority.AllPursuit;state.Types=priority._persuitTypes;
    var types=new Il2CppSystem.Collections.Generic.HashSet<WildSpawnType>();if(state.Types!=null)foreach(var role in state.Types)types.Add(role);
    state.ExtendedTypes=types;priority._persuitTypes=types;priority.AllPursuit=true;
   }
   Plugin.Self.Log.LogInfo($"Companion native pursuit enabled: profile={actor.ProfileId}; range={range}; isolatedSettings={extended.Pointer!=original.Pointer&&file.Mind.Pointer!=original.FileSettings.Mind.Pointer}");
  }
  if(target.Profile?.Info?.Settings is {} settings)state.ExtendedTypes?.Add(settings.Role);
  if(state.Priority!=null)state.Priority.AllPursuit=true;
  owner.EnemiesController.HavePursuitableEnemy=true;
 }
 internal static void RestorePursuit(CompanionSquad.Member member,Player actor){
  var state=member.Native;var owner=actor.AIData?.BotOwner;
  if(state.Extended!=null&&owner?.Settings?.Pointer==state.Extended.Pointer&&state.Original!=null){owner.Settings=state.Original;owner.EnemiesController.HavePursuitableEnemy=state.HadPursuitableEnemy;}
  if(state.Priority!=null){state.Priority.AllPursuit=state.AllPursuit;if(state.Priority._persuitTypes?.Pointer==state.ExtendedTypes?.Pointer)state.Priority._persuitTypes=state.Types;}
  state.Extended=null;state.Original=null;state.Priority=null;state.Types=state.ExtendedTypes=null;
 }
 internal static void Retire(CompanionSquad.Member member,Player actor){RestorePursuit(member,actor);RemoveRequests(member.Native);}
 static bool Hunting(BotOwner? owner){
  var actor=owner?.GetPlayer;if(!CompanionSquad.TryMember(actor,out var member)||member.Order!=CompanionOrder.Hunt||!CompanionSquad.NativeActionAllowed(actor!))return false;
  var target=owner!.Memory?.GoalEnemy?.Person?.TryCast<Player>();return target?.HealthController?.IsAlive==true&&!target.IsYourPlayer&&!CompanionSquad.Owns(target)&&!AiTeamControl.IsTeammate(target)&&target.Profile?.Info?.Settings?.Role!=WildSpawnType.shooterBTR;
 }
 static void AllowPursuit(BossBlackDivision __instance,ref bool __result){if(Hunting(__instance.Owner))__result=true;}
 static void ReleaseBuilding(BossBlackDivision __instance,ref bool __result){if(Hunting(__instance.Owner))__result=false;}
 static void PursuitRadius(BotPriorityAxeTarget __instance,ref bool __result){if(Hunting(__instance._owner)&&__instance._closestPerson?.Person?.TryCast<Player>() is {} target&&target.HealthController?.IsAlive==true&&!target.IsYourPlayer&&!CompanionSquad.Owns(target)&&!AiTeamControl.IsTeammate(target))__result=true;}
 static void UsePursuit(BlackDivisionPersuit __instance,ref bool __result){
  var owner=__instance._owner;if(!Hunting(owner)||owner.Memory.GoalEnemy.IsVisible||owner.Memory.GoalEnemy.CanShoot)return;
  if(owner.PriorityAxeTarget?.HavePointToGo==true){owner.EnemiesController.HavePursuitableEnemy=true;__result=true;}
 }
 static void UseGenericPursuit(PursuitLayer __instance,ref bool __result){
  var owner=__instance._owner;var actor=owner?.GetPlayer;if(!CompanionSquad.TryMember(actor,out var member))return;
  bool injected=member.Native.Pursuit?.Pointer==__instance.Pointer;
  if(injected&&!Hunting(owner)){__result=false;return;}
  if(Hunting(owner)&&owner!.Memory.GoalEnemy.IsVisible!=true&&owner.Memory.GoalEnemy.CanShoot!=true&&owner.PriorityAxeTarget?.HavePointToGo==true){owner.EnemiesController.HavePursuitableEnemy=true;__result=true;}
  else if(injected)__result=false;
 }
 internal static void BeforeBrain(BotOwner owner){
  try{
   var actor=owner.GetPlayer;if(!CompanionSquad.TryMember(actor,out var member))return;var state=member.Native;
   if(state.Brain==null||state.Brain.Pointer!=owner.Brain?.BaseBrain?.Pointer)return;
   if(state.Requests!=null){bool wanted=CompanionSquad.NativeActionAllowed(actor)&&member.Order is not (CompanionOrder.Hunt or CompanionOrder.Autonomous)&&!CompanionSquad.InCombat(actor);SetLayer(state.Brain,state.Requests,state.LayerIndex,wanted);}
   if(state.Pursuit!=null){bool wanted=Hunting(owner)&&owner.Memory.GoalEnemy.IsVisible!=true&&owner.Memory.GoalEnemy.CanShoot!=true;SetLayer(state.Brain,state.Pursuit,state.PursuitIndex,wanted);}
  }catch(Exception error){if(UnityEngine.Time.unscaledTime>=nextWarning){nextWarning=UnityEngine.Time.unscaledTime+15;Plugin.Self.Log.LogWarning("Companion native layer state: "+error.Message);}}
 }
 static float nextWarning;
 static void SetLayer(BaseBrain brain,AICoreLayer<BotLogicDecision> layer,int index,bool wanted){if(brain.IsLayerActive(layer)==wanted)return;if(wanted)brain.ActivateLayer(index);else brain.DeactivateLayer(index);}
}
