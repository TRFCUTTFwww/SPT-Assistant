using System;
namespace LocalSpt5Esp;
internal static class BallisticAdvantageRules {
 // Positive favors the local player. Mixed coverage never promises a safe hit.
 internal static int Evaluate(float minimum,float maximum,bool defending){
  if(!float.IsFinite(minimum)||!float.IsFinite(maximum)||minimum<0||maximum>1||minimum>maximum)return 0;
  if(minimum>=.65f)return defending?-1:1;
  if(maximum<=.35f)return defending?1:-1;
  return 0;
 }
}

// Collider counts are samples, not area weights or probabilities of being hit.
internal struct BallisticCoverage {
 internal int Covered,Uncovered;
 internal bool Unknown;
 internal float Minimum,Maximum;
 internal void Add(bool covered,float chance){
  if(!float.IsFinite(chance)||chance<0||chance>1){Unknown=true;return;}
  if(!covered){Uncovered++;return;}
  Minimum=Covered==0?chance:Math.Min(Minimum,chance);Maximum=Covered==0?chance:Math.Max(Maximum,chance);Covered++;
 }
 internal int Advantage(bool defending){
  if(Unknown||Covered==0&&Uncovered==0||Covered>0&&Uncovered>0)return 0;
  return BallisticAdvantageRules.Evaluate(Covered>0?Minimum:1,Covered>0?Maximum:1,defending);
 }
}
