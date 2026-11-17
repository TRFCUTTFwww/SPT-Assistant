using System;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.UI;
using EFT.SeasonalRewards;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
// Only expose the native Seasons menu. Never alter GameModeDescriptor,
// character selection, matchmaking, profile identities or reward conditions.
internal static class SeasonMenuControl {
 static ConfigEntry<bool> enabled=null!;static MenuScreen? menu;static Profile? profile;static SeasonalRewardController? rewards;static float next;static bool applied,restoring;static string notice="";
 internal static void Initialize(Plugin plugin){
  enabled=FeatureConfig.Bind("70. 赛季","Seasons menu in PVE","PVE 开放赛季入口",true,"开放角色选择中的赛季入口，使用现有 PVE 角色并共用进度。");
  plugin.Patches.Patch(AccessTools.Method(typeof(MenuScreen),nameof(MenuScreen.RefreshSeasonWidget)),postfix:new HarmonyMethod(typeof(SeasonMenuControl),nameof(Observe)));
  plugin.Patches.Patch(AccessTools.Method(typeof(MenuScreen),nameof(MenuScreen.HandleSeasonWidgetClick)),prefix:new HarmonyMethod(typeof(SeasonMenuControl),nameof(Click)));
 }
 internal static bool Enabled=>enabled!=null&&enabled.Value;
 static TarkovApplication? application;
 static TarkovApplication? App()=>application!=null?application:application=Singleton<TarkovApplication>.Instance??Singleton<AbstractApplication>.Instance?.TryCast<TarkovApplication>()??UnityEngine.Object.FindObjectOfType<TarkovApplication>();
 static bool Ready=>!ItemSpawner.InRaid()&&profile!=null&&profile.Id==App()?._menuOperation?._profile?.Id&&rewards!=null&&App()?._menuOperation?._seasonalRewardController!=null&&App()?.GetClientBackEndSession()?.BackEndConfig?.Config?.SeasonalRewards?.Season!=null;
 static void Observe(MenuScreen __instance,Profile __0,SeasonalRewardController __1){menu=__instance;profile=__0;rewards=__1;applied=false;Apply();}
 static void Apply(){
  if(restoring||!enabled.Value||menu==null||menu._modeDescriptor?.IsPve!=true||!Ready||!menu.gameObject.activeInHierarchy)return;
  var widget=menu._seasonWidget;if(widget==null)return;widget.Show(SeasonWidgetData.From(profile,rewards));widget._handleClick=true;applied=true;
 }
 static bool Click(MenuScreen __instance){
  if(!enabled.Value||__instance._modeDescriptor?.IsPve!=true)return true;
  Open();return false;
 }
 static void Open(){try{
  if(!Ready){notice="赛季数据尚未就绪，请返回主菜单后重试。";FeatureToasts.Show(notice);return;}
  App()!._menuOperation.ShowSeasonsHubScreen();notice="";
 }catch(Exception error){notice="赛季菜单暂不可用，请查看诊断日志。";FeatureToasts.Show(notice);Plugin.Self.Error(error);}}
 internal static void Tick(){if(Time.unscaledTime<next||ItemSpawner.InRaid())return;next=Time.unscaledTime+2;
  try{if(menu==null)menu=UnityEngine.Object.FindObjectOfType<MenuScreen>();if(profile==null||rewards==null){var operation=App()?._menuOperation;profile=operation?._profile;rewards=operation?._seasonalRewardController;}if(!applied)Apply();}catch(Exception error){next=Time.unscaledTime+10;Plugin.Self.Error(error);}
 }
 internal static object Diagnostics()=>new{enabled=enabled.Value,menu=menu!=null,active=menu?.gameObject.activeInHierarchy,pve=menu?._modeDescriptor?.IsPve,mode=menu?._modeDescriptor?.GameMode.ToString(),hasProfile=profile!=null,hasRewardController=rewards!=null,nativeController=App()?._menuOperation?._seasonalRewardController!=null,season=App()?.GetClientBackEndSession()?.BackEndConfig?.Config?.SeasonalRewards?.Season!=null,ready=Ready,applied};
 internal static void Draw(Rect r){
  InterfaceMenu.Label(new(r.x,r.y,r.width,34),"赛季 / Seasons",InterfaceMenu.Accent,20);
  InterfaceMenu.Paragraph(new(r.x,r.y+48,r.width,64),"开放角色选择中的赛季入口，使用现有 PVE 角色并共用进度。",null,14);
  if(InterfaceMenu.Button(new(r.x,r.y+132,r.width,40),I18n.Text("PVE 开放赛季入口")+" · "+I18n.Text(enabled.Value?"开启":"关闭"),enabled.Value)){enabled.Value=!enabled.Value;applied=false;SeasonSelectionControl.Refresh();if(menu!=null&&profile!=null&&rewards!=null)menu.RefreshSeasonWidget(profile,rewards);}
  bool old=GUI.enabled;GUI.enabled=old&&enabled.Value&&Ready;try{if(InterfaceMenu.Button(new(r.x,r.y+188,r.width,40),"打开原生赛季菜单"))Open();}finally{GUI.enabled=old;}
  InterfaceMenu.Paragraph(new(r.x,r.y+248,r.width,88),notice.Length>0?notice:Ready?"赛季菜单数据已就绪。":"赛季数据尚未就绪，请返回主菜单后重试。",null,13);
 }
 internal static void Shutdown(){try{if(applied&&menu!=null&&profile!=null&&rewards!=null){applied=false;restoring=true;try{menu.RefreshSeasonWidget(profile,rewards);}finally{restoring=false;}}}finally{menu=null;profile=null;rewards=null;application=null;}}
}
