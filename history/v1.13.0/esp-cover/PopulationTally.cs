using System;
using System.Collections.Generic;
namespace LocalSpt5Esp;
internal enum PmcFaction { Unknown, Usec, Bear }
internal sealed class PopulationTally {
 readonly HashSet<string> seen=new(StringComparer.Ordinal);
 internal readonly int[] Counts=new int[Enum.GetValues<CharacterKind>().Length];
 internal int Total,Usec,Bear,Unknown;
 internal static PmcFaction Faction(string role,string side){
  if(role.Equals("pmcUSEC",StringComparison.OrdinalIgnoreCase))return PmcFaction.Usec;
  if(role.Equals("pmcBEAR",StringComparison.OrdinalIgnoreCase))return PmcFaction.Bear;
  if(side.Equals("Usec",StringComparison.OrdinalIgnoreCase))return PmcFaction.Usec;
  if(side.Equals("Bear",StringComparison.OrdinalIgnoreCase))return PmcFaction.Bear;
  return PmcFaction.Unknown;
 }
 internal void Clear(){seen.Clear();Array.Clear(Counts);Total=Usec=Bear=Unknown=0;}
 internal void Add(string identity,CharacterKind kind,string role,string side,bool alive,bool local){
  if(!alive||local||!seen.Add(identity))return;
  Counts[(int)kind]++;Total++;
  if(kind!=CharacterKind.Pmc)return;
  switch(Faction(role,side)){case PmcFaction.Usec:Usec++;break;case PmcFaction.Bear:Bear++;break;default:Unknown++;break;}
 }
}
