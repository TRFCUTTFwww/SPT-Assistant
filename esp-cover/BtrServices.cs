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
  FreeTransfer=FeatureConfig.Bind(section,"Free transfer","免费物品转移",false,"BTR 原生物品转移服务报价和支付金额为 0，不影响过图转移或掩护服务。");
  InstantTransfer=FeatureConfig.Bind(section,"Instant transfer","秒物品转移",false,"结算向服务端提交 BTR 物品后立即发信，无额外配送等待；战局内仍按原生方式暂存。需房主服务端安装 2.3。");
  LargeTransfer=FeatureConfig.Bind(section,"Expanded transfer container","扩展转移仓（10×100）",false,"1000 格。创建本局转移仓时应用；已创建的仓保持原尺寸，下一局生效，避免挤出已有物品。");
  InstantTransfer.SettingChanged+=DeliveryChanged;
  SaveDelivery();
  void Patch(Type type,string method,string prefix,Type[]? args=null)=>plugin.Patches.Patch(AccessTools.Method(type,method,args),prefix:new HarmonyMethod(typeof(BtrServices),prefix));
  Patch(typeof(BtrController),"GetPlayerTaxiPriceForProfile",nameof(Taxi));
  Patch(typeof(BtrTransferItemsController),"GetGridItemsPrice",nameof(TransferPrice),new[]{typeof(Stash)});
  Patch(typeof(BtrTransferItemsController),"GetDeliveryPriceForProfile",nameof(DeliveryPrice));
  Patch(typeof(BtrTransferItemsController),"CalculateServicePrice",nameof(ServicePrice));
  Patch(typeof(TransferItemsController),"GetGridSizeForProfileId",nameof(GridSize));
 }
 static void DeliveryChanged(object? sender,EventArgs args)=>SaveDelivery();
 internal static void Reset(){if(InstantTransfer!=null)InstantTransfer.SettingChanged-=DeliveryChanged;}
 static void SaveDelivery(){try{var settings=NextRaidLoot.Read();settings.InstantBtrDelivery=InstantTransfer.Value;NextRaidLoot.Save(settings);}catch(Exception error){Plugin.Self.Log.LogWarning("BTR delivery settings: "+error.Message);}}
 static bool Taxi(ref int __result){if(!FreeRide.Value)return true;__result=0;return false;}
 static bool TransferPrice(ref int __result){if(!FreeTransfer.Value)return true;__result=0;return false;}
 static bool DeliveryPrice(ref double __result){if(!FreeTransfer.Value)return true;__result=0;return false;}
 static bool ServicePrice(ETraderServiceType __1,ref int __result){if(__1!=ETraderServiceType.BtrItemsDelivery||!FreeTransfer.Value)return true;__result=0;return false;}
 static bool GridSize(TransferItemsController __instance,ref IntVec2 __result){if(!LargeTransfer.Value||__instance.TryCast<BtrTransferItemsController>()==null)return true;__result=new IntVec2(10,100);return false;}
 internal static float Draw(float x,float y,float width){foreach(var entry in new ConfigEntryBase[]{FreeRide,FreeTransfer,InstantTransfer,LargeTransfer}){InterfaceMenu.DrawEntry(entry,new Rect(x,y,width,84));y+=96;}return y;}
}
