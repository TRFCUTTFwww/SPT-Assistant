using System.Text.Json;
namespace LocalGearMail;
public static class FunItemPolicy {
 public static readonly bool EnabledAtStartup=Read("FunItemsEnabled")&&File.Exists(System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalMinecraft","Local.Minecraft.Server.dll"));
 public static readonly bool DeveloperTraderEnabledAtStartup=Read("DeveloperTraderEnabled");
 public static bool IsFunTemplate(string id)=>id.StartsWith("a7a000",StringComparison.Ordinal)||id.StartsWith("a7e000",StringComparison.Ordinal);
 static bool Read(string key)=>ReadFlag(System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","misc-settings.json"),key);
 internal static bool ReadFlag(string path,string key){if(!File.Exists(path))return true;using var doc=JsonDocument.Parse(File.ReadAllText(path));return !doc.RootElement.TryGetProperty(key,out var value)||value.GetBoolean();}
}
