using BepInEx.Configuration;
namespace LocalSpt5Esp;
internal static class FeatureConfig {
 internal static ConfigEntry<T> Bind<T>(string section,string key,string name,T value,string help="",AcceptableValueBase? range=null)=>Plugin.Self.Config.Bind(section,key,value,ConfigLimits.Describe(section,key,value,new ConfigDescription(help,range,new CnConfig.ConfigurationManagerAttributes{DispName=name,Category=section,Description=help})));
}
