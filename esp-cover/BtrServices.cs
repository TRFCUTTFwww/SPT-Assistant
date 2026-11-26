using System;
using BepInEx.Configuration;
using EFT;
using EFT.InventoryLogic;
using EFT.Vehicle;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class BtrServices {
 internal static ConfigEntry<bool> FreeRide=null!,FreeTransfer=null!,InstantTransfer=null!,LargeTransfer=null!;
 internal static void Initialize(Plugin plugin){
  const string section="18. BTR";
  FreeRide=FeatureConfig.Bind(section,"Free ride","免费乘车",false,"BTR 原生乘车服务报价和支付金额为 0；重新打开司机服务刷新报价。");
  FreeTransfer=FeatureConfig.Bind(section,"Free transfer","免费物品转移",false,"BTR 原生物品转移服务报价和支付金额为 0。");
  InstantTransfer=FeatureConfig.Bind(section,"Instant transfer","秒物品转移",false,"战局中交给 BTR 并完成托运后，立即通过邮件送达；失败会自动重试。");
  LargeTransfer=FeatureConfig.Bind(section,"Expanded transfer container","扩展转移仓（10×100）",false,"将 BTR 物品转移仓扩容为 10×100（1000 格），下一局生效。");
  InstantTransfer.SettingChanged+=DeliveryChanged;
  SaveDelivery();
  BtrInstantTransfer.Initialize(plugin);
  void Patch(Type type,string method,string prefix,Type[]? args=null)=>plugin.Patches.Patch(AccessTools.Method(type,method,args),prefix:new HarmonyMethod(typeof(BtrServices),prefix));
  Patch(typeof(BtrController),"GetPlayerTaxiPriceForProfile",nameof(Taxi));
  Patch(typeof(BtrTransferItemsController),"GetGridItemsPrice",nameof(TransferPrice),new[]{typeof(Stash)});
  Patch(typeof(BtrTransferItemsController),"GetDeliveryPriceForProfile",nameof(DeliveryPrice));
  Patch(typeof(BtrTransferItemsController),"CalculateServicePrice",nameof(ServicePrice));
  Patch(typeof(TraderServiceAffordabilityChecker),"CanAfford",nameof(Afford));
  Patch(typeof(TraderServicesItemPaymaster),"TryToPay",nameof(Pay));
  Patch(typeof(TransferItemsController),"GetGridSizeForProfileId",nameof(GridSize));
 }
 static void DeliveryChanged(object? sender,EventArgs args)=>SaveDelivery();
 internal static void Reset(){if(InstantTransfer!=null)InstantTransfer.SettingChanged-=DeliveryChanged;}
 static void SaveDelivery(){try{var settings=NextRaidLoot.Read();settings.InstantBtrDelivery=InstantTransfer.Value;NextRaidLoot.Save(settings);}catch(Exception error){Plugin.Self.Log.LogWarning("BTR delivery settings: "+error.Message);}}
 static bool Afford(TraderServiceAffordabilityChecker __instance,ref bool __result){if(!((__instance.ServiceType==ETraderServiceType.PlayerTaxi&&FreeRide.Value)||(__instance.ServiceType==ETraderServiceType.BtrItemsDelivery&&FreeTransfer.Value)))return true;__result=true;return false;}
 static bool Pay(TraderServicesItemPaymaster __instance,ref Il2CppSystem.Threading.Tasks.Task<bool> __result){
  // No fabricated cash item: only a payment whose native required amounts are
  // all zero may skip the inventory removal operation.
  if(__instance._itemsToPay==null)return true;bool any=false;
  foreach(var pair in __instance._itemsToPay){any=true;if(pair.Value!=0||pair.Key is not ("5449016a4bdc2d6f028b456f" or "5696686a4bdc2da3298b456a" or "569668774bdc2da2298b4568"))return true;}
  if(!any)return true;__result=Il2CppSystem.Threading.Tasks.Task.FromResult(true);return false;
 }
 static bool Taxi(ref int __result){if(!FreeRide.Value)return true;__result=0;return false;}
 static bool TransferPrice(ref int __result){if(!FreeTransfer.Value)return true;__result=0;return false;}
 static bool DeliveryPrice(ref double __result){if(!FreeTransfer.Value)return true;__result=0;return false;}
 static bool ServicePrice(ETraderServiceType __1,ref int __result){if(__1!=ETraderServiceType.BtrItemsDelivery||!FreeTransfer.Value)return true;__result=0;return false;}
 static bool GridSize(TransferItemsController __instance,ref IntVec2 __result){if(!LargeTransfer.Value||__instance.TryCast<BtrTransferItemsController>()==null)return true;__result=new IntVec2(10,100);return false;}
 internal static float Draw(float x,float y,float width){foreach(var entry in new ConfigEntryBase[]{FreeRide,FreeTransfer,InstantTransfer,LargeTransfer}){InterfaceMenu.DrawEntry(entry,new Rect(x,y,width,84));y+=96;}return y;}
}
