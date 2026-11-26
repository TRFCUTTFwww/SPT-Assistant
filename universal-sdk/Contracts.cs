namespace Local.ModHost;

public enum LogLevel { Info, Warning, Error }

/// <summary>All callbacks run on Unity's main thread. Constructors/properties must be side-effect free.</summary>
public interface IHotModule {
    int ApiVersion { get; }
    string Version { get; }
    bool CanReload { get; }
    void Initialize(ModuleContext context);
    void Tick();
    void LateTick();
    void Draw();
    void Shutdown();
}

/// <summary>One context per generation. Register cleanup immediately after acquiring a resource.</summary>
public sealed class ModuleContext {
    readonly Action<LogLevel,string> logger;
    readonly List<Action> cleanup=[];
    readonly CancellationTokenSource cancellation=new();
    bool closed;
    public string Id { get; }
    public string ModuleDirectory { get; }
    public string DataDirectory { get; }
    public string PatchId { get; }
    public CancellationToken Stopping=>cancellation.Token;
    public bool IsActive=>!closed;
    public ModuleContext(string id,string moduleDirectory,string dataDirectory,string patchId,Action<LogLevel,string> logger) {
        Id=id;ModuleDirectory=moduleDirectory;DataDirectory=dataDirectory;PatchId=patchId;this.logger=logger;
    }
    public void Log(LogLevel level,string message)=>logger(level,message);
    public void OnShutdown(Action action) {
        ArgumentNullException.ThrowIfNull(action);
        if(closed)throw new ObjectDisposedException(nameof(ModuleContext));
        cleanup.Add(action);
    }
    /// <summary>Host use. Cancellation and every cleanup run even if another cleanup throws.</summary>
    public void Release() {
        if(closed)return;
        closed=true;
        List<Exception> errors=[];
        try {cancellation.Cancel();}catch(Exception e){errors.Add(e);}
        for(int i=cleanup.Count-1;i>=0;i--)try {cleanup[i]();}catch(Exception e){errors.Add(e);}
        cleanup.Clear();
        if(errors.Count>0)throw new AggregateException("Module cleanup failed",errors);
    }
}
