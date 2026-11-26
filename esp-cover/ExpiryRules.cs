namespace LocalSpt5Esp;
// Preserve the negative setting on disk; native timers receive a finite lifetime.
internal static class ExpiryRules {
 internal static float Lifetime(float seconds)=>seconds<0?float.MaxValue:seconds;
}
