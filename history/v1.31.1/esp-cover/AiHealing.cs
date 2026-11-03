using System;
using Comfort.Common;
using EFT;
namespace LocalSpt5Esp;
internal static class AiHealing {
 internal static void Heal(Player actor){var world=Singleton<GameWorld>.Instance??throw new ArgumentException("请进入战局。");new LiveActorReference(world,actor).Resolve();if(actor.IsYourPlayer||!(actor.IsAI||actor.AIData?.BotOwner!=null))throw new ArgumentException("只能治疗命中的 AI。");var health=actor.ActiveHealthController??throw new ArgumentException("AI 生命控制器未就绪。");foreach(var part in new[]{EBodyPart.Head,EBodyPart.Chest,EBodyPart.Stomach,EBodyPart.LeftArm,EBodyPart.RightArm,EBodyPart.LeftLeg,EBodyPart.RightLeg}){health.RemoveNegativeEffects(part);health.FullRestoreBodyPart(part);}health.RemoveNegativeEffects(EBodyPart.Common);health.RestoreFullHealth();health.ChangeHydration(health.Hydration.Maximum-health.Hydration.Current);health.ChangeEnergy(health.Energy.Maximum-health.Energy.Current);health.RemoveIntoxication();}
}
