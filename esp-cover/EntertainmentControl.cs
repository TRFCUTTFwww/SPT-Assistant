using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.CameraControl;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class EntertainmentControl {
 static ConfigEntry<bool> thirdPerson=null!,firstOnAim=null!,jitter=null!,lookDown=null!;
 static ConfigEntry<float> distance=null!,shoulder=null!,frequency=null!,headYaw=null!,headPitch=null!,headRoll=null!,bodyAngle=null!,bowAngle=null!;
 static ConfigEntry<KeyCode> viewKey=null!,spinKey=null!;static ConfigEntry<bool> spin=null!,reverseSpin=null!;static ConfigEntry<float> spinSpeed=null!;static double spinOrigin;static bool spinning;
 static ConfigEntry<KeyCode> jitterKey=null!,bowKey=null!;
 static Player? owner;static PlayerCameraController? controller;static IntPtr world;
 static EPointOfView originalView;static bool viewCaptured;
 static Camera.CameraCallback? renderCallback;static bool nearWall;static float nextViewLog;
 static readonly Il2CppStructArray<RaycastHit> cameraHits=new(64);
 static Camera? movedCamera;static Vector3 originalCameraPosition;static Quaternion originalCameraRotation;
 readonly record struct BonePose(Transform Bone,Quaternion Original,Quaternion Applied);
 static readonly List<BonePose> poses=new(2);static float nextError;
 internal static void Initialize(Plugin plugin){
  const string section="43. 娱乐";
  thirdPerson=FeatureConfig.Bind(section,"Third person","第三人称",false,"切换为身后跟随视角，显示完整人物；关闭后恢复原视角。");
  viewKey=FeatureConfig.Bind(section,"Third person key","第三人称切换键",KeyCode.None,"默认不占用按键，可自行绑定。");
  jitterKey=FeatureConfig.Bind(section,"Model jitter key","人物摇头晃动切换键",KeyCode.None);bowKey=FeatureConfig.Bind(section,"Model bow key","人物弯腰看地面切换键",KeyCode.None);
  firstOnAim=FeatureConfig.Bind(section,"First person when aiming","瞄准时切回第一人称",true,"松开瞄准后回到第三人称。");
  distance=FeatureConfig.Bind(section,"Camera distance","第三人称距离（米）",2.5f,"靠墙自动收近，空间不足时临时切第一人称，离开后恢复。",new AcceptableValueRange<float>(1,6));
  shoulder=FeatureConfig.Bind(section,"Camera shoulder","第三人称肩位偏移（米）",.4f,"正数右肩，负数左肩。",new AcceptableValueRange<float>(-1,1));
  jitter=FeatureConfig.Bind(section,"Model jitter","高频摇头晃动",false,"人物模型高频随机摇头、扭动上身；第三人称下可直接看到。真实移动方向和瞄准角度保持玩家操作。");
  lookDown=FeatureConfig.Bind(section,"Model look down","人物低头看地板",false,"人物模型持续低头；可与高频摇头同时开启。只改变模型动作，不改变镜头、准星和射击方向。");
  bowAngle=FeatureConfig.Bind(section,"Model bow depth","看地面弯腰深度（度）",100f,"从腰部深度前屈；95～110 度为明显弯腰到底。",new AcceptableValueRange<float>(45,125));
  frequency=FeatureConfig.Bind(section,"Jitter frequency","晃动频率（次／秒）",24f,"按时间采样，实际可见频率受游戏帧率限制。",new AcceptableValueRange<float>(1,60));
  headYaw=FeatureConfig.Bind(section,"Head yaw","左右摇头幅度（度）",85f,"",new AcceptableValueRange<float>(0,160));
  headPitch=FeatureConfig.Bind(section,"Head pitch","上下摇头幅度（度）",45f,"",new AcceptableValueRange<float>(0,80));
  headRoll=FeatureConfig.Bind(section,"Head roll","歪头幅度（度）",35f,"",new AcceptableValueRange<float>(0,80));
  bodyAngle=FeatureConfig.Bind(section,"Body jitter","上身晃动幅度（度）",18f,"",new AcceptableValueRange<float>(0,40));
  spin=FeatureConfig.Bind(section,"Model continuous spin","人物模型高速 360° 旋转",false,"持续旋转本地人物的外观骨骼；建议用第三人称或自由镜头观察。");spinSpeed=FeatureConfig.Bind(section,"Model spin speed","模型旋转速度（度／秒）",1440f,"360 度为每秒一圈，1440 度为每秒四圈。",new AcceptableValueRange<float>(90,7200));reverseSpin=FeatureConfig.Bind(section,"Model spin reverse","模型反向旋转",false);spinKey=FeatureConfig.Bind(section,"Model spin toggle key","模型旋转切换键",KeyCode.None);
  ModelPoseLock.Initialize();
  plugin.Patches.Patch(AccessTools.Method(typeof(PlayerCameraController),"LateUpdate"),prefix:new HarmonyMethod(typeof(EntertainmentControl),nameof(BeforeCamera)),postfix:new HarmonyMethod(typeof(EntertainmentControl),nameof(AfterCamera)));
  plugin.Patches.Patch(AccessTools.Method(typeof(Player),"ComplexLateUpdate"),prefix:new HarmonyMethod(typeof(EntertainmentControl),nameof(BeforeVisual)),postfix:new HarmonyMethod(typeof(EntertainmentControl),nameof(AfterVisual)));
  plugin.Patches.Patch(AccessTools.Method(typeof(Player),"VisualPass"),prefix:new HarmonyMethod(typeof(EntertainmentControl),nameof(BeforeVisualPass)),postfix:new HarmonyMethod(typeof(EntertainmentControl),nameof(AfterVisualPass)));
  plugin.Patches.Patch(AccessTools.Method(typeof(Player),"ComplexUpdate"),prefix:new HarmonyMethod(typeof(EntertainmentControl),nameof(BeforeVisual)));
  plugin.Patches.Patch(AccessTools.Method(typeof(Player),"ManualUpdate"),prefix:new HarmonyMethod(typeof(EntertainmentControl),nameof(BeforeVisual)));
  plugin.Patches.Patch(typeof(Player.FirearmController).GetMethod("ManualUpdate",new[]{typeof(float)}),prefix:new HarmonyMethod(typeof(EntertainmentControl),nameof(BeforeWeaponLogic)));
  renderCallback=DelegateSupport.ConvertDelegate<Camera.CameraCallback>(new Action<Camera>(BeforeRender));Camera.onPreCull+=renderCallback;
  plugin.Log.LogInfo("Entertainment ready: local third-person camera, native POV/render switching and reversible model-only jitter");
 }
 static bool Local(Player? player)=>owner!=null&&player?.Pointer==owner.Pointer&&player.IsYourPlayer&&player.HealthController?.IsAlive==true&&Singleton<GameWorld>.Instance?.Pointer==world;
 internal static void Update(){
  ModelPoseLock.RestoreAll();RestorePose();
  try{
   var current=Singleton<GameWorld>.Instance;var player=current?.MainPlayer;
   if(!RaidClock.Active||player?.IsYourPlayer!=true||player.HealthController?.IsAlive!=true){Reset();return;}
   if(owner!=null&&(owner.Pointer!=player.Pointer||world!=current!.Pointer))Reset();
   owner=player;world=current!.Pointer;
   ModelPoseLock.Update(current,player);
   if(!InterfaceMenu.InputBlocked&&viewKey.Value!=KeyCode.None&&Input.GetKeyDown(viewKey.Value)){thirdPerson.Value=!thirdPerson.Value;FeatureToasts.Show("第三人称",thirdPerson.Value);}
   if(!InterfaceMenu.InputBlocked){if(jitterKey.Value!=KeyCode.None&&Input.GetKeyDown(jitterKey.Value))jitter.Value=!jitter.Value;if(bowKey.Value!=KeyCode.None&&Input.GetKeyDown(bowKey.Value))lookDown.Value=!lookDown.Value;}
   if(!InterfaceMenu.InputBlocked&&spinKey.Value!=KeyCode.None&&Input.GetKeyDown(spinKey.Value))spin.Value=!spin.Value;
   if(spin.Value&&!spinning)spinOrigin=Time.realtimeSinceStartupAsDouble;spinning=spin.Value;
   UpdateView();
  }catch(Exception error){Warn(error);}
 }
 static void UpdateView(){
  if(owner==null)return;
  if(!thirdPerson.Value&&!DirectorStage.FreeActive){RestoreView();return;}
  if(controller==null||controller.Player?.Pointer!=owner.Pointer||Spectating())return;
  if(!viewCaptured){originalView=owner.PointOfView;viewCaptured=true;}
  var desired=!WantsThird||nearWall?EPointOfView.FirstPerson:EPointOfView.ThirdPerson;
  SetView(desired);
 }
 static bool WantsThird=>DirectorStage.FreeActive||thirdPerson.Value&&!(firstOnAim.Value&&owner?.ProceduralWeaponAnimation?.IsAiming==true);
 static bool Spectating()=>controller?.IsInSpectatorCamera==true||controller?._currentCameraState?.TryCast<SpectatorCameraState>()!=null;
 static void SetView(EPointOfView view){
  if(owner==null||controller==null)return;
  if(view==EPointOfView.FirstPerson)RestoreCamera();
  if(owner.PointOfView!=view){owner.PointOfView=view;owner.PlayerBody?.UpdatePlayerRenders(view,owner.Side);controller.UpdatePointOfView();}
  if(view==EPointOfView.ThirdPerson){if(controller._currentCameraState?.TryCast<ThirdPersonCameraState>()==null)controller.InitiateOperation<LockCameraState>();}
  else if(controller._currentCameraState?.TryCast<FirstPersonCameraState>()==null)controller.InitiateOperation<FirstPersonCameraState>();
 }
 static void BeforeCamera(PlayerCameraController __instance){if(Local(__instance.Player)){ModelPoseLock.RestoreAll();RestorePose();}}
 static void AfterCamera(PlayerCameraController __instance){
  if(!Local(__instance.Player))return;
  try{
   controller=__instance;UpdateView();
   if(viewCaptured&&WantsThird&&!Spectating()){var camera=CameraManager.Instance?.Camera??__instance.Camera;if(DirectorStage.FreeActive)DirectorStage.PositionCamera(camera);else PositionCamera(camera);}
   ApplyPose();ModelPoseLock.Apply(__instance.Player);
  }catch(Exception error){Warn(error);}
 }
 static void BeforeRender(Camera camera){
  if(owner==null||!Local(owner))return;
  var main=CameraManager.Instance?.Camera??controller?.Camera;if(main==null||main.Pointer!=camera.Pointer)return;
  try{ModelPoseLock.RestoreAll();RestorePose();if(viewCaptured&&WantsThird&&controller!=null&&!Spectating()){if(DirectorStage.FreeActive)DirectorStage.PositionCamera(camera);else PositionCamera(camera);}ApplyPose();ModelPoseLock.ApplyAll();}catch(Exception error){Warn(error);}
 }
 static void PositionCamera(Camera camera){
  if(camera==null||owner==null)return;
  RestorePose();
  var angles=owner.Rotation;var rotation=Quaternion.Euler(angles.y,angles.x,0);
  var head=owner.PlayerBones?.Head?.Original;
  var pivot=(head!=null?head.position:owner.Position+Vector3.up*(owner.IsInPronePose ? .45f :owner.Pose==EPlayerPose.Duck?1.1f:1.6f))+Vector3.up*.15f;
  var delta=rotation*new Vector3(shoulder.Value,0,-Math.Max(1,distance.Value));float length=delta.magnitude,safe=length;
  int mask=LayerMask.GetMask("HighPolyCollider","Terrain","LowPolyCollider"),ignored=0;var current=Singleton<GameWorld>.Instance;
  if(mask!=0&&length>.001f){int count=Physics.SphereCastNonAlloc(pivot,.18f,delta/length,cameraHits,length,mask,QueryTriggerInteraction.Ignore);for(int i=0;i<count;i++){var hit=cameraHits[i];if(hit.collider==null)continue;if(current!=null&&CrosshairProbe.LocalCollider(current,owner,hit.collider)){ignored++;continue;}safe=Math.Min(safe,Math.Max(.05f,hit.distance-.08f));}}
  nearWall=safe<(nearWall ? .9f : .7f);
  SetView(nearWall?EPointOfView.FirstPerson:EPointOfView.ThirdPerson);
  var position=nearWall?(owner.CameraPosition?.position??pivot-Vector3.up*.15f):pivot+delta/length*safe;
  if(!nearWall&&movedCamera?.Pointer!=camera.Pointer){RestoreCamera();movedCamera=camera;originalCameraPosition=camera.transform.localPosition;originalCameraRotation=camera.transform.localRotation;}
  camera.transform.SetPositionAndRotation(position,rotation);
  if(Time.unscaledTime>=nextViewLog){nextViewLog=Time.unscaledTime+20;Plugin.Self.Log.LogInfo($"Third-person render position: headDistance={Vector3.Distance(position,head!=null?head.position:owner.Position):F2}m, wallFallback={nearWall}, ownCollidersIgnored={ignored}");}
 }
 static void BeforeVisual(Player __instance){ModelPoseLock.Restore(__instance);if(Local(__instance))RestorePose();}
 static bool BeforeVisualPass(Player __instance,out bool __state){__state=PlayerVisualGuard.Ready(__instance);if(__state)BeforeVisual(__instance);return __state;}
 static void AfterVisualPass(Player __instance,bool __state){if(__state)AfterVisual(__instance);}
 static void AfterVisual(Player __instance){try{if(Local(__instance))ApplyPose();ModelPoseLock.Apply(__instance);}catch(Exception error){Warn(error);}}
 static void ApplyPose(){
  RestorePose();if((!jitter.Value&&!lookDown.Value&&!spin.Value)||owner==null||!Local(owner)||ModelPoseLock.Locked(owner))return;
  var bones=owner.PlayerBones;if(bones==null)return;
  if(spin.Value){var root=bones.RootJoint??bones.Pelvis?.Original;if(root!=null&&root.Pointer!=owner.transform.Pointer&&root.IsChildOf(owner.transform))ApplyWorldBone(root,Quaternion.AngleAxis(EntertainmentRules.SpinAngle(Time.realtimeSinceStartupAsDouble-spinOrigin,spinSpeed.Value*(reverseSpin.Value?-1:1)),Vector3.up));}
  var sample=jitter.Value?EntertainmentRules.Jitter(Time.realtimeSinceStartupAsDouble,frequency.Value,headYaw.Value,headPitch.Value,headRoll.Value):System.Numerics.Vector3.Zero;
  sample=EntertainmentRules.HeadPose(sample,lookDown.Value);
  var torso=bones.Spine3?.Original;
  float bow=lookDown.Value?bowAngle.Value:0;
  if(bow>0){var waist=bones.Spine1??torso??bones.Ribcage?.Original;var rotation=EntertainmentRules.WorldPitch(owner.Rotation.x,bow);ApplyWorldBone(waist,new Quaternion(rotation.X,rotation.Y,rotation.Z,rotation.W));}
  if(jitter.Value&&torso!=null&&torso.Pointer!=bones.Neck?.Pointer){var body=EntertainmentRules.Jitter(Time.realtimeSinceStartupAsDouble+.17,frequency.Value,bodyAngle.Value,bodyAngle.Value*.5f,bodyAngle.Value);ApplyBone(torso,Quaternion.Euler(body.X,body.Y,body.Z));}
  // Rig-local X is not the character's left/right axis. Use a world-space
  // nod around the actor's right axis, shared between neck and skull.
  float pitch=sample.X-(lookDown.Value?Math.Clamp(Mathf.DeltaAngle(0,owner.Rotation.y),-90,90)+bow:0);
  var tilt=EntertainmentRules.WorldPitch(owner.Rotation.x,pitch);
  var nod=new Quaternion(tilt.X,tilt.Y,tilt.Z,tilt.W);
  var yaw=Quaternion.AngleAxis(sample.Y,Vector3.up);
  var roll=Quaternion.AngleAxis(sample.Z,Quaternion.Euler(0,owner.Rotation.x,0)*Vector3.forward);
  var delta=roll*yaw*nod;var head=bones.Head?.Original;var neck=bones.Neck;
  if(head!=null&&neck!=null&&head.Pointer!=neck.Pointer&&head.IsChildOf(neck)){ApplyWorldBone(neck,Quaternion.Slerp(Quaternion.identity,delta,.4f));ApplyWorldBone(head,Quaternion.Slerp(Quaternion.identity,delta,.6f));}
  else ApplyWorldBone(head??neck,delta);
 }
 static void ApplyWorldBone(Transform? bone,Quaternion delta){if(bone==null)return;var original=bone.localRotation;bone.rotation=delta*bone.rotation;poses.Add(new(bone,original,bone.localRotation));}
 [HarmonyPriority(Priority.First)] static void BeforeWeaponLogic(Player.FirearmController __instance){if(__instance._player is {} actor)RestoreForLogic(actor);}
 internal static void RestoreForLogic(Player actor){ModelPoseLock.Restore(actor);if(Local(actor))RestorePose();}
 static void ApplyBone(Transform? bone,Quaternion delta){if(bone==null)return;var original=bone.localRotation;var applied=original*delta;bone.localRotation=applied;poses.Add(new(bone,original,applied));}
 static void RestorePose(){for(int i=poses.Count-1;i>=0;i--)try{var pose=poses[i];if(pose.Bone!=null&&Quaternion.Angle(pose.Bone.localRotation,pose.Applied)<.01f)pose.Bone.localRotation=pose.Original;}catch{}poses.Clear();}
 static void RestoreCamera(){try{if(movedCamera!=null){movedCamera.transform.localPosition=originalCameraPosition;movedCamera.transform.localRotation=originalCameraRotation;}}catch{}movedCamera=null;}
 static void RestoreView(){if(!viewCaptured){RestoreCamera();nearWall=false;return;}viewCaptured=false;RestoreCamera();if(owner!=null&&Local(owner)&&controller!=null&&controller.Player?.Pointer==owner.Pointer&&!Spectating())SetView(originalView);nearWall=false;}
 internal static void Reset(){ModelPoseLock.Reset();RestorePose();try{RestoreView();}catch(Exception error){Warn(error);}owner=null;controller=null;world=IntPtr.Zero;spinning=false;viewCaptured=false;nearWall=false;}
 internal static void Shutdown(){Reset();if(renderCallback!=null){Camera.onPreCull-=renderCallback;renderCallback=null;}}
 static void Warn(Exception error){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+15;Plugin.Self.Log.LogWarning("Entertainment: "+error.Message);}
}
