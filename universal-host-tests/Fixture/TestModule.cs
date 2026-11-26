using Local.ModHost;

namespace Fixture;
public class TestModule:IHotModule {
    ModuleContext? context;
    public virtual int ApiVersion=>1;
    public string Version=>"1.0.0";
    string Data(string file)=>Path.Combine(context!.DataDirectory,file);
    bool Flag(string file)=>File.Exists(Data(file));
    void Count(string file,int delta=1) {
        string path=Data(file);int n=File.Exists(path)?int.Parse(File.ReadAllText(path)):0;
        File.WriteAllText(path,(n+delta).ToString());
    }
    public bool CanReload {get {if(Flag("reload-fault"))throw new Exception("CanReload fault");return !Flag("busy");}}
    public void Initialize(ModuleContext context) {
        this.context=context;
        Count("live");Count("starts");
        context.OnShutdown(()=>{Count("live",-1);Count("releases");if(Flag("cleanup-fault"))throw new Exception("cleanup fault");});
        context.Stopping.Register(()=>Count("cancelled"));
        File.WriteAllText(Data("dependency"),Private.Helper.Value);
        File.WriteAllText(Data("assets"),File.ReadAllText(Path.Combine(context.ModuleDirectory,"asset.txt")));
        if(File.Exists(Path.Combine(context.ModuleDirectory,"init-fault")))throw new Exception("partial initialization fault");
    }
    public void Tick(){Count("ticks");if(Flag("tick-fault"))throw new Exception("tick fault");}
    public void LateTick(){Count("late");if(Flag("late-fault"))throw new Exception("late fault");}
    public void Draw(){Count("draws");if(Flag("draw-fault"))throw new Exception("draw fault");}
    public void Shutdown(){Count("stops");if(Flag("shutdown-fault"))throw new Exception("shutdown fault");}
}
public sealed class WrongApi:TestModule {public override int ApiVersion=>99;}
public sealed class BadConstructor:TestModule {public BadConstructor()=>throw new Exception("constructor fault");}
public sealed class WrongContract { }
