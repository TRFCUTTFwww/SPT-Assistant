namespace LocalQuickhacks;

internal static class GatewayTiming {
    // Finish the overhead completion track and the live-body effect before damage.
    internal const float ExecutionDuration=1.25f;
    internal static bool CanExecute(float completed,float now)=>completed>=0&&now-completed>=ExecutionDuration;
}
