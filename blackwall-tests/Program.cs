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
Check(PoseRetarget.ElbowName("Base HumanRForearm1")&&!PoseRetarget.ElbowName("Base HumanRForearm3")&&!PoseRetarget.ElbowName("Base HumanRForearm1Twist"),"retarget selects elbow, never distal wrist/twist bone");
var sourceReference=Quaternion.CreateFromAxisAngle(Vector3.UnitX,.8f);var targetReference=Quaternion.CreateFromAxisAngle(Vector3.UnitY,-.6f);var motion=Quaternion.CreateFromAxisAngle(Vector3.UnitZ,.5f);
var mapped=PoseRetarget.Map(targetReference,sourceReference,sourceReference*motion);
Check(Math.Abs(Quaternion.Dot(mapped,Quaternion.Normalize(targetReference*motion)))>.99999,"non-commuting bind calibration preserves the intended wrist delta");
Check(Math.Abs(PoseRetarget.Flexion(Quaternion.Identity,Quaternion.CreateFromAxisAngle(Vector3.UnitX,.5f),Vector3.UnitX)-.5f*180/MathF.PI)<.001,"finger flexion follows calibrated bend axis");
Check(Math.Abs(PoseRetarget.Flexion(Quaternion.Identity,Quaternion.CreateFromAxisAngle(Vector3.UnitZ,1),Vector3.UnitX))<.001,"finger sideways twist is not copied");
Check(Math.Abs(PoseRetarget.Flexion(Quaternion.Identity,Quaternion.CreateFromAxisAngle(Vector3.UnitX,2),Vector3.UnitX,70)-70)<.001,"finger flexion is anatomically bounded");
byte[] Wave(short bits,short format,byte[] data){using var ms=new MemoryStream();using var w=new BinaryWriter(ms);w.Write("RIFF"u8);w.Write(36+data.Length);w.Write("WAVEfmt "u8);w.Write(16);w.Write(format);w.Write((short)1);w.Write(48000);w.Write(48000*bits/8);w.Write((short)(bits/8));w.Write(bits);w.Write("data"u8);w.Write(data.Length);w.Write(data);return ms.ToArray();}
var pcm16=WavePcm.Read(Wave(16,1,new byte[]{0,128,0,0,255,127}));Check(pcm16.Channels==1&&pcm16.Rate==48000&&pcm16.Samples[0]==-1&&pcm16.Samples[1]==0&&pcm16.Samples[2]>.999,"16-bit signed PCM decodes without a Unity web request");
var pcm24=WavePcm.Read(Wave(24,1,new byte[]{0,0,128,0,0,0,255,255,127}));Check(pcm24.Samples[0]==-1&&pcm24.Samples[2]>.999,"24-bit PCM sign extension");
Check(WavePcm.Read(Wave(32,3,BitConverter.GetBytes(.75f))).Samples[0]==.75f,"float PCM preserves audio amplitude");
bool rejected=false;try{WavePcm.Read(Wave(32,3,BitConverter.GetBytes(float.NaN)));}catch(InvalidDataException){rejected=true;}Check(rejected,"invalid float audio cannot enter Unity");
rejected=false;try{WavePcm.Read(Wave(16,1,new byte[]{1}));}catch(InvalidDataException){rejected=true;}Check(rejected,"incomplete audio frame is rejected");
foreach(string file in args.Where(f=>f.EndsWith(".wav",StringComparison.OrdinalIgnoreCase))){var wave=WavePcm.Read(file);Check(wave.Samples.Length>1000&&wave.Samples.Any(x=>Math.Abs(x)>.01),"installed sound contains audible PCM samples");Console.WriteLine($"WAV {Path.GetFileName(file)} {wave.Rate}Hz {wave.Channels}ch {wave.Samples.Length/(double)wave.Channels/wave.Rate:F2}s");}
foreach(string file in args.Where(f=>f.EndsWith(".glb",StringComparison.OrdinalIgnoreCase))){
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
