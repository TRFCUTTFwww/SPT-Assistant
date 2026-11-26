using System;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using EFT.Ballistics;
using HarmonyLib;
namespace LocalSpt5Esp;
internal static class PlayerRicochet {
 static ConfigEntry<bool> enabled=null!;
 internal static void Initialize(Plugin plugin){enabled=FeatureConfig.Bind("29. 生存便利","Full body ricochet","玩家全身百分百跳弹",false,"玩家全身部位（含无护甲处）对枪弹 100% 触发跳弹。");var parameters=new[]{typeof(float),typeof(Shot),typeof(UnityEngine.Vector3),typeof(UnityEngine.Vector3),typeof(UnityEngine.Vector3)};foreach(var type in new[]{typeof(BallisticCollider),typeof(BodyPartCollider)}){var method=type.GetMethod("Deflects",parameters)??throw new MissingMethodException(type.Name,"Deflects");plugin.Patches.Patch(method,prefix:new HarmonyMethod(typeof(PlayerRicochet),nameof(Deflect)));}}
 static bool Deflect(BallisticCollider __instance,Shot __1,ref bool __result){if(enabled?.Value!=true||__1==null)return true;var body=__instance.TryCast<BodyPartCollider>();if(body==null)return true;var player=Singleton<GameWorld>.Instance?.MainPlayer;if(player?.IsYourPlayer!=true||body.PlayerProfileID!=player.ProfileId)return true;__result=true;return false;}
 internal static bool Blocks(ActiveHealthController health,DamageInfo info){if(enabled?.Value!=true||info?.DamageType!=EDamageType.Bullet)return false;var player=Singleton<GameWorld>.Instance?.MainPlayer;return player?.IsYourPlayer==true&&player.ActiveHealthController?.Pointer==health.Pointer;}
 internal static void Command(string[] args){if(args.Length!=1||args[0] is not ("on" or "off"))throw new ArgumentException("/ricochet on|off");enabled.Value=args[0]=="on";RaidChat.Output("玩家全身跳弹："+(enabled.Value?"开":"关"));}
}
