using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.HealthSystem;
using HarmonyLib;
using UnityEngine;
using Local.ModHost;
using NumericsVector=System.Numerics.Vector3;
namespace AllTheModium.Client;
public sealed partial class SwordModule {
 readonly record struct GuardPose(Transform Bone,Quaternion Original,Quaternion Applied);
 readonly List<GuardPose> guardPoses=[];
 readonly List<Transform> guardMeshes=[];
 Player.KnifeController? guardHands;
 string guardItem="";
 float guardBlend;
 int blockedHits;
 float lastGuardDamage,lastGuardResult;
 bool guardDiagnosticsDirty;
 float nextGuardDiagnostics;

 void InstallGuard(Harmony harmony){
  harmony.Patch(AccessTools.Method(typeof(Player.KnifeController),nameof(Player.KnifeController.MakeAlternativeKick)),prefix:new HarmonyMethod(typeof(SwordModule),nameof(GuardInsteadOfStab)));
  harmony.Patch(AccessTools.Method(typeof(Player.KnifeController),nameof(Player.KnifeController.MakeKnifeKick)),prefix:new HarmonyMethod(typeof(SwordModule),nameof(GuardBeforeSwing)));
  // Scale the final health amount after armor processing. Do not change a
  // boxed DamageInfo value or detour any native value-type method.
  harmony.Patch(AccessTools.Method(typeof(ActiveHealthController),nameof(ActiveHealthController.ApplyDamage),[typeof(EBodyPart),typeof(float),typeof(DamageInfo)]),prefix:new HarmonyMethod(typeof(SwordModule),nameof(GuardDamage)));
  context!.Log(LogLevel.Info,"Sword guard settings available: bullet blocking, health damage reduction and model size.");
 }
 static bool LocalSword(Player.KnifeController? hands){
  var local=Singleton<GameWorld>.Instance?.MainPlayer;
  string id=hands?.GetItem()?.StringTemplateId??"";return hands!=null&&local!=null&&local.IsYourPlayer&&hands._player?.Pointer==local.Pointer&&Swords.Find(id)!=null&&!Swords.IsUtility(id);
 }
 static bool Requested(Player player,Player.KnifeController hands){
  bool control=player.IsYourPlayer&&player.HealthController?.IsAlive==true&&!player.IsSprintEnabled&&!player.IsInventoryOpened&&Application.isFocused&&!Cursor.visible&&Time.timeScale>0;
  return active?.guardEnabled.Value==true&&SwordGuardRules.CanRaise(control,hands.CurrentOperation?.TryCast<Player.KnifeController.Idling>()!=null,Input.GetMouseButton(1),Input.GetMouseButton(0));
 }
 static bool GuardInsteadOfStab(Player.KnifeController __instance,ref bool __result){
  if(active?.context?.IsActive!=true||!active.guardEnabled.Value||!LocalSword(__instance))return true;
  __result=false;return false;
 }
 static void GuardBeforeSwing(Player.KnifeController __instance){
  var a=active;if(a?.context?.IsActive!=true||!LocalSword(__instance))return;
  a.RestoreGuardPose();a.guardBlend=0;
 }
 static void GuardDamage(ActiveHealthController __instance,ref float __1,DamageInfo __2){
  var a=active;if(a?.context?.IsActive!=true||__2==null||__instance.Player?.IsYourPlayer!=true)return;
  var player=Singleton<GameWorld>.Instance?.MainPlayer;
  var hands=player?.HandsController?.TryCast<Player.KnifeController>();
  if(player==null||!LocalSword(hands)||!Requested(player,hands!))return;
  var direction=__2.Direction;var look=player.LookDirection;
  bool frontal=SwordGuardRules.Frontal(new NumericsVector(look.x,look.y,look.z),new NumericsVector(direction.x,direction.y,direction.z));
  bool eligible=SwordGuardRules.Eligible(__2.DamageType==EDamageType.Bullet,__2.DamageType==EDamageType.Melee,a.blockBullets.Value);
  float reduced=SwordGuardRules.Damage(__1,true,eligible,__2.OverDamageFrom.HasValue,frontal,a.reductionPercent.Value);
  if(reduced==__1)return;
  a.lastGuardDamage=__1;a.lastGuardResult=reduced;a.blockedHits++;a.guardDiagnosticsDirty=true;__1=reduced;
 }
 void TickGuard(){
  RestoreGuardPose();
  var player=Singleton<GameWorld>.Instance?.MainPlayer;
  var hands=player?.HandsController?.TryCast<Player.KnifeController>();
  if(player==null||!LocalSword(hands)){ResetGuard();return;}
  string item=hands!.GetItem().Id.ToString();
  if(guardHands?.Pointer!=hands.Pointer||guardItem!=item){
   ResetGuard();guardHands=hands;guardItem=item;
   var root=hands.ControllerGameObject;
   if(root!=null)foreach(var mesh in root.GetComponentsInChildren<MeshFilter>(true))if(mesh.name.StartsWith("weapon_apoc_wasteland_gladius_LOD",StringComparison.Ordinal))guardMeshes.Add(mesh.transform);
  }
  bool requested=Requested(player,hands);
  // Attacks and non-idle operations must take the pose away immediately.
  if(player.IsSprintEnabled||hands.CurrentOperation?.TryCast<Player.KnifeController.Idling>()==null)guardBlend=0;
  else guardBlend=Mathf.MoveTowards(guardBlend,requested?1:0,Time.unscaledDeltaTime/.14f);
  if(diagnosticsEnabled.Value&&guardDiagnosticsDirty&&Time.unscaledTime>=nextGuardDiagnostics){
   nextGuardDiagnostics=Time.unscaledTime+1;guardDiagnosticsDirty=false;
   try{File.WriteAllText(Path.Combine(context!.DataDirectory,"guard-status.json"),System.Text.Json.JsonSerializer.Serialize(new{version=Version,utc=DateTime.UtcNow,blockedHits,lastGuardDamage,lastGuardResult}));}
   catch(Exception e){context!.Log(LogLevel.Warning,"Sword guard diagnostics: "+e.Message);}
  }
 }
 void ApplyGuardPose(){
  if(context?.IsActive!=true||guardBlend<=0||!LocalSword(guardHands))return;
  // Rotate only the two sword render meshes around their grip. The native
  // arm rig, camera, and hit collider remain under the game's animation.
  RestoreGuardPose();var camera=EFT.CameraControl.CameraManager.Instance?.Camera;if(camera==null)return;
  var right=camera.transform.right;var up=camera.transform.up;
  var blade=SwordGuardRules.GuardBlade(new(right.x,right.y,right.z),new(up.x,up.y,up.z));
  var direction=new Vector3(blade.X,blade.Y,blade.Z);
  // Imported blade runs along +Z, thickness along X. Point the blade to
  // screen upper-left, with its broad face toward the camera. Local Euler
  // angles are unsuitable because the weapon's animated parent is rotated.
  var target=Quaternion.LookRotation(direction,Vector3.Cross(direction,camera.transform.forward));
  foreach(var bone in guardMeshes){if(bone==null)continue;var original=bone.localRotation;var local=bone.parent==null?target:Quaternion.Inverse(bone.parent.rotation)*target;var applied=Quaternion.Slerp(original,local,Mathf.SmoothStep(0,1,guardBlend));bone.localRotation=applied;guardPoses.Add(new(bone,original,applied));}
 }
 void RestoreGuardPose(){
  foreach(var pose in guardPoses)if(pose.Bone!=null&&Quaternion.Angle(pose.Bone.localRotation,pose.Applied)<.01f)pose.Bone.localRotation=pose.Original;
  guardPoses.Clear();
 }
 void ResetGuard(){RestoreGuardPose();guardMeshes.Clear();guardHands=null;guardItem="";guardBlend=0;}
}
