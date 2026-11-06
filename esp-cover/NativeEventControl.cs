using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal enum NativeLabMode { MapDefault,Normal,Dark,Event }
internal enum NativeLabLight { MapDefault,On,Off }
internal static class NativeEventControl {
 sealed record Group(ControlledLampGroup Lamp,bool On,bool External);
 sealed record Lamp(LampController Controller,Turnable.EState State);
 static ConfigEntry<NativeLabMode> mode=null!;static ConfigEntry<NativeLabLight> lights=null!;
 static readonly List<Group> groups=new();static readonly List<Lamp> lamps=new();
 static Vector2 scroll;static IntPtr world;static NativeLabLight? applied;static float next;static string notice="",lightStatus="进入实验室后可控制原生灯光。";
 internal static void Initialize(Plugin plugin){if(!ClientEventVersion.Native)return;const string section="57. 原生活动";
  mode=FeatureConfig.Bind(section,"Native laboratory map","原生实验室场景",NativeLabMode.MapDefault,"可选择地图默认、常规或断电实验室；下局生效。");
  lights=FeatureConfig.Bind(section,"Native laboratory lights","原生实验室灯光",NativeLabLight.MapDefault,"原生灯具实时控制，地图默认会恢复接管前状态。");
  foreach(string method in new[]{"OnReadyToStartMatchingAsync","LocalGameMatching"})plugin.Patches.Patch(AccessTools.Method(typeof(TarkovApplication),method),prefix:new HarmonyMethod(typeof(NativeEventControl),nameof(Prepare)));
  plugin.Log.LogInfo("Native Halloween control ready: "+ClientEventVersion.Version+"; legacy map port is not loaded");
 }
 static void Prepare(TarkovApplication __instance){if(!ClientEventVersion.Native||mode.Value==NativeLabMode.MapDefault)return;try{
  var settings=__instance._raidSettings;if(settings?.SelectedLocation?.Id is not "laboratory" and not "laboratory_dark" and not "laboratory_event")return;
  string id=mode.Value==NativeLabMode.Event?"laboratory_event":mode.Value==NativeLabMode.Dark?"laboratory_dark":"laboratory";
  var location=settings._locationSettings?.locations?.Values.FirstOrDefault(l=>l.Id==id);
  if(location==null){notice="当前原生地图列表没有目标实验室，请重新准备地图。";FeatureToasts.Show(notice);return;}
  settings.SelectedLocation=location;if(__instance._localRaidSettings!=null){__instance._localRaidSettings.selectedLocation=location;__instance._localRaidSettings.location=id;}
  Plugin.Self.Log.LogInfo("Native laboratory selected: "+id+"; "+location.Scene?.path);
 }catch(Exception error){notice=error.Message;Plugin.Self.Error(error);}}
 static bool InLab(GameObject obj)=>obj.scene.IsValid()&&obj.scene.isLoaded&&obj.scene.path.Contains("Laboratory",StringComparison.OrdinalIgnoreCase);
 static void Restore(){foreach(var group in groups)try{if(group.Lamp!=null){group.Lamp.SetLightStateExternal(group.On);group.Lamp._stateExternalApplied=group.External;}}catch{}foreach(var lamp in lamps)try{if(lamp.Controller!=null&&!lamp.Controller.IsDestroyed)lamp.Controller.Switch(lamp.State);}catch{}}
 internal static void Reset(){Restore();groups.Clear();lamps.Clear();world=IntPtr.Zero;applied=null;}
 internal static void Tick(){if(!ClientEventVersion.Native||Time.unscaledTime<next)return;next=Time.unscaledTime+.5f;try{
  var current=Singleton<GameWorld>.Instance;var local=current?.MainPlayer;
  if(!RaidClock.Active||local?.Location is not "laboratory" and not "laboratory_dark" and not "laboratory_event"){if(world!=IntPtr.Zero)Reset();return;}
  if(world!=current!.Pointer){Reset();world=current.Pointer;}
  if(applied==lights.Value)return;
  if(groups.Count+lamps.Count==0){foreach(var lamp in UnityEngine.Object.FindObjectsOfType<LampController>(true))if(InLab(lamp.gameObject))lamps.Add(new(lamp,lamp.LampState));foreach(var group in UnityEngine.Object.FindObjectsOfType<ControlledLampGroup>(true))if(InLab(group.gameObject))groups.Add(new(group,group._currentLightState,group._stateExternalApplied));}
  if(groups.Count+lamps.Count==0){lightStatus="等待实验室灯具加载。";return;}
  if(lights.Value==NativeLabLight.MapDefault)Restore();else{foreach(var group in groups)if(group.Lamp!=null)group.Lamp.SetLightStateExternal(lights.Value==NativeLabLight.On);foreach(var lamp in lamps)if(lamp.Controller!=null&&!lamp.Controller.IsDestroyed)lamp.Controller.Switch(lights.Value==NativeLabLight.On?Turnable.EState.On:Turnable.EState.Off);}
  applied=lights.Value;lightStatus=$"原生灯光已应用 · 灯组 {groups.Count} / 灯具 {lamps.Count}";
 }catch(Exception error){lightStatus="灯具状态暂不可用："+error.Message;next=Time.unscaledTime+5;}
 }
 static void SetEvent(bool enabled){try{SeasonalEventControls.SetNativeHalloween(enabled);notice="已写入原生万圣节配置，重启服务端和客户端后生效。";}catch(Exception error){notice=error.Message;}FeatureToasts.Show(notice);}
 internal static void Draw(Rect rect){scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-20,810));DrawBody(new Rect(4,4,rect.width-32,794));GUI.EndScrollView();}
 static void DrawBody(Rect rect){float y=rect.y;InterfaceMenu.Label(new(rect.x,y,rect.width,32),"万圣节与实验室",InterfaceMenu.Accent,20);y+=44;
  InterfaceMenu.Paragraph(new(rect.x,y,rect.width,54),"活动开关重启后生效；地图选择在下局生效。",null,13);y+=68;
  bool prior=GUI.enabled;GUI.enabled=prior&&!RaidClock.Active;float half=(rect.width-12)/2;
  if(InterfaceMenu.Button(new(rect.x,y,half,36),"开启原生万圣节"))SetEvent(true);if(InterfaceMenu.Button(new(rect.x+half+12,y,half,36),"关闭原生季节活动"))SetEvent(false);GUI.enabled=prior;y+=50;
  foreach(var choice in new[]{NativeLabMode.MapDefault,NativeLabMode.Normal,NativeLabMode.Dark,NativeLabMode.Event}){string name=choice==NativeLabMode.MapDefault?"按原生地图选择":choice==NativeLabMode.Normal?"常规实验室":choice==NativeLabMode.Event?"原生万圣节实验室":"黑色军团断电实验室";GUI.enabled=prior&&!RaidClock.Active;if(InterfaceMenu.Button(new(rect.x,y,rect.width,34),name,mode.Value==choice))mode.Value=choice;y+=44;}GUI.enabled=prior;
  PasscodeHints.DrawOption(new(rect.x,y,rect.width,34));y+=48;
  foreach(var choice in new[]{NativeLabLight.MapDefault,NativeLabLight.On,NativeLabLight.Off}){string name=choice==NativeLabLight.MapDefault?"灯光：地图默认":choice==NativeLabLight.On?"开灯":"关灯";if(InterfaceMenu.Button(new(rect.x,y,rect.width,34),name,lights.Value==choice))lights.Value=choice;y+=44;}
  InterfaceMenu.Paragraph(new(rect.x,y,rect.width,140),lightStatus+"\n"+notice+"\n僵尸、召唤与活动日期可在“活动控制”页分别配置。原生活动开关在重启后生效，场景选择在准备下局时应用。",null,12);
 }
}
