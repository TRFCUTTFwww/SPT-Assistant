using System;
namespace LocalSpt5Esp;
internal static class ExfilRequirementRules {
 internal static bool CanBypass(string status,bool secret,bool lever,bool train)=>!secret&&!lever&&!train&&status is ("RegularMode" or "UncompleteRequirements" or "Countdown");
 internal static bool Local(bool active,bool raid,bool alive,bool isLocal,string requested,string local,bool eligible,bool banned)=>active&&raid&&alive&&isLocal&&!string.IsNullOrEmpty(local)&&string.Equals(requested,local,StringComparison.Ordinal)&&eligible&&!banned;
}
