namespace LocalSpt5Esp;

public static class RaidItemPoolRules {
 public static bool ShouldRecoverOrphanedHands(
  bool isLocal,
  bool isAlive,
  bool hasHandsController,
  bool hasScheduledProcess,
  bool isScheduledStatus,
  bool isLoading,
  float orphanSeconds)=>
  isLocal&&isAlive&&!hasHandsController&&!hasScheduledProcess&&isScheduledStatus&&!isLoading&&orphanSeconds>=1.0f;

 public static bool CanReplayProceed(
  long currentWorld,
  long expectedWorld,
  long currentActor,
  long expectedActor,
  bool isAlive,
  bool itemOwned,
  bool alreadyInHands,
  bool handsHealthy)=>
  currentWorld!=0&&currentWorld==expectedWorld&&currentActor!=0&&currentActor==expectedActor&&isAlive&&itemOwned&&!alreadyInHands&&handsHealthy;
}
