using System;
namespace LocalSpt5Esp;
internal static class ClientEventRules {
 internal static bool LegacyPort(string productVersion)=>Parse(productVersion) is {} version&&version.Major==1&&version.Minor==2;
 internal static bool Native(string productVersion)=>Parse(productVersion) is {} version&&version.Major==1&&version.Minor>=3;
 static Version? Parse(string value)=>Version.TryParse(value.Split('-','+')[0],out var version)?version:null;
}
