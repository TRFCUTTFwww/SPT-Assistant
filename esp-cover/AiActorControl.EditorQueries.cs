using System;
using System.Linq;
using Comfort.Common;
using EFT;
namespace LocalSpt5Esp;
internal static partial class AiActorControl {
 internal static bool EditorPlayerCeasefire(Player actor){
  var current=Singleton<GameWorld>.Instance;new LiveActorReference(current,actor).Resolve();
  var human=current.MainPlayer;var owner=actor.AIData?.BotOwner;
  return human!=null&&owner!=null&&Ignored(owner,human);
 }
 internal static bool EditorSidesHostile(){
  if(!EditorSidesReady)return false;
  var current=Singleton<GameWorld>.Instance;var live=current?.AllAlivePlayersList?.ToArray()??Array.Empty<Player>();
  Player[] Present(System.Collections.Generic.List<Player> marked)=>live.Where(p=>p!=null&&p.HealthController?.IsAlive==true&&marked.Any(m=>m.Pointer==p.Pointer&&m.ProfileId==p.ProfileId)).ToArray();
  var a=Present(sideA);var b=Present(sideB);if(a.Length==0||b.Length==0)return false;
  return a.All(left=>b.All(right=>left.Pointer!=right.Pointer&&relations.Resolve(left.ProfileId,right.ProfileId,left.AIData?.BotOwner?.BotsGroup?.Id,right.AIData?.BotOwner?.BotsGroup?.Id)==true));
 }
}
