using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Local.ModHost;
using Local.ModHost.Runtime;
using UnityEngine;

namespace Local.UniversalModHost;

[BepInPlugin("local.universal.modhost","Universal Mod Host","1.0.0")]
public sealed class HostPlugin:BasePlugin {
    internal static HostPlugin? Instance;
    ModuleManager? manager;
    HostDriver? driver;
    ConfigEntry<bool> automatic=null!;
    ConfigEntry<KeyCode> key=null!;
    public override void Load() {
        Instance=this;
        automatic=Config.Bind("Loading","Automatic",true,"自动发现新模块与完整发布；各模块独立等待可重载状态。");
        key=Config.Bind("Loading","Reload key",KeyCode.F9,"Ctrl + 此键重新检查所有模块；不会跳过正在进行操作的保护。");
        string root=Path.GetDirectoryName(typeof(HostPlugin).Assembly.Location)!;
        manager=new ModuleManager(root,Write,(id)=>new Harmony(id).UnpatchSelf());
        driver=AddComponent<HostDriver>();
        Log.LogInfo("Universal Mod Host 1.0.0 ready; modules="+Path.Combine(root,"modules")+"; Ctrl+"+key.Value);
    }
    void Write(Local.ModHost.LogLevel level,string message) {
        if(level==Local.ModHost.LogLevel.Error)Log.LogError(message);
        else if(level==Local.ModHost.LogLevel.Warning)Log.LogWarning(message);
        else Log.LogInfo(message);
    }
    internal void Tick() {
        if(manager==null)return;
        if(Input.GetKeyDown(key.Value)&&(Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl)))manager.Request();
        manager.Automatic=automatic.Value;manager.Tick(Time.realtimeSinceStartupAsDouble);
        manager.Dispatch(m=>m.Tick(),"Tick");
    }
    internal void LateTick()=>manager?.Dispatch(m=>m.LateTick(),"LateTick");
    internal void Draw()=>manager?.Dispatch(m=>{
        var matrix=GUI.matrix;var color=GUI.color;var background=GUI.backgroundColor;var content=GUI.contentColor;
        var skin=GUI.skin;int depth=GUI.depth;bool enabled=GUI.enabled;
        try {m.Draw();}finally {GUI.matrix=matrix;GUI.color=color;GUI.backgroundColor=background;GUI.contentColor=content;GUI.skin=skin;GUI.depth=depth;GUI.enabled=enabled;}
    },"Draw");
    public override bool Unload() {
        try {manager?.Dispose();}finally {manager=null;Instance=null;if(driver!=null)UnityEngine.Object.Destroy(driver);driver=null;}
        return true;
    }
}

public sealed class HostDriver:MonoBehaviour {
    public HostDriver(IntPtr pointer):base(pointer) { }
    public void Update()=>HostPlugin.Instance?.Tick();
    public void LateUpdate()=>HostPlugin.Instance?.LateTick();
    public void OnGUI()=>HostPlugin.Instance?.Draw();
    public void OnApplicationQuit()=>HostPlugin.Instance?.Unload();
}
