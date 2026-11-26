namespace LocalSpt5Esp;
internal static class InstantSearchRules {
 internal static bool Allowed(bool enabled,bool inRaid,bool alive,bool terminated,long localController,long operationController,string localProfile,string operationProfile)=>
  enabled&&inRaid&&alive&&!terminated&&localController!=0&&localController==operationController&&!string.IsNullOrEmpty(localProfile)&&localProfile==operationProfile;
}
