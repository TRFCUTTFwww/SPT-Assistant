using System.Numerics;
using LocalBlackwallPulse;

int checks=0;
void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
bool Target(float distance=20,float dot=1,bool ai=true,bool alive=true,bool local=false,bool visible=true,bool walls=false)=>PulseRules.Target(distance,dot,60,70,ai,alive,local,visible,walls);
Check(Target(),"forward AI");Check(!Target(ai:false)&&!Target(alive:false)&&!Target(local:true),"exclude players, corpses, local");
Check(!Target(distance:61)&&!Target(distance:float.NaN)&&!Target(dot:float.NaN)&&!Target(dot:0),"range, invalid input and behind cone");
Check(!Target(visible:false)&&Target(visible:false,walls:true),"wall mode is explicit");
Check(PulseRules.Weight(0)==0&&PulseRules.Weight(.3f)==1&&PulseRules.Weight(PulseRules.CastDuration)==0,"pose blends in and out");
var cast=new CastLatch();Check(cast.Begin(10)&&!cast.Begin(10.1f),"no overlapping casts");
Check(!cast.Step(10.2f)&&cast.Step(10.7f)&&!cast.Step(10.8f),"exactly one release per cast");
Check(!cast.Step(12)&&!cast.Active&&cast.Begin(12),"no cooldown after return");cast.Cancel();Check(!cast.Step(12.7f),"cancel removes pending damage");
cast.Begin(20);Check(!cast.Step(25)&&!cast.Active,"long pause cannot execute stale release");
foreach(string file in args){
 var animation=new GlbAnimation(file);Check(animation.Clips.Length==4,"four station gestures");
 var positions=new Vector3[animation.Bones.Length];var rotations=new Quaternion[positions.Length];
 Check(animation.Names.ContainsKey("RightHandIndex1")&&animation.Names.ContainsKey("Torso_World_RightArm_Hand_IK_JNT"),"finger and IK tracks");
 for(int c=0;c<4;c++){
  Check(Math.Abs(animation.Clips[c].Duration-1.6666666f)<.001,"complete clip duration");
  animation.Sample(c,0,positions,rotations);int finger=animation.Names["RightHandIndex1"];var first=rotations[finger];
  bool moved=false;for(float t=0;t<1.67f;t+=.05f){animation.Sample(c,t,positions,rotations);moved|=Math.Abs(Quaternion.Dot(first,rotations[finger]))<.999;Check(positions.All(p=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z))&&rotations.All(q=>float.IsFinite(q.W)&&Math.Abs(q.Length()-1)<.001),"finite normalized animation");}
  Check(moved,"gesture contains actual finger motion");
  animation.Sample(c,100,positions,rotations);Check(rotations.All(q=>float.IsFinite(q.W)),"sampling clamps at end");
 }
}
Console.WriteLine($"PASS {checks} Blackwall checks: target boundaries, cancellation, one-shot release, pose blending, GLB decoding and real hand motion.");
