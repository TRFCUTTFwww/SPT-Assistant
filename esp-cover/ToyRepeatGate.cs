using System;
namespace LocalSpt5Esp;
internal sealed class ToyRepeatGate {
 readonly ToyRepeatPulse pulse=new();double next,last;
 internal void Reset(){pulse.Reset();next=last=0;}
 internal bool IsDown(int frame)=>pulse.IsDown(frame);
 internal bool Step(bool held,int frame,double now,int rpm,bool ready){
  if(!held||!double.IsFinite(now)||rpm<=0){Reset();return false;}
  if(now<last)Reset();last=now;
  if(pulse.CanAcquire(frame)&&ready&&now+1e-6>=next){pulse.Press(frame);next=now+60d/rpm;}
  return pulse.IsDown(frame);
 }
}
internal sealed class ToyRepeatPulse {
 int down=-1,released=-1;
 internal void Reset(){down=released=-1;}
 internal bool CanAcquire(int frame){if(down>=0&&frame>down){down=-1;released=frame;}return down<0&&frame>released;}
 internal void Press(int frame)=>down=frame;
 internal bool IsDown(int frame)=>down==frame;
}
