namespace LocalSpt5Esp;
internal static class AiActivationRules {
 internal static bool SpawnWindow(bool timerStarted,bool startDate,bool botsEnabled)=>timerStarted&&startDate&&botsEnabled;
 internal static bool Ready(bool settled,bool faulted,bool active,bool registered,bool movement,bool navigation)=>settled&&!faulted&&active&&registered&&movement&&navigation;
}
