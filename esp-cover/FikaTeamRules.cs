using System;
namespace LocalSpt5Esp;
internal static class FikaTeamRules {
 internal static bool Teammate(bool local,bool sameProfile,bool ai,bool? observedAi,bool fika,string group,string localGroup) {
  if(local||sameProfile||ai||observedAi==true||!fika)return false;
  // ObservedPlayer explicitly identifies whether it represents a bot. Faction,
  // role and nickname cannot distinguish human Scavs from replicated AI.
  if(observedAi==false)return true;
  return group.Length>0&&group.Equals("Fika",StringComparison.Ordinal)&&group.Equals(localGroup,StringComparison.Ordinal);
 }
}
