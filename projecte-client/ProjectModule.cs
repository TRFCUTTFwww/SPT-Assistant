using BepInEx.Configuration;
using HarmonyLib;
using Local.ModHost;
namespace ProjectE.Client;
public sealed partial class ProjectModule:IHotModule {
 static ProjectModule? active;ModuleContext? context;ConfigFile? settingsFile;
 public int ApiVersion=>1;
 public string Version=>"1.2.5";
 public bool CanReload=>table?.Busy!=true&&artifactPending==null&&artifactQueue.Count==0&&(terminal?.CanReload??true)&&catalog?.Busy!=true;
 public void Initialize(ModuleContext context){this.context=context;active=this;context.OnShutdown(Cleanup);settingsFile=new(Path.Combine(BepInEx.Paths.ConfigPath,"local.projecte.cfg"),true);InitializeExtra(new(context.PatchId));}
 public void Tick(){if(context?.IsActive==true)TickExtra();}
 public void LateTick(){table?.KeepCursor();}
 public void Draw(){DrawExtra();}
 public void Shutdown(){Cleanup();}
 void Cleanup(){table?.Clear();hud?.Clear();if(active==this)active=null;context=null;}
 internal static bool SettingsActive=>active?.context?.IsActive==true&&active.settingsFile!=null;
}
