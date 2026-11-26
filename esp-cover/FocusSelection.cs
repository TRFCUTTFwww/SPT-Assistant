using System;
using System.Collections.Generic;
namespace LocalSpt5Esp;
internal sealed class FocusSelection {
 internal readonly record struct Candidate(long Key,float X,float Y);
 long selected;float changed;
 internal void Clear(){selected=0;changed=0;}
 internal long Choose(IReadOnlyList<Candidate> candidates,float centerX,float centerY,float now){
  long best=0;float bestDistance=120,currentDistance=float.MaxValue;Candidate? current=null;
  foreach(var c in candidates){float x=c.X-centerX,y=c.Y-centerY,d=MathF.Sqrt(x*x+y*y);
   if(c.Key==selected){currentDistance=d;current=c;}
   if(d<bestDistance||(d==bestDistance&&c.Key<best)){bestDistance=d;best=c.Key;}
  }
  float margin=24;
  if(current.HasValue)foreach(var c in candidates)if(c.Key!=selected){float x=c.X-current.Value.X,y=c.Y-current.Value.Y;float spacing=MathF.Sqrt(x*x+y*y);margin=Math.Min(margin,Math.Max(1.5f,spacing*0.25f));}
  if(selected!=0&&currentDistance<=150&&(best==selected||best==0||bestDistance+margin>=currentDistance||now-changed<0.3f))return selected;
  if(best!=selected){selected=best;changed=now;}
  return selected;
 }
}
