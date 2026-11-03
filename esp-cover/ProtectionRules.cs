namespace LocalSpt5Esp;
internal static class ProtectionRules {
 internal static bool CompanionProtection(bool companion,bool requested)=>!companion&&requested;
 internal static bool Protect(long localHealth,long targetHealth,bool playerEnabled,bool aiEnabled,bool aiOwner,bool sameProfile){if(localHealth==0||targetHealth==0)return false;if(localHealth==targetHealth)return playerEnabled;return aiEnabled&&aiOwner&&!sameProfile;}
}
