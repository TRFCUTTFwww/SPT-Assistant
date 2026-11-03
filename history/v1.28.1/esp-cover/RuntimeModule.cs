using LocalSpt5Esp.HotReload;
namespace LocalSpt5Esp;
public sealed class RuntimeModule:IRuntimeModule {
 Plugin? plugin;
 Overlay? overlay;
 public int ApiVersion=>1;
 public string Version=>"1.28.1";
 public void Initialize(IHostServices host){plugin=new Plugin(host);plugin.Load();InterfaceMenu.Initialize(plugin);FeatureToasts.Initialize(plugin);overlay=new Overlay();}
 public void Update(){long stamp=RuntimePerformance.Start();try{FloatingStatus.Update();ConfigProfiles.Keys();InterfaceMenu.Update();EntertainmentControl.Update();HitSounds.Tick();overlay?.Update();}finally{RuntimePerformance.UpdateDone(stamp);}}
 public void LateUpdate(){long stamp=RuntimePerformance.Start();try{overlay?.LateUpdate();InterfaceMenu.KeepCursor();}finally{RuntimePerformance.LateDone(stamp);}}
 public void OnGUI(){long stamp=RuntimePerformance.Start();try{overlay?.OnGUI();FloatingStatus.Draw();InterfaceMenu.Draw();FeatureToasts.Draw();}finally{RuntimePerformance.GuiDone(stamp);}}
 public void Unload(){
  try{CommandoConsole.Reset();RuntimePerformance.Shutdown();FeatureToasts.Shutdown();FloatingStatus.Shutdown();InterfaceMenu.Shutdown();overlay?.OnDestroy();}finally{plugin?.Patches?.UnpatchSelf();LootDisplay.Shutdown();overlay=null;plugin=null;Plugin.Self=null!;}
 }
}
