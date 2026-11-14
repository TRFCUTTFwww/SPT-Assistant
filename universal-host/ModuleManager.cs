using System.Security.Cryptography;
using System.Text.Json;

namespace Local.ModHost.Runtime;

internal sealed class ModuleManager:IDisposable {
    readonly string root,modules,state;
    readonly Action<LogLevel,string> log;
    readonly Action<string> unpatch;
    readonly Dictionary<string,ModuleSlot> slots=new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,string> scanErrors=new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,long> requests=new(StringComparer.OrdinalIgnoreCase);
    double nextScan;
    bool requested,disposed;
    internal bool Automatic=true;
    internal IReadOnlyDictionary<string,ModuleSlot> Slots=>slots;
    internal ModuleManager(string root,Action<LogLevel,string> log,Action<string> unpatch) {
        this.root=root;this.log=log;this.unpatch=unpatch;
        modules=Path.Combine(root,"modules");state=Path.Combine(root,"state");
        Directory.CreateDirectory(modules);Directory.CreateDirectory(state);
    }
    internal void Request() {requested=true;nextScan=0;}
    internal void Tick(double now) {
        if(disposed)return;
        if(now>=nextScan) {
            nextScan=now+1;
            try {Scan(now,requested);}catch(Exception e){log(LogLevel.Error,"Module discovery: "+e.Message);}
            requested=false;
        }
        foreach(var slot in slots.Values)slot.Advance();
    }
    void Scan(double now,bool force) {
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(string home in Directory.GetDirectories(modules).Order(StringComparer.Ordinal)) {
            string id=Path.GetFileName(home);
            if(!Package.ValidId(id))continue;
            seen.Add(id);
            try {
                if((File.GetAttributes(home)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Module folder cannot be a link");
                string file=Path.Combine(home,"module.json");
                if(!File.Exists(file)){if(slots.TryGetValue(id,out var missing))missing.Missing();continue;}
                if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0||new FileInfo(file).Length>1024*1024)throw new InvalidDataException("Manifest is a link or exceeds 1 MiB");
                byte[] bytes=File.ReadAllBytes(file);
                string trigger=Path.Combine(home,"reload.request");
                long requestStamp=File.Exists(trigger)?File.GetLastWriteTimeUtc(trigger).Ticks:0;
                bool localRequest=requests.TryGetValue(id,out long previousRequest)&&previousRequest!=requestStamp;
                requests[id]=requestStamp;
                string token=Convert.ToHexString(SHA256.HashData(bytes))+":"+requestStamp;
                if(!slots.TryGetValue(id,out var slot))slots.Add(id,slot=new(id,home,Path.Combine(state,id),log,unpatch));
                slot.Observe(bytes,token,now,force||localRequest,Automatic);
                scanErrors.Remove(id);
            } catch(Exception e) {
                if(!scanErrors.TryGetValue(id,out var previous)||previous!=e.Message) {
                    scanErrors[id]=e.Message;
                    if(slots.TryGetValue(id,out var slot))slot.Report(e);else log(LogLevel.Error,id+": "+e.Message);
                }
            }
        }
        foreach(var pair in slots)if(!seen.Contains(pair.Key))pair.Value.Missing();
        WriteStatus();
    }
    void WriteStatus() {
        ModuleSlot.AtomicWrite(Path.Combine(root,"host-status.json"),JsonSerializer.SerializeToUtf8Bytes(new {
            hostVersion="1.0.0",apiVersion=1,processId=Environment.ProcessId,updatedUtc=DateTime.UtcNow,
            modules=slots.Values.Select(s=>s.Status).ToArray(),discoveryErrors=scanErrors
        },Package.Json));
    }
    internal void Dispatch(Action<IHotModule> callback,string name) {if(!disposed)foreach(var slot in slots.Values)slot.Dispatch(callback,name);}
    public void Dispose() {
        if(disposed)return;disposed=true;
        foreach(var slot in slots.Values)slot.Dispose();
        try {WriteStatus();}catch(Exception e){log(LogLevel.Warning,"Host final status: "+e.Message);}
    }
}
