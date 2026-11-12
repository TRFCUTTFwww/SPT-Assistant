using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace LocalBlackwallPulse;

[BepInPlugin("local.blackwall.pulse","Blackwall Pulse","0.1.0")]
public sealed class Plugin:BasePlugin {
 internal static Plugin? Instance;
 internal ConfigEntry<bool> Enabled=null!,ThroughWalls=null!,Effects=null!,Filter=null!;
 internal ConfigEntry<float> Range=null!,Cone=null!,Volume=null!;
 internal ConfigEntry<int> Limit=null!;
 internal ConfigEntry<KeyCode> Key=null!;
 Harmony? patches;
 public override void Load(){
  Instance=this;
  Enabled=Config.Bind("Pulse","Enabled",true,"Enable Blackwall Pulse. Assistant provides the shortcut and settings when installed.");
  Range=Config.Bind("Pulse","Range",60f,new ConfigDescription("Maximum distance in metres.",new AcceptableValueRange<float>(1,300)));
  Cone=Config.Bind("Pulse","Cone",70f,new ConfigDescription("Full targeting cone in degrees.",new AcceptableValueRange<float>(1,180)));
  Limit=Config.Bind("Pulse","Max targets",8,new ConfigDescription("Maximum AI per cast.",new AcceptableValueRange<int>(1,24)));
  ThroughWalls=Config.Bind("Pulse","Through walls",false);
  Effects=Config.Bind("Visuals","Effects",true);
  Filter=Config.Bind("Visuals","Screen filter",true);
  Volume=Config.Bind("Audio","Volume",.65f,new ConfigDescription("Ability volume.",new AcceptableValueRange<float>(0,1)));
  Key=Config.Bind("Input","Standalone key",KeyCode.None,"Used only without a connected Assistant. None by default.");
  PulseRuntime.Initialize(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,Log);
  patches=new Harmony("local.blackwall.pulse");
  // Restore before native animation/weapon logic; apply only after the pose pass.
  foreach(string name in new[]{"ComplexUpdate","ManualUpdate","ComplexLateUpdate","VisualPass"}){
   var method=AccessTools.Method(typeof(EFT.Player),name);if(method!=null)patches.Patch(method,prefix:new HarmonyMethod(typeof(ArmAnimation),nameof(ArmAnimation.BeforeNative)));
  }
  patches.Patch(AccessTools.Method(typeof(EFT.Player),"ComplexLateUpdate"),postfix:new HarmonyMethod(typeof(ArmAnimation),nameof(ArmAnimation.AfterNative)));
  patches.Patch(AccessTools.Method(typeof(EFT.Player.FirearmController),"ManualUpdate",new[]{typeof(float)}),prefix:new HarmonyMethod(typeof(ArmAnimation),nameof(ArmAnimation.BeforeWeapon)));
  patches.Patch(AccessTools.Method(typeof(EFT.Player.FirearmController),"SetTriggerPressed"),prefix:new HarmonyMethod(typeof(Plugin),nameof(BeforeTrigger)));
  AddComponent<PulseDriver>();Log.LogInfo("Blackwall Pulse 0.1.0 ready; standalone assembly and optional Assistant API.");
 }
 public override bool Unload(){PulseRuntime.Shutdown();patches?.UnpatchSelf();Instance=null;return true;}
 static bool BeforeTrigger(EFT.Player.FirearmController __instance,bool __0)=>!__0||!PulseRuntime.Owns(__instance);
}

public sealed class PulseDriver:MonoBehaviour {
 public PulseDriver(IntPtr pointer):base(pointer){}
 public void Update()=>PulseRuntime.Tick();
 public void LateUpdate()=>PulseRuntime.Late();
 public void OnGUI()=>PulseRuntime.Draw();
}

// Primitive-only API: Assistant can reload or be removed without linking its assembly.
public static class PulseApi {
 public static int ApiVersion=>1;
 public static string Version=>"0.1.0";
 public static string Status=>PulseRuntime.Status;
 public static bool Busy=>PulseRuntime.Busy;
 public static bool TryCast()=>PulseRuntime.Cast();
 public static bool TryPreview()=>PulseRuntime.Cast(true);
 public static string Diagnostics()=>PulseRuntime.Diagnostics();
 public static void Configure(bool enabled,float range,float cone,int limit,bool walls,bool effects,bool filter,float volume)=>PulseRuntime.Configure(enabled,range,cone,limit,walls,effects,filter,volume);
 public static void SetProtection(Func<long,bool>? guard)=>PulseRuntime.Protection=guard;
 public static void SetInputBlocked(bool blocked)=>PulseRuntime.AssistantInput(blocked);
 public static void Disconnect(){PulseRuntime.Disconnect();}
}
