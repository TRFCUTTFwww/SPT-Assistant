using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using UnityEngine;
using Object=UnityEngine.Object;
namespace LocalSpt5Esp;
internal static class InteractionPoints {
 internal sealed record Row(string Key,string Name,string Detail,Vector3 Position);
 sealed record Entry(string Key,string Id,WorldInteractiveObject Item,string Kind);
 static readonly List<Entry> entries=new();static ConfigEntry<bool> markers=null!;static ConfigEntry<float> range=null!;static IntPtr world;static float nextScan,nextError;
 internal static bool Markers=>markers.Value;
 internal static void ToggleMarkers()=>markers.Value=!markers.Value;
 internal static void Initialize(){markers=FeatureConfig.Bind("41. 交互点","Show interactions","定位刷卡与拉闸交互点",false,"显示本局刷卡器与拉闸的位置。");range=FeatureConfig.Bind("41. 交互点","Interaction distance","交互点定位距离",750f,"米",new AcceptableValueRange<float>(0,1500));}
 internal static void Reset(){entries.Clear();world=IntPtr.Zero;nextScan=0;}
 internal static void Tick(GameWorld? current){if(current==null){if(world!=IntPtr.Zero)Reset();return;}if(world!=current.Pointer){Reset();world=current.Pointer;}if((markers.Value||TeleportControl.InteractionPage)&&Time.unscaledTime>=nextScan)try{Refresh();}catch(Exception e){nextScan=Time.unscaledTime+30;if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+20;Plugin.Self.Log.LogWarning("Interaction scan: "+e.Message);}}}
 internal static void Refresh(){var current=Singleton<GameWorld>.Instance;if(!RaidClock.Active||current==null)return;if(world!=current.Pointer){Reset();world=current.Pointer;}nextScan=Time.unscaledTime+30;entries.Clear();foreach(var item in Object.FindObjectsOfType<WorldInteractiveObject>(true)){if(item==null)continue;string kind=item.TryCast<CardReader>()!=null||item.TryCast<KeycardDoor>()!=null?"刷卡点":item.TryCast<Switch>()!=null||item.TryCast<DoorSwitch>()!=null?"拉闸／开关":"";if(kind.Length==0)continue;string id=item.Id??"";entries.Add(new(id+"@"+item.Pointer.ToInt64(),id,item,kind));}entries.Sort((a,b)=>string.Compare(a.Key,b.Key,StringComparison.Ordinal));}
 static bool Live(Entry entry)=>entry.Item!=null&&(entry.Item.Id??"")==entry.Id&&Singleton<GameWorld>.Instance?.Pointer==world;
 static Vector3 Position(Entry entry){var local=Singleton<GameWorld>.Instance?.MainPlayer?.Position??entry.Item.transform.position;var position=entry.Item.GetInteractionPosition(local);if(!TeleportRules.Finite(new(position.x,position.y,position.z))||Vector3.Distance(position,entry.Item.transform.position)>12)position=entry.Item.transform.position;return position;}
 static string Label(Entry entry){string name=entry.Item.gameObject.name.Replace("(Clone)","").Replace('_',' ').Trim();return entry.Kind+(name.Length>0?" · "+(name.Length>52?name[..52]:name):"");}
 static string State(Entry entry)=>!entry.Item.gameObject.activeInHierarchy?"当前未启用":entry.Item.DoorState switch{EDoorState.Open=>"已打开",EDoorState.Shut=>"已关闭",EDoorState.Locked=>"锁定",_=>"切换中"};
 internal static IEnumerable<Row> Rows {get{if(world==IntPtr.Zero||Time.unscaledTime>=nextScan)Refresh();foreach(var entry in entries)if(Live(entry)){string key=entry.Item.KeyId??"";yield return new(entry.Key,Label(entry),State(entry)+(key.Length>0?" · "+ItemSpawner.TemplateName(key):""),Position(entry));}}}
 internal static bool TryTarget(string key,out Vector3 point,out string name){point=default;name="交互点";var entry=entries.FirstOrDefault(e=>e.Key==key);if(!RaidClock.Active||entry==null||!Live(entry))return false;point=Position(entry);name=Label(entry);return TeleportRules.Finite(new(point.x,point.y,point.z));}
 internal static void DrawMarkers(Camera camera,Player local){if(!markers.Value)return;try{foreach(var entry in entries){if(!Live(entry))continue;var pos=entry.Item.transform.position;float distance=Vector3.Distance(local.Position,pos);if(distance>range.Value)continue;var screen=camera.WorldToScreenPoint(pos+Vector3.up*.3f);if(screen.z<=0||screen.x<0||screen.x>Screen.width||screen.y<0||screen.y>Screen.height)continue;InterfaceMenu.Label(new Rect(screen.x-180,Screen.height-screen.y-12,360,44),Label(entry)+" · "+distance.ToString("F0")+" 米\n"+State(entry),new Color(.7f,.9f,1),12);}}catch(Exception e){if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+20;Plugin.Self.Log.LogWarning("Interaction markers: "+e.Message);}}}
}
