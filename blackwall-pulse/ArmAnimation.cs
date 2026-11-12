using EFT;
using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;
using NQuaternion=System.Numerics.Quaternion;
namespace LocalBlackwallPulse;
internal static class ArmAnimation {
 sealed record Change(Transform Bone,Vector3 Position,Quaternion Rotation,Quaternion Applied);
 sealed record Finger(Transform Bone,int Source,Quaternion Start,Quaternion SourceStart,Vector3 SourceAxis,Vector3 TargetAxis,float Limit);
 static GlbAnimation? animation;static Player? actor;static Camera? camera;
 static Transform? upper,fore,palm;static int clip,ik=-1,sourcePalm=-1;
 static NVector[] positions=Array.Empty<NVector>(),globalPositions=Array.Empty<NVector>();
 static NQuaternion[] rotations=Array.Empty<NQuaternion>(),globalRotations=Array.Empty<NQuaternion>();
 static readonly List<Change> changes=new();static readonly List<Finger> fingers=new();
 static readonly List<(Renderer Renderer,bool Before)> hidden=new();static readonly List<SkinnedMeshRenderer> copies=new();
 static Vector3 sourceOrigin,startView;static Quaternion handStart,sourceRotationStart,cameraStart;
 static string report="not prepared";static bool firstPerson;
 static IntPtr observed;static object? rig;
 internal static bool Ready=>animation!=null;
 internal static SkinnedMeshRenderer[] CastRenderers=>copies.Where(x=>x!=null).ToArray();
 internal static Transform? CastPalm=>palm;
 internal static object? InspectRig(Player? player){
  if(player==null)return null;if(observed==player.Pointer)return rig;observed=player.Pointer;
  var root=player.PlayerBody?.SkeletonHands;var bones=root?.GetComponentsInChildren<Transform>(true).Where(t=>t!=null).ToArray()??Array.Empty<Transform>();
  var palms=bones.Where(t=>Clean(t.name).EndsWith("rpalm",StringComparison.Ordinal)).ToArray();
  var mesh=player.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r!=null&&palms.Any(p=>r.bones.Any(b=>b!=null&&b.Pointer==p.Pointer))).Select(r=>new{name=r.name,active=r.gameObject.activeInHierarchy,enabled=r.enabled,layer=r.gameObject.layer}).ToArray();
  return rig=new{root=root?.name,palms=palms.Select(t=>t.name).ToArray(),elbows=bones.Where(t=>PoseRetarget.ElbowName(t.name)).Select(t=>t.name).ToArray(),boundMeshes=mesh};
 }
 internal static void Load(string path){try{animation=new(path);positions=new NVector[animation.Bones.Length];rotations=new NQuaternion[positions.Length];globalPositions=new NVector[positions.Length];globalRotations=new NQuaternion[positions.Length];report="four gestures loaded";}catch(Exception error){report=error.Message;PulseRuntime.Log.LogWarning("Station animation: "+error.Message);}}
 static string Clean(string name)=>new(name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
 internal static bool Prepare(Player player,Camera view,int variant){
  Reset();if(animation==null)return false;actor=player;camera=view;clip=variant;
  var handRoot=player.PlayerBody?.SkeletonHands;
  var all=handRoot?.GetComponentsInChildren<Transform>(true).Where(t=>t!=null).ToArray()??Array.Empty<Transform>();
  palm=all.FirstOrDefault(t=>Clean(t.name).EndsWith("rpalm",StringComparison.Ordinal)||Clean(t.name).EndsWith("righthand",StringComparison.Ordinal));firstPerson=palm!=null;
  palm??=player.PlayerBones?.RightPalm;
  for(var t=palm?.parent;t!=null;t=t.parent){if(fore==null&&PoseRetarget.ElbowName(t.name))fore=t;if(Clean(t.name).EndsWith("rupperarm",StringComparison.Ordinal)||Clean(t.name).EndsWith("rightarm",StringComparison.Ordinal)){upper=t;break;}}
  if(palm==null||fore==null||upper==null){report="Missing first-person shoulder/elbow/palm";Reset();return false;}
  ik=Find("Torso_World_RightArm_Hand_IK_JNT","Torso_RightArm_Hand_IK_JNT","RightHand");sourcePalm=Find("RightHand");
  Sample(0);sourceOrigin=U(globalPositions[ik]);sourceRotationStart=U(globalRotations[sourcePalm]);handStart=palm.rotation;cameraStart=view.transform.rotation;startView=view.transform.InverseTransformPoint(palm.position);
  var handBones=palm.GetComponentsInChildren<Transform>(true).Where(t=>t!=null).ToArray();
  Transform? Digit(int f,int j)=>handBones.FirstOrDefault(t=>Clean(t.name).EndsWith("rdigit"+f+j,StringComparison.Ordinal));
  Vector3 targetNormal=palm.forward;
  if(Digit(2,1) is {} ti&&Digit(5,1) is {} tp){var normal=Vector3.Cross(ti.position-palm.position,tp.position-palm.position);if(normal.sqrMagnitude>.000001f)targetNormal=normal.normalized;}
  var sourceNormal=Vector3.Cross(U(globalPositions[Find("RightHandIndex1")])-U(globalPositions[sourcePalm]),U(globalPositions[Find("RightHandPinky1")])-U(globalPositions[sourcePalm])).normalized;
  string[] names={"Thumb","Index","Middle","Ring","Pinky"};
  for(int f=0;f<5;f++)for(int j=1;j<=3;j++){
   string name="RightHand"+names[f]+j;if(!animation.Names.TryGetValue(name,out int index))continue;
   var bone=Digit(f+1,j)??handBones.FirstOrDefault(t=>Clean(t.name).EndsWith(Clean(name),StringComparison.Ordinal));if(bone==null)continue;
   int child=Array.FindIndex(animation.Bones,b=>b.Parent==index);
   var sourceDirection=child>=0?U(globalPositions[child]-globalPositions[index]):U(globalPositions[index]-globalPositions[animation.Bones[index].Parent]);
   var targetDirection=bone.childCount>0?bone.GetChild(0).position-bone.position:bone.position-bone.parent.position;
   var sourceAxis=Quaternion.Inverse(U(globalRotations[index]))*Vector3.Cross(sourceDirection,sourceNormal).normalized;
   var targetAxis=bone.InverseTransformDirection(Vector3.Cross(targetDirection,targetNormal).normalized);
   fingers.Add(new(bone,index,bone.localRotation,U(rotations[index]),sourceAxis,targetAxis,f==0?55:85));
  }
  if(fingers.Count<10){report="Insufficient first-person fingers: "+fingers.Count;Reset();return false;}
  // Copy visible first-person clothing meshes; native renderers stay hidden during the gesture.
  // Meshes may be siblings of SkeletonHands; select them by bone binding.
  var skins=player.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r!=null&&r.bones.Any(b=>b!=null&&(b.Pointer==palm.Pointer||b.Pointer==fore.Pointer||b.Pointer==upper.Pointer))).ToArray();
  if(skins!=null)foreach(var source in skins){
   if(source==null||source.sharedMesh==null||!source.enabled||source.forceRenderingOff||!source.gameObject.activeInHierarchy||source.name.StartsWith("Blackwall",StringComparison.Ordinal))continue;
   var node=new GameObject("Blackwall cast arms"){hideFlags=HideFlags.HideAndDontSave};node.layer=source.gameObject.layer;node.transform.SetParent(source.transform,false);
   var copy=node.AddComponent<SkinnedMeshRenderer>();copy.sharedMesh=source.sharedMesh;copy.bones=source.bones;copy.rootBone=source.rootBone;copy.localBounds=source.localBounds;copy.sharedMaterials=source.sharedMaterials;copy.quality=source.quality;copy.shadowCastingMode=ShadowCastingMode.Off;copy.receiveShadows=source.receiveShadows;copies.Add(copy);Hide(source);
  }
  if(copies.Count==0){report="No active first-person arm mesh; cast cancelled";Reset();return false;}
  var weapon=player.PlayerBones?.Weapon_Root_Anim;
  if(weapon!=null)foreach(var renderer in weapon.GetComponentsInChildren<Renderer>(true))if(renderer!=null&&!copies.Any(x=>x.Pointer==renderer.Pointer))Hide(renderer);
  report=$"gesture_{clip+1:00}; firstPerson={firstPerson}; {upper.name} / {fore.name} / {palm.name}; fingers={fingers.Count}; hidden={hidden.Count}; copies={copies.Count}";return true;
 }
 static void Hide(Renderer renderer){if(hidden.Any(x=>x.Renderer.Pointer==renderer.Pointer))return;hidden.Add((renderer,renderer.forceRenderingOff));renderer.forceRenderingOff=true;}
 static int Find(params string[] names){foreach(string n in names)if(animation!.Names.TryGetValue(n,out int i))return i;throw new InvalidDataException("Station animation missing "+names[0]);}
 static void Sample(float time){animation!.Sample(clip,time,positions,rotations);for(int i=0;i<positions.Length;i++){int parent=animation.Bones[i].Parent;if(parent<0){globalPositions[i]=positions[i];globalRotations[i]=rotations[i];}else{globalPositions[i]=globalPositions[parent]+NVector.Transform(positions[i],globalRotations[parent]);globalRotations[i]=NQuaternion.Normalize(globalRotations[parent]*rotations[i]);}}}
 internal static object Diagnostics()=>new{ready=Ready,report,active=actor!=null,firstPerson,mappedFingers=fingers.Count,hiddenRenderers=hidden.Count,castMeshes=copies.Count,elbow=fore?.name};
 internal static void BeforeNative(Player __instance){if(actor?.Pointer==__instance.Pointer)Restore();}
 internal static void BeforeWeapon(Player.FirearmController __instance){if(PulseRuntime.Owns(__instance))Restore();}
 internal static void AfterNative(Player __instance){if(actor?.Pointer!=__instance.Pointer)return;try{Apply(actor,PulseRuntime.Age);}catch(Exception error){Reset();PulseRuntime.Log.LogWarning("Blackwall hand pose: "+error.Message);}}
 internal static void Apply(Player? player,float age){
  Restore();if(player==null||actor?.Pointer!=player.Pointer||age<0||age>=PulseRules.CastDuration||camera==null||upper==null||fore==null||palm==null)return;
  foreach(var item in hidden)if(item.Renderer!=null)item.Renderer.forceRenderingOff=true;
  float weight=PulseRules.Weight(age);Sample(Math.Min(age,animation!.Clips[clip].Duration));var view=camera.transform;
  var move=Vector3.ClampMagnitude(U(globalPositions[ik])-sourceOrigin,.55f);var anchor=Vector3.Lerp(startView,new Vector3(.14f,-.13f,.43f),weight);
  var target=view.TransformPoint(anchor+new Vector3(move.x*.42f,move.y*.42f,move.z*.32f));
  var start=upper.position;float a=Vector3.Distance(start,fore.position),b=Vector3.Distance(fore.position,palm.position);if(a<.08f||b<.08f)return;
  var delta=target-start;float distance=Math.Clamp(delta.magnitude,Math.Abs(a-b)+.015f,a+b-.015f);var direction=delta.normalized;target=start+direction*distance;
  var bend=Vector3.ProjectOnPlane(view.right*.65f-view.up,direction).normalized;
  float along=(a*a-b*b+distance*distance)/(2*distance);float height=MathF.Sqrt(Math.Max(0,a*a-along*along));var elbow=start+direction*along+bend*height;
  Rotate(upper,Quaternion.Slerp(upper.rotation,Quaternion.FromToRotation(fore.position-start,elbow-start)*upper.rotation,weight));
  Rotate(fore,Quaternion.Slerp(fore.rotation,Quaternion.FromToRotation(palm.position-fore.position,target-fore.position)*fore.rotation,weight));
  var hand=view.rotation*Quaternion.Inverse(cameraStart)*handStart*Quaternion.Inverse(sourceRotationStart)*U(globalRotations[sourcePalm]);Rotate(palm,Quaternion.Slerp(palm.rotation,hand,weight));
  foreach(var f in fingers){if(f.Bone==null)continue;float degrees=PoseRetarget.Flexion(N(f.SourceStart),N(U(rotations[f.Source])),N(f.SourceAxis),f.Limit);var value=f.Start*Quaternion.AngleAxis(degrees,f.TargetAxis);LocalRotate(f.Bone,Quaternion.Slerp(f.Bone.localRotation,value,weight));}
 }
 static void Rotate(Transform bone,Quaternion world){var p=bone.localPosition;var q=bone.localRotation;bone.rotation=world;changes.Add(new(bone,p,q,bone.localRotation));}
 static void LocalRotate(Transform bone,Quaternion value){var p=bone.localPosition;var q=bone.localRotation;bone.localRotation=value;changes.Add(new(bone,p,q,value));}
 internal static void Restore(){for(int i=changes.Count-1;i>=0;i--){var c=changes[i];if(c.Bone!=null&&Quaternion.Angle(c.Bone.localRotation,c.Applied)<.02f){c.Bone.localRotation=c.Rotation;c.Bone.localPosition=c.Position;}}changes.Clear();}
 internal static void Reset(){Restore();foreach(var copy in copies)if(copy!=null)UnityEngine.Object.Destroy(copy.gameObject);copies.Clear();foreach(var item in hidden)if(item.Renderer!=null)item.Renderer.forceRenderingOff=item.Before;hidden.Clear();fingers.Clear();actor=null;camera=null;upper=fore=palm=null;}
 internal static void Shutdown(){Reset();animation=null;}
 static Vector3 U(NVector v)=>new(v.X,v.Y,-v.Z);
 static Quaternion U(NQuaternion q)=>new(-q.X,-q.Y,q.Z,q.W);
 static NVector N(Vector3 v)=>new(v.x,v.y,v.z);
 static NQuaternion N(Quaternion q)=>new(q.x,q.y,q.z,q.w);
}
