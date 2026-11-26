using System;
namespace LocalSpt5Esp;
internal enum CabinetOutcome { Unknown, Wallet, Hand, Grenade }
internal static class EventCabinetRules {
 internal static CabinetOutcome Resolve(bool wallet,bool hand,bool grenade,string clip) {
  int count=(wallet?1:0)+(hand?1:0)+(grenade?1:0);
  if(count>1)return CabinetOutcome.Unknown;
  if(grenade)return CabinetOutcome.Grenade;
  if(wallet)return CabinetOutcome.Wallet;
  if(hand)return CabinetOutcome.Hand;
  if(clip.EndsWith("_Grenade",StringComparison.Ordinal))return CabinetOutcome.Grenade;
  if(clip.EndsWith("_Wallet",StringComparison.Ordinal))return CabinetOutcome.Wallet;
  if(clip.EndsWith("_Fuck",StringComparison.Ordinal))return CabinetOutcome.Hand;
  return CabinetOutcome.Unknown;
 }
}