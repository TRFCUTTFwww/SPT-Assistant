using System;
namespace LocalSpt5Esp;
internal sealed class ShotCorrectionWindow {
 double until;long weapon;int fire;double last=-1;
 internal bool Active(double now)=>double.IsFinite(now)&&now<until;
 internal bool Fired(double now,double seconds,long weaponPointer,int fireIndex){if(!double.IsFinite(now)||!double.IsFinite(seconds)||seconds<=0)return false;if(weapon==weaponPointer&&fire==fireIndex&&last>=0&&now-last<.05)return false;weapon=weaponPointer;fire=fireIndex;last=now;until=now+seconds;return true;}
 internal void Reset(){until=0;last=-1;weapon=0;fire=0;}
}
