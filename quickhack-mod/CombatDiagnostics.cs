using System.Text.Json;
using EFT;
using UnityEngine;

namespace LocalQuickhacks;
internal static class CombatDiagnostics {
    static string Home=>Path.Combine(Assets.Home,"diagnostics");
    internal static void Record(BotOwner owner,EnemyInfo? enemy,string stage) {
        try {
            Directory.CreateDirectory(Home);
            var actor=owner.GetPlayer; var target=enemy?.Person;
            var entry=new { utc=DateTime.UtcNow,stage,actor=actor?.ProfileId,target=target?.ProfileId,
                ownerState=owner.BotState.ToString(),active=actor?.gameObject.activeInHierarchy,standby=owner.StandBy?.StandByType.ToString(),
                goal=owner.Memory?.GoalEnemy?.Person?.ProfileId,peace=owner.Memory?.IsPeace,known=enemy?.HaveSeen,visible=enemy?.IsVisible,canShoot=enemy?.CanShoot,
                shooting=owner.ShootData?.Shooting,canShootByState=owner.ShootData?.CanShootByState,lastTrigger=owner.ShootData?.LastTriggerPressd,
                aiming=owner.AimingManager?.CurrentAiming?.GetType().Name,hands=actor?.HandsController?.GetIl2CppType()?.Name,
                friendly=target!=null?owner.BotsGroup?.IsAlly(target):null,
                groupEnemy=target!=null?owner.BotsGroup?.BotGroupHostility?.Enemies?.ContainsKey(target):null,
                nativeAlly=target!=null?owner.BotsGroup?.BotGroupHostility?.Allies?.ContainsKey(target):null,
                registeredGroupInfo=target!=null&&owner.BotsGroup?.BotGroupHostility?.Enemies?.ContainsKey(target)==true?owner.BotsGroup.BotGroupHostility.Enemies[target].Pointer==enemy?.GroupInfo?.Pointer:(bool?)null,
                distance=target!=null&&actor!=null?Vector3.Distance(actor.Position,target.Position):(float?)null };
            File.AppendAllText(Path.Combine(Home,"cyberpsychosis.jsonl"),JsonSerializer.Serialize(entry)+Environment.NewLine);
            Plugin.Logger.LogInfo($"Cyberpsychosis {stage}: goal={entry.goal}; visible={entry.visible}; canShoot={entry.canShoot}; fireState={entry.canShootByState}; ally={entry.friendly}; standby={entry.standby}");
        } catch(Exception e) { Runtime.Warn("Cyberpsychosis diagnostics",e); }
    }
}
