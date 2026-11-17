using System;
namespace LocalSpt5Esp;
internal static class SeasonSelectionRules {
 internal static bool CanAlias(bool enabled,bool inRaid,bool seasonLocked,bool pveHasProfile,bool pveSelectable,string pveId)=>enabled&&!inRaid&&seasonLocked&&pveHasProfile&&pveSelectable&&!string.IsNullOrWhiteSpace(pveId);
 internal static bool CanSubmit(bool eligible,string pveId,string selectedId)=>eligible&&!string.IsNullOrEmpty(pveId)&&string.Equals(pveId,selectedId,StringComparison.Ordinal);
}
