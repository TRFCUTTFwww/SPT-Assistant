using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace LocalQuickhacks.Hosting;

// All lifecycle calls run on the driver's Unity thread. Payload files are read
// into memory and immediately closed, so running code never locks the .module.
internal sealed class Reloader : IDisposable {
    readonly ConfigFile config;
    readonly ManualLogSource log;
    readonly string directory,path;
    readonly Action<string,bool> notify;
    IQuickhackModule? module;
    byte[]? lastGood;
    (long Size,long Modified) observed,accepted;
    double stableAt,nextPoll;
    bool requested,disposed;
    string hash="",error="";
    int generation;
    internal IQuickhackModule? Current=>module;
    internal bool Automatic=true;
    internal Reloader(ConfigFile config,ManualLogSource log,string directory,Action<string,bool>? notify=null) {
        this.config=config; this.log=log; this.directory=directory; this.notify=notify??((_,_)=>{});
        path=Path.Combine(directory,"runtime","Local.Cp2077.Quickhacks.Core.module");
    }
    internal void Request() { requested=true; nextPoll=0; }
    internal void Poll(double now) {
        if(disposed||now<nextPoll) return;
        nextPoll=now+.25;
        try {
            if(!requested&&!Automatic&&module!=null) return;
            var file=new FileInfo(path);
            if(!file.Exists) { if(requested||module==null) throw new FileNotFoundException("找不到快速破解逻辑模块。",path); return; }
            var stamp=(file.Length,file.LastWriteTimeUtc.Ticks);
            if(!requested) {
                if(stamp==accepted) return;
                if(stamp!=observed) { observed=stamp; stableAt=now+.5; return; }
                if(now<stableAt) return;
            }
            if(module?.CanReload==false) {
                if(error!="deferred") { error="deferred"; notify("更新已就绪，完成上传或退出门禁破解后切换。",false); WriteStatus(); }
                return;
            }
            requested=false;
            // Accept a failed stamp too: retry on the next publish or hotkey,
            // rather than continuously replaying a broken module.
            accepted=observed=stamp;
            Swap(File.ReadAllBytes(path));
        } catch(Exception e) {
            requested=false;
            if(error==e.GetBaseException().Message) return;
            error=e.GetBaseException().Message;
            log.LogWarning("Quickhack reload: "+e); notify("热更新失败，当前版本继续运行："+error,true); WriteStatus();
        }
    }
    IQuickhackModule Prepare(byte[] bytes) {
        var context=new PayloadContext();
        using var stream=new MemoryStream(bytes,false);
        var assembly=context.LoadFromStream(stream);
        var type=assembly.GetType("LocalQuickhacks.RuntimeModule",true)!;
        var result=Activator.CreateInstance(type) as IQuickhackModule??throw new InvalidOperationException("快速破解模块接口不匹配。");
        if(result.ApiVersion!=1) throw new InvalidOperationException("快速破解热更新 API 版本不匹配。");
        return result;
    }
    void Swap(byte[] bytes) {
        // A corrupt candidate never shuts down the healthy module.
        var candidate=Prepare(bytes);
        var previous=module; var fallback=lastGood;
        try {
            previous?.Shutdown(); module=null;
            config.Reload();
            candidate.Initialize(config,log,directory);
            module=candidate; lastGood=bytes; hash=Convert.ToHexString(SHA256.HashData(bytes)); error="";
            generation++;
            notify("快速破解 "+candidate.Version+" · 已热更新",false);
            log.LogInfo("Quickhacks core "+candidate.Version+" loaded; generation "+generation);
        } catch(Exception e) {
            try { candidate.Shutdown(); } catch(Exception cleanup) { log.LogWarning(cleanup); }
            module=null;
            if(fallback!=null) {
                var restore=Prepare(fallback);
                try { restore.Initialize(config,log,directory); module=restore; }
                catch { try { restore.Shutdown(); } catch { } throw; }
            }
            error=e.GetBaseException().Message;
            log.LogWarning("Quickhack update rolled back: "+e);
            notify(module!=null?"更新失败，已恢复上一版本："+error:"模块启动失败："+error,true);
        }
        WriteStatus();
    }
    void WriteStatus() {
        try {
            string output=Path.Combine(directory,"runtime","hotreload-status.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            string temporary=output+".tmp";
            File.WriteAllBytes(temporary,JsonSerializer.SerializeToUtf8Bytes(new {
                hostVersion="0.3.0",coreVersion=module?.Version,generation,sha256=hash,
                ready=module!=null,waiting=error=="deferred",error=error=="deferred"?"":error,updatedUtc=DateTime.UtcNow
            }));
            File.Move(temporary,output,true);
        } catch(Exception e) { log.LogWarning("Quickhack reload status: "+e.Message); }
    }
    public void Dispose() { if(disposed) return; disposed=true; try { module?.Shutdown(); } finally { module=null; lastGood=null; } }
    sealed class PayloadContext:AssemblyLoadContext {
        // IL2CPP/Harmony native trampolines require stable metadata. Old managed
        // metadata stays resident; Shutdown releases patches, delegates and Unity
        // objects. Never collect code that may have a native trampoline.
        internal PayloadContext():base("Quickhacks-"+Guid.NewGuid().ToString("N"),false) { }
        protected override Assembly? Load(AssemblyName name) {
            var shared=Default.Assemblies.FirstOrDefault(a=>a.GetName().Name==name.Name);
            return shared??Default.LoadFromAssemblyName(name);
        }
    }
}
