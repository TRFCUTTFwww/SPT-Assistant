namespace LocalQuickhacks;

internal static class CombatHackRules {
    internal const float OverheatDuration=8,OverheatDamage=160;
    // Integrate elapsed time, including the final partial tick, independently of FPS.
    internal static float HeatDamage(float elapsed,float previous)=>
        OverheatDamage/OverheatDuration*Math.Max(0,Math.Clamp(elapsed,0,OverheatDuration)-Math.Clamp(previous,0,OverheatDuration));
    internal static float BurnoutDamage(float missingRam)=>80*(1+3*Math.Clamp(missingRam/24,0,1));
}
