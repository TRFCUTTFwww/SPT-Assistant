namespace ItemUseGuard;
internal static class UseRules {
 internal static bool Orphan(bool local,bool alive,bool hands,bool queued,bool scheduled,bool loading,float stableSeconds)=>local&&alive&&!hands&&!queued&&scheduled&&!loading&&stableSeconds>=5;
 internal static bool Resume(long world,long expectedWorld,long player,long expectedPlayer,bool alive,bool owned,bool sameHands,bool idle)=>world!=0&&world==expectedWorld&&player!=0&&player==expectedPlayer&&alive&&owned&&sameHands&&idle;
}
