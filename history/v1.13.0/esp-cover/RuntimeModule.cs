using LocalSpt5Esp.HotReload;
namespace LocalSpt5Esp;
public sealed class RuntimeModule:IRuntimeModule {
 Plugin? plugin;
 Overlay? overlay;
 public int ApiVersion=>1;
 public string Version=>"1.13.0";
 public void Initialize(IHostServices host){plugin=new Plugin(host);plugin.Load();InterfaceMenu.Initialize(plugin);FeatureToasts.Initialize(plugin);overlay=new Overlay();}
 public void Update(){FloatingStatus.Update();ConfigProfiles.Keys();InterfaceMenu.Update();overlay?.Update();}
 public void LateUpdate(){overlay?.LateUpdate();InterfaceMenu.KeepCursor();}
 public void OnGUI(){overlay?.OnGUI();FloatingStatus.Draw();InterfaceMenu.Draw();FeatureToasts.Draw();}
 public void Unload(){
  try{FeatureToasts.Shutdown();FloatingStatus.Shutdown();InterfaceMenu.Shutdown();overlay?.OnDestroy();}finally{plugin?.Patches?.UnpatchSelf();LootDisplay.Shutdown();overlay=null;plugin=null;Plugin.Self=null!;}
 }
}
