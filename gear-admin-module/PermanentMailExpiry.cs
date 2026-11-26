using HarmonyLib;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Dialog;
using SPTarkov.Server.Core.Services.Commerce;

namespace LocalGearMail;

internal static class PermanentMailExpiry {
 internal static long Lifetime=>CustomFleaOffers.PermanentEndTime-DateTimeOffset.UtcNow.ToUnixTimeSeconds();
 internal static void Install(Harmony harmony){
  harmony.Patch(AccessTools.PropertyGetter(typeof(InsuranceConfig),nameof(InsuranceConfig.StorageTimeOverrideSeconds)),postfix:new HarmonyMethod(typeof(PermanentMailExpiry),nameof(InsuranceStorage)));
  harmony.Patch(AccessTools.Method(typeof(MailSendService),nameof(MailSendService.SendMessageToPlayer)),prefix:new HarmonyMethod(typeof(PermanentMailExpiry),nameof(BeforeSend)));
 }
 static void InsuranceStorage(ref double __result){if(__result<0)__result=Lifetime;}
 internal static void Normalize(SendMessageDetails details){if(details.ItemsMaxStorageLifetimeSeconds is <0)details.ItemsMaxStorageLifetimeSeconds=Lifetime;}
 static void BeforeSend(SendMessageDetails __0)=>Normalize(__0);
}
