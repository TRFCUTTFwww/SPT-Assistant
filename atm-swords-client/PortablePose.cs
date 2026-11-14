using Comfort.Common;
using EFT;
using UnityEngine;
namespace AllTheModium.Client;
public sealed partial class SwordModule {
 sealed record ToolPose(Transform Transform,Vector3 Position,Quaternion Rotation,Vector3 AppliedPosition,Quaternion AppliedRotation);
 readonly List<ToolPose> toolPoses=[];
 void RestoreToolPose(){foreach(var pose in toolPoses){if(pose.Transform==null)continue;if(Vector3.Distance(pose.Transform.localPosition,pose.AppliedPosition)<.0001f)pose.Transform.localPosition=pose.Position;if(Quaternion.Angle(pose.Transform.localRotation,pose.AppliedRotation)<.01f)pose.Transform.localRotation=pose.Rotation;}toolPoses.Clear();}
 void ApplyToolPose(){
  RestoreToolPose();var player=Singleton<GameWorld>.Instance?.MainPlayer;
  var hands=player?.HandsController?.TryCast<Player.KnifeController>();string id=hands?.GetItem()?.StringTemplateId??"";
  if(player?.IsYourPlayer!=true||hands==null||id is not ("a7e000000000000000000101" or "a7e000000000000000000102"))return;
  var camera=EFT.CameraControl.CameraManager.Instance?.Camera;if(camera==null)return;
  // A tablet's +Z is its top edge and +Y is its width, unlike a sword blade.
  // Present its broad face to the camera and move it clear of the near plane.
  var orientation=Quaternion.AngleAxis(-12,camera.transform.forward)*Quaternion.LookRotation(camera.transform.up,camera.transform.right);
  foreach(var filter in hands.ControllerGameObject.GetComponentsInChildren<MeshFilter>(true)){
   if(!filter.name.StartsWith("weapon_apoc_wasteland_gladius_LOD",StringComparison.Ordinal))continue;
   var t=filter.transform;var position=t.localPosition;var rotation=t.localRotation;
   t.position+=camera.transform.forward*.16f+camera.transform.up*.02f;t.rotation=orientation;
   toolPoses.Add(new(t,position,rotation,t.localPosition,t.localRotation));
  }
 }
}
