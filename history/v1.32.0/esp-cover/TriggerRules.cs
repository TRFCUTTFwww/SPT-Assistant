using System;
namespace LocalSpt5Esp;
internal sealed class TriggerPulse {
 int down=-1,released=-1;
 internal void Reset(){down=released=-1;}
 internal bool CanAcquire(int frame){if(down>=0&&frame>down){down=-1;released=frame;}return down<0&&frame>released;}
 internal void Press(int frame)=>down=frame;
 internal bool IsDown(int frame)=>down==frame;
}
internal sealed class TriggerGate {
 string target="";double since,next;
 internal void Reset(){target="";since=next=0;}
 internal bool Ready(string id,double now,double delay,double interval){
  if(string.IsNullOrEmpty(id)||!double.IsFinite(now)||!double.IsFinite(delay)||!double.IsFinite(interval)||delay<0||interval<=0){target="";return false;}
  if(now<since){target="";next=0;}
  if(id!=target){target=id;since=now;}
  if(now-since+1e-6<delay||now+1e-6<next)return false;
  next=now+interval;return true;
 }
}
