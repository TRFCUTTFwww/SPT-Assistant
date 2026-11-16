using Comfort.Common;
using EFT;
using HarmonyLib;
namespace AllTheModium.Client;
public sealed partial class SwordModule {
 void InitializeHandsLifecycle(Harmony harmony){
  harmony.Patch(AccessTools.Method(typeof(Player.BaseKnifeController),nameof(Player.BaseKnifeController.Drop)),prefix:new HarmonyMethod(typeof(SwordModule),nameof(HandsDropping)));
  // Custom knives have no native firearm mastering group. Return the native
  // no-mastery result directly instead of logging every animation update.
  harmony.Patch(AccessTools.Method(typeof(SkillManager),nameof(SkillManager.GetMastering)),prefix:new HarmonyMethod(typeof(SwordModule),nameof(CustomMastering)));
 }
 static bool CustomMastering(string __0,ref Mastering __result){
  if(active?.context?.IsActive!=true||Swords.Find(__0)==null)return true;
  __result=null!;return false;
 }
 static void HandsDropping(Player.BaseKnifeController __instance){
  var a=active;if(a?.context?.IsActive!=true||__instance._player?.IsYourPlayer!=true)return;
  a.RestoreToolPose();a.ResetGuard();a.ClearToolMeshes();
 }
}
