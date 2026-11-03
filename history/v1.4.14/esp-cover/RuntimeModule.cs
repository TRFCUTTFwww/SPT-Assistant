using LocalSpt5Esp.HotReload;
namespace LocalSpt5Esp;
public sealed class RuntimeModule:IRuntimeModule {
 Plugin? plugin;
 Overlay? overlay;
 public int ApiVersion=>1;
 public string Version=>"1.4.14";
 public void Initialize(IHostServices host){plugin=new Plugin(host);plugin.Load();overlay=new Overlay();}
 public void Update()=>overlay?.Update();
 public void LateUpdate()=>overlay?.LateUpdate();
 public void OnGUI()=>overlay?.OnGUI();
 public void Unload(){
  try{overlay?.OnDestroy();}finally{plugin?.Patches?.UnpatchSelf();LootDisplay.Shutdown();overlay=null;plugin=null;Plugin.Self=null!;}
 }
}
