using System;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class ToyRangeControl {
 internal static ConfigEntry<bool> Enabled=null!;internal static ConfigEntry<float> Maximum=null!;
 internal static void Initialize(Plugin plugin){const string section="50. 玩具枪工具";Enabled=FeatureConfig.Bind(section,"Toy custom maximum range","玩具枪自定义最大射程",false,"仅作用于本地玩家的玩具枪真实弹丸；开关独立于命中编辑。");Maximum=FeatureConfig.Bind(section,"Toy maximum range meters","玩具枪最大射程（米）",500f,"到达上限后结束弹丸。调高会延长原弹丸寿命；仍受真实下坠、阻力和障碍碰撞影响。",new AcceptableValueRange<float>(1,10000));plugin.Patches.Patch(typeof(Shot).GetMethod("UpdateBulletPosition",new[]{typeof(float)}),prefix:new HarmonyMethod(typeof(ToyRangeControl),nameof(BeforePosition)),postfix:new HarmonyMethod(typeof(ToyRangeControl),nameof(AfterPosition)));}
 static bool Applies(Shot shot)=>Enabled.Value&&shot.Weapon?.StringTemplateId==ToyDeveloper.Toy&&Singleton<GameWorld>.Instance?.MainPlayer?.RaidId==shot.PlayerRaidId&&RaidClock.Active;
 static void BeforePosition(Shot __instance,out Vector3 __state){__state=Applies(__instance)?__instance.CurrentPosition:new Vector3(float.NaN,0,0);}
 static void AfterPosition(Shot __instance,Vector3 __state){if(!float.IsFinite(__state.x)||!Applies(__instance))return;var origin=__instance.MasterOrigin;var end=__instance.CurrentPosition;if(ToyRangeRules.Clip(new(origin.x,origin.y,origin.z),new(__state.x,__state.y,__state.z),new(end.x,end.y,end.z),Maximum.Value,out var clipped))__instance._currentPosition=new Vector3(clipped.X,clipped.Y,clipped.Z);}
 internal static void ShotCreated(Shot shot){if(!Applies(shot))return;float speed=Math.Max(.1f,shot.StartVelocity.magnitude);shot.AmmoLifeTime=Math.Max(shot.AmmoLifeTime,Math.Min(1800,Maximum.Value/speed*2+10));}
 internal static bool AtLimit(Shot shot)=>Applies(shot)&&(shot.CurrentPosition-shot.MasterOrigin).sqrMagnitude>=Math.Max(0,Maximum.Value-.001f)*Math.Max(0,Maximum.Value-.001f);
 internal static bool AllowedHit(Shot shot)=>!Applies(shot)||(shot.HitPoint-shot.MasterOrigin).sqrMagnitude<=(Maximum.Value+.01f)*(Maximum.Value+.01f);
 internal static void Finish(Shot shot,bool limit){if(limit)shot.BulletState=Shot.EBulletState.StopHit;}
}
