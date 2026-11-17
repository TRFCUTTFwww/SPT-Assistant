namespace LocalSpt5Esp;
internal static class SeasonSelectionRules {
 internal static bool CanSubmit(bool enabled,bool inRaid,bool currentController,bool unlocked,bool sameDescriptor)=>enabled&&!inRaid&&currentController&&unlocked&&sameDescriptor;
}
