using EFT;
using HarmonyLib;
namespace LocalSpt5Esp;
internal static class AiHostilityEvaluation {
 internal static void Initialize(Plugin plugin){
  plugin.Patches.Patch(typeof(BotHostilityService).GetMethod("EvaluateHostility",new[]{typeof(BotGroupInnerHostilityData),typeof(BotHostilityData)}),postfix:new HarmonyMethod(typeof(AiHostilityEvaluation),nameof(Bot)));
  plugin.Patches.Patch(typeof(BotHostilityService).GetMethod("EvaluateHostility",new[]{typeof(BotGroupInnerHostilityData),typeof(PlayerHostilityData)}),postfix:new HarmonyMethod(typeof(AiHostilityEvaluation),nameof(Human)));
 }
 static void Bot(BotGroupInnerHostilityData __0,BotHostilityData __1,ref HostilityEvaluationResult __result)=>Apply(__0,__1?.Player,ref __result);
 static void Human(BotGroupInnerHostilityData __0,PlayerHostilityData __1,ref HostilityEvaluationResult __result)=>Apply(__0,__1?.Player,ref __result);
 static void Apply(BotGroupInnerHostilityData? data,IPlayer? target,ref HostilityEvaluationResult result){
  var group=data?.OwnedGroup?.TryCast<BotsGroup>();if(group==null||target==null)return;
  if(AiActorControl.NativeGroupStatus(group,target) is {} desired)result=new(desired,"SPT Assistant consistent evaluated relation");
  // UpdateHostilityStatusForAllPlayers uses this returned status to index the
  // matching dictionary. Overriding only UpdatePlayerHostilityCollection leaves
  // its caller indexing the old dictionary and throws every update.
 }
}
