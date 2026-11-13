using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
namespace LocalBlackwallPulse;
internal sealed class PulseSettings {
 internal readonly ConfigEntry<bool> Enabled,ThroughWalls,Effects,Filter;
 internal readonly ConfigEntry<float> Range,Cone,Volume;internal readonly ConfigEntry<int> Limit;internal readonly ConfigEntry<KeyCode> Key;
 internal PulseSettings(ConfigFile c){Enabled=c.Bind("Pulse","Enabled",true);Range=c.Bind("Pulse","Range",60f);Cone=c.Bind("Pulse","Cone",70f);Limit=c.Bind("Pulse","Max targets",8);ThroughWalls=c.Bind("Pulse","Through walls",false);Effects=c.Bind("Visuals","Effects",true);Filter=c.Bind("Visuals","Screen filter",false,"Optional full-screen red glow. Disable to retain an unobstructed view.");Volume=c.Bind("Audio","Volume",.65f);Key=c.Bind("Input","Standalone key",KeyCode.None);}
}
internal static class RuntimeHost {
 internal static PulseSettings? Settings;internal static object? Host;internal static string Hash="";internal static bool ExternalDriver;static Harmony? patches;
 internal static void Start(object host,Type driver,bool hot){
  if(Settings!=null)Stop();Host=host;ExternalDriver=hot;
  const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
  var log=(ManualLogSource)host.GetType().GetProperty("Log",flags)!.GetValue(host)!;
  var config=(ConfigFile)host.GetType().GetProperty("Config",flags)!.GetValue(host)!;
  config.Reload();Settings=new PulseSettings(config);
  try{
   string home=Path.Combine(BepInEx.Paths.PluginPath,"BlackwallPulse");PulseRuntime.Initialize(home,log);patches=new Harmony("local.blackwall.pulse.runtime");
   patches.Patch(AccessTools.Method(typeof(EFT.Player.FirearmController),"SetTriggerPressed"),prefix:new HarmonyMethod(typeof(RuntimeHost),nameof(BeforeTrigger)));
   // IL2CPP registered MonoBehaviour callbacks retain their original managed
   // targets. A hot payload is driven explicitly through the Assistant API.
   log.LogInfo("Blackwall Pulse 0.2.10 active; mode="+(hot?"hot runtime":"startup")+"; native driver="+driver.Assembly.GetName().Version);
  }catch{Stop();throw;}
 }
 static bool BeforeTrigger(EFT.Player.FirearmController __instance,bool __0)=>!__0||!PulseRuntime.Owns(__instance);
 internal static void Stop(){try{if(Settings!=null)PulseRuntime.Shutdown();}finally{patches?.UnpatchSelf();patches=null;Settings=null;}}
}
