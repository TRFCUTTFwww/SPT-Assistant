using System.Text.Json;

namespace Local.ModHost.Runtime;

internal sealed class ModuleSlot:IDisposable {
    readonly string home,state;
    readonly Action<LogLevel,string> log;
    readonly Action<string> unpatch;
    readonly string session=Guid.NewGuid().ToString("N");
    Task<Package>? reading;
    Package? pending,lastGood;
    IHotModule? current;
    ModuleContext? context;
    bool disposed,remove,blocked,recoveryAttempted,manualPending;
    string observation="",accepted="";
    byte[]? observedBytes;
    double stableAt;
    int attempt,generation;
    string phase="discovered",error="";
    internal string Id {get;}
    internal bool Ready=>current!=null;
    internal bool Busy=>reading!=null||pending!=null||remove;
    internal object Status=>new {id=Id,phase,ready=Ready,generation,version=lastGood?.Manifest.Version,revision=lastGood?.Manifest.Revision,sha256=lastGood?.Fingerprint,waiting=phase=="waiting",restartRequired=blocked,error};
    internal ModuleSlot(string id,string home,string state,Action<LogLevel,string> log,Action<string> unpatch) {
        Id=id;this.home=home;this.state=state;this.log=log;this.unpatch=unpatch;
        Directory.CreateDirectory(Path.Combine(state,"data"));WriteStatus();
    }
    internal void Observe(byte[] bytes,string token,double now,bool force,bool automatic) {
        if(disposed||blocked)return;
        remove=false;
        if(force){accepted="";manualPending=true;}
        if(!automatic&&accepted.Length>0&&!manualPending)return;
        if(token==accepted)return;
        if(token!=observation||force) {observation=token;observedBytes=bytes;stableAt=now+.5;}
        if(now<stableAt||reading!=null||pending!=null)return;
        var input=observedBytes!;
        accepted=token;observedBytes=null;manualPending=false;
        reading=Task.Run(()=>Package.Read(home,input));
        Set("validating","");
    }
    internal void Missing() {remove=true;pending=null;accepted=observation="";observedBytes=null;}
    internal void Report(Exception e) {Set("rejected",e.GetBaseException().Message);}
    internal void Advance() {
        if(disposed||blocked)return;
        if(reading?.IsCompleted==true) {
            var done=reading;reading=null;
            try {var package=done.GetAwaiter().GetResult();if(!remove)pending=package;}
            catch(Exception e){Report(e);TryColdRecovery();}
        }
        if(remove) {
            if(!CanStop())return;
            if(Stop())Set("removed","");
            remove=false;return;
        }
        if(pending==null)return;
        if(!CanStop())return;
        var next=pending;pending=null;
        if(!next.Manifest.Enabled) {if(Stop())Set("disabled","");return;}
        Activate(next);
    }
    bool CanStop() {
        if(current==null)return true;
        try {if(current.CanReload)return true;Set("waiting","");return false;}
        catch(Exception e) {Fault("CanReload",e);return false;}
    }
    void Activate(Package next) {
        IHotModule candidate;
        try {candidate=next.Prepare();}
        catch(Exception e){Report(e);TryColdRecovery();return;}
        // Verification/construction finishes before the old generation is touched.
        if(!Stop())return;
        try {
            Start(candidate,next);
            lastGood=next;recoveryAttempted=false;generation++;
            Set("running","");
            try {AtomicWrite(Path.Combine(state,"last-good.json"),JsonSerializer.SerializeToUtf8Bytes(next.Manifest,Package.Json));}
            catch(Exception e){log(LogLevel.Warning,Id+" could not persist last-good manifest: "+e.Message);}
            log(LogLevel.Info,$"{Id} {next.Manifest.Version} loaded; generation {generation}; revision {next.Manifest.Revision}");
        } catch(Exception e) {
            string failure=e.GetBaseException().Message;
            if(!Stop())return;
            if(lastGood!=null) {
                try {Start(lastGood.Prepare(),lastGood);generation++;Set("rolled-back",failure);}
                catch(Exception restore){if(Stop())Set("failed",failure+"; rollback: "+restore.GetBaseException().Message);}
            } else {Set("failed",failure);TryColdRecovery();}
        }
    }
    void TryColdRecovery() {
        if(Ready||blocked||recoveryAttempted||remove)return;
        recoveryAttempted=true;
        string saved=Path.Combine(state,"last-good.json");
        if(!File.Exists(saved))return;
        try {
            byte[] bytes=File.ReadAllBytes(saved);
            reading=Task.Run(()=>Package.Read(home,bytes));
            Set("recovering",error);
        } catch(Exception e){Set("failed",e.Message);}
    }
    void Start(IHotModule module,Package package) {
        string patchId=$"local.modhost.{Id}.{session}.{++attempt}";
        context=new ModuleContext(Id,package.Directory,Path.Combine(state,"data"),patchId,(level,message)=>log(level,"["+Id+"] "+message));
        context.OnShutdown(()=>unpatch(patchId));
        current=module; // Partial Initialize also receives Shutdown + registered cleanup.
        module.Initialize(context);
    }
    bool Stop() {
        var old=current;var lifetime=context;
        current=null;context=null;
        List<Exception> errors=[];
        try {old?.Shutdown();}catch(Exception e){errors.Add(e);}
        try {lifetime?.Release();}catch(Exception e){errors.Add(e);}
        if(errors.Count==0)return true;
        // Unknown remaining native resources cannot safely coexist with a replacement.
        blocked=true;pending=null;remove=false;
        Set("cleanup-failed",string.Join("; ",errors.Select(e=>e.GetBaseException().Message)));
        return false;
    }
    internal void Dispatch(Action<IHotModule> call,string callback) {
        if(disposed||current==null)return;
        try {call(current);}catch(Exception e){Fault(callback,e);}
    }
    void Fault(string callback,Exception e) {
        pending=null;
        // An already validating update is discarded; do not silently restart a faulted generation.
        if(reading!=null){_ = reading.ContinueWith(t=>{_ = t.Exception;},TaskContinuationOptions.OnlyOnFaulted);reading=null;}
        if(Stop())Set("faulted",callback+": "+e.GetBaseException().Message);
    }
    void Set(string next,string message) {
        if(phase==next&&error==message)return;
        phase=next;error=message;
        if(message.Length>0)log(LogLevel.Error,Id+" "+next+": "+message);
        WriteStatus();
    }
    void WriteStatus() {
        try {AtomicWrite(Path.Combine(state,"status.json"),JsonSerializer.SerializeToUtf8Bytes(new {updatedUtc=DateTime.UtcNow,module=Status},Package.Json));}
        catch(Exception e){log(LogLevel.Warning,Id+" status write failed: "+e.Message);}
    }
    internal static void AtomicWrite(string path,byte[] bytes) {File.WriteAllBytes(path+".tmp",bytes);File.Move(path+".tmp",path,true);}
    public void Dispose() {
        if(disposed)return;
        disposed=true;
        if(reading!=null){_ = reading.ContinueWith(t=>{_ = t.Exception;},TaskContinuationOptions.OnlyOnFaulted);reading=null;}
        pending=null;Stop();if(!blocked)Set("stopped","");
    }
}
