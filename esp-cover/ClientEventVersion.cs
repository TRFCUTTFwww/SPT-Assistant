using System.Diagnostics;
using System.IO;
namespace LocalSpt5Esp;
internal static class ClientEventVersion {
 static string? version;
 internal static string Version=>version??=FileVersionInfo.GetVersionInfo(Path.Combine(BepInEx.Paths.GameRootPath,"EscapeFromTarkov.exe")).ProductVersion??"";
 internal static bool Native=>ClientEventRules.Native(Version);
 internal static bool LegacyPort=>ClientEventRules.LegacyPort(Version);
}
