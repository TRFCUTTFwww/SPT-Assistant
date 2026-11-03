using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.CameraControl;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using UnityEngine.AI;
namespace LocalSpt5Esp;
internal static class AiSceneDirector {
 sealed class Actor {
  internal LiveActorReference Reference=null!;internal Player Player=null!;internal ActorScenePreset Settings=null!;internal Vector2 BaseRotation;internal bool WasPaused,OldProne;internal float OldPose;internal Vector3 Origin;
  internal float Started,NextJump,NextPath,WaitUntil;internal int Point,Corner;internal Vector3[] Corners=Array.Empty<Vector3>();internal Vector3 Goal;internal bool Finished;
  internal readonly Dictionary<BotAbstractMeds,float> MedicineTimes=new();
 }
 static readonly Dictionary<IntPtr,Actor> actors=new();static IntPtr world;static float nextError;
 static ConfigEntry<KeyCode> applyKey=null!,removeKey=null!;
 static ConfigEntry<bool> enabled=null!,facing=null!,noDoors=null!,noHeal=null!,noMove=null!,look=null!,jump=null!,nod=null!,shake=null!,follow=null!,loop=null!,relative=null!;
 static ConfigEntry<float> yaw=null!,pitch=null!,speed=null!,interval=null!,amplitude=null!,wait=null!,followDistance=null!;static ConfigEntry<ScenePose> pose=null!;
 static readonly List<ScenePoint> route=new();static Vector3 routeOrigin;static string routeMap="",notice="";static Vector2 scroll;
 [ThreadStatic] static bool driving;
 internal static void Initialize(Plugin plugin){
  const string section="49. AI 动作导演";
  applyKey=FeatureConfig.Bind(section,"Apply scene key","所选 AI 应用导演设置键",KeyCode.None);removeKey=FeatureConfig.Bind(section,"Remove scene key","恢复 AI 列表勾选角色键",KeyCode.None,"仅恢复 AI 列表当前勾选的角色；解除模型锁定、导演动作及行动暂停。没有勾选时不操作其他角色。");
  enabled=FeatureConfig.Bind(section,"Scene birth enabled","出生应用导演动作",false);
  pose=FeatureConfig.Bind(section,"Scene stance","AI 姿势",ScenePose.Original);
  facing=FeatureConfig.Bind(section,"Scene set facing","固定朝向与抬头角度",false);
  yaw=FeatureConfig.Bind(section,"Scene heading","朝向角度（度）",0f,"0 北／90 东／180 南／270 西",new AcceptableValueRange<float>(0,360));
  pitch=FeatureConfig.Bind(section,"Scene pitch","抬头角度（负数抬头）",0f,"",new AcceptableValueRange<float>(-89,89));
  noDoors=FeatureConfig.Bind(section,"Scene no doors","禁止 AI 开门",false);noHeal=FeatureConfig.Bind(section,"Scene no healing","禁止 AI 治疗／打药",false);noMove=FeatureConfig.Bind(section,"Scene no movement","禁止 AI 移动",false);
  look=FeatureConfig.Bind(section,"Scene look at player","持续看向玩家",false);follow=FeatureConfig.Bind(section,"Scene follow player","跟随玩家",false);
  followDistance=FeatureConfig.Bind(section,"Scene follow distance","跟随保持距离（米）",3f,"",new AcceptableValueRange<float>(1,30));
  jump=FeatureConfig.Bind(section,"Scene jump","一直原地跳",false);nod=FeatureConfig.Bind(section,"Scene nod","循环点头",false);shake=FeatureConfig.Bind(section,"Scene shake","循环摇头",false);
  interval=FeatureConfig.Bind(section,"Scene action interval","动作周期（秒）",1.2f,"",new AcceptableValueRange<float>(.3f,60));
  amplitude=FeatureConfig.Bind(section,"Scene action amplitude","点头／摇头幅度（度）",25f,"",new AcceptableValueRange<float>(0,80));
  speed=FeatureConfig.Bind(section,"Scene move speed","路线移动输入倍率",1f,"最终速度仍受人物和姿势影响。",new AcceptableValueRange<float>(.1f,5));
  loop=FeatureConfig.Bind(section,"Scene loop route","循环行动路线",true);relative=FeatureConfig.Bind(section,"Scene relative route","路线相对生成点",true);
  wait=FeatureConfig.Bind(section,"Scene waypoint wait","每点停留秒数",1f,"",new AcceptableValueRange<float>(0,600));
  plugin.Patches.Patch(typeof(BotDoorOpener).GetMethod("Interact"),prefix:new HarmonyMethod(typeof(AiSceneDirector),nameof(AllowDoor)));
  plugin.Patches.Patch(typeof(BotMedecine).GetMethod("ManualUpdate"),prefix:new HarmonyMethod(typeof(AiSceneDirector),nameof(AllowHeal)));
  plugin.Patches.Patch(typeof(Player).GetMethod("Move",new[]{typeof(Vector2)}),prefix:new HarmonyMethod(typeof(AiSceneDirector),nameof(Movement)));
 }
 static bool AllowDoor(BotDoorOpener __instance)=>!Try(__instance._owner?.GetPlayer,out var entry)||!entry.Settings.NoDoors;
 static bool AllowHeal(BotMedecine __instance)=>!Try(__instance._owner?.GetPlayer,out var entry)||!entry.Settings.NoHealing;
 static void StopMedicine(Actor entry){var medicine=entry.Player.AIData?.BotOwner?.Medecine;if(medicine==null)return;foreach(var component in new BotAbstractMeds?[]{medicine.FirstAid,medicine.Stimulators,medicine.SurgicalKit})if(component!=null){if(!entry.MedicineTimes.ContainsKey(component))entry.MedicineTimes[component]=component._nextPosibleUseTime;component._nextPosibleUseTime=float.MaxValue;if(component.Using)component.CancelCurrent();}}
 internal static void FilterMotion(Il2CppObjectBase controller,ref Vector3 motion){foreach(var entry in actors.Values)if(entry.Settings.NoMovement&&entry.Player.CharacterController?.Pointer==controller.Pointer&&Try(entry.Player,out _)){motion.x=motion.z=0;return;}}
 static void Movement(Player __instance,ref Vector2 __0){if(!driving&&Try(__instance,out var entry)&&(entry.Settings.NoMovement||DirectorRules.OwnsLocomotion(entry.Settings)))__0=Vector2.zero;}
 static bool Try(Player? actor,out Actor entry){entry=null!;return actor!=null&&actors.TryGetValue(actor.Pointer,out entry!)&&entry.Reference.World==Singleton<GameWorld>.Instance?.Pointer&&entry.Reference.Profile==actor.ProfileId;}
 internal static ActorScenePreset Snapshot(){var actions=SceneActions.None;if(look.Value)actions|=SceneActions.LookAtPlayer;if(follow.Value)actions|=SceneActions.FollowPlayer;if(jump.Value)actions|=SceneActions.Jump;if(nod.Value)actions|=SceneActions.Nod;if(shake.Value)actions|=SceneActions.ShakeHead;return DirectorRules.Validate(new(){Enabled=enabled.Value,Pose=pose.Value,SetFacing=facing.Value,Yaw=yaw.Value,Pitch=pitch.Value,NoDoors=noDoors.Value,NoHealing=noHeal.Value,NoMovement=noMove.Value,Actions=actions,Speed=speed.Value,FollowDistance=followDistance.Value,ActionInterval=interval.Value,ActionAmplitude=amplitude.Value,Loop=loop.Value,Wait=wait.Value,RelativeRoute=relative.Value,Map=routeMap,Route=route.Select(p=>relative.Value?new ScenePoint(p.X-routeOrigin.x,p.Y-routeOrigin.y,p.Z-routeOrigin.z):p with{}).ToArray()});}
 internal static void Load(ActorScenePreset value){value=DirectorRules.Validate(value);enabled.Value=value.Enabled;pose.Value=value.Pose;facing.Value=value.SetFacing;yaw.Value=value.Yaw;pitch.Value=value.Pitch;noDoors.Value=value.NoDoors;noHeal.Value=value.NoHealing;noMove.Value=value.NoMovement;look.Value=value.Actions.HasFlag(SceneActions.LookAtPlayer);follow.Value=value.Actions.HasFlag(SceneActions.FollowPlayer);jump.Value=value.Actions.HasFlag(SceneActions.Jump);nod.Value=value.Actions.HasFlag(SceneActions.Nod);shake.Value=value.Actions.HasFlag(SceneActions.ShakeHead);speed.Value=value.Speed;followDistance.Value=value.FollowDistance;interval.Value=value.ActionInterval;amplitude.Value=value.ActionAmplitude;loop.Value=value.Loop;wait.Value=value.Wait;relative.Value=value.RelativeRoute;route.Clear();routeOrigin=Singleton<GameWorld>.Instance?.MainPlayer?.Position??Vector3.zero;routeMap=value.Map;foreach(var point in value.Route)route.Add(value.RelativeRoute?new(point.X+routeOrigin.x,point.Y+routeOrigin.y,point.Z+routeOrigin.z):point with{});}
 internal static ActorScenePreset For(Player player,bool portable=true){if(!Try(player,out var entry))return new(){Enabled=true,SetFacing=false,InitializeFacing=true,Pose=player.IsInPronePose?ScenePose.Prone:player.PoseLevel<.5f?ScenePose.Crouched:ScenePose.Standing,Yaw=player.Rotation.x,Pitch=Math.Clamp(Mathf.DeltaAngle(0,player.Rotation.y),-89,89),Map=Singleton<GameWorld>.Instance?.LocationId??""};var points=entry.Settings.Route.Select(p=>{var absolute=new Vector3(p.X,p.Y,p.Z)+(entry.Settings.RelativeRoute?entry.Origin:Vector3.zero);var saved=portable?absolute-player.Position:absolute;return new ScenePoint(saved.x,saved.y,saved.z);}).ToArray();var initial=(entry.Settings.Actions&(SceneActions.Nod|SceneActions.ShakeHead))!=0?entry.BaseRotation:player.Rotation;return entry.Settings with{InitializeFacing=true,Yaw=entry.Settings.SetFacing?entry.Settings.Yaw:initial.x,Pitch=entry.Settings.SetFacing?entry.Settings.Pitch:initial.y,Route=points,RelativeRoute=portable,StartPoint=Math.Min(entry.Point,Math.Max(0,points.Length-1)),Map=Singleton<GameWorld>.Instance?.LocationId??"",ActionPhase=(Time.unscaledTime-entry.Started)%entry.Settings.ActionInterval};}
 internal static bool OriginalPaused(Player player)=>Try(player,out var entry)?entry.WasPaused:ModelPoseLock.PreviousPaused(player);
 internal static void ResumeScenePhase(Player player){if(Try(player,out var entry)){entry.Started=Time.unscaledTime-entry.Settings.ActionPhase;entry.NextJump=Time.unscaledTime+.2f;}}
 internal static void Apply(GameWorld current,Player player,ActorScenePreset value){
  value=DirectorRules.Validate(value);if(!value.Enabled){Remove(player);return;}new LiveActorReference(current,player).Resolve();if(player.IsYourPlayer)throw new ArgumentException("请选择 AI。");
  if(value.Route.Length>0&&!value.RelativeRoute&&!string.Equals(value.Map,current.LocationId,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("绝对路线属于其他地图。");
  Remove(player);if(value.SetFacing||value.InitializeFacing)player.Rotation=new Vector2(value.Yaw,value.Pitch);var bot=player.AIData?.BotOwner??throw new ArgumentException("AI 未就绪。");
  var entry=new Actor{BaseRotation=player.Rotation,Reference=new(current,player),Player=player,Settings=value,WasPaused=AiTestControl.IsPaused(player),OldPose=player.PoseLevel,OldProne=player.IsInPronePose,Origin=player.Position,Point=value.StartPoint,Started=Time.unscaledTime-value.ActionPhase,NextJump=Time.unscaledTime+.2f};actors[player.Pointer]=entry;world=current.Pointer;
  // Scripted locomotion owns AI decisions; native Player movement/animations
  // still run. Fine-grained restrictions alone keep the normal combat brain.
  if(DirectorRules.OwnsLocomotion(value))AiTestControl.SetPaused(new[]{player},true);
  if(value.NoHealing)StopMedicine(entry);SetPose(player,value.Pose);notice="已应用导演动作："+(player.Profile?.Nickname??"AI");
 }
 static void SetPose(Player player,ScenePose stance){if(stance==ScenePose.Original)return;bool prone=stance==ScenePose.Prone;if(player.IsInPronePose!=prone)player.ToggleProne();if(!prone)player.MovementContext.SetPoseLevel(stance==ScenePose.Crouched?0:1,true);}
 internal static void Remove(Player player){if(!actors.Remove(player.Pointer,out var entry))return;try{entry.Reference.Resolve();foreach(var medicine in entry.MedicineTimes)if(medicine.Key._nextPosibleUseTime==float.MaxValue)medicine.Key._nextPosibleUseTime=medicine.Value;Move(player,Vector2.zero);if(DirectorRules.OwnsLocomotion(entry.Settings))AiTestControl.SetPaused(new[]{player},entry.WasPaused);if(player.IsInPronePose!=entry.OldProne)player.ToggleProne();player.MovementContext.SetPoseLevel(entry.OldPose,true);}catch{}}
 static void Move(Player player,Vector2 input){try{driving=true;player.Move(input);}finally{driving=false;}}
 internal static void Tick(GameWorld? current){if(current==null||!RaidClock.Active){Reset();return;}if(world!=IntPtr.Zero&&world!=current.Pointer)Reset();world=current.Pointer;if(!InterfaceMenu.InputBlocked){if(removeKey.Value!=KeyCode.None&&Input.GetKeyDown(removeKey.Value)){AiTestControl.ResumeChecked();}else if(applyKey.Value!=KeyCode.None&&Input.GetKeyDown(applyKey.Value)){var settings=Snapshot() with{Enabled=true};foreach(var selected in AiTestControl.PoseSelection)try{Apply(current,selected,settings);}catch(Exception e){notice=e.Message;}}}foreach(var entry in actors.Values.ToArray())try{var player=entry.Reference.Resolve();if(player.HealthController?.IsAlive!=true){Remove(player);continue;}if(SceneTemplates.IsStaging(player)||ModelPoseLock.Locked(player)){Move(player,Vector2.zero);continue;}var settings=entry.Settings;if(settings.NoHealing)StopMedicine(entry);SetPose(player,settings.Pose);
   float elapsed=Time.unscaledTime-entry.Started;var rotation=(settings.Actions&(SceneActions.Nod|SceneActions.ShakeHead))!=0?entry.BaseRotation:player.Rotation;if(settings.SetFacing)rotation=new(settings.Yaw,settings.Pitch);
   if(settings.Actions.HasFlag(SceneActions.LookAtPlayer)&&current.MainPlayer is {} local){var delta=(local.PlayerBones?.Head?.Original?.position??local.Position+Vector3.up*1.5f)-(player.PlayerBones?.Head?.Original?.position??player.Position+Vector3.up*1.5f);var look=DirectorRules.Look(new(delta.x,delta.y,delta.z),rotation.x);rotation=new(look.X,look.Y);}
   float wave=Mathf.Sin(elapsed/settings.ActionInterval*Mathf.PI*2)*settings.ActionAmplitude;if(settings.Actions.HasFlag(SceneActions.Nod))rotation.y+=wave;if(settings.Actions.HasFlag(SceneActions.ShakeHead))rotation.x+=wave;rotation.y=Math.Clamp(rotation.y,-89,89);player.Rotation=rotation;
   if(settings.Actions.HasFlag(SceneActions.Jump)&&Time.unscaledTime>=entry.NextJump&&player.MovementContext.IsGrounded){var stamina=player.Physical?.Stamina;if(stamina?.TotalCapacity!=null)stamina.Current=stamina.TotalCapacity.Value;player.Jump();entry.NextJump=Time.unscaledTime+settings.ActionInterval;}
   if(settings.NoMovement){Move(player,Vector2.zero);continue;}
   if(settings.Actions.HasFlag(SceneActions.FollowPlayer)&&current.MainPlayer is {} leader){if(Vector3.Distance(player.Position,leader.Position)>settings.FollowDistance)Follow(entry,leader.Position,rotation.x);else Move(player,Vector2.zero);}
   else if(settings.Route.Length>0&&!entry.Finished){if(Time.unscaledTime<entry.WaitUntil){Move(player,Vector2.zero);continue;}var p=settings.Route[entry.Point];var goal=new Vector3(p.X,p.Y,p.Z)+(settings.RelativeRoute?entry.Origin:Vector3.zero);if(Vector3.Distance(player.Position,goal)<.7f){entry.Point++;entry.Corners=Array.Empty<Vector3>();entry.WaitUntil=Time.unscaledTime+settings.Wait;if(entry.Point>=settings.Route.Length){entry.Point=0;if(!settings.Loop)entry.Finished=true;}Move(player,Vector2.zero);}else Follow(entry,goal,rotation.x);}
   else if(DirectorRules.OwnsLocomotion(settings))Move(player,Vector2.zero);
  }catch(Exception e){Remove(entry.Player);if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+5;notice=e.Message;Plugin.Self.Log.LogWarning("AI director: "+e.Message);}}
 }
 static void Follow(Actor entry,Vector3 goal,float yaw){if(Time.unscaledTime>=entry.NextPath&&(entry.Corners.Length==0||(goal-entry.Goal).sqrMagnitude>1||entry.Corner>=entry.Corners.Length)){entry.NextPath=Time.unscaledTime+.6f;if(!NavMesh.SamplePosition(goal,out var sample,3,NavMesh.AllAreas)){Move(entry.Player,Vector2.zero);return;}var path=new NavMeshPath();if(!NavMesh.CalculatePath(entry.Player.Position,sample.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete){Move(entry.Player,Vector2.zero);entry.Corners=Array.Empty<Vector3>();notice="路线不可达，角色已停止。";return;}entry.Corners=path.corners.ToArray();entry.Corner=0;entry.Goal=goal;}
  while(entry.Corner<entry.Corners.Length&&Vector3.Distance(entry.Player.Position,entry.Corners[entry.Corner])<.45f)entry.Corner++;
  if(entry.Corner>=entry.Corners.Length){Move(entry.Player,Vector2.zero);return;}var delta=entry.Corners[entry.Corner]-entry.Player.Position;if(!entry.Settings.SetFacing&&!entry.Settings.Actions.HasFlag(SceneActions.LookAtPlayer)){var direction=DirectorRules.Look(new(delta.x,0,delta.z),yaw);yaw=direction.X;if(entry.Settings.Actions.HasFlag(SceneActions.ShakeHead))yaw+=Mathf.Sin((Time.unscaledTime-entry.Started)/entry.Settings.ActionInterval*Mathf.PI*2)*entry.Settings.ActionAmplitude;entry.Player.Rotation=new Vector2(yaw,entry.Player.Rotation.y);}var local=DirectorRules.LocalMove(new(delta.x,delta.y,delta.z),yaw);Move(entry.Player,new Vector2(local.X,local.Y)*entry.Settings.Speed);
 }
 internal static void AddRoute(Vector3 point){if(route.Count>=256)throw new ArgumentException("最多 256 个路线点。");var current=Singleton<GameWorld>.Instance??throw new ArgumentException("请进入战局。");if(route.Count==0){routeOrigin=AiTestControl.PoseSelection.FirstOrDefault()?.Position??current.MainPlayer.Position;routeMap=current.LocationId??"";}if(!string.Equals(routeMap,current.LocationId,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("请先清除其他地图的路线。");if(!NavMesh.SamplePosition(point,out var hit,3,NavMesh.AllAreas))throw new ArgumentException("目标附近没有导航地面。");route.Add(new(hit.position.x,hit.position.y,hit.position.z));notice="路线点 "+route.Count;}
 internal static void AppendToSelected(Vector3 point){foreach(var player in AiTestControl.PoseSelection){if(!Try(player,out var entry))Apply(Singleton<GameWorld>.Instance!,player,Snapshot() with{Enabled=true});entry=actors[player.Pointer];var p=entry.Settings.RelativeRoute?point-entry.Origin:point;entry.Settings=entry.Settings with{Route=entry.Settings.Route.Append(new ScenePoint(p.x,p.y,p.z)).ToArray(),Map=Singleton<GameWorld>.Instance!.LocationId};entry.Finished=false;AiTestControl.SetPaused(new[]{player},true);}}
 internal static void Reset(){foreach(var entry in actors.Values.ToArray())Remove(entry.Player);actors.Clear();world=IntPtr.Zero;}
 static void Action(Action work){try{work();}catch(Exception e){notice=e.Message;FeatureToasts.Show(notice);}}
 internal static void Draw(Rect rect){scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-20,1700+route.Count*25));float x=8,y=8,w=rect.width-36,half=(w-12)/2;InterfaceMenu.Label(new Rect(x,y,w,32),"AI 动作与行为",InterfaceMenu.Accent,21);y+=48;
  if(InterfaceMenu.Button(new Rect(x,y,half,36),"应用到所选 AI"))Action(()=>{var targets=AiTestControl.PoseSelection;if(targets.Length==0)throw new ArgumentException("请先在 AI 管理选择角色。");var value=Snapshot() with{Enabled=true};foreach(var actor in targets)Apply(Singleton<GameWorld>.Instance!,actor,value);});
  if(InterfaceMenu.Button(new Rect(x+half+12,y,half,36),"恢复列表勾选 AI 行动"))AiTestControl.ResumeChecked();y+=48;
  if(InterfaceMenu.Button(new Rect(x,y,w,32),"读取玩家朝向与抬头角度"))Action(()=>{var player=Singleton<GameWorld>.Instance!.MainPlayer;yaw.Value=(player.Rotation.x%360+360)%360;pitch.Value=Math.Clamp(Mathf.DeltaAngle(0,player.Rotation.y),-89,89);facing.Value=true;});y+=44;
  var fields=new ConfigEntryBase[]{enabled,pose,facing,yaw,pitch,noDoors,noHeal,noMove,look,follow,followDistance,jump,nod,shake,interval,amplitude,speed,loop,relative,wait};for(int i=0;i<fields.Length;i++){InterfaceMenu.DrawEntry(fields[i],new Rect(x+i%2*(half+12),y+i/2*88,half,80));}y+=(fields.Length+1)/2*88+20;
  InterfaceMenu.Label(new Rect(x,y,w,28),"行动路线 · "+route.Count+" 点",InterfaceMenu.Accent,16);y+=40;
  if(InterfaceMenu.Button(new Rect(x,y,half,34),"添加玩家当前位置"))Action(()=>AddRoute(Singleton<GameWorld>.Instance!.MainPlayer.Position));if(InterfaceMenu.Button(new Rect(x+half+12,y,half,34),"添加准星地面点"))Action(()=>{var cam=CameraManager.Instance?.Camera??Camera.main;if(cam==null||!Physics.Raycast(cam.ViewportPointToRay(new(.5f,.5f)),out var hit,2000,LayerMask.GetMask("HighPolyCollider","Terrain","LowPolyCollider")))throw new ArgumentException("未指向地面。");AddRoute(hit.point);});y+=46;
  if(InterfaceMenu.Button(new Rect(x,y,half,32),"删除最后一点")&&route.Count>0)route.RemoveAt(route.Count-1);if(InterfaceMenu.Button(new Rect(x+half+12,y,half,32),"清空路线"))route.Clear();y+=44;
  foreach(var point in route){InterfaceMenu.Label(new Rect(x,y,w,24),$"{point.X:F1}, {point.Y:F1}, {point.Z:F1}",null,12);y+=25;}
  InterfaceMenu.Paragraph(new Rect(x,y,w,80),notice+"\n路线和跟随由导航寻路；走不通时停止。禁止移动优先于路线／跟随，静止跳跃仍可执行。路线、跟随和跳跃会接管 AI 行动以便拍摄。",InterfaceMenu.Accent,12);GUI.EndScrollView();}
}
