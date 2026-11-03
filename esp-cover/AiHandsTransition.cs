using System;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
namespace LocalSpt5Esp;
internal static class AiHandsTransition {
 static readonly Callback<IEmptyHandsController> forcedReady=(Action<Result<IEmptyHandsController>>)(_=>{});
 internal static bool IsEmpty(Player actor){var hands=actor.HandsController;return hands!=null&&(hands.TryCast<Player.EmptyHandsController>()!=null||hands.Item==null||hands.Item.TryCast<EmptyHands>()!=null);}
 internal static void ForceEmpty(Player actor){
  var current=Singleton<GameWorld>.Instance??throw new ArgumentException("请进入战局。");new LiveActorReference(current,actor).Resolve();if(actor.IsYourPlayer||!(actor.IsAI||actor.AIData?.BotOwner!=null))throw new ArgumentException("只能强制清理当前 AI 的持枪状态。");if(IsEmpty(actor))return;
  try{actor.HandsController?.TryCast<Player.FirearmController>()?.SetTriggerPressed(false);}catch{}
  var scheduled=actor._ScheduledProcess_k__BackingField;actor._ScheduledProcess_k__BackingField=null!;try{scheduled?.Abort();}catch(Exception e){Plugin.Self.Log.LogWarning("Abort AI scheduled hands: "+e.Message);}
  actor._processStatus=Player.EProcessStatus.None;
  try{actor.FastForwardCurrentOperations();}catch{}
  if(!IsEmpty(actor)&&actor.HandsController!=null)actor.DestroyHandsController();
  if(!IsEmpty(actor))actor.SetEmptyHands(forcedReady);
  Plugin.Self.Log.LogInfo("Forced AI empty hands: "+actor.ProfileId+"; empty="+IsEmpty(actor));
 }
 internal static void Empty(Player actor,Callback<IEmptyHandsController> callback,bool direct=false){
  if(direct){try{actor.FastForwardCurrentOperations();}catch{}actor.SetEmptyHands(callback);return;}
  try{actor.Proceed(false,callback,true);}catch(Exception error){Plugin.Self.Log.LogWarning("AI scheduled empty hands failed; retry direct SetEmptyHands: "+error);Empty(actor,callback,true);}
 }
}
