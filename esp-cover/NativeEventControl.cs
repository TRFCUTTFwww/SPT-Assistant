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
  foreach(string method in new[]{"OnReadyToStartMatchingAsync","LocalGameMatching","GamePrepare"})plugin.Patches.Patch(AccessTools.Method(typeof(TarkovApplication),method),prefix:new HarmonyMethod(typeof(NativeEventControl),nameof(Prepare)));
  plugin.Patches.Patch(AccessTools.Method(typeof(TarkovApplication),"LoadMapAndData"),prefix:new HarmonyMethod(typeof(NativeEventControl),nameof(TraceScene)));
  plugin.Patches.Patch(AccessTools.Method(typeof(EftClientBackendSession),"LocalRaidStarted"),prefix:new HarmonyMethod(typeof(NativeEventControl),nameof(TraceLocalRaid)));
  plugin.Log.LogInfo("Native Halloween control ready: "+ClientEventVersion.Version+"; legacy map port is not loaded");
 }
 static bool IsLab(string? id)=>id is "laboratory" or "laboratory_dark" or "laboratory_event";
 static void Prepare(TarkovApplication __instance, System.Reflection.MethodBase __originalMethod){if(!ClientEventVersion.Native||mode.Value==NativeLabMode.MapDefault)return;try{
  var settings=__instance._raidSettings;if(settings?.SelectedLocation?.Id is not "laboratory" and not "laboratory_dark" and not "laboratory_event")return;
  string id=mode.Value==NativeLabMode.Event?"laboratory_event":mode.Value==NativeLabMode.Dark?"laboratory_dark":"laboratory";
  var location=settings._locationSettings?.locations?.Values.FirstOrDefault(l=>l.Id==id);
  if(location==null){notice="当前原生地图列表没有目标实验室，请重新准备地图。";FeatureToasts.Show(notice);return;}
  string beforeId=settings.LocationId;string beforeSelected=settings.SelectedLocation?.Id??"";
  ApplySelection(settings,__instance._localRaidSettings,location);
  Plugin.Self.Log.LogInfo($"Native laboratory {__originalMethod.Name}: {beforeId}/{beforeSelected} -> {settings.LocationId}/{settings.SelectedLocation?.Id}; {location.Scene?.path}");
 }catch(Exception error){notice=error.Message;Plugin.Self.Error(error);}}
 static void ApplySelection(EFT.RaidSettings settings,LocalRaidSettings? local,JsonType.LocationSettings.Location location){
  // Keep the scene selection and both local-raid identifiers together.
  settings.SelectedLocation=location;settings.LocationId=location.Id;
  if(local!=null){local.selectedLocation=location;local.location=location.Id;}
 }
 internal static object Diagnostics(TarkovApplication app,bool check=false){
  var settings=app._raidSettings;var results=new List<object>();
  if(check){
   var locations=settings?._locationSettings??throw new InvalidOperationException("Location list unavailable");
   var normal=locations.locations.Values.First(l=>l.Id=="laboratory");
   foreach(string id in new[]{"laboratory_dark","laboratory_event","laboratory"}){
    var target=locations.locations.Values.First(l=>l.Id==id);
    var isolated=new EFT.RaidSettings();isolated._locationSettings=locations;isolated.LocationId=normal.Id;isolated.SelectedLocation=normal;
    isolated.SelectedLocation=target;string setterId=isolated.LocationId;
    var local=new LocalRaidSettings();ApplySelection(isolated,local,target);
    bool pass=isolated.LocationId==id&&isolated.SelectedLocation?.Id==id&&local.location==id&&local.selectedLocation?.Id==id;
    results.Add(new{id,setterId,locationId=isolated.LocationId,selected=isolated.SelectedLocation?.Id,scene=isolated.SelectedLocation?.Scene?.path,local=local.location,pass});
    if(!pass)throw new InvalidOperationException("Laboratory selection check failed: "+id);
   }
  }
  return new{requested=mode.Value.ToString(),locationId=settings?.LocationId,selected=settings?.SelectedLocation?.Id,cached=settings?._selectedLocation?.Id,scene=settings?.SelectedLocation?.Scene?.path,local=app._localRaidSettings?.location,localSelected=app._localRaidSettings?.selectedLocation?.Id,worldLocation=Singleton<GameWorld>.Instance?.MainPlayer?.Location,checks=results};
 }
 static JsonType.LocationSettings.Location? RequestedLocation(){
  if(mode.Value==NativeLabMode.MapDefault)return null;
  string id=mode.Value==NativeLabMode.Event?"laboratory_event":mode.Value==NativeLabMode.Dark?"laboratory_dark":"laboratory";
  return Singleton<TarkovApplication>.Instance?._raidSettings?._locationSettings?.locations?.Values.FirstOrDefault(l=>l.Id==id);
 }
 static void TraceScene(ScenePresetLoadConfig __0){try{
  if(__0==null)return;string path=__0.key?.path??"";if(!path.Contains("laboratory",StringComparison.OrdinalIgnoreCase))return;
  // Matchmaking recreates RaidSettings after the earlier selection hook. The
  // actual scene request must use the same preset as the backend location ID.
  if(RequestedLocation() is {} selected){__0.key=selected.Scene;var app=Singleton<TarkovApplication>.Instance;if(app?._raidSettings is {} settings)ApplySelection(settings,app._localRaidSettings,selected);}
  Plugin.Self.Log.LogInfo($"Native laboratory scene loading: {path} -> {__0.key?.path}; requested={mode.Value}");
 }catch(Exception error){Plugin.Self.Log.LogWarning(error);}}
 static void TraceLocalRaid(LocalRaidSettings __0){try{
  if(__0==null||!IsLab(__0.location))return;
  if(RequestedLocation() is {} selected){__0.location=selected.Id;__0.selectedLocation=selected;}
  Plugin.Self.Log.LogInfo($"Native laboratory server request: {__0.location}; selected={__0.selectedLocation?.Id}; requested={mode.Value}");
 }catch(Exception error){Plugin.Self.Log.LogWarning(error);}}
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
