using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx.Configuration;
using Comfort.Common;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using NumericsVector=System.Numerics.Vector3;
using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;
using Path=System.IO.Path;
namespace LocalSpt5Esp;
internal enum AimTargetPart { Head, Chest, Stomach, LeftArm, RightArm, LeftLeg, RightLeg }

internal static class CombatAssist {
 internal static ConfigEntry<AimMode> Mode=null!;
 internal static ConfigEntry<AimTargetPart> TargetPart=null!;
 internal static ConfigEntry<KeyCode> HoldKey=null!,CycleKey=null!,AlwaysKey=null!;
 internal static ConfigEntry<bool> AlwaysOn=null!;
 static ConfigEntry<float> Angle=null!,Range=null!,RocketDelay=null!,CloseRange=null!,CloseAngle=null!;
 static ConfigEntry<bool> VisibleOnly=null!,ThroughCover=null!,InfiniteRockets=null!;
 static Player? local,target;static Camera? view;static GameWorld? game;
 static readonly Dictionary<IntPtr,BodyPartCollider> targetColliders=new();
 static readonly Il2CppStructArray<RaycastHit> visionHits=new(64);
 static int environmentMask;static long redirected,missed;
 static float nextTarget,nextRegistry,nextRocket,nextError;
 static string registry="";static DateTime registryStamp;
 static HashSet<string> launchers=new(StringComparer.Ordinal);
 const string LauncherTemplate="676bf44c5539167c3603e869";
 internal static void Initialize(Plugin p){
  Mode=CnConfig.Bind(p.Config,"8. Aim assist","Mode",AimMode.Snap,"Off / Snap: move view to head / Silent: redirect local bullets toward head without moving view. Modes are mutually exclusive.");
  TargetPart=CnConfig.Bind(p.Config,"8. Aim assist","Target body part",AimTargetPart.Head);
  AimTargetFilter.Initialize(p);
  HoldKey=CnConfig.Bind(p.Config,"8. Aim assist","Hold key",KeyCode.Mouse4,"Hold to activate selected mode. Mouse4 is a mouse side button.");
  AlwaysOn=CnConfig.Bind(p.Config,"8. Aim assist","Always active",false,"Activate the selected Snap or Silent mode without holding the mouse side button. Off mode still disables aim assist. Does not fire automatically.");
  AlwaysKey=CnConfig.Bind(p.Config,"8. Aim assist","Always active toggle key",KeyCode.F4,"Toggle always-active aim assist; when disabled, hold the configured mouse side button.");
  CycleKey=CnConfig.Bind(p.Config,"8. Aim assist","Cycle mode key",KeyCode.F3,"Cycle Off -> Snap -> Silent");
  Angle=CnConfig.Bind(p.Config,"8. Aim assist","Acquisition angle",20f,new ConfigDescription("Maximum angle from the center of view, in degrees",new AcceptableValueRange<float>(1,180)));
  Range=CnConfig.Bind(p.Config,"8. Aim assist","Maximum distance",200f,new ConfigDescription("Meters",new AcceptableValueRange<float>(10,1000)));
  CloseRange=CnConfig.Bind(p.Config,"8. Aim assist","Close target distance",8f,new ConfigDescription("Prefer close threats and allow a wider angle within this distance",new AcceptableValueRange<float>(1,20)));
  CloseAngle=CnConfig.Bind(p.Config,"8. Aim assist","Close target angle",140f,new ConfigDescription("Acquisition angle for close threats, in degrees",new AcceptableValueRange<float>(30,180)));
  VisibleOnly=CnConfig.Bind(p.Config,"8. Aim assist","Visible targets only",true,"Require an unobstructed line from the camera to the target head.");
  ThroughCover=CnConfig.Bind(p.Config,"8. Aim assist","Silent through cover",true,"Silent bullets can select covered AI and start immediately outside the target head when the shot path is blocked. Snap visibility is unchanged. Rockets retain physical cover collisions.");
  InfiniteRockets=CnConfig.Bind(p.Config,"9. Infinite launcher","Enabled",true,"Repeat-fire only registered gifted RShG-2 launchers without consuming the chamber or discarding the weapon.");
  RocketDelay=CnConfig.Bind(p.Config,"9. Infinite launcher","Shot interval",1.5f,new ConfigDescription("Seconds between rockets while holding the normal fire key",new AcceptableValueRange<float>(1,10)));
  registry=Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","unlimited-launchers.json");
  Patch(p,typeof(BallisticsCalculator),"CreateShot",nameof(RedirectBullet));
  Patch(p,typeof(Player.FirearmController),"InitiateRocket",nameof(RedirectRocket));
  Patch(p,typeof(Player.FirearmController),"SetTriggerPressed",nameof(KeepLauncher));
  RefreshRegistry();
 }
 static void Patch(Plugin p,Type type,string name,string prefix){
  try{var methods=type.GetMethods().Where(m=>m.Name==name&&m.DeclaringType==type).ToArray();if(methods.Length!=1)throw new InvalidOperationException($"Expected one {type.Name}.{name}, found {methods.Length}");p.Patches.Patch(methods[0],prefix:new HarmonyMethod(typeof(CombatAssist),prefix));p.Log.LogInfo("Combat assist patched: "+methods[0]);}
  catch(Exception e){p.Log.LogError("Combat assist patch failed: "+name+" "+e);}
 }
 internal static void Keys(){
  if(Hotkeys.Pressed(CycleKey.Value)){Mode.Value=(AimMode)(((int)Mode.Value+1)%3);target=null;nextTarget=0;Plugin.Self.Log.LogInfo("Aim mode: "+Mode.Value);}
  if(Hotkeys.Pressed(AlwaysKey.Value)){AlwaysOn.Value=!AlwaysOn.Value;target=null;nextTarget=0;Plugin.Self.Log.LogInfo("Always-active aim assist: "+AlwaysOn.Value);}
 }
 static bool TriggerActive=>!InterfaceMenu.Open&&(AlwaysOn.Value||Input.GetKey(HoldKey.Value));
 internal static string Status=>$"自瞄：{CnConfig.AimName(Mode.Value)} · 常开：{CnConfig.OnOff(AlwaysOn.Value)}";
 internal static void Reset(){local=target=null;view=null;game=null;targetColliders.Clear();nextTarget=nextRocket=0;}
 static bool Ready()=>local!=null&&local.IsYourPlayer&&local.HealthController!=null&&local.HealthController.IsAlive&&view!=null&&!Cursor.visible;
 internal static void Tick(GameWorld? world,Player? player,Camera? camera){
  local=player;view=camera;game=world;
  if(!Ready()||world==null){target=null;return;}
  try{
   RefreshRegistry();
   var controller=local!.HandsController?.TryCast<Player.FirearmController>();
   if(controller==null||controller._isInventoryOpened){target=null;return;}
   if(Mode.Value==AimMode.Off||!TriggerActive){target=null;}
   else{
    if(Time.unscaledTime>=nextTarget){nextTarget=Time.unscaledTime+0.05f;Select(world);}
    if(ValidTarget()&&Mode.Value==AimMode.Snap){
     var direction=(TargetPoint(target!)-view!.transform.position).normalized;
     var rotation=new Vector2(Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,-Mathf.Asin(Mathf.Clamp(direction.y,-1,1))*Mathf.Rad2Deg);
     local.MovementContext.SetRotation(rotation);
    }
   }
   if(Registered(controller)&&controller.SuitableForHandInput&&!controller.IsInReloadOperation()&&controller.CanPressTrigger()&&Input.GetMouseButton(0)&&Time.unscaledTime>=nextRocket){
    nextRocket=Time.unscaledTime+RocketDelay.Value;
    // Bypass the disposable firing operation; vanilla's missile, sound and effects still run.
    var chambers=controller.Item.Chambers;Rocket? rocket=null;
    if(chambers!=null&&chambers.Length>0)rocket=chambers[0]?.ContainedItem?.TryCast<Rocket>();
    if(rocket==null){Warn(new InvalidOperationException("Registered RShG-2 has no loaded ShG-2 rocket"));return;}
    var round=rocket.Clone(Guid.NewGuid().ToString("N")[..24]).TryCast<Rocket>();
    if(round==null)throw new InvalidOperationException("Rocket clone failed");
    round.IsUsed=false;controller.LaunchRocketShot(round);
   }
  }catch(Exception e){Warn(e);}
 }
 internal static string Diagnostics=>$"silent redirects={redirected}, no eligible target={missed}";
 static NumericsVector N(Vector3 v)=>new(v.x,v.y,v.z);
 static Vector3 U(NumericsVector v)=>new(v.X,v.Y,v.Z);
 static EBodyPart SelectedPart()=>TargetPart.Value switch{AimTargetPart.Chest=>EBodyPart.Chest,AimTargetPart.Stomach=>EBodyPart.Stomach,AimTargetPart.LeftArm=>EBodyPart.LeftArm,AimTargetPart.RightArm=>EBodyPart.RightArm,AimTargetPart.LeftLeg=>EBodyPart.LeftLeg,AimTargetPart.RightLeg=>EBodyPart.RightLeg,_=>EBodyPart.Head};
 static BodyPartCollider? TargetCollider(Player p){
  var selected=SelectedPart();
  if(targetColliders.TryGetValue(p.Pointer,out var cached)&&cached!=null&&cached.Collider!=null&&cached.PlayerProfileID==p.ProfileId&&cached.BodyPartType==selected)return cached;
  var parts=p._hitColliders;BodyPartCollider? best=null;float volume=-1;
  if(parts!=null)for(int i=0;i<parts.Length;i++){var part=parts[i];if(part==null||part.BodyPartType!=selected||part.Collider==null)continue;float size=part.Collider.bounds.size.sqrMagnitude;if(size>volume){volume=size;best=part;}}
  if(best!=null)targetColliders[p.Pointer]=best;return best;
 }
 static Vector3 TargetPoint(Player p){var part=TargetCollider(p);if(part?.Collider!=null)return part.Collider.bounds.center;return SelectedPart()==EBodyPart.Head&&p.PlayerBones?.Head!=null?p.PlayerBones.Head.position:p.Position+Vector3.up*(SelectedPart() switch{EBodyPart.Chest=>1.3f,EBodyPart.Stomach=>1f,EBodyPart.LeftLeg or EBodyPart.RightLeg=>0.5f,_=>1.6f});}
 static bool AliveAI(Player? p)=>p!=null&&!p.IsYourPlayer&&(p.IsAI||p.AIData?.BotOwner!=null)&&p.HealthController!=null&&p.HealthController.IsAlive;
 static bool RequireVisibility=>VisibleOnly.Value&&!(Mode.Value==AimMode.Silent&&ThroughCover.Value);
 static bool ValidTarget(){
  if(!Ready()||!AliveAI(target)||!AimTargetFilter.Allowed(target!))return false;
  var point=TargetPoint(target!);var direction=point-view!.transform.position;float distance=direction.magnitude;
  return distance<=Range.Value&&Vector3.Angle(view.transform.forward,direction)<=AimMath.Limit(distance,Angle.Value,CloseRange.Value,CloseAngle.Value)&&(!RequireVisibility||ClearLine(view.transform.position,point,target!));
 }
 static bool ClearLine(Vector3 origin,Vector3 destination,Player candidate){
  if(environmentMask==0)environmentMask=LayerMask.GetMask("HighPolyCollider","Terrain","LowPolyCollider");
  if(environmentMask==0)return false;
  var delta=destination-origin;float distance=delta.magnitude;if(distance<0.001f)return true;
  int count=Physics.RaycastNonAlloc(origin,delta/distance,visionHits,Math.Max(0,distance-0.01f),environmentMask,QueryTriggerInteraction.Ignore);
  if(count>=visionHits.Length)return false;
  for(int i=0;i<count;i++){
   var collider=visionHits[i].collider;if(collider==null)continue;
   var t=collider.transform;
   if(t.IsChildOf(local!.transform)||t.IsChildOf(candidate.transform))continue;
   var map=game?.PlayersColliders;
   if(map!=null&&map.ContainsKey(collider)){var owner=map[collider];if(owner!=null&&(owner.ProfileId==local.ProfileId||owner.ProfileId==candidate.ProfileId))continue;}
   var part=collider.GetComponent<BodyPartCollider>();
   if(part!=null&&(part.PlayerProfileID==local.ProfileId||part.PlayerProfileID==candidate.ProfileId))continue;
   return false;
  }
  return true;
 }
 static Vector3 CorrectOrigin(Vector3 origin,Player candidate,Vector3 point){
  var collider=TargetCollider(candidate)?.Collider;
  float radius=collider!=null?collider.bounds.extents.magnitude:0.2f;
  return U(AimMath.CorrectCloseOrigin(N(origin),N(view!.transform.position),N(point),radius,collider!=null&&collider.bounds.Contains(origin)));
 }
 static void Select(GameWorld world,Vector3? shotOrigin=null){
  target=null;float best=float.MaxValue;var all=world.AllAlivePlayersList;if(all==null)return;
  var origin=view!.transform.position;
  for(int i=0;i<all.Count;i++){
   var candidate=all[i];if(!AliveAI(candidate)||!AimTargetFilter.Allowed(candidate))continue;
   var point=TargetPoint(candidate);var direction=point-origin;float distance=direction.magnitude;if(distance>Range.Value)continue;
   float angle=Vector3.Angle(view.transform.forward,direction),limit=AimMath.Limit(distance,Angle.Value,CloseRange.Value,CloseAngle.Value);
   if(angle>limit)continue;
   float score=AimMath.Score(distance,angle,limit,CloseRange.Value);if(score>=best)continue;
   if(RequireVisibility&&(!ClearLine(origin,point,candidate)||shotOrigin.HasValue&&!ClearLine(CorrectOrigin(shotOrigin.Value,candidate,point),point,candidate)))continue;
   best=score;target=candidate;
  }
 }
 static bool PrepareSilent(Vector3 origin){
  if(Mode.Value!=AimMode.Silent||!TriggerActive)return false;
  game=Singleton<GameWorld>.Instance;local=game!=null?game.MainPlayer:null;view=Camera.main;
  if(!Ready()||game==null)return false;
  var controller=local!.HandsController?.TryCast<Player.FirearmController>();if(controller==null||controller._isInventoryOpened)return false;
  // A shot can arrive before LateUpdate, or after its cached target dies or moves.
  Select(game,origin);if(target==null){missed++;return false;}return true;
 }
 static Vector3 ShotDirection(Vector3 origin,float speed){
  var destination=TargetPoint(target!);
  return AimMath.TryIntercept(N(destination-origin),N(target!.Velocity),N(Physics.gravity),speed,out var direction,out _)?U(direction):(destination-origin).normalized;
 }
 static void RedirectBullet(ref Vector3 __1,ref Vector3 __2,int __5,Ammo __0,float __7,Item __6,int __8){
  try{
   if(__8>0||Mode.Value!=AimMode.Silent||!TriggerActive)return;
   var world=Singleton<GameWorld>.Instance;var player=world!=null?world.MainPlayer:null;
   if(player==null||!player.IsYourPlayer)return;
   var held=player.HandsController?.TryCast<Player.FirearmController>()?.Item;
   bool owned=__5==player.RaidId||held!=null&&__6!=null&&held.Id.ToString()==__6.Id.ToString();
   if(!owned||!PrepareSilent(__1))return;
   var point=TargetPoint(target!);
   __1=CorrectOrigin(__1,target!,point);
   if(ThroughCover.Value&&!ClearLine(__1,point,target!)){
    // Spawn just outside the selected body part, avoiding travel through the cover.
    var toward=(point-__1).normalized;
    var collider=TargetCollider(target!)?.Collider;
    var reach=collider!=null?collider.bounds.extents.magnitude+0.05f:0.25f;
    __1=point-toward*reach;
    __2=toward;
   }else __2=ShotDirection(__1,__0.InitialSpeed*__7);
   redirected++;
  }catch(Exception e){Warn(e);}
 }
 static void RedirectRocket(Player.FirearmController __instance,Vector3 __1,ref Vector3 __2){
  try{if(__instance._player!=null&&__instance._player.IsYourPlayer&&PrepareSilent(__1)){__2=ShotDirection(__1,144);redirected++;}}catch(Exception e){Warn(e);}
 }
 static bool Registered(Player.FirearmController fc)=>InfiniteRockets.Value&&fc._player!=null&&fc._player.IsYourPlayer&&fc.Item!=null&&fc.Item.StringTemplateId==LauncherTemplate&&launchers.Contains(fc.Item.Id.ToString());
 static bool KeepLauncher(Player.FirearmController __instance){try{return !Registered(__instance);}catch(Exception e){Warn(e);return true;}}
 static void RefreshRegistry(){
  if(Time.unscaledTime<nextRegistry)return;nextRegistry=Time.unscaledTime+2;
  try{
   if(!File.Exists(registry)){launchers.Clear();registryStamp=default;return;}
   var stamp=File.GetLastWriteTimeUtc(registry);if(stamp==registryStamp)return;
   var ids=JsonSerializer.Deserialize<List<string>>(File.ReadAllText(registry))??new();
   launchers=new HashSet<string>(ids.Where(id=>id.Length==24&&id.All(Uri.IsHexDigit)),StringComparer.Ordinal);
   registryStamp=stamp;Plugin.Self.Log.LogInfo("Unlimited launcher registry loaded: "+launchers.Count);
  }catch(Exception e){Warn(e);}
 }
 static void Warn(Exception e){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+20;Plugin.Self.Error(e);}
}

