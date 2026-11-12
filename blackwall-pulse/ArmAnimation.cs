using EFT;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using NQuaternion=System.Numerics.Quaternion;

namespace LocalBlackwallPulse;

internal static class ArmAnimation {
 sealed record Change(Transform Bone,Vector3 Position,Quaternion Rotation,Vector3 AppliedPosition,Quaternion AppliedRotation);
 sealed record Finger(Transform Bone,int Source,Quaternion Start,Quaternion SourceStart);
 static GlbAnimation? animation;static Player? actor;static Camera? camera;
 static Transform? upper,fore,palm;static int clip,ik=-1,sourcePalm=-1;
 static NVector[] positions=Array.Empty<NVector>(),globalPositions=Array.Empty<NVector>();
 static NQuaternion[] rotations=Array.Empty<NQuaternion>(),globalRotations=Array.Empty<NQuaternion>();
 static readonly List<Change> changes=new();static readonly List<Finger> fingers=new();
 static readonly List<(Renderer Renderer,bool Before)> hidden=new();
 static Vector3 sourceOrigin;static Quaternion handStart,sourceRotationStart,cameraStart;
 static string report="not prepared";
 internal static bool Ready=>animation!=null;
 internal static void Load(string path){try{animation=new(path);positions=new NVector[animation.Bones.Length];rotations=new NQuaternion[positions.Length];globalPositions=new NVector[positions.Length];globalRotations=new NQuaternion[positions.Length];report="four gestures loaded";}catch(Exception error){report=error.Message;PulseRuntime.Log.LogWarning("Station animation: "+error.Message);}}
 static string Clean(string name)=>new(name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
 internal static bool Prepare(Player player,Camera view,int variant){
  Reset();if(animation==null)return false;actor=player;camera=view;clip=variant;
  var all=player.GetComponentsInChildren<Transform>(true).Where(t=>t!=null).ToArray();
  // PlayerBones identifies the active palm; name lookup covers both EFT skeleton layouts.
  palm=player.PlayerBones?.RightPalm??all.FirstOrDefault(t=>Clean(t.name).EndsWith("rpalm",StringComparison.Ordinal));
  fore=Ancestor(palm,"rforearm","rightforearm");upper=Ancestor(fore,"rupperarm","rightarm");
  if(palm==null||fore==null||upper==null){report="Missing right arm: "+string.Join(",",all.Where(t=>Clean(t.name).Contains("palm")||Clean(t.name).Contains("forearm")).Select(t=>t.name).Take(16));Reset();return false;}
  ik=Find("Torso_World_RightArm_Hand_IK_JNT","Torso_RightArm_Hand_IK_JNT","RightHand");sourcePalm=Find("RightHand");
  Sample(0);sourceOrigin=U(globalPositions[ik]);sourceRotationStart=U(globalRotations[sourcePalm]);handStart=palm.rotation;cameraStart=view.transform.rotation;
  string[] sourceNames={"Thumb","Index","Middle","Ring","Pinky"};
  for(int finger=0;finger<5;finger++)for(int joint=1;joint<=3;joint++){
   string source="RightHand"+sourceNames[finger]+joint;if(!animation.Names.TryGetValue(source,out int index))continue;
   string digit="rdigit"+(finger+1)+joint;
   var bone=palm.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>Clean(t.name).EndsWith(digit,StringComparison.Ordinal)||Clean(t.name).EndsWith(Clean(source),StringComparison.Ordinal));
   if(bone!=null)fingers.Add(new(bone,index,bone.localRotation,U(rotations[index])));
  }
  // Do not change inventory or hands controllers. Hide only the currently held weapon mesh.
  var weapon=player.PlayerBones?.Weapon_Root_Anim;
  if(player.HandsController?.TryCast<Player.FirearmController>()!=null&&weapon!=null)foreach(var renderer in weapon.GetComponentsInChildren<Renderer>(true)){
   if(renderer==null||renderer.TryCast<SkinnedMeshRenderer>() is {} skin&&skin.bones.Any(b=>b!=null&&b.Pointer==palm.Pointer))continue;
   hidden.Add((renderer,renderer.forceRenderingOff));renderer.forceRenderingOff=true;
  }
  if(fingers.Count<10){report="Insufficient finger mapping: "+string.Join(",",palm.GetComponentsInChildren<Transform>(true).Select(t=>t.name).Take(35));Reset();return false;}
  report=$"gesture_{clip+1:00}; {upper.name} / {fore.name} / {palm.name}; fingers={fingers.Count}";return true;
 }
 static Transform? Ancestor(Transform? from,params string[] fragments){for(var t=from?.parent;t!=null;t=t.parent){string name=Clean(t.name);if(fragments.Any(name.Contains)&&!name.Contains("twist"))return t;}return null;}
 static int Find(params string[] names){foreach(string n in names)if(animation!.Names.TryGetValue(n,out int i))return i;throw new InvalidDataException("Station animation missing hand IK");}
 static void Sample(float time){
  animation!.Sample(clip,time,positions,rotations);
  for(int i=0;i<positions.Length;i++){
   int parent=animation.Bones[i].Parent;
   if(parent<0){globalPositions[i]=positions[i];globalRotations[i]=rotations[i];}
   else{globalPositions[i]=globalPositions[parent]+NVector.Transform(positions[i],globalRotations[parent]);globalRotations[i]=NQuaternion.Normalize(globalRotations[parent]*rotations[i]);}
  }
 }
 internal static object Diagnostics()=>new{ready=Ready,report,active=actor!=null,mappedFingers=fingers.Count};
 internal static void BeforeNative(Player __instance){if(actor?.Pointer==__instance.Pointer)Restore();}
 internal static void BeforeWeapon(Player.FirearmController __instance){if(PulseRuntime.Owns(__instance))Restore();}
 internal static void AfterNative(Player __instance){if(actor?.Pointer!=__instance.Pointer)return;try{Apply(actor,PulseRuntime.Age);}catch(Exception error){Reset();PulseRuntime.Log.LogWarning("Blackwall hand pose: "+error.Message);}}
 internal static void Apply(Player? player,float age){
  Restore();if(player==null||actor?.Pointer!=player.Pointer||age<0||age>=PulseRules.CastDuration||camera==null||upper==null||fore==null||palm==null)return;
  float weight=PulseRules.Weight(age);Sample(Math.Min(age,animation!.Clips[clip].Duration));
  var view=camera.transform;var sourceMove=U(globalPositions[ik])-sourceOrigin;
  // CP2077 uses a different bind pose and axes. Preserve its hand trajectory and
  // finger articulation, solve EFT's arm lengths instead of copying arm rotations.
  sourceMove=Vector3.ClampMagnitude(sourceMove,.65f);
  var target=view.position+view.right*(.22f+sourceMove.x*.45f)+view.up*(-.19f+sourceMove.y*.45f)+view.forward*(.48f+sourceMove.z*.35f);
  var start=upper.position;float a=Vector3.Distance(start,fore.position),b=Vector3.Distance(fore.position,palm.position);if(a<.03f||b<.03f)return;
  var delta=target-start;float distance=Math.Clamp(delta.magnitude,Math.Abs(a-b)+.005f,a+b-.005f);var direction=delta.normalized;
  var bend=Vector3.ProjectOnPlane(view.right*.7f-view.up,direction).normalized;
  float along=(a*a-b*b+distance*distance)/(2*distance);float height=MathF.Sqrt(Math.Max(0,a*a-along*along));var elbow=start+direction*along+bend*height;
  Rotate(upper,Quaternion.Slerp(upper.rotation,Quaternion.FromToRotation(fore.position-start,elbow-start)*upper.rotation,weight));
  Rotate(fore,Quaternion.Slerp(fore.rotation,Quaternion.FromToRotation(palm.position-fore.position,target-fore.position)*fore.rotation,weight));
  var sourceDelta=U(globalRotations[sourcePalm])*Quaternion.Inverse(sourceRotationStart);
  var hand=view.rotation*Quaternion.Inverse(cameraStart)*handStart*sourceDelta;
  Rotate(palm,Quaternion.Slerp(palm.rotation,hand,weight));
  foreach(var f in fingers){if(f.Bone==null)continue;var value=f.Start*Quaternion.Inverse(f.SourceStart)*U(rotations[f.Source]);LocalRotate(f.Bone,Quaternion.Slerp(f.Bone.localRotation,value,weight));}
 }
 static void Rotate(Transform bone,Quaternion world){var p=bone.localPosition;var q=bone.localRotation;bone.rotation=world;changes.Add(new(bone,p,q,bone.localPosition,bone.localRotation));}
 static void LocalRotate(Transform bone,Quaternion value){var p=bone.localPosition;var q=bone.localRotation;bone.localRotation=value;changes.Add(new(bone,p,q,p,value));}
 internal static void Restore(){for(int i=changes.Count-1;i>=0;i--){var c=changes[i];if(c.Bone==null)continue;if(Quaternion.Angle(c.Bone.localRotation,c.AppliedRotation)<.02f)c.Bone.localRotation=c.Rotation;if((c.Bone.localPosition-c.AppliedPosition).sqrMagnitude<.00000001f)c.Bone.localPosition=c.Position;}changes.Clear();}
 internal static void Reset(){Restore();foreach(var item in hidden)if(item.Renderer!=null)item.Renderer.forceRenderingOff=item.Before;hidden.Clear();fingers.Clear();actor=null;camera=null;upper=fore=palm=null;}
 internal static void Shutdown(){Reset();animation=null;}
 static Vector3 U(NVector v)=>new(v.X,v.Y,-v.Z);
 static Quaternion U(NQuaternion q)=>new(-q.X,-q.Y,q.Z,q.W);
}
