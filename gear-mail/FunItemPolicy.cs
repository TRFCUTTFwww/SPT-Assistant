using System.Text.Json;
namespace LocalGearMail;
public static class FunItemPolicy {
 public static readonly bool EnabledAtStartup=Read();
 public static bool IsFunTemplate(string id)=>id.StartsWith("a7a000",StringComparison.Ordinal)||id.StartsWith("a7e000",StringComparison.Ordinal);
 static bool Read(){var path=System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","misc-settings.json");if(!File.Exists(path))return true;using var doc=JsonDocument.Parse(File.ReadAllText(path));return !doc.RootElement.TryGetProperty("FunItemsEnabled",out var value)||value.GetBoolean();}
}
