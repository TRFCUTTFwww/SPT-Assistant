using HarmonyLib;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Helpers.Ragfair;
namespace LocalRaidControls;
public static class TraderRepairRules {
 [ThreadStatic]static int merchantDepth;
 internal static void Install(Harmony harmony){
  harmony.Patch(AccessTools.Method(typeof(RepairController),"TraderRepair"),prefix:new HarmonyMethod(typeof(TraderRepairRules),nameof(Enter)),finalizer:new HarmonyMethod(typeof(TraderRepairRules),nameof(Leave)));
  harmony.Patch(AccessTools.Method(typeof(RepairHelper),"UpdateItemDurability"),prefix:new HarmonyMethod(typeof(TraderRepairRules),nameof(KeepMaximum)));
 }
 static void Enter(out int __state){__state=merchantDepth;if(Patches.Read().TraderRepairNoLoss)merchantDepth++;}
 static void Leave(int __state){merchantDepth=__state;}
 static void KeepMaximum(bool __4,ref bool __6){if(merchantDepth>0&&!__4)__6=false;}
}
