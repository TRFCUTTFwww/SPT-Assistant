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
Check(ArmMeshRules.Score("Hands_BOSS_Killa",true,true,true,true,true,true)>=0,"native forceRenderingOff does not make a bound arm mesh unusable");
Check(ArmMeshRules.Score("Hands_BOSS_Killa",true,true,true,false,true,true)>ArmMeshRules.Score("Top_BOSS_Killa_base_lod0",true,true,true,false,true,true),"prefer hands over full-body clothing sharing the same palm");
Check(ArmMeshRules.Score("Hands_BOSS_Killa",true,true,true,false,true,true)>ArmMeshRules.Score("Hands_BOSS_Killa",true,true,true,true,true,true),"prefer currently visible rig over hidden duplicates");
Check(ArmMeshRules.Score("Hands_BOSS_Killa",true,true,true,false,true,false)<0&&ArmMeshRules.Score("Blackwall cast arms",true,true,true,false,true,true)<0,"reject mismatched bone bindings and owned copies");
Check(new[]{"Base HumanRUpperarm","Base HumanRForearm1","Base HumanRPalm","Base HumanRDigit11","RightHandIndex1"}.All(ArmMeshRules.RightArmBone),"recognize right-arm skinning on separate body rigs");
Check(new[]{"Base HumanLUpperarm","Base HumanLPalm","Base HumanSpine3","Base HumanRThigh"}.All(n=>!ArmMeshRules.RightArmBone(n)),"body mask retains left arm torso and legs");
Check(ArmMeshRules.RightHandBone("Base HumanRPalm")&&ArmMeshRules.RightHandBone("RightHand")&&!ArmMeshRules.RightHandBone("Base HumanRUpperarm")&&!ArmMeshRules.RightHandBone("Base HumanLPalm"),"shoulder-weighted armour is not mistaken for a native hand mesh");
var armBones=new[]{false,true};Check(ArmMeshRules.ArmWeight(1,.5f,armBones)&&!ArmMeshRules.ArmWeight(1,0,armBones)&&!ArmMeshRules.ArmWeight(0,1,armBones)&&!ArmMeshRules.ArmWeight(9,1,armBones),"skin mask ignores zero weights and unrelated bones");
Check(ArmMeshRules.KeepBodyTriangle(false,false,false)&&!ArmMeshRules.KeepBodyTriangle(true,false,false)&&!ArmMeshRules.KeepBodyTriangle(false,true,false)&&!ArmMeshRules.KeepBodyTriangle(false,false,true),"remove right-arm boundary triangles while preserving the body");
var skinTriangles=new[]{0,1,2,3,4,5,6,7,8};var armVertices=new[]{false,false,false,true,false,false,true,true,true};
var armTriangles=ArmMeshRules.Triangles(skinTriangles,armVertices,true);var bodyTriangles=ArmMeshRules.Triangles(skinTriangles,armVertices,false);
Check(armTriangles.SequenceEqual(new[]{3,4,5,6,7,8})&&bodyTriangles.SequenceEqual(new[]{0,1,2}),"cast contains only arm triangles, native mesh keeps body triangles");
Check(armTriangles.Concat(bodyTriangles).Order().SequenceEqual(skinTriangles)&&!armTriangles.Intersect(bodyTriangles).Any(),"native and cast geometry partition has no duplicated or missing triangle");
bool badTriangles=false;try{ArmMeshRules.Triangles(new[]{0,1,9},armVertices,true);}catch(InvalidDataException){badTriangles=true;}Check(badTriangles,"invalid skin indices fail before touching a Unity mesh");
var sourceReference=Quaternion.CreateFromAxisAngle(Vector3.UnitX,.8f);var targetReference=Quaternion.CreateFromAxisAngle(Vector3.UnitY,-.6f);var motion=Quaternion.CreateFromAxisAngle(Vector3.UnitZ,.5f);
var mapped=PoseRetarget.Map(targetReference,sourceReference,sourceReference*motion);
var axisConversion=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI/2);
var mappedFinger=PoseRetarget.MapLocal(targetReference,sourceReference,sourceReference*motion,axisConversion);
Check(Math.Abs(Quaternion.Dot(mappedFinger,Quaternion.Normalize(targetReference*axisConversion*motion*Quaternion.Inverse(axisConversion))))>.99999,"finger motion converts source axes into target bind axes");
Check(Math.Abs(Quaternion.Dot(PoseRetarget.MapLocal(targetReference,sourceReference,sourceReference,axisConversion),targetReference))>.99999,"finger rest pose is independent of current weapon grip");
Check(Math.Abs(Quaternion.Dot(mapped,Quaternion.Normalize(targetReference*motion)))>.99999,"non-commuting bind calibration preserves the intended wrist delta");
Check(Math.Abs(PoseRetarget.Flexion(Quaternion.Identity,Quaternion.CreateFromAxisAngle(Vector3.UnitX,.5f),Vector3.UnitX)-.5f*180/MathF.PI)<.001,"finger flexion follows calibrated bend axis");
Check(Math.Abs(PoseRetarget.Flexion(Quaternion.Identity,Quaternion.CreateFromAxisAngle(Vector3.UnitZ,1),Vector3.UnitX))<.001,"finger sideways twist is not copied");
Check(Math.Abs(PoseRetarget.Flexion(Quaternion.Identity,Quaternion.CreateFromAxisAngle(Vector3.UnitX,2),Vector3.UnitX,70)-70)<.001,"finger flexion is anatomically bounded");
byte[] Wave(short bits,short format,byte[] data){using var ms=new MemoryStream();using var w=new BinaryWriter(ms);w.Write("RIFF"u8);w.Write(36+data.Length);w.Write("WAVEfmt "u8);w.Write(16);w.Write(format);w.Write((short)1);w.Write(48000);w.Write(48000*bits/8);w.Write((short)(bits/8));w.Write(bits);w.Write("data"u8);w.Write(data.Length);w.Write(data);return ms.ToArray();}
var pcm16=WavePcm.Read(Wave(16,1,new byte[]{0,128,0,0,255,127}));Check(pcm16.Channels==1&&pcm16.Rate==48000&&pcm16.Samples[0]==-1&&pcm16.Samples[1]==0&&pcm16.Samples[2]>.999,"16-bit signed PCM decodes without a Unity web request");
var quiet=new WavePcm(2,48000,new[]{0f,.1f,-.1f,1f,-1f});var louder=quiet.WithGain(3);Check(louder.Channels==quiet.Channels&&louder.Rate==quiet.Rate&&louder.Samples.Length==quiet.Samples.Length&&louder.Samples[0]==0&&Math.Abs(louder.Samples[1]-.3f)<.00001f&&Math.Abs(louder.Samples[2]+.3f)<.00001f,"audio gain raises quiet samples without changing silence or format");Check(louder.Samples.All(float.IsFinite)&&louder.Samples.Max(x=>Math.Abs(x))<=.980001f&&louder.Samples[3]>.8f&&louder.Samples[4]<-.8f&&quiet.Samples[1]==.1f,"audio boost limits peaks symmetrically and leaves original PCM intact");
var segment=new WavePcm(1,48000,new[]{.1f,.2f});var mixed=DeathMix.Mix(new[]{segment,segment,segment,segment,segment});Check(mixed.Channels==2&&mixed.Samples.Length==19208&&Math.Abs(mixed.Samples[0]-.1398f)<.0001f&&Math.Abs(mixed.Samples[4]-.0398f)<.0001f&&Math.Abs(mixed.Samples[19200]-.0631f)<.0001f,"death mix preserves random layers, sequence and 200ms delay");
var stereo=new WavePcm(2,48000,new[]{1f,-1f});var stereoMix=DeathMix.Mix(Enumerable.Repeat(stereo,5).ToArray());Check(stereoMix.Samples.All(float.IsFinite)&&stereoMix.Samples.Max(x=>Math.Abs(x))<=.980001f&&stereoMix.Samples[0]>0&&stereoMix.Samples[1]<0,"death mix preserves stereo and prevents clipping");
var pcm24=WavePcm.Read(Wave(24,1,new byte[]{0,0,128,0,0,0,255,255,127}));Check(pcm24.Samples[0]==-1&&pcm24.Samples[2]>.999,"24-bit PCM sign extension");
Check(WavePcm.Read(Wave(32,3,BitConverter.GetBytes(.75f))).Samples[0]==.75f,"float PCM preserves audio amplitude");
bool rejected=false;try{WavePcm.Read(Wave(32,3,BitConverter.GetBytes(float.NaN)));}catch(InvalidDataException){rejected=true;}Check(rejected,"invalid float audio cannot enter Unity");
rejected=false;try{WavePcm.Read(Wave(16,1,new byte[]{1}));}catch(InvalidDataException){rejected=true;}Check(rejected,"incomplete audio frame is rejected");
foreach(string file in args.Where(f=>f.EndsWith(".wav",StringComparison.OrdinalIgnoreCase))){var wave=WavePcm.Read(file);Check(wave.Samples.Length>1000&&wave.Samples.Any(x=>Math.Abs(x)>.01),"installed sound contains audible PCM samples");Console.WriteLine($"WAV {Path.GetFileName(file)} {wave.Rate}Hz {wave.Channels}ch {wave.Samples.Length/(double)wave.Channels/wave.Rate:F2}s");}
var eventIds=new[]{"198549883","934676726","295523828","71869785","648806972"};var eventFiles=eventIds.Select(id=>args.FirstOrDefault(f=>Path.GetFileName(f)==id+".wav")).ToArray();if(eventFiles.All(f=>f!=null)){var eventMix=DeathMix.Mix(eventFiles.Select(f=>WavePcm.Read(f!)).ToArray());Check(eventMix.Samples.Length>48000*2&&eventMix.Samples.All(float.IsFinite)&&eventMix.Samples.Max(x=>Math.Abs(x))<=1,"real five-layer event mixes mono and stereo source audio");}
foreach(string file in args.Where(f=>f.EndsWith(".glb",StringComparison.OrdinalIgnoreCase))){
 var animation=new GlbAnimation(file);Check(animation.Clips.Length==4,"four station gestures");
 var positions=new Vector3[animation.Bones.Length];var rotations=new Quaternion[positions.Length];
 Check(animation.Names.ContainsKey("RightHandIndex1")&&animation.Names.ContainsKey("Torso_World_RightArm_Hand_IK_JNT"),"finger and IK tracks");
 Check(new[]{"RightInHandThumb","RightHandThumb1","RightHandThumb2","Torso_fppCamera_Aim_JNT","Torso_fppCamera_Target_JNT","Torso_fppCamera_Up_GRP"}.All(animation.Names.ContainsKey),"thumb metacarpal and both phalanges plus source camera frame exist");
 for(int c=0;c<4;c++){
  Check(Math.Abs(animation.Clips[c].Duration-1.6666666f)<.001,"complete clip duration");
  animation.Sample(c,0,positions,rotations);int finger=animation.Names["RightHandIndex1"];var first=rotations[finger];
  bool moved=false;for(float t=0;t<1.67f;t+=.05f){animation.Sample(c,t,positions,rotations);moved|=Math.Abs(Quaternion.Dot(first,rotations[finger]))<.999;Check(positions.All(p=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z))&&rotations.All(q=>float.IsFinite(q.W)&&Math.Abs(q.Length()-1)<.001),"finite normalized animation");}
  Check(moved,"gesture contains actual finger motion");
  animation.Sample(c,100,positions,rotations);Check(rotations.All(q=>float.IsFinite(q.W)),"sampling clamps at end");
 }
}
Console.WriteLine($"PASS {checks} Blackwall checks: target boundaries, cancellation, one-shot release, pose blending, GLB decoding and real hand motion.");
