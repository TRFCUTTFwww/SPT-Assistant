using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.InputSystem;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
namespace ProjectE.Client;
public sealed partial class ProjectModule {
 Transmutation? table;MinecraftHud? hud;ProjectResources? resources;InventoryScreenLifecycle? inventoryScreens;
 CatalogSync? catalog;ConfigEntry<bool> catalogEnabled=null!;ConfigEntry<KeyCode> catalogKey=null!;
 ConfigEntry<bool> hudEnabled=null!,hudDiagnostics=null!,showHealth=null!,showHunger=null!,showHydration=null!,showExperience=null!,showHotbar=null!;
 ConfigEntry<float> hudScale=null!,volume=null!,burnVolume=null!;
 ConfigEntry<KeyCode> tableKey=null!;
 int releaseFrame;
 void InitializeExtra(Harmony harmony){
  hudEnabled=settingsFile!.Bind("HUD","Enabled",true,"显示原版 MC 爱心、饱食度、经验条及同步塔科夫快捷栏。");
  showHealth=settingsFile.Bind("HUD","Show hearts",true,"显示 MC 爱心生命条。");
  showHunger=settingsFile.Bind("HUD","Show hunger",true,"显示 MC 饱食度条。");
  showHydration=settingsFile.Bind("HUD","Show hydration",true,"显示 MC 口渴条。");
  showExperience=settingsFile.Bind("HUD","Show experience",true,"显示 MC 经验条和等级。");
  showHotbar=settingsFile.Bind("HUD","Show hotbar",true,"显示 MC 风格物品快捷栏；不改变原生快捷键和物品绑定。");
  hudDiagnostics=settingsFile.Bind("Diagnostics","HUD enabled",false,"临时记录快捷栏诊断文件；默认关闭，避免战局内周期性写盘。");
  hudScale=settingsFile.Bind("HUD","Scale",1.35f,new ConfigDescription("底部 HUD 缩放，最终取整为像素倍率，保持原版素材清晰。",new AcceptableValueRange<float>(.6f,2.5f)));
  volume=settingsFile.Bind("Audio","Volume",.65f,new ConfigDescription("ProjectE 原版音效音量。",new AcceptableValueRange<float>(0,1)));
  burnVolume=settingsFile.Bind("Audio","EMC conversion volume",.35f,new ConfigDescription("消耗物品换成 EMC 时的额外音量倍率，同时用于首次学习提示。",new AcceptableValueRange<float>(0,1)));
  tableKey=settingsFile.Bind("Table","Open key",KeyCode.F10,"转换桌开关快捷键。");
  catalogEnabled=settingsFile.Bind("Hot update","Catalog sync",true,"服务端逻辑发布后在线同步物品模板、本地化、图鉴与商人，无需重新登录。");
  catalogKey=settingsFile.Bind("Hot update","Sync key",KeyCode.F9,"手动刷新在线模板与商人目录。");
  resources=new(context!);table=new(context!,resources);hud=new(context!,resources);inventoryScreens=new(context!);context!.OnShutdown(()=>{table?.Clear();hud?.Clear();});
  harmony.Patch(AccessTools.Method(typeof(GamePlayerOwner),"TranslateAxes"),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(TableAxes)));
  harmony.Patch(AccessTools.Method(typeof(GamePlayerOwner),"TranslateCommand"),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(TableCommand)));
  harmony.Patch(AccessTools.Method(typeof(InputManager),"Update"),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(TableInput)));
  harmony.Patch(AccessTools.Method(typeof(EventSystem),"Update"),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(TableInput)));
  harmony.Patch(AccessTools.Method(typeof(Player.FirearmController),"SetTriggerPressed",[typeof(bool)]),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(TableTrigger)));
  InitializeTools(harmony);
  catalog=new(context!);
  context.Log(Local.ModHost.LogLevel.Info,"ProjectE ready: F10 transmutation table, original sounds, MC survival HUD and native hotbar.");
 }
 static bool TableBlocked=>active?.context?.IsActive==true&&(active.table?.Open==true||Time.frameCount<=active.releaseFrame);
 static bool TableAxes(GamePlayerOwner __instance)=>!TableBlocked||__instance.Player?.IsYourPlayer!=true;
 static bool TableCommand(GamePlayerOwner __instance,ref InputNode.ETranslateResult __result){if(!TableBlocked||__instance.Player?.IsYourPlayer!=true)return true;__result=InputNode.ETranslateResult.BlockAll;return false;}
 static bool TableInput()=>!TableBlocked;
 static bool TableTrigger(Player.FirearmController __instance,bool __0)=>!TableBlocked||!__0||__instance._player?.IsYourPlayer!=true;
 void TickExtra(){
  if(table==null)return;table.Volume=volume.Value;table.BurnVolume=burnVolume.Value;
  if(Input.GetKeyDown(tableKey.Value)){if(!table.Open)Singleton<GameWorld>.Instance?.MainPlayer?.HandsController?.TryCast<Player.FirearmController>()?.SetTriggerPressed(false);table.Toggle();releaseFrame=Time.frameCount+2;}
  else if(table.Open&&Input.GetKeyDown(KeyCode.Escape)){table.SetOpen(false);releaseFrame=Time.frameCount+2;}
  if(table.Open||Time.frameCount<=releaseFrame&&(Input.GetMouseButton(0)||Input.GetMouseButton(1)))releaseFrame=Time.frameCount+2;
  inventoryScreens?.Tick(table.Open);table.Tick();
#if !SPT_ASSISTANT
  if(Input.GetKeyDown(catalogKey.Value))catalog?.Refresh();catalog?.Tick(catalogEnabled.Value,table.Busy);
#endif
  TickTools();if(hudEnabled.Value)hud?.Tick(hudDiagnostics.Value);
 }
 void DrawExtra(){if(hudEnabled.Value&&table?.Open!=true)hud?.Draw(hudScale.Value,showHealth.Value,showHunger.Value,showHydration.Value,showExperience.Value,showHotbar.Value);DrawTools();table?.Draw();}
}
public static class ProjectEApi {
 public static bool Active=>ProjectModule.SettingsActive;
 public static ConfigEntryBase[] GetSettings()=>ProjectModule.ExtraSettings;
 public static void ToggleTable()=>ProjectModule.ToggleTable();
}
public sealed partial class ProjectModule {
 internal static ConfigEntryBase[] ExtraSettings=>SettingsActive?[active!.hudEnabled,active.showHealth,active.showHunger,active.showHydration,active.showExperience,active.showHotbar,active.hudScale,active.volume,active.burnVolume,active.tableKey,active.chargeKey,active.skillKey,active.modeKey,active.totemEnabled,active.totemProtectionSeconds,active.artifactsEnabled,active.watchEnabled,active.watchCharge,active.repairEnabled]:[];
 internal static void ToggleTable()=>active?.table?.Toggle();
}
