using LocalSpt5Esp.HotReload;
namespace LocalSpt5Esp;
public sealed class RuntimeModule:IRuntimeModule {
 Plugin? plugin;
 Overlay? overlay;
 public int ApiVersion=>1;
 public string Version=>"1.9.0";
 public void Initialize(IHostServices host){plugin=new Plugin(host);plugin.Load();InterfaceMenu.Initialize(plugin);overlay=new Overlay();}
 public void Update(){FloatingStatus.Update();ConfigProfiles.Keys();InterfaceMenu.Update();overlay?.Update();}
 public void LateUpdate(){overlay?.LateUpdate();InterfaceMenu.KeepCursor();}
 public void OnGUI(){overlay?.OnGUI();FloatingStatus.Draw();InterfaceMenu.Draw();}
 public void Unload(){
  try{FloatingStatus.Shutdown();InterfaceMenu.Shutdown();overlay?.OnDestroy();}finally{plugin?.Patches?.UnpatchSelf();LootDisplay.Shutdown();overlay=null;plugin=null;Plugin.Self=null!;}
 }
}
