using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.InputSystem;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
namespace ProjectE.Client;
public sealed partial class ProjectModule {
 Transmutation? table;MinecraftHud? hud;ProjectResources? resources;
 ConfigEntry<bool> hudEnabled=null!;
 ConfigEntry<float> hudScale=null!,volume=null!;
 ConfigEntry<KeyCode> tableKey=null!;
 int releaseFrame;
 void InitializeExtra(Harmony harmony){
  hudEnabled=settingsFile!.Bind("HUD","Enabled",true,"显示原版 MC 爱心、饱食度、经验条及同步塔科夫快捷栏。");
  hudScale=settingsFile.Bind("HUD","Scale",1.35f,new ConfigDescription("底部 HUD 缩放，最终取整为像素倍率，保持原版素材清晰。",new AcceptableValueRange<float>(.6f,2.5f)));
  volume=settingsFile.Bind("Audio","Volume",.65f,new ConfigDescription("ProjectE 原版音效音量。",new AcceptableValueRange<float>(0,1)));
  tableKey=settingsFile.Bind("Table","Open key",KeyCode.F10,"转换桌开关快捷键。");
  resources=new(context!);table=new(context!,resources);hud=new(context!,resources);context!.OnShutdown(()=>{table?.Clear();hud?.Clear();});
  harmony.Patch(AccessTools.Method(typeof(GamePlayerOwner),"TranslateAxes"),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(TableAxes)));
  harmony.Patch(AccessTools.Method(typeof(GamePlayerOwner),"TranslateCommand"),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(TableCommand)));
  harmony.Patch(AccessTools.Method(typeof(InputManager),"Update"),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(TableInput)));
  harmony.Patch(AccessTools.Method(typeof(EventSystem),"Update"),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(TableInput)));
  harmony.Patch(AccessTools.Method(typeof(Player.FirearmController),"SetTriggerPressed",[typeof(bool)]),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(TableTrigger)));
  context.Log(Local.ModHost.LogLevel.Info,"ProjectE ready: F10 transmutation table, original sounds, MC survival HUD and native hotbar.");
 }
 static bool TableBlocked=>active?.context?.IsActive==true&&(active.table?.Open==true||Time.frameCount<=active.releaseFrame);
 static bool TableAxes(GamePlayerOwner __instance)=>!TableBlocked||__instance.Player?.IsYourPlayer!=true;
 static bool TableCommand(GamePlayerOwner __instance,ref InputNode.ETranslateResult __result){if(!TableBlocked||__instance.Player?.IsYourPlayer!=true)return true;__result=InputNode.ETranslateResult.BlockAll;return false;}
 static bool TableInput()=>!TableBlocked;
 static bool TableTrigger(Player.FirearmController __instance,bool __0)=>!TableBlocked||!__0||__instance._player?.IsYourPlayer!=true;
 void TickExtra(){
  if(table==null)return;table.Volume=volume.Value;
  if(Input.GetKeyDown(tableKey.Value)){if(!table.Open)Singleton<GameWorld>.Instance?.MainPlayer?.HandsController?.TryCast<Player.FirearmController>()?.SetTriggerPressed(false);table.Toggle();releaseFrame=Time.frameCount+2;}
  else if(table.Open&&Input.GetKeyDown(KeyCode.Escape)&&!table.Busy){table.SetOpen(false);releaseFrame=Time.frameCount+2;}
  if(table.Open||Time.frameCount<=releaseFrame&&(Input.GetMouseButton(0)||Input.GetMouseButton(1)))releaseFrame=Time.frameCount+2;
  table.Tick();if(hudEnabled.Value)hud?.Tick();
 }
 void DrawExtra(){if(hudEnabled.Value&&table?.Open!=true)hud?.Draw(hudScale.Value);table?.Draw();}
}
public static class ProjectEApi {
 public static bool Active=>ProjectModule.SettingsActive;
 public static ConfigEntryBase[] GetSettings()=>ProjectModule.ExtraSettings;
 public static void ToggleTable()=>ProjectModule.ToggleTable();
}
public sealed partial class ProjectModule {
 internal static ConfigEntryBase[] ExtraSettings=>SettingsActive?[active!.hudEnabled,active.hudScale,active.volume,active.tableKey]:[];
 internal static void ToggleTable()=>active?.table?.Toggle();
}
