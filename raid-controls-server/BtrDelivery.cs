using System.Reflection;
using HarmonyLib;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.InRaid;
namespace LocalRaidControls;
internal static class BtrDelivery {
 [ThreadStatic]static HashSet<MongoId>? btrItems;
 static readonly FieldInfo saveField=typeof(BtrDeliveryService).GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Single(f=>f.FieldType==typeof(SaveServer));
 internal static void Install(Harmony h){
  h.Patch(AccessTools.Method(typeof(BtrDeliveryService),"HandleItemTransferEvent"),prefix:new HarmonyMethod(typeof(BtrDelivery),nameof(Begin)),finalizer:new HarmonyMethod(typeof(BtrDelivery),nameof(End)));
  h.Patch(AccessTools.Method(typeof(BtrDeliveryService),"HandleTransferItemDelivery"),prefix:new HarmonyMethod(typeof(BtrDelivery),nameof(BeforeQueue)),postfix:new HarmonyMethod(typeof(BtrDelivery),nameof(AfterQueue)));
 }
 static void Begin(EndLocalRaidRequestData __1,out HashSet<MongoId>? __state){
  __state=btrItems;btrItems=null;
  if(!Patches.Read().InstantBtrDelivery||__1.TransferItems==null)return;
  btrItems=__1.TransferItems.Where(p=>(p.Key.EndsWith("_btr",StringComparison.Ordinal)||p.Key.EndsWith("_BtrItemsDelivery",StringComparison.Ordinal))).SelectMany(p=>p.Value??[]).Select(i=>i.Id).ToHashSet();
 }
 static void End(HashSet<MongoId>? __state){btrItems=__state;}
 static void BeforeQueue(BtrDeliveryService __instance,MongoId __0,out HashSet<MongoId>? __state){
  __state=null;if(btrItems?.Count>0){var saves=(SaveServer)saveField.GetValue(__instance)!;__state=(saves.GetProfile(__0).BtrDeliveryList??[]).Select(p=>p.Id).ToHashSet();}
 }
 static void AfterQueue(BtrDeliveryService __instance,MongoId __0,HashSet<MongoId>? __state){
  if(__state==null||btrItems==null)return;
  var saves=(SaveServer)saveField.GetValue(__instance)!;
  foreach(var package in (saves.GetProfile(__0).BtrDeliveryList??[]).Where(p=>!__state.Contains(p.Id)&&p.Items?.Any(i=>btrItems.Contains(i.Id))==true).ToArray()){
   // Keep the native queue and insurance cleanup. Remove a package only after mail succeeds.
   package.ScheduledTime=(int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()-1;
   __instance.SendBTRDelivery(__0,package.Items!);
   __instance.RemoveBTRDeliveryPackageFromProfile(__0,package);
  }
 }
}
