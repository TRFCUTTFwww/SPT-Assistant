namespace LocalBlackwallPulse;

// Shared with the offline checks. No Unity objects or clock access here.
internal static class PulseRules {
 internal const float ReleaseAt=.62f;
 internal const float CastDuration=1.667f;
 internal const float VictimDuration=3.91f;
 internal static bool Target(float distance,float alignment,float range,float cone,bool ai,bool alive,bool local,bool visible,bool walls)=>
  ai&&alive&&!local&&float.IsFinite(distance)&&distance>=0&&distance<=Math.Clamp(range,1,300)&&float.IsFinite(alignment)&&alignment>=MathF.Cos(Math.Clamp(cone,1,180)*MathF.PI/360)&&(walls||visible);
 internal static float Weight(float age)=>Math.Clamp(Math.Min(age/.22f,(CastDuration-age)/.28f),0,1);
 internal static bool Release(float age,bool released)=>!released&&age>=ReleaseAt&&age<CastDuration;
}

internal sealed class CastLatch {
 internal bool Active{get;private set;}
 internal bool Released{get;private set;}
 internal float Started{get;private set;}
 internal bool Begin(float now){if(Active||!float.IsFinite(now))return false;Active=true;Released=false;Started=now;return true;}
 internal bool Step(float now){if(!Active)return false;float age=now-Started;if(age<0||age>=PulseRules.CastDuration){Cancel();return false;}if(!PulseRules.Release(age,Released))return false;Released=true;return true;}
 internal void Cancel(){Active=false;Released=false;}
}
