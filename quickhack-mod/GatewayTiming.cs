namespace LocalQuickhacks;

internal static class GatewayTiming {
    // Infection starts the animation immediately. Its end is the damage deadline.
    internal static bool CanExecute(float started,float duration,float now)=>started>=0&&now>=started+duration;
}
