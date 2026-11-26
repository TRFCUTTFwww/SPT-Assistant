using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using EFT;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class GrenadeAlert {
 static ConfigEntry<bool> enabled=null!;static ConfigEntry<float> range=null!;
 static readonly Dictionary<IntPtr,float> deadlines=new();static readonly List<(Grenade Grenade,string Fuse)> nearby=new();
 static float nextScan,nextError;
 internal static void Initialize(Plugin p){enabled=FeatureConfig.Bind("30. 手雷预警","Enabled","附近手雷方向与距离提示",true);range=FeatureConfig.Bind("30. 手雷预警","Range","手雷预警距离（米）",60f,"",new AcceptableValueRange<float>(10,150));try{p.Patches.Patch(typeof(Grenade).GetMethod("StartTimer"),postfix:new HarmonyMethod(typeof(GrenadeAlert),nameof(Started)));p.Patches.Patch(typeof(Grenade).GetMethod("StopTimer"),postfix:new HarmonyMethod(typeof(GrenadeAlert),nameof(Stopped)));}catch(Exception e){p.Log.LogError("Grenade fuse hooks unavailable: "+e);}}
 static void Started(Grenade __instance){try{if(!RaidClock.Active)return;float delay=__instance.WeaponSource?.GetExplDelay??0;if(delay>0&&float.IsFinite(delay)&&!deadlines.ContainsKey(__instance.Pointer))deadlines[__instance.Pointer]=Time.time+Math.Max(0,delay-__instance._timeSpent);}catch{}}
 static void Stopped(Grenade __instance)=>deadlines.Remove(__instance.Pointer);
 internal static void Reset(){deadlines.Clear();nearby.Clear();nextScan=0;}
 internal static void Tick(GameWorld? world,Player? player){if(!enabled.Value||!RaidClock.Active||world==null||player==null){Reset();return;}if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+.1f;try{nearby.Clear();var live=new HashSet<IntPtr>();var all=world.Grenades;if(all!=null)for(int i=0;i<all.Count;i++){var grenade=all.GetByIndex(i)?.TryCast<Grenade>();if(grenade==null)continue;live.Add(grenade.Pointer);if(Vector3.Distance(grenade.transform.position,player.Position)>range.Value)continue;string type=grenade.WeaponSource?.ThrowType.ToString()??"";if(type.Contains("smoke",StringComparison.OrdinalIgnoreCase)||type.Contains("gas",StringComparison.OrdinalIgnoreCase))continue;string fuse=grenade._needExplodeByContact?"触碰引爆":deadlines.TryGetValue(grenade.Pointer,out var deadline)?"约 "+Math.Max(0,deadline-Time.time).ToString("0.0")+"秒":"引信未同步";nearby.Add((grenade,fuse));}foreach(var key in deadlines.Keys.Where(k=>!live.Contains(k)).ToArray())deadlines.Remove(key);}catch(Exception e){if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+20;Plugin.Self.Error(e);}}}
 internal static void Draw(Camera? camera,Player? player){if(!enabled.Value||camera==null||player==null||!RaidClock.Active)return;foreach(var entry in nearby.Take(8))try{if(entry.Grenade==null)continue;var position=entry.Grenade.transform.position;float distance=Vector3.Distance(position,player.Position);RaidLoot.DrawMarker(camera,position,"手雷 · "+entry.Fuse,distance,distance<15?new Color(1,.28f,.24f):new Color(1,.7f,.15f),false);}catch{}}
}
