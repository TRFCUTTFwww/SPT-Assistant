using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

namespace LocalQuickhacks.Hosting;

[BepInPlugin("local.cp2077.quickhacks","CP2077 Quickhacks","0.3.0")]
public sealed class HostPlugin:BasePlugin {
    internal static HostPlugin? Instance;
    internal Reloader? Loader;
    ConfigEntry<bool> automatic=null!;
    ConfigEntry<KeyCode> reloadKey=null!;
    QuickhackDriver? driver;
    string notice="";
    bool failed;
    float until;
    GUIStyle? label;
    public override void Load() {
        Instance=this;
        automatic=Config.Bind("Hot Reload","Automatic",true,"自动检测逻辑模块更新；上传或门禁破解完成后切换。");
        reloadKey=Config.Bind("Hot Reload","Key",KeyCode.F8,"按 Ctrl + 此键重载独立快速破解逻辑及本地资源。");
        Loader=new Reloader(Config,Log,Path.GetDirectoryName(typeof(HostPlugin).Assembly.Location)!,Notify);
        driver=AddComponent<QuickhackDriver>(); Loader.Request();
        Log.LogInfo("Standalone Quickhacks hot reload ready: Ctrl+"+reloadKey.Value+"; automatic module detection enabled.");
    }
    void Notify(string text,bool error) { notice=text; failed=error; until=Time.unscaledTime+(error?12:5); }
    internal void Tick() {
        if(Input.GetKeyDown(reloadKey.Value)&&(Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl))) Loader?.Request();
        if(Loader!=null) { Loader.Automatic=automatic.Value; Loader.Poll(Time.realtimeSinceStartupAsDouble); Loader.Current?.Tick(); }
    }
    internal void Draw() {
        Loader?.Current?.Draw();
        if(Time.unscaledTime>=until) return;
        var savedColor=GUI.color; int depth=GUI.depth;
        try {
            GUI.depth=-600;
            if(label==null) label=new GUIStyle(GUI.skin.label) { fontSize=16,wordWrap=true };
            label.normal.textColor=failed?new Color(1,.3f,.33f):new Color(.6f,1,.8f);
            float width=Math.Min(720,Screen.width-40);
            var rect=new Rect((Screen.width-width)/2,22,width,63);
            GUI.color=new Color(.02f,.04f,.035f,.95f); GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color=Color.white;
            GUI.Label(new Rect(rect.x+14,rect.y+10,rect.width-28,rect.height-16),notice,label);
        } finally {GUI.color=savedColor;GUI.depth=depth;}
    }
    public override bool Unload() {
        try { Loader?.Dispose(); } finally {
            Loader=null; Instance=null;
            if(driver!=null) UnityEngine.Object.Destroy(driver);
            driver=null; label=null;
        }
        return true;
    }
}

public sealed class QuickhackDriver:MonoBehaviour {
    public QuickhackDriver(IntPtr pointer):base(pointer) { }
    public void Update()=>HostPlugin.Instance?.Tick();
    public void LateUpdate()=>HostPlugin.Instance?.Loader?.Current?.LateTick();
    public void OnGUI()=>HostPlugin.Instance?.Draw();
}
