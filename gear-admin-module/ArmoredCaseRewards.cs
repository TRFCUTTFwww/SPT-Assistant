using HarmonyLib;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
namespace LocalGearMail;

// Supply the missing 1.3 story-case contents through the native reward path.
// Inventory placement, item-event replies and case consumption remain native.
public static class ArmoredCaseRewards {
 public const string Case="68dfc07da7cfeed3e60204e7",Keycard="67bdea8667098e658f064695",Instructions="689b4f338dbf3fd40f0bc778";
 const string PatchId="local.spt5.armored-case-rewards";
 public static RewardDetails Create()=>new(){RewardCount=0,FoundInRaid=false,RewardTplPool=[],GuaranteedItems=new(){[new MongoId(Keycard)]=new(){Min=1,Max=1},[new MongoId(Instructions)]=new(){Min=1,Max=1}}};
 public static bool Install(TemplateTable templates){
  if(new[]{Case,Keycard,Instructions}.Any(id=>!templates.Items.ContainsKey(new MongoId(id))))return false;
  var harmony=new Harmony(PatchId);
  // Replace an earlier hot-loaded generation so its collectible context can exit.
  harmony.UnpatchSelf();
  harmony.Patch(AccessTools.Method(typeof(InventoryHelper),nameof(InventoryHelper.GetRandomLootContainerRewardDetails)),postfix:new HarmonyMethod(typeof(ArmoredCaseRewards),nameof(UseMissingRewards)));
  return true;
 }
 public static void UseMissingRewards(MongoId __0,ref RewardDetails? __result){if(__result==null&&__0.ToString()==Case)__result=Create();}
}