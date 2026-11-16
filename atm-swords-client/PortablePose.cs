using Comfort.Common;
using EFT;
using UnityEngine;
namespace AllTheModium.Client;
public sealed partial class SwordModule {
 sealed record ToolPose(Transform Transform,Vector3 Position,Quaternion Rotation,Vector3 AppliedPosition,Quaternion AppliedRotation);
 readonly List<ToolPose> toolPoses=[];
 readonly List<Transform> toolMeshes=[];
 IntPtr toolHands,toolRoot;
 void ClearToolMeshes(){toolMeshes.Clear();toolHands=toolRoot=IntPtr.Zero;}
 void RestoreToolPose(){foreach(var pose in toolPoses){if(pose.Transform==null)continue;if(Vector3.Distance(pose.Transform.localPosition,pose.AppliedPosition)<.0001f)pose.Transform.localPosition=pose.Position;if(Quaternion.Angle(pose.Transform.localRotation,pose.AppliedRotation)<.01f)pose.Transform.localRotation=pose.Rotation;}toolPoses.Clear();}
 void ApplyToolPose(){
  RestoreToolPose();var player=Singleton<GameWorld>.Instance?.MainPlayer;
  var hands=player?.HandsController?.TryCast<Player.KnifeController>();string id=hands?.GetItem()?.StringTemplateId??"";
  if(context?.IsActive!=true||player?.IsYourPlayer!=true||hands==null||hands.Destroyed||id is not ("a7e000000000000000000101" or "a7e000000000000000000102")){ClearToolMeshes();return;}
  if(player.IsSprintEnabled||hands.CurrentOperation?.TryCast<Player.KnifeController.Idling>()==null)return;
  var root=hands.ControllerGameObject;if(root==null){ClearToolMeshes();return;}
  if(toolHands!=hands.Pointer||toolRoot!=root.Pointer){
   ClearToolMeshes();toolHands=hands.Pointer;toolRoot=root.Pointer;
   foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
    if(filter.name.StartsWith("weapon_apoc_wasteland_gladius_LOD",StringComparison.Ordinal))toolMeshes.Add(filter.transform);
  }
  var camera=EFT.CameraControl.CameraManager.Instance?.Camera;if(camera==null)return;
  // A tablet's +Z is its top edge and +Y is its width, unlike a sword blade.
  // Present its broad face to the camera and move it clear of the near plane.
  var orientation=Quaternion.AngleAxis(-12,camera.transform.forward)*Quaternion.LookRotation(camera.transform.up,camera.transform.right);
  foreach(var t in toolMeshes){
   if(t==null)continue;var position=t.localPosition;var rotation=t.localRotation;
   t.position+=camera.transform.forward*.16f+camera.transform.up*.02f;t.rotation=orientation;
   toolPoses.Add(new(t,position,rotation,t.localPosition,t.localRotation));
  }
 }
}
