using System;
using Comfort.Common;
using EFT;
namespace LocalSpt5Esp;
internal sealed class LiveActorReference {
 internal readonly IntPtr World,Pointer;internal readonly string Profile,Name;internal readonly bool Local;
 internal LiveActorReference(GameWorld world,Player actor){World=world.Pointer;Pointer=actor.Pointer;Profile=actor.ProfileId;Name=actor.Profile?.Nickname??Profile;Local=actor.IsYourPlayer;}
 internal Player Resolve(){var world=Singleton<GameWorld>.Instance;if(!RaidClock.Active||world?.Pointer!=World)throw new ArgumentException("战局已改变，请重新选择角色。");Player? actor=null;if(Local&&world.MainPlayer?.Pointer==Pointer)actor=world.MainPlayer;else{var list=world.AllAlivePlayersList;if(list!=null)for(int i=0;i<list.Count;i++)if(list[i]?.Pointer==Pointer){actor=list[i];break;}}if(actor==null||actor.ProfileId!=Profile||actor.IsYourPlayer!=Local||actor.HealthController?.IsAlive!=true||!Local&&!actor.IsAI)throw new ArgumentException("所选角色已死亡、离开战局或被替换。");return actor;}
}
