using EFT;
using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;
using NQuaternion=System.Numerics.Quaternion;
namespace LocalBlackwallPulse;
internal static class ArmAnimation {
 sealed record BoneCopy(Transform Source,Transform Copy);
 sealed record Finger(Transform Bone,int Source,Quaternion Rest,Quaternion SourceRest,Quaternion Axes);
 sealed record BodyMask(SkinnedMeshRenderer Renderer,Mesh Original,Mesh Masked,int Removed,int OriginalIndices);
 static readonly List<BodyMask> bodyMasks=new();
 static readonly List<Mesh> castMeshes=new();
 sealed record Surface(SkinnedMeshRenderer Pose,MeshRenderer Visible,Mesh Baked);
 static readonly List<Surface> surfaces=new();
 static int poseSamples,renderSamples,maskRebounds;static string[] selectedSkins=Array.Empty<string>();static object? lastCast,lastPose,peakPose;
 static object V(Vector3 v)=>new{x=v.x,y=v.y,z=v.z};
 static object RenderTrace()=>new{poseSamples,renderSamples,maskRebounds,selectedSkins,castMeshCount=copies.Count,lastPose,peakPose,nativeMasks=bodyMasks.Select(m=>new{name=m.Renderer.name,masked=m.Renderer.sharedMesh?.Pointer==m.Masked.Pointer,removed=m.Removed}).ToArray()};
 static GlbAnimation? animation;static Player? actor;static Camera? camera;static GameObject? skeleton;
 static Transform? upper,fore,palm;static int clip,sourcePalm,sourceCamera,sourceTarget,sourceUp,sourceElbow;
 static NVector[] positions=Array.Empty<NVector>(),globalPositions=Array.Empty<NVector>();
 static NQuaternion[] rotations=Array.Empty<NQuaternion>(),globalRotations=Array.Empty<NQuaternion>();
 static readonly List<Finger> fingers=new();static readonly List<BoneCopy> boneCopies=new();
 static readonly Dictionary<IntPtr,Transform> cloned=new();
 static readonly List<(Renderer Renderer,bool Before,bool Enabled)> hidden=new();static readonly List<SkinnedMeshRenderer> copies=new();
 static Quaternion palmCorrection;static float armScale=1;static string report="not prepared";static bool firstPerson;
 internal static bool Ready=>animation!=null;
 internal static Renderer[] CastRenderers=>surfaces.Where(x=>x.Visible!=null).Select(x=>(Renderer)x.Visible).ToArray();
 internal static Transform? CastPalm=>palm;
 internal static object Check(Player? player,Camera? view){
  if(actor!=null)return new{ready=false,report="Cast in progress"};
  if(player==null||view==null)return new{ready=false,report="Player or camera unavailable"};
  try{
   bool ready=Prepare(player,view,0);var before=boneCopies.Select(b=>(Rotation:b.Source.localRotation,Scale:b.Source.localScale)).ToArray();var visibility=hidden.ToArray();
   bool leftHidden=true,originalsHidden=true,finite=true;int leftRoots=boneCopies.Count(b=>LeftArmRoot(b.Source.name));
   var poses=new List<object>();
   foreach(float age in new[]{.05f,.3f,.62f,1.2f,1.6f})if(ready){Apply(player,age);poses.Add(new{age,pose=lastPose,skins=copies.Select(r=>ProbeSkin(r,view)).ToArray()});leftHidden&=leftRoots>0&&boneCopies.Where(b=>LeftArmRoot(b.Source.name)).All(b=>b.Copy.localScale.sqrMagnitude<1e-10f);originalsHidden&=hidden.All(h=>h.Renderer==null||(!h.Renderer.enabled&&h.Renderer.forceRenderingOff));finite&=boneCopies.All(b=>float.IsFinite(b.Copy.position.x)&&float.IsFinite(b.Copy.position.y)&&float.IsFinite(b.Copy.position.z));}
   bool nativeUnchanged=boneCopies.Select((b,i)=>Quaternion.Angle(before[i].Rotation,b.Source.localRotation)<.001f&&(before[i].Scale-b.Source.localScale).sqrMagnitude<1e-10f).All(x=>x);var masks=bodyMasks.ToArray();bool bodyArmsMasked=masks.All(m=>m.Renderer==null||m.Renderer.sharedMesh?.Pointer==m.Masked.Pointer);var details=Diagnostics();var geometry=CheckGeometry();Reset();
   bool visibilityRestored=visibility.All(h=>h.Renderer==null||(h.Renderer.enabled==h.Enabled&&h.Renderer.forceRenderingOff==h.Before));
   bool bodyMeshesRestored=masks.All(m=>m.Renderer==null||m.Renderer.sharedMesh?.Pointer==m.Original.Pointer);
   bool originalGeometryRestored=masks.All(m=>AllIndices(m.Original).Length==m.OriginalIndices);
   var angles=new List<object>();var probe=new GameObject("Blackwall diagnostic camera");
   try{var virtualView=probe.AddComponent<Camera>();virtualView.enabled=false;virtualView.cullingMask=view.cullingMask;virtualView.fieldOfView=view.fieldOfView;virtualView.nearClipPlane=view.nearClipPlane;var euler=view.transform.eulerAngles;
    foreach(float pitch in new[]{-60f,0f,60f}){probe.transform.SetPositionAndRotation(view.transform.position,Quaternion.Euler(pitch,euler.y,0));if(Prepare(player,virtualView,0)){Apply(player,.62f);angles.Add(new{pitch,pose=lastPose,skins=copies.Select(r=>ProbeSkin(r,virtualView)).ToArray()});}Reset();}
   }finally{UnityEngine.Object.Destroy(probe);}
   return new{ready,nativeUnchanged,leftHidden,originalsHidden,visibilityRestored,bodyArmsMasked,bodyMeshesRestored,originalGeometryRestored,finite,details,geometry,poses,angles,camera=new{fov=view.fieldOfView,near=view.nearClipPlane,far=view.farClipPlane},rig=InspectAll(player)};
  }finally{Reset();}
 }
 internal static object? InspectRig(Player? player){
  if(player==null)return null;
  var root=player.PlayerBody?.SkeletonHands;var bones=root?.GetComponentsInChildren<Transform>(true).Where(t=>t!=null).ToArray()??Array.Empty<Transform>();
  var palms=bones.Where(t=>Clean(t.name).EndsWith("rpalm",StringComparison.Ordinal)).ToArray();
  var mesh=player.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r!=null&&!r.name.StartsWith("Blackwall",StringComparison.Ordinal)&&palms.Any(p=>r.bones.Any(b=>b!=null&&b.Pointer==p.Pointer))).Select(r=>new{name=r.name,active=r.gameObject.activeInHierarchy,enabled=r.enabled,hidden=r.forceRenderingOff,layer=r.gameObject.layer,palms=palms.Where(p=>r.bones.Any(b=>b!=null&&b.Pointer==p.Pointer)).Select(p=>p.Pointer.ToInt64()).ToArray()}).ToArray();
  return new{root=root?.name,palms=palms.Select(t=>new{name=t.name,id=t.Pointer.ToInt64()}).ToArray(),boundMeshes=mesh};
 }
 static object ProbeSkin(SkinnedMeshRenderer renderer,Camera view){
  var baked=new Mesh{name="Blackwall diagnostic baked arm"};
  try{
   renderer.BakeMesh(baked);var vertices=baked.vertices;var weights=renderer.sharedMesh.boneWeights;
   var hand=renderer.bones.Select(b=>b!=null&&(ArmMeshRules.RightHandBone(b.name)||Clean(b.name).Contains("rdigit",StringComparison.Ordinal))).ToArray();
   var points=new List<Vector3>();
   for(int i=0;i<Math.Min(vertices.Length,weights.Length);i++){var w=weights[i];if(ArmMeshRules.ArmWeight(w.boneIndex0,w.weight0,hand)||ArmMeshRules.ArmWeight(w.boneIndex1,w.weight1,hand)||ArmMeshRules.ArmWeight(w.boneIndex2,w.weight2,hand)||ArmMeshRules.ArmWeight(w.boneIndex3,w.weight3,hand))points.Add(view.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertices[i])));}
   return new{renderer=renderer.name,enabled=renderer.enabled,hidden=renderer.forceRenderingOff,active=renderer.gameObject.activeInHierarchy,boundsCenter=V(view.transform.InverseTransformPoint(renderer.bounds.center)),materials=renderer.sharedMaterials.Select(m=>new{name=m.name,shader=m.shader?.name,queue=m.renderQueue,properties=Enumerable.Range(0,m.shader!.GetPropertyCount()).Where(i=>m.shader!.GetPropertyType(i)==ShaderPropertyType.Float||m.shader!.GetPropertyType(i)==ShaderPropertyType.Range).Select(i=>new{name=m.shader!.GetPropertyName(i),value=m.GetFloat(m.shader!.GetPropertyName(i))}).ToArray()}).ToArray(),handVertices=points.Count,minimum=points.Count>0?V(new Vector3(points.Min(p=>p.x),points.Min(p=>p.y),points.Min(p=>p.z))):null,maximum=points.Count>0?V(new Vector3(points.Max(p=>p.x),points.Max(p=>p.y),points.Max(p=>p.z))):null};
  }finally{UnityEngine.Object.Destroy(baked);}
 }
 static object InspectAll(Player player){
  string Path(Transform t){var names=new List<string>();for(int i=0;t!=null&&i<12;i++,t=t.parent)names.Add(t.name);names.Reverse();return string.Join("/",names);}
  return new{
   renderers=UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>(true).Where(r=>r!=null&&(r.transform.IsChildOf(player.transform)||r.name.StartsWith("Blackwall",StringComparison.Ordinal)&&Vector3.Distance(r.transform.position,player.Position)<3)).Select(r=>new{name=r.name,id=r.Pointer.ToInt64(),path=Path(r.transform),enabled=r.enabled,hidden=r.forceRenderingOff,active=r.gameObject.activeInHierarchy,layer=r.gameObject.layer,mesh=r.sharedMesh?.name,vertices=r.sharedMesh?.vertexCount,readable=r.sharedMesh?.isReadable,indexCount=r.sharedMesh?.GetIndexCount(0),directIndices=r.sharedMesh?.GetTrianglesImpl(0,true)?.Length,armVertices=ArmVertices(r).Count(x=>x),materials=r.sharedMaterials.Select(m=>m?.name).ToArray(),palms=r.bones.Where(b=>b!=null&&(Clean(b.name).EndsWith("rpalm",StringComparison.Ordinal)||Clean(b.name).EndsWith("righthand",StringComparison.Ordinal))).Select(b=>new{id=b.Pointer.ToInt64(),path=Path(b)}).ToArray()}).ToArray(),
   cameras=UnityEngine.Object.FindObjectsOfType<Camera>(true).Where(c=>c.isActiveAndEnabled).Select(c=>new{name=c.name,depth=c.depth,mask=c.cullingMask}).ToArray()
  };
 }
 internal static void Load(string path){try{animation=new(path);positions=new NVector[animation.Bones.Length];rotations=new NQuaternion[positions.Length];globalPositions=new NVector[positions.Length];globalRotations=new NQuaternion[positions.Length];report="four gestures loaded";}catch(Exception error){report=error.Message;PulseRuntime.Log.LogWarning("Station animation: "+error.Message);}}
 static string Clean(string name)=>new(name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
 static bool LeftArmRoot(string name){var n=Clean(name);return n.EndsWith("lupperarm",StringComparison.Ordinal)||n.EndsWith("leftarm",StringComparison.Ordinal);}
 static void HideCopiedLeftArm(){foreach(var b in boneCopies)if(b.Copy!=null&&LeftArmRoot(b.Source.name))b.Copy.localScale=Vector3.zero;}
 static int Find(string name)=>animation!.Names.TryGetValue(name,out int i)?i:throw new InvalidDataException("Station animation missing "+name);
 static Quaternion Frame(Vector3 forward,Vector3 normal){if(forward.sqrMagnitude<1e-12f||normal.sqrMagnitude<1e-12f)throw new InvalidDataException("Degenerate palm/camera frame");forward.Normalize();normal.Normalize();if(Vector3.Cross(forward,normal).sqrMagnitude<1e-6f)throw new InvalidDataException("Parallel palm/camera frame");return Quaternion.LookRotation(forward,normal);}
 static Transform[] DigitBones(Transform hand,int digit){
  string prefix="rdigit"+digit;
  return hand.GetComponentsInChildren<Transform>(true).Where(t=>t!=null&&Clean(t.name).Contains(prefix,StringComparison.Ordinal)).Where(t=>int.TryParse(Clean(t.name).Split(prefix,StringSplitOptions.None)[^1],out _)).OrderBy(t=>int.Parse(Clean(t.name).Split(prefix,StringSplitOptions.None)[^1])).TakeLast(3).ToArray();
 }
 static Transform CloneBone(Transform original){
  if(actor==null||original.Pointer==actor.transform.Pointer)return skeleton!.transform;
  if(cloned.TryGetValue(original.Pointer,out var existing))return existing;
  var parent=original.parent;var node=new GameObject("Blackwall bone "+original.name){hideFlags=HideFlags.HideAndDontSave};var copy=node.transform;
  bool inside=original.IsChildOf(actor.transform);
  copy.SetParent(parent!=null&&inside?CloneBone(parent):skeleton!.transform,false);
  copy.localPosition=original.localPosition;copy.localRotation=original.localRotation;copy.localScale=original.localScale;
  if(parent==null||!inside){copy.position=original.position;copy.rotation=original.rotation;}
  cloned[original.Pointer]=copy;boneCopies.Add(new(original,copy));return copy;
 }
 internal static bool Prepare(Player player,Camera view,int variant){
  Reset();if(animation==null)return false;actor=player;camera=view;clip=variant;
  var all=player.PlayerBody?.SkeletonHands?.GetComponentsInChildren<Transform>(true).Where(t=>t!=null).ToArray()??Array.Empty<Transform>();
  var palms=all.Where(t=>Clean(t.name).EndsWith("rpalm",StringComparison.Ordinal)||Clean(t.name).EndsWith("righthand",StringComparison.Ordinal)).ToList();
  if(player.PlayerBones?.RightPalm is {} nativePalm&&!palms.Any(p=>p.Pointer==nativePalm.Pointer))palms.Add(nativePalm);
  var renderers=player.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r!=null).ToArray();
  var matches=(from candidate in palms from skin in renderers
   let score=ArmMeshRules.Score(skin.name,skin.sharedMesh!=null,skin.gameObject.activeInHierarchy,skin.enabled,skin.forceRenderingOff,(view.cullingMask&(1<<skin.gameObject.layer))!=0,skin.bones.Any(b=>b!=null&&b.Pointer==candidate.Pointer))
   where score>=0 orderby score descending select (Palm:candidate,Mesh:skin)).ToArray();
  var native=matches.FirstOrDefault().Palm;Transform? nativeFore=null,nativeUpper=null;
  firstPerson=native!=null&&all.Any(t=>t.Pointer==native.Pointer);
  for(var t=native?.parent;t!=null;t=t.parent){if(nativeFore==null&&PoseRetarget.ElbowName(t.name))nativeFore=t;if(Clean(t.name).EndsWith("rupperarm",StringComparison.Ordinal)||Clean(t.name).EndsWith("rightarm",StringComparison.Ordinal)){nativeUpper=t;break;}}
  if(native==null||nativeFore==null||nativeUpper==null){report="Missing shoulder/elbow/palm bound to arm mesh";Reset();return false;}
  // Shared RPalm bindings do not make third-person clothing part of the
  // first-person skin. Copy only the primary mesh, not another complete arm.
  var skins=new[]{matches[0].Mesh};
  selectedSkins=skins.Select(s=>s.name).ToArray();poseSamples=renderSamples=maskRebounds=0;peakPose=null;
  // Bind poses describe an open hand, independently of the weapon currently held.
  var bind=new Dictionary<IntPtr,Matrix4x4>();
  foreach(var skin in skins){var bs=skin.bones;var rest=skin.sharedMesh.bindposes;for(int i=0;i<Math.Min(bs.Length,rest.Length);i++)if(bs[i]!=null)bind.TryAdd(bs[i].Pointer,skin.transform.localToWorldMatrix*rest[i].inverse);}
  Matrix4x4 Rest(Transform bone)=>bind.TryGetValue(bone.Pointer,out var matrix)?matrix:throw new InvalidDataException("Missing mesh bind pose: "+bone.name);
  Vector3 Position(Transform bone)=>Rest(bone).MultiplyPoint3x4(Vector3.zero);
  var digits=Enumerable.Range(1,5).Select(d=>DigitBones(native,d)).ToArray();
  if(digits[0].Length<2||digits.Skip(1).Any(d=>d.Length<3)){report="Incomplete finger chains: "+string.Join(",",digits.Select(d=>d.Length));Reset();return false;}
  sourcePalm=Find("RightHand");sourceElbow=Find("RightForeArm");sourceCamera=Find("Torso_fppCamera_Aim_JNT");sourceTarget=Find("Torso_fppCamera_Target_JNT");sourceUp=Find("Torso_fppCamera_Up_GRP");
  animation.BindPose(positions,rotations);Globals();
  var sourceFrame=SourcePalmFrame();var targetFrame=Frame(Position(digits[2][0])-Position(native),Vector3.Cross(Position(digits[1][0])-Position(native),Position(digits[4][0])-Position(native)));
  palmCorrection=Quaternion.Inverse(targetFrame)*Rest(native).rotation;
  float sourceLength=NVector.Distance(globalPositions[Find("RightArm")],globalPositions[sourceElbow])+NVector.Distance(globalPositions[sourceElbow],globalPositions[sourcePalm]);
  armScale=Math.Clamp((Vector3.Distance(nativeUpper.position,nativeFore.position)+Vector3.Distance(nativeFore.position,native.position))/sourceLength,.75f,1.25f);
  skeleton=new GameObject("Blackwall cast skeleton"){hideFlags=HideFlags.HideAndDontSave};skeleton.transform.SetParent(player.transform,false);
  upper=CloneBone(nativeUpper);fore=CloneBone(nativeFore);palm=CloneBone(native);
  string[] names={"Thumb","Index","Middle","Ring","Pinky"};
  for(int f=0;f<5;f++)for(int j=0;j<digits[f].Length;j++){
   var original=digits[f][j];string sourceName=f==0?(j==0?"RightInHandThumb":"RightHandThumb"+j):"RightHand"+names[f]+(j+1);int source=Find(sourceName);var targetBind=Rest(original).rotation;var parentBind=Rest(original.parent).rotation;
   var axes=Quaternion.Inverse(targetBind)*targetFrame*Quaternion.Inverse(sourceFrame)*U(globalRotations[source]);
   fingers.Add(new(CloneBone(original),source,Quaternion.Inverse(parentBind)*targetBind,U(rotations[source]),axes));
  }
  foreach(var source in skins){
   var isolated=FilterArm(source,true,false,out _);castMeshes.Add(isolated);
   var node=new GameObject("Blackwall cast arms"){hideFlags=HideFlags.HideAndDontSave};node.layer=source.gameObject.layer;node.transform.SetParent(source.transform,false);
   var copy=node.AddComponent<SkinnedMeshRenderer>();copy.sharedMesh=isolated;copy.bones=new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Transform>(source.bones.Select(b=>b==null?null!:CloneBone(b)).ToArray());copy.rootBone=source.rootBone!=null?CloneBone(source.rootBone):skeleton.transform;copy.localBounds=source.localBounds;copy.sharedMaterials=source.sharedMaterials;copy.quality=source.quality;copy.updateWhenOffscreen=true;copy.shadowCastingMode=ShadowCastingMode.Off;copy.receiveShadows=source.receiveShadows;copies.Add(copy);
   // CPU skinning supplies the actual visible surface. The pose renderer is
   // never drawn, so GPU skin/bounds caches cannot retain the native pose.
   copy.enabled=false;
   var surfaceNode=new GameObject("Blackwall baked right arm"){hideFlags=HideFlags.HideAndDontSave};surfaceNode.layer=source.gameObject.layer;surfaceNode.transform.SetParent(node.transform,false);
   var baked=new Mesh{name="Blackwall posed right arm",hideFlags=HideFlags.HideAndDontSave};baked.MarkDynamic();castMeshes.Add(baked);
   surfaceNode.AddComponent<MeshFilter>().sharedMesh=baked;var visible=surfaceNode.AddComponent<MeshRenderer>();visible.sharedMaterials=source.sharedMaterials;visible.shadowCastingMode=ShadowCastingMode.Off;visible.receiveShadows=source.receiveShadows;surfaces.Add(new(copy,visible,baked));
  }
  if(copies.Count==0){report="No usable arm mesh bound to selected palm";Reset();return false;}
  HideCopiedLeftArm();
  foreach(var match in matches)if(match.Mesh!=null&&match.Mesh.name.Contains("hand",StringComparison.OrdinalIgnoreCase))Hide(match.Mesh);
  var controller=player.HandsController?.TryCast<Player.ItemHandsController>()?.ControllerGameObject;
  if(controller!=null)foreach(var renderer in controller.GetComponentsInChildren<Renderer>(true))if(renderer!=null&&!renderer.name.StartsWith("Blackwall",StringComparison.Ordinal)&&!copies.Any(x=>x.Pointer==renderer.Pointer))Hide(renderer);
  var weapon=player.PlayerBones?.Weapon_Root_Anim;
  if(weapon!=null)foreach(var renderer in weapon.GetComponentsInChildren<Renderer>(true))if(renderer!=null&&!copies.Any(x=>x.Pointer==renderer.Pointer))Hide(renderer);
  // Split every original arm mesh too: native visual/LOD updates can restore
  // renderer flags between LateUpdate and drawing. Never copy a whole torso.
  foreach(var renderer in renderers)if(renderer!=null&&!renderer.name.StartsWith("Blackwall",StringComparison.Ordinal))MaskBodyArm(renderer);
  report=$"gesture_{clip+1:00}; independent skeleton; selected={matches[0].Mesh.name}; fingers={fingers.Count}; copies={copies.Count}";return true;
 }
 static bool[] ArmVertices(SkinnedMeshRenderer renderer){
  var mesh=renderer.sharedMesh??throw new InvalidDataException("Missing arm mesh");
  var arm=renderer.bones.Select(b=>b!=null&&ArmMeshRules.RightArmBone(b.name)).ToArray();var weights=mesh.boneWeights;
  if(weights.Length!=mesh.vertexCount)throw new InvalidDataException("Missing readable skin weights");
  return weights.Select(w=>ArmMeshRules.ArmWeight(w.boneIndex0,w.weight0,arm)||ArmMeshRules.ArmWeight(w.boneIndex1,w.weight1,arm)||ArmMeshRules.ArmWeight(w.boneIndex2,w.weight2,arm)||ArmMeshRules.ArmWeight(w.boneIndex3,w.weight3,arm)).ToArray();
 }
 static Mesh FilterArm(SkinnedMeshRenderer renderer,bool keepArm,bool empty,out int removed){
  var mesh=renderer.sharedMesh;var vertices=ArmVertices(renderer);Mesh? masked=null;removed=0;
  try{
   masked=UnityEngine.Object.Instantiate(mesh);masked.name=keepArm?"Blackwall right arm only":"Blackwall native arm mask";masked.hideFlags=HideFlags.HideAndDontSave;
   for(int sub=0;sub<mesh.subMeshCount;sub++){
    // EFT retains index data but disables the public read/write wrappers.
    // Work on our private clone through the native mesh methods and verify
    // the actual result; Unity's public wrappers only log failures.
    var triangles=ReadIndices(mesh,sub);var keep=empty?Array.Empty<int>():ArmMeshRules.Triangles(triangles,vertices,keepArm);removed+=(triangles.Length-keep.Length)/3;
    var data=new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>(keep);
    masked.SetIndicesImpl(sub,MeshTopology.Triangles,IndexFormat.UInt32,data.Cast<Il2CppSystem.Array>(),0,keep.Length,false,0);
    if(!ReadIndices(masked,sub).SequenceEqual(keep))throw new InvalidDataException("Native arm mesh write did not apply: "+renderer.name);
   }
   return masked;
  }catch{if(masked!=null)UnityEngine.Object.Destroy(masked);throw;}
 }
 static int[] ReadIndices(Mesh mesh,int sub){var indices=mesh.GetTrianglesImpl(sub,true).ToArray();if(indices.Length!=mesh.GetIndexCount(sub))throw new InvalidDataException("Incomplete mesh index data: "+mesh.name);return indices;}
 static int[] AllIndices(Mesh mesh)=>Enumerable.Range(0,mesh.subMeshCount).SelectMany(sub=>ReadIndices(mesh,sub)).ToArray();
 static void MaskBodyArm(SkinnedMeshRenderer renderer){
  // Shoulder weights on armour/backpacks do not make them hand geometry.
  var mesh=renderer.sharedMesh;if(mesh==null||!renderer.bones.Any(b=>b!=null&&ArmMeshRules.RightHandBone(b.name)))return;
  try{int before=AllIndices(mesh).Length;var masked=FilterArm(renderer,false,renderer.name.Contains("hand",StringComparison.OrdinalIgnoreCase),out int removed);bodyMasks.Add(new(renderer,mesh,masked,removed,before));renderer.sharedMesh=masked;}
  catch(Exception error){Hide(renderer);PulseRuntime.Log.LogWarning("Blackwall body arm mask fallback: "+renderer.name+"; "+error.Message);}
 }
 static object CheckGeometry(){
  bool Only(SkinnedMeshRenderer r,bool arm){var vertices=ArmVertices(r);var triangles=AllIndices(r.sharedMesh);return triangles.Length>0&&ArmMeshRules.Triangles(triangles,vertices,arm).Length==triangles.Length;}
  return new{singleCastSkin=copies.Count==1,bakedSurface=surfaces.Count==1&&surfaces.All(s=>!s.Pose.enabled&&s.Visible.enabled&&s.Baked.vertexCount==s.Pose.sharedMesh.vertexCount&&AllIndices(s.Baked).SequenceEqual(AllIndices(s.Pose.sharedMesh))),castRightArmOnly=copies.Count>0&&copies.All(r=>Only(r,true)),nativeRightArmRemoved=bodyMasks.Count>0&&bodyMasks.All(m=>AllIndices(m.Renderer.sharedMesh).Length==0||Only(m.Renderer,false)),primaryHandsEmpty=bodyMasks.Any(m=>m.Renderer.name.Contains("hand",StringComparison.OrdinalIgnoreCase)&&AllIndices(m.Renderer.sharedMesh).Length==0)};
 }
 static void Hide(Renderer renderer){if(hidden.Any(x=>x.Renderer.Pointer==renderer.Pointer))return;hidden.Add((renderer,renderer.forceRenderingOff,renderer.enabled));renderer.forceRenderingOff=true;renderer.enabled=false;}
 static void Globals(){for(int i=0;i<positions.Length;i++){int parent=animation!.Bones[i].Parent;if(parent<0){globalPositions[i]=positions[i];globalRotations[i]=rotations[i];}else{globalPositions[i]=globalPositions[parent]+NVector.Transform(positions[i],globalRotations[parent]);globalRotations[i]=NQuaternion.Normalize(globalRotations[parent]*rotations[i]);}}}
 static Quaternion SourcePalmFrame()=>Frame(U(globalPositions[Find("RightHandMiddle1")]-globalPositions[sourcePalm]),Vector3.Cross(U(globalPositions[Find("RightHandIndex1")]-globalPositions[sourcePalm]),U(globalPositions[Find("RightHandPinky1")]-globalPositions[sourcePalm])));
 internal static object Diagnostics()=>new{ready=Ready,report,active=actor!=null,firstPerson,mappedFingers=fingers.Count,hiddenRenderers=hidden.Count,bodyArmMasks=bodyMasks.Select(m=>new{name=m.Renderer.name,removedTriangles=m.Removed}).ToArray(),castMeshes=copies.Count,independentBones=boneCopies.Count,hiddenLeftArmRoots=boneCopies.Count(b=>LeftArmRoot(b.Source.name)),elbow=fore?.name,renderTrace=actor!=null?RenderTrace():lastCast};
 internal static void Apply(Player? player,float age,bool beforeRender=false){
  if(player==null||actor?.Pointer!=player.Pointer||age<0||age>=PulseRules.CastDuration||camera==null||upper==null||fore==null||palm==null)return;
  poseSamples++;if(beforeRender)renderSamples++;
  maskRebounds+=bodyMasks.Count(m=>m.Renderer!=null&&m.Renderer.sharedMesh?.Pointer!=m.Masked.Pointer);
  // Only copied bones are posed. Weapon animation can continue on the native rig.
  foreach(var entry in boneCopies)if(entry.Source!=null&&entry.Copy!=null){entry.Copy.localPosition=entry.Source.localPosition;entry.Copy.localRotation=entry.Source.localRotation;entry.Copy.localScale=entry.Source.localScale;}
  HideCopiedLeftArm();
  foreach(var item in hidden)if(item.Renderer!=null){item.Renderer.forceRenderingOff=true;item.Renderer.enabled=false;}
  foreach(var mask in bodyMasks)if(mask.Renderer!=null)mask.Renderer.sharedMesh=mask.Masked;
  animation!.Sample(clip,Math.Min(age,animation.Clips[clip].Duration),positions,rotations);Globals();
  float weight=PulseRules.Weight(age);var view=camera.transform;
  var sourceView=Frame(U(globalPositions[sourceTarget]-globalPositions[sourceCamera]),U(globalPositions[sourceUp]-globalPositions[sourceTarget]));
  var toView=Quaternion.Inverse(sourceView);var sourceOffset=toView*U(globalPositions[sourcePalm]-globalPositions[sourceCamera])*armScale;
  var desired=view.TransformPoint(sourceOffset);var target=Vector3.Lerp(palm.position,desired,weight);
  var start=upper.position;float a=Vector3.Distance(start,fore.position),b=Vector3.Distance(fore.position,palm.position);if(a<.08f||b<.08f)return;
  var delta=target-start;float distance=Math.Clamp(delta.magnitude,Math.Abs(a-b)+.015f,a+b-.015f);var direction=delta.normalized;target=start+direction*distance;
  var sourceBend=view.TransformPoint(toView*U(globalPositions[sourceElbow]-globalPositions[sourceCamera])*armScale)-start;
  var bend=Vector3.ProjectOnPlane(sourceBend,direction).normalized;if(bend.sqrMagnitude<.001f)bend=Vector3.ProjectOnPlane(view.right-view.up,direction).normalized;
  float along=(a*a-b*b+distance*distance)/(2*distance);float height=MathF.Sqrt(Math.Max(0,a*a-along*along));var elbow=start+direction*along+bend*height;
  upper.rotation=Quaternion.FromToRotation(fore.position-start,elbow-start)*upper.rotation;
  fore.rotation=Quaternion.FromToRotation(palm.position-fore.position,target-fore.position)*fore.rotation;
  var hand=view.rotation*toView*SourcePalmFrame()*palmCorrection;palm.rotation=Quaternion.Slerp(palm.rotation,hand,weight);
  foreach(var f in fingers){var motion=PoseRetarget.MapLocal(N(f.Rest),N(f.SourceRest),N(U(rotations[f.Source])),N(f.Axes));f.Bone.localRotation=Quaternion.Slerp(f.Bone.localRotation,new Quaternion(motion.X,motion.Y,motion.Z,motion.W),weight);}
  foreach(var surface in surfaces){surface.Pose.BakeMesh(surface.Baked);surface.Baked.RecalculateBounds();}
  lastPose=new{age,weight,sourceOffset=V(sourceOffset),shoulder=V(view.InverseTransformPoint(upper.position)),palm=V(view.InverseTransformPoint(palm.position)),desired=V(sourceOffset),targetError=Vector3.Distance(palm.position,desired),upperLength=a,foreLength=b,clamped=delta.magnitude>distance+.001f};
  if(beforeRender&&age>=.6f&&peakPose==null)peakPose=new{pose=lastPose,pitch=view.eulerAngles.x,skins=copies.Select(r=>ProbeSkin(r,camera)).ToArray()};
 }
 internal static void Reset(){
  if(actor!=null)lastCast=RenderTrace();
  foreach(var mask in bodyMasks)if(mask.Masked!=null){if(mask.Renderer!=null&&mask.Renderer.sharedMesh?.Pointer==mask.Masked.Pointer)mask.Renderer.sharedMesh=mask.Original;UnityEngine.Object.Destroy(mask.Masked);}bodyMasks.Clear();
  foreach(var copy in copies)if(copy!=null){copy.gameObject.SetActive(false);UnityEngine.Object.Destroy(copy.gameObject);}copies.Clear();surfaces.Clear();
  foreach(var mesh in castMeshes)if(mesh!=null)UnityEngine.Object.Destroy(mesh);castMeshes.Clear();
  if(skeleton!=null){skeleton.SetActive(false);UnityEngine.Object.Destroy(skeleton);}skeleton=null;boneCopies.Clear();cloned.Clear();
  foreach(var item in hidden)if(item.Renderer!=null){item.Renderer.forceRenderingOff=item.Before;item.Renderer.enabled=item.Enabled;}hidden.Clear();fingers.Clear();actor=null;camera=null;upper=fore=palm=null;
 }
 internal static void Shutdown(){Reset();animation=null;}
 static Vector3 U(NVector v)=>new(v.X,v.Y,-v.Z);
 static Quaternion U(NQuaternion q)=>new(-q.X,-q.Y,q.Z,q.W);
 static NQuaternion N(Quaternion q)=>new(q.x,q.y,q.z,q.w);
}
