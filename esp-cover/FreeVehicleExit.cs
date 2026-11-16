using System;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
namespace LocalSpt5Esp;
// Only a native zero-valued currency requirement qualifies. Other item payments
// and positive fares continue through the game's original inventory operations.
internal static class FreeVehicleExit {
 static bool Free(TransferItemRequirement requirement,Player player)=>RaidClock.Active&&player?.IsYourPlayer==true&&Singleton<GameWorld>.Instance?.MainPlayer?.Pointer==player.Pointer&&requirement.Count==0&&requirement.Id is "5449016a4bdc2d6f028b456f" or "5696686a4bdc2da3298b456a" or "569668774bdc2da2298b4568";
 internal static void Initialize(Plugin plugin){
  void Patch(string method,string prefix,Type[]? args=null)=>plugin.Patches.Patch(AccessTools.Method(typeof(TransferItemRequirement),method,args),prefix:new HarmonyMethod(typeof(FreeVehicleExit),prefix));
  Patch("TransferExitItems",nameof(Prepare),new[]{typeof(Player),typeof(ExfiltrationPoint),typeof(Il2CppSystem.Collections.Generic.IReadOnlyCollection<ItemsCount>),typeof(bool)});
  Patch("CanTransferItems",nameof(CanPay));Patch("TryGetTransferItems",nameof(Items));
  Patch("TransferExitItem",nameof(Single));
  Patch("TransferExitItems",nameof(Multiple),new[]{typeof(Player),typeof(Il2CppSystem.Collections.Generic.IReadOnlyCollection<ItemsCount>)});
 }
 static bool Prepare(TransferItemRequirement __instance,Player __0,ref Diz.LanguageExtensions.OperationResult<TransferItemRequirement.PreparedTransferResult> __result){
  if(!Free(__instance,__0))return true;
  var empty=new Il2CppSystem.Collections.Generic.List<IOperationResult>();
  __result=new TransferItemRequirement.PreparedTransferResult(empty.Cast<Il2CppSystem.Collections.Generic.IReadOnlyList<IOperationResult>>());return false;
 }
 static bool CanPay(TransferItemRequirement __instance,Player __0,ref int __1,ref bool __result){if(!Free(__instance,__0))return true;__1=0;__result=true;return false;}
 static bool Items(TransferItemRequirement __instance,Player __0,ref Il2CppReferenceArray<ItemsCount> __1,ref int __2,ref bool __result){if(!Free(__instance,__0))return true;__1=new Il2CppReferenceArray<ItemsCount>(0);__2=0;__result=true;return false;}
 static bool Single(TransferItemRequirement __instance,Player __0,ref bool __result){if(!Free(__instance,__0))return true;__result=true;return false;}
 static bool Multiple(TransferItemRequirement __instance,Player __0,ref bool __result)=>Single(__instance,__0,ref __result);
}
