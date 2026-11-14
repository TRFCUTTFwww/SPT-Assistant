using Local.ModHost;

namespace Example;

// Copy this project for a new module. Add game references only when required.
public sealed class ExampleModule:IHotModule {
    ModuleContext? context;
    public int ApiVersion=>1;
    public string Version=>"1.0.0";
    public bool CanReload=>true;
    public void Initialize(ModuleContext context) {
        this.context=context;
        // Register unsubscriptions / Unity object cleanup as soon as acquired.
        // Use new Harmony(context.PatchId) for patches: the host unpatches this owner.
        context.OnShutdown(()=>this.context=null);
        context.Log(LogLevel.Info,"Example module initialized. No gameplay changes.");
        File.WriteAllText(Path.Combine(context.DataDirectory,"example-started.txt"),DateTime.UtcNow.ToString("O"));
    }
    public void Tick() {if(context?.IsActive!=true)return;}
    public void LateTick() { }
    public void Draw() { }
    public void Shutdown() { }
}
