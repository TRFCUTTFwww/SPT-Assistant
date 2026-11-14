using BepInEx.Configuration;
namespace AllTheModium.Client;
public sealed partial class SwordModule {
 ConfigFile? settingsFile;
 ConfigEntry<bool> guardEnabled=null!,blockBullets=null!;
 ConfigEntry<float> reductionPercent=null!,sizePercent=null!;
 void InitializeSettings(){
  settingsFile=new ConfigFile(Path.Combine(BepInEx.Paths.ConfigPath,"local.allthemodium.swords.cfg"),true);
  guardEnabled=settingsFile.Bind("Guard","Enabled",true,"启用右键格挡；关闭后恢复右键刺击。");
  blockBullets=settingsFile.Bind("Guard","Block bullets",true,"开启时正面子弹也受格挡减伤；关闭后只对近战减伤。");
  reductionPercent=settingsFile.Bind("Guard","Damage reduction percent",50f,new ConfigDescription("格挡减少的生命伤害百分比：0 不减伤，100 完全挡住符合条件的生命伤害。",new AcceptableValueRange<float>(0,100)));
  sizePercent=settingsFile.Bind("Model","Size percent",55f,new ConfigDescription("相对于初始剑模型的大小，默认 55%，约 46 厘米。修改立即应用。",new AcceptableValueRange<float>(20,100)));
 }
}
// Match the optional settings bridge used by other independent mods. Never
// cache a module instance in Assistant; hot reload leaves old assemblies loaded.
public static class SwordSettingsApi {
 public static bool Active=>SwordModule.SettingsActive;
 public static ConfigEntryBase[] GetSettings()=>SwordModule.SettingsEntries;
}
public sealed partial class SwordModule {
 internal static bool SettingsActive=>active?.context?.IsActive==true&&active.settingsFile!=null;
 internal static ConfigEntryBase[] SettingsEntries=>SettingsActive?[active!.guardEnabled,active.blockBullets,active.reductionPercent,active.sizePercent]:[];
}
