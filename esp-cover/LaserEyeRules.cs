using System;
using System.Numerics;
namespace LocalSpt5Esp;
// Preserve short presses across Update/LateUpdate without turning hold-to-fire
// into a toggle. Blocking input always discards the pending pulse.
internal sealed class LaserEyeInput {
 const float MinimumPulse=.12f;
 float until;bool wasHeld;
 internal bool Sample(float now,bool held,bool down,bool up,bool allowed){
  if(!allowed||!float.IsFinite(now)){Reset();return false;}
  if(down||held&&!wasHeld)until=now+MinimumPulse;
  wasHeld=held&&!up;
  return wasHeld||now<until;
 }
 internal void Reset(){until=0;wasHeld=false;}
}
internal static class LaserEyeRules {
 internal static bool CanFire(bool enabled,bool held,bool raid,bool alive,bool blocked,bool freeCamera,bool cursor,bool focused)
  =>enabled&&held&&raid&&alive&&!blocked&&!freeCamera&&!cursor&&focused;
 internal static bool Beam(Vector3 eye,Vector3 aim,Vector3 forward,float range,out Vector3 direction,out float length){
  direction=Vector3.Zero;length=0;
  if(!Finite(eye)||!Finite(aim)||!Finite(forward)||!float.IsFinite(range)||range<=0)return false;
  var delta=aim-eye;float distance=delta.Length();
  // A shoulder camera can see a wall behind the eyes. Never turn a beam backwards.
  if(!float.IsFinite(distance)||distance<.001f||Vector3.Dot(delta,forward)<=0)return false;
  direction=delta/distance;length=Math.Min(range,distance+.025f);return true;
 }
 static bool Finite(Vector3 value)=>float.IsFinite(value.X)&&float.IsFinite(value.Y)&&float.IsFinite(value.Z);
}
