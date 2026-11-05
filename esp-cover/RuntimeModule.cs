using LocalSpt5Esp.HotReload;
namespace LocalSpt5Esp;
public sealed class RuntimeModule:IRuntimeModule {
 Plugin? plugin;
 Overlay? overlay;
 public int ApiVersion=>1;
 internal const string ReleaseVersion="1.36.40";
 public string Version=>ReleaseVersion;
 public void Initialize(IHostServices host){plugin=new Plugin(host);plugin.Load();CompanionPlans.Automatic.Value=false;InterfaceMenu.Initialize(plugin);FeatureToasts.Initialize(plugin);overlay=new Overlay();}
 public void Update(){long stamp=RuntimePerformance.Start();try{EventRaidLauncher.Tick();EventRaidDiagnostics.Tick();FloatingStatus.Update();ConfigProfiles.Keys();InterfaceMenu.Update();DirectorStage.Update();EntertainmentControl.Update();HitSounds.Tick();overlay?.Update();PasscodeHints.Tick();CoordinateReader.Tick();}finally{RuntimePerformance.UpdateDone(stamp);}}
 public void LateUpdate(){long stamp=RuntimePerformance.Start();try{overlay?.LateUpdate();InterfaceMenu.KeepCursor();}finally{RuntimePerformance.LateDone(stamp);}}
 public void OnGUI(){long stamp=RuntimePerformance.Start();try{if(!DirectorStage.HudHidden){overlay?.OnGUI();FloatingStatus.Draw();FeatureToasts.Draw();PasscodeHints.Draw();}InterfaceMenu.Draw();}finally{RuntimePerformance.GuiDone(stamp);}}
 public void Unload(){
  try{PasscodeHints.Reset();CommandoConsole.Reset();RuntimePerformance.Shutdown();FeatureToasts.Shutdown();FloatingStatus.Shutdown();InterfaceMenu.Shutdown();overlay?.OnDestroy();}finally{plugin?.Patches?.UnpatchSelf();LootDisplay.Shutdown();overlay=null;plugin=null;Plugin.Self=null!;}
 }
}
