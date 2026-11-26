using System;
using Comfort.Common;
using EFT;
namespace LocalSpt5Esp;
// Human destinations are separate from LiveActorReference, whose AI-only guard
// protects every existing batch actor editing and movement command.
internal sealed class PlayerTeleportTarget {
 readonly IntPtr world,pointer;readonly string profile;
 internal readonly string Name;
 internal PlayerTeleportTarget(GameWorld current,Player actor){world=current.Pointer;pointer=actor.Pointer;profile=actor.ProfileId;Name=actor.Profile?.Nickname??profile;}
 internal Player Resolve(){
  var current=Singleton<GameWorld>.Instance;
  if(!RaidClock.Active||current?.Pointer!=world)throw new ArgumentException("战局已改变，请重新选择玩家。");
  var list=current.AllAlivePlayersList;
  if(list!=null)for(int i=0;i<list.Count;i++){
   var actor=list[i];if(actor==null||actor.Pointer!=pointer)continue;
   if(actor.ProfileId==profile&&Eligible(current,actor))return actor;break;
  }
  throw new ArgumentException("目标玩家已死亡、离开或被替换，请刷新列表。");
 }
 internal static bool Eligible(GameWorld current,Player actor)=>!actor.IsYourPlayer&&actor.Pointer!=current.MainPlayer?.Pointer&&actor.ProfileId!=current.MainPlayer?.ProfileId&&actor.HealthController?.IsAlive==true&&!FikaTeams.IsAi(actor);
}
