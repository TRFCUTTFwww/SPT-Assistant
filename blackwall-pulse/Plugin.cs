using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
namespace LocalBlackwallPulse;
[BepInPlugin("local.blackwall.pulse","Blackwall Pulse","0.2.10")]
public sealed class Plugin:BasePlugin {
 internal static Plugin? Instance;
 public override void Load(){Instance=this;RuntimeHost.Start(this,typeof(PulseDriver),false);RuntimeHost.Hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(Plugin).Assembly.Location)));AddComponent<PulseDriver>();}
 public override bool Unload(){RuntimeHost.Stop();Instance=null;return true;}
}
public sealed class PulseDriver:MonoBehaviour {
 public PulseDriver(IntPtr pointer):base(pointer){}
 public void Update()=>PulseRuntime.Tick();
 public void LateUpdate()=>PulseRuntime.Late();
 public void OnGUI()=>PulseRuntime.Draw();
}
public static class PulseApi {
 public static int ApiVersion=>1;
 public static string Version=>"0.2.10";
 public static bool Active=>RuntimeHost.Settings!=null;
 public static object? Host=>RuntimeHost.Host;
 public static string PayloadHash=>RuntimeHost.Hash;
 public static string Status=>PulseRuntime.Status;
 public static bool Busy=>PulseRuntime.Busy;
 public static bool TryCast()=>PulseRuntime.Cast();
 public static bool TryPreview()=>PulseRuntime.Cast(true);
 public static bool PreviewDeathSound()=>PulseRuntime.SoundPreview();
 public static string Diagnostics()=>PulseRuntime.Diagnostics();
 public static bool ExternalDriver=>RuntimeHost.ExternalDriver;
 public static void DriveUpdate(){if(RuntimeHost.ExternalDriver&&RuntimeHost.Settings!=null)PulseRuntime.Tick();}
 public static void DriveLate(){if(RuntimeHost.ExternalDriver&&RuntimeHost.Settings!=null)PulseRuntime.Late();}
 public static void DriveGUI(){if(RuntimeHost.ExternalDriver&&RuntimeHost.Settings!=null)PulseRuntime.Draw();}
 public static object CheckArms()=>ArmAnimation.Check(Comfort.Common.Singleton<EFT.GameWorld>.Instance?.MainPlayer,EFT.CameraControl.CameraManager.Instance?.Camera);
 public static void Configure(bool enabled,float range,float cone,int limit,bool walls,bool effects,bool filter,float volume)=>PulseRuntime.Configure(enabled,range,cone,limit,walls,effects,filter,volume);
 public static void SetProtection(Func<long,bool>? guard)=>PulseRuntime.Protection=guard;
 public static void SetInputBlocked(bool blocked)=>PulseRuntime.AssistantInput(blocked);
 public static void Disconnect()=>PulseRuntime.Disconnect();
 public static void StartHot(object host,string hash){RuntimeHost.Start(host,host.GetType().Assembly.GetType("LocalBlackwallPulse.PulseDriver",true)!,true);RuntimeHost.Hash=hash;}
 public static void StopForReload()=>RuntimeHost.Stop();
}
