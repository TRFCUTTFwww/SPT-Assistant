using System;
using System.Collections.Generic;
using System.Linq;
namespace LocalSpt5Esp;
internal sealed class AimTargetRules {
 readonly HashSet<CharacterKind> kinds;
 readonly HashSet<string> roles,names,profiles;
 readonly bool usec,bear;
 internal AimTargetRules(IEnumerable<CharacterKind> excludedKinds,bool excludeUsec,bool excludeBear,string excludedRoles,string excludedNames,string excludedProfiles){kinds=new(excludedKinds);usec=excludeUsec;bear=excludeBear;roles=Tokens(excludedRoles);names=Tokens(excludedNames);profiles=Tokens(excludedProfiles);}
 internal static HashSet<string> Tokens(string value)=>new(value.Split(new[]{',',';','\r','\n','，','；','、'},StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries),StringComparer.OrdinalIgnoreCase);
 internal static string Join(IEnumerable<string> values)=>string.Join("; ",values.OrderBy(value=>value,StringComparer.OrdinalIgnoreCase));
 internal bool Excludes(CharacterKind kind,string side,string role,string name,string profile){
  if(kinds.Contains(kind)||roles.Contains(role)||names.Contains(name)||profiles.Contains(profile))return true;
  if(kind!=CharacterKind.Pmc)return false;
  bool roleUsec=role.Equals("pmcUSEC",StringComparison.OrdinalIgnoreCase),roleBear=role.Equals("pmcBEAR",StringComparison.OrdinalIgnoreCase);
  bool isUsec=roleUsec||!roleBear&&side.Equals("Usec",StringComparison.OrdinalIgnoreCase);
  bool isBear=roleBear||!roleUsec&&side.Equals("Bear",StringComparison.OrdinalIgnoreCase);
  return usec&&isUsec||bear&&isBear;
 }
}
