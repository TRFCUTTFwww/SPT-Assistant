using System;
namespace LocalSpt5Esp;
internal sealed class RaidPauseState {
 long owner;bool paused;double remaining;
 internal double Update(long timer,bool enabled,double elapsed,double duration){
  if(!enabled){Reset();return duration;}
  if(!paused||owner!=timer){owner=timer;remaining=Math.Max(0,duration-elapsed);paused=true;}
  return RaidRules.Duration(elapsed,remaining);
 }
 internal void SetRemaining(double seconds){if(paused)remaining=seconds;}
 internal void Reset(){owner=0;paused=false;remaining=0;}
}
