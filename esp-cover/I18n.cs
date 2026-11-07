using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using BepInEx.Configuration;
namespace LocalSpt5Esp;
internal static class I18n {
 static ConfigEntry<string>? language;
 static readonly Dictionary<CnConfig.ConfigurationManagerAttributes,(string Name,string Category,string Help)> original=new();
 static readonly LocalizationCatalog catalog=new(Read("en.json"),Read("en-formats.json"),ReadVerbatim());
 static Dictionary<string,string> Read(string name){using var stream=typeof(I18n).Assembly.GetManifestResourceStream("LocalSpt5Esp.Locales."+name)??throw new InvalidOperationException("Missing locale resource: "+name);return JsonSerializer.Deserialize<Dictionary<string,string>>(stream)??throw new InvalidOperationException("Invalid locale resource: "+name);}
 static Dictionary<string,int[]> ReadVerbatim(){using var stream=typeof(I18n).Assembly.GetManifestResourceStream("LocalSpt5Esp.Locales.en-verbatim.json")!;return JsonSerializer.Deserialize<Dictionary<string,int[]>>(stream)!;}
 internal static bool English=>language?.Value.Equals("en",StringComparison.OrdinalIgnoreCase)==true;
 internal static string Text(string value)=>English?catalog.Text(value):value;
 internal static string[] Texts(string[] values)=>values.Select(Text).ToArray();
 internal static void Initialize(Plugin plugin){
  language=plugin.Config.Bind("24. Interface","Language","zh-CN",new ConfigDescription("界面语言 / Interface language",new AcceptableValueList<string>("zh-CN","en"),new CnConfig.ConfigurationManagerAttributes{DispName="语言 / Language",Category="24. 界面与主题",Description="界面语言 / Interface language"}));
  foreach(var entry in plugin.Config)foreach(var attributes in entry.Value.Description.Tags.OfType<CnConfig.ConfigurationManagerAttributes>())original[attributes]=(attributes.DispName,attributes.Category,string.IsNullOrEmpty(attributes.Description)?entry.Value.Description.Description:attributes.Description);
  language.SettingChanged+=Changed;Refresh();
 }
 internal static void Toggle(){if(language!=null){language.Value=English?"zh-CN":"en";Plugin.Self.Config.Save();}}
 static void Changed(object? sender,EventArgs args)=>Refresh();
 static void Refresh(){
  foreach(var entry in original){entry.Key.DispName=Text(entry.Value.Name);entry.Key.Category=Text(entry.Value.Category);entry.Key.Description=Text(entry.Value.Help);}
  try{var manager=BepInEx.Unity.IL2CPP.IL2CPPChainloader.Instance.Plugins.Values.FirstOrDefault(p=>p.Metadata.GUID=="com.bepis.bepinex.configurationmanager")?.Instance;manager?.GetType().GetMethod("BuildSettingList",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)?.Invoke(manager,null);}catch(Exception error){Plugin.Self.Log.LogWarning("Could not refresh settings language: "+error.Message);}
 }
 internal static object Diagnostics(){
  if(language==null)throw new InvalidOperationException("Language setting unavailable");string saved=language.Value;
  try{language.Value="en";string search=Text("搜索"),sentence=Text("已对 3 名 AI 提交玩家来源伤害；实际扣血合计 120。受伤害倍率与 AI 无敌设置影响。");
   bool metadata=original.Keys.Any(a=>a.DispName=="No key or keycard consumption");
   language.Value="zh-CN";return new{original=saved,englishSearch=search,englishSentence=sentence,englishMetadata=metadata,chineseSearch=Text("搜索"),resources=typeof(I18n).Assembly.GetManifestResourceNames().Where(n=>n.Contains("Locales")),verbatimName=catalog.Text("已读取 开启")};
  }finally{language.Value=saved;}
 }
 internal static void Shutdown(){if(language!=null)language.SettingChanged-=Changed;foreach(var entry in original){entry.Key.DispName=entry.Value.Name;entry.Key.Category=entry.Value.Category;entry.Key.Description=entry.Value.Help;}original.Clear();language=null;}
}
