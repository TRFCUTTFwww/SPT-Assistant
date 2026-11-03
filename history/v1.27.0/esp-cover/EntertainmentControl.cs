using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.CameraControl;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class EntertainmentControl {
 static ConfigEntry<bool> thirdPerson=null!,firstOnAim=null!,jitter=null!;
 static ConfigEntry<float> distance=null!,shoulder=null!,frequency=null!,headYaw=null!,headPitch=null!,headRoll=null!,bodyAngle=null!;
 static ConfigEntry<KeyCode> viewKey=null!;
 static Player? owner;static PlayerCameraController? controller;static IntPtr world;
 static EPointOfView originalView;static bool viewCaptured;
 static Camera? movedCamera;static Vector3 originalCameraPosition;static Quaternion originalCameraRotation;
 readonly record struct BonePose(Transform Bone,Quaternion Original,Quaternion Applied);
 static readonly List<BonePose> poses=new(2);static float nextError;
 internal static void Initialize(Plugin plugin){
  const string section="43. 娱乐";
  thirdPerson=FeatureConfig.Bind(section,"Third person","第三人称",false,"切换为身后跟随视角，显示完整人物；关闭后恢复原视角。");
  viewKey=FeatureConfig.Bind(section,"Third person key","第三人称切换键",KeyCode.None,"默认不占用按键，可自行绑定。");
  firstOnAim=FeatureConfig.Bind(section,"First person when aiming","瞄准时切回第一人称",true,"松开瞄准后回到第三人称。");
  distance=FeatureConfig.Bind(section,"Camera distance","第三人称距离（米）",2.5f,"靠近墙壁时自动收近。",new AcceptableValueRange<float>(.5f,6));
  shoulder=FeatureConfig.Bind(section,"Camera shoulder","第三人称肩位偏移（米）",.4f,"正数右肩，负数左肩。",new AcceptableValueRange<float>(-1,1));
  jitter=FeatureConfig.Bind(section,"Model jitter","高频摇头晃动",false,"人物模型高频随机摇头、扭动上身；第三人称下可直接看到。真实移动方向和瞄准角度保持玩家操作。");
  frequency=FeatureConfig.Bind(section,"Jitter frequency","晃动频率（次／秒）",24f,"按时间采样，实际可见频率受游戏帧率限制。",new AcceptableValueRange<float>(1,60));
  headYaw=FeatureConfig.Bind(section,"Head yaw","左右摇头幅度（度）",85f,"",new AcceptableValueRange<float>(0,160));
  headPitch=FeatureConfig.Bind(section,"Head pitch","上下摇头幅度（度）",45f,"",new AcceptableValueRange<float>(0,80));
  headRoll=FeatureConfig.Bind(section,"Head roll","歪头幅度（度）",35f,"",new AcceptableValueRange<float>(0,80));
  bodyAngle=FeatureConfig.Bind(section,"Body jitter","上身晃动幅度（度）",18f,"",new AcceptableValueRange<float>(0,40));
  plugin.Patches.Patch(AccessTools.Method(typeof(PlayerCameraController),"LateUpdate"),prefix:new HarmonyMethod(typeof(EntertainmentControl),nameof(BeforeCamera)),postfix:new HarmonyMethod(typeof(EntertainmentControl),nameof(AfterCamera)));
  plugin.Patches.Patch(AccessTools.Method(typeof(Player),"ComplexLateUpdate"),prefix:new HarmonyMethod(typeof(EntertainmentControl),nameof(BeforeVisual)),postfix:new HarmonyMethod(typeof(EntertainmentControl),nameof(AfterVisual)));
  plugin.Patches.Patch(AccessTools.Method(typeof(Player),"VisualPass"),prefix:new HarmonyMethod(typeof(EntertainmentControl),nameof(BeforeVisual)),postfix:new HarmonyMethod(typeof(EntertainmentControl),nameof(AfterVisual)));
  plugin.Patches.Patch(AccessTools.Method(typeof(Player),"ComplexUpdate"),prefix:new HarmonyMethod(typeof(EntertainmentControl),nameof(BeforeVisual)));
  plugin.Patches.Patch(AccessTools.Method(typeof(Player),"ManualUpdate"),prefix:new HarmonyMethod(typeof(EntertainmentControl),nameof(BeforeVisual)));
  plugin.Log.LogInfo("Entertainment ready: local third-person camera, native POV/render switching and reversible model-only jitter");
 }
 static bool Local(Player? player)=>owner!=null&&player?.Pointer==owner.Pointer&&player.IsYourPlayer&&player.HealthController?.IsAlive==true&&Singleton<GameWorld>.Instance?.Pointer==world;
 internal static void Update(){
  RestorePose();
  try{
   var current=Singleton<GameWorld>.Instance;var player=current?.MainPlayer;
   if(!RaidClock.Active||player?.IsYourPlayer!=true||player.HealthController?.IsAlive!=true){Reset();return;}
   if(owner!=null&&(owner.Pointer!=player.Pointer||world!=current!.Pointer))Reset();
   owner=player;world=current!.Pointer;
   if(!InterfaceMenu.InputBlocked&&viewKey.Value!=KeyCode.None&&Input.GetKeyDown(viewKey.Value)){thirdPerson.Value=!thirdPerson.Value;FeatureToasts.Show("第三人称",thirdPerson.Value);}
   UpdateView();
  }catch(Exception error){Warn(error);}
 }
 static void UpdateView(){
  if(owner==null)return;
  if(!thirdPerson.Value){RestoreView();return;}
  if(controller==null||controller.Player?.Pointer!=owner.Pointer||controller.IsSpectatorCameraActive||controller.IsInSpectatorCamera)return;
  if(!viewCaptured){originalView=owner.PointOfView;viewCaptured=true;}
  var desired=firstOnAim.Value&&owner.ProceduralWeaponAnimation?.IsAiming==true?EPointOfView.FirstPerson:EPointOfView.ThirdPerson;
  SetView(desired);
 }
 static void SetView(EPointOfView view){
  if(owner==null||controller==null)return;
  if(view==EPointOfView.FirstPerson)RestoreCamera();
  if(owner.PointOfView!=view){owner.PointOfView=view;owner.PlayerBody?.UpdatePlayerRenders(view,owner.Side);controller.UpdatePointOfView();}
  if(view==EPointOfView.ThirdPerson){if(controller._currentCameraState?.TryCast<ThirdPersonCameraState>()==null)controller.InitiateOperation<LockCameraState>();}
  else if(controller._currentCameraState?.TryCast<FirstPersonCameraState>()==null)controller.InitiateOperation<FirstPersonCameraState>();
 }
 static void BeforeCamera(PlayerCameraController __instance){if(Local(__instance.Player))RestorePose();}
 static void AfterCamera(PlayerCameraController __instance){
  if(!Local(__instance.Player))return;
  try{
   controller=__instance;UpdateView();
   if(viewCaptured&&thirdPerson.Value&&owner!.PointOfView==EPointOfView.ThirdPerson&&!__instance.IsSpectatorCameraActive&&!__instance.IsInSpectatorCamera)PositionCamera(__instance.Camera);
   ApplyPose();
  }catch(Exception error){Warn(error);}
 }
 static void PositionCamera(Camera camera){
  if(camera==null||owner==null)return;
  if(movedCamera?.Pointer!=camera.Pointer){RestoreCamera();movedCamera=camera;originalCameraPosition=camera.transform.localPosition;originalCameraRotation=camera.transform.localRotation;}
  var angles=owner.Rotation;var rotation=Quaternion.Euler(angles.y,angles.x,0);
  var head=owner.PlayerBones?.Head?.Original;
  var pivot=(head!=null?head.position:owner.Position+Vector3.up*(owner.IsInPronePose ? .45f :owner.Pose==EPlayerPose.Duck?1.1f:1.6f))+Vector3.up*.15f;
  var delta=rotation*new Vector3(shoulder.Value,0,-distance.Value);float length=delta.magnitude;
  var position=pivot+delta;int mask=LayerMask.GetMask("HighPolyCollider","Terrain","LowPolyCollider");
  if(mask!=0&&length>.001f&&Physics.SphereCast(pivot,.18f,delta/length,out var hit,length,mask,QueryTriggerInteraction.Ignore))position=pivot+delta/length*Math.Max(.05f,hit.distance-.08f);
  camera.transform.SetPositionAndRotation(position,rotation);
 }
 static void BeforeVisual(Player __instance){if(Local(__instance))RestorePose();}
 static void AfterVisual(Player __instance){if(Local(__instance))try{ApplyPose();}catch(Exception error){Warn(error);}}
 static void ApplyPose(){
  RestorePose();if(!jitter.Value||owner==null||!Local(owner))return;
  var bones=owner.PlayerBones;if(bones==null)return;
  var sample=EntertainmentRules.Jitter(Time.realtimeSinceStartupAsDouble,frequency.Value,headYaw.Value,headPitch.Value,headRoll.Value);
  ApplyBone(bones.Neck,Quaternion.Euler(sample.X,sample.Y,sample.Z));
  var torso=bones.Spine3?.Original;
  if(torso!=null&&torso.Pointer!=bones.Neck?.Pointer){var body=EntertainmentRules.Jitter(Time.realtimeSinceStartupAsDouble+.17,frequency.Value,bodyAngle.Value,bodyAngle.Value*.5f,bodyAngle.Value);ApplyBone(torso,Quaternion.Euler(body.X,body.Y,body.Z));}
 }
 static void ApplyBone(Transform? bone,Quaternion delta){if(bone==null)return;var original=bone.localRotation;var applied=original*delta;bone.localRotation=applied;poses.Add(new(bone,original,applied));}
 static void RestorePose(){for(int i=poses.Count-1;i>=0;i--)try{var pose=poses[i];if(pose.Bone!=null&&Quaternion.Angle(pose.Bone.localRotation,pose.Applied)<.01f)pose.Bone.localRotation=pose.Original;}catch{}poses.Clear();}
 static void RestoreCamera(){try{if(movedCamera!=null){movedCamera.transform.localPosition=originalCameraPosition;movedCamera.transform.localRotation=originalCameraRotation;}}catch{}movedCamera=null;}
 static void RestoreView(){if(!viewCaptured){RestoreCamera();return;}viewCaptured=false;RestoreCamera();if(owner!=null&&Local(owner)&&controller!=null&&controller.Player?.Pointer==owner.Pointer&&!controller.IsSpectatorCameraActive&&!controller.IsInSpectatorCamera)SetView(originalView);}
 internal static void Reset(){RestorePose();try{RestoreView();}catch(Exception error){Warn(error);}owner=null;controller=null;world=IntPtr.Zero;viewCaptured=false;}
 static void Warn(Exception error){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+15;Plugin.Self.Log.LogWarning("Entertainment: "+error.Message);}
}
