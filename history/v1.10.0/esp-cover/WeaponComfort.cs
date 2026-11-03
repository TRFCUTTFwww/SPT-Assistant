using System;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class WeaponComfort {
 static ConfigEntry<bool> infinite=null!,noMalfunction=null!,noHeat=null!,lockDurability=null!;
 static string lastAmmo="";static IntPtr lastWeapon;static float nextTick,nextError;
 internal static void Initialize(Plugin p){
  infinite=FeatureConfig.Bind("28. 武器便利","Infinite ammunition","无限普通弹药（自动补满弹匣）",false,"补充当前弹种，不修改 AI 弹药；支持普通弹匣武器。");noMalfunction=FeatureConfig.Bind("28. 武器便利","No malfunction","禁用当前武器卡壳",false);noHeat=FeatureConfig.Bind("28. 武器便利","No overheat","禁用当前武器过热",false);lockDurability=FeatureConfig.Bind("28. 武器便利","Lock durability","锁定当前武器耐久",false);
  Hook(p,typeof(Player.FirearmController),"Shot",null,nameof(AfterShot));Hook(p,typeof(Player.FirearmController),"GetMalfunctionState",nameof(BeforeMalfunction),null);Hook(p,typeof(Weapon),"OnShot",nameof(BeforeWear),nameof(AfterWear));
 }
 static void Hook(Plugin p,Type type,string name,string? before,string? after){try{var method=type.GetMethods().Single(m=>m.DeclaringType==type&&m.Name==name);p.Patches.Patch(method,prefix:before==null?null:new HarmonyMethod(typeof(WeaponComfort),before),postfix:after==null?null:new HarmonyMethod(typeof(WeaponComfort),after));}catch(Exception e){p.Log.LogError("Weapon convenience hook failed: "+name+" "+e);}}
 static bool Local(Player.FirearmController c)=>RaidClock.Active&&c?._player?.IsYourPlayer==true;
 static bool Local(Weapon weapon){var held=Singleton<GameWorld>.Instance?.MainPlayer?.HandsController?.TryCast<Player.FirearmController>()?.Item;return RaidClock.Active&&held!=null&&held.Pointer==weapon.Pointer;}
 static bool BeforeMalfunction(Player.FirearmController __instance,ref Weapon.EMalfunctionState __result,ref Weapon.EMalfunctionSource __6){if(!noMalfunction.Value||!Local(__instance))return true;__result=Weapon.EMalfunctionState.None;__6=(Weapon.EMalfunctionSource)0;return false;}
 static void BeforeWear(Weapon __instance,out float? __state){__state=lockDurability.Value&&Local(__instance)?__instance.Repairable?.Durability:null;}
 static void AfterWear(Weapon __instance,float? __state){if(__state is float value&&__instance.Repairable!=null)__instance.Repairable.Durability=value;if(noHeat.Value&&Local(__instance))__instance.MalfState?.ResetOverheat();}
 static void AfterShot(Player.FirearmController __instance){if(Local(__instance))try{Refill(__instance);}catch(Exception e){Warn(e);}}
 internal static void Tick(Player? player){if(!RaidClock.Active||player==null){lastWeapon=IntPtr.Zero;lastAmmo="";return;}if(Time.unscaledTime<nextTick)return;nextTick=Time.unscaledTime+.1f;try{var gun=player.HandsController?.TryCast<Player.FirearmController>();if(gun==null)return;if(noHeat.Value)gun.Item.MalfState?.ResetOverheat();Refill(gun);}catch(Exception e){Warn(e);}}
 static void Refill(Player.FirearmController gun){var weapon=gun.Item;if(weapon==null)return;if(lastWeapon!=weapon.Pointer){lastWeapon=weapon.Pointer;lastAmmo="";}var mag=weapon.GetCurrentMagazine();var slot=mag?.Cartridges;if(slot==null)return;var ammo=slot.Last?.TryCast<Ammo>()??weapon.FirstLoadedChamberSlot?.ContainedItem?.TryCast<Ammo>();if(ammo!=null)lastAmmo=ammo.StringTemplateId;if(!infinite.Value||lastAmmo.Length==0||slot.Count>=slot.MaxCount||gun.IsInReloadOperation())return;var factory=Singleton<ItemFactory>.Instance;if(factory==null)return;int remaining=slot.MaxCount-slot.Count;int budget=100;while(remaining>0&&budget-->0){var created=factory.CreateItem(factory.NextId.ToString(),lastAmmo,null)?.TryCast<Ammo>();if(created==null)break;created.StackObjectsCount=Math.Min(remaining,Math.Max(1,created.StackMaxSize));if(!slot.Add(created,false).Succeeded)break;remaining-=created.StackObjectsCount;}mag!.RaiseRefreshEvent(false,true);}
 static void Warn(Exception e){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+20;Plugin.Self.Error(e);}
}
