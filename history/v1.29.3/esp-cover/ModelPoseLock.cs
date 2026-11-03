using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class ModelPoseLock {
 readonly record struct Bone(Transform Transform,Vector3 Position,Quaternion Rotation);
 readonly record struct Change(Transform Transform,Vector3 Position,Quaternion Rotation,Vector3 AppliedPosition,Quaternion AppliedRotation);
 sealed class Frozen {
  internal Player Actor=null!;internal string Id="";internal IntPtr World,Pointer;internal Transform? Root;
  internal bool Ready,Ai,WasPaused;internal Vector3 Offset;internal Quaternion Rotation;
  internal readonly List<Bone> Bones=new();internal readonly List<Change> Changes=new();
 }
 static readonly Dictionary<IntPtr,Frozen> frozen=new();
 static ConfigEntry<KeyCode> playerLock=null!,playerUnlock=null!,aiLock=null!,aiUnlock=null!;
 static Vector2 scroll;static string notice="先调整动作和朝向，再点击锁定。";static float nextError;
 internal static void Initialize(){
  const string section="43. 娱乐";
  playerLock=FeatureConfig.Bind(section,"Player pose lock key","玩家动作锁定键",KeyCode.None,"锁定当前模型动作及世界朝向；默认不占用按键。");
  playerUnlock=FeatureConfig.Bind(section,"Player pose unlock key","玩家动作解除键",KeyCode.None,"解除玩家模型动作锁定。");
  aiLock=FeatureConfig.Bind(section,"Selected AI pose lock key","所选 AI 动作锁定键",KeyCode.None,"锁定 AI 列表勾选的角色；无勾选时使用查看／修改的角色。同时暂停其行为，解除后恢复此前暂停状态。");
  aiUnlock=FeatureConfig.Bind(section,"Selected AI pose unlock key","所选 AI 动作解除键",KeyCode.None,"仅解除当前所选 AI 的动作锁定；不影响其他 AI 或原有暂停设置。");
 }
 internal static bool Locked(Player actor)=>frozen.TryGetValue(actor.Pointer,out var entry)&&entry.Ready;
 internal static bool PreviousPaused(Player actor)=>frozen.TryGetValue(actor.Pointer,out var entry)&&entry.Ai?entry.WasPaused:AiTestControl.IsPaused(actor);
 internal static SceneLockedPose? Export(Player actor){if(!frozen.TryGetValue(actor.Pointer,out var entry)||!entry.Ready||entry.Root==null)return null;string Path(Transform bone){var names=new List<string>();for(var current=bone;current!=null&&current.Pointer!=entry.Root.Pointer;current=current.parent)names.Add(current.name);names.Reverse();return string.Join("/",names);}var q=entry.Rotation;return new(new(entry.Offset.x,entry.Offset.y,entry.Offset.z),q.x,q.y,q.z,q.w,entry.Bones.Select(p=>new SceneBonePose(Path(p.Transform),new(p.Position.x,p.Position.y,p.Position.z),p.Rotation.x,p.Rotation.y,p.Rotation.z,p.Rotation.w)).ToArray());}
 internal static void Import(Player actor,SceneLockedPose value){DirectorRules.ValidateLocked(value);if(!Request(actor))throw new ArgumentException("模型姿势未能准备。");var entry=frozen[actor.Pointer];var root=entry.Root!;try{if(value.Bones==null||value.Bones.Length>2048)throw new ArgumentException("姿势骨骼数量无效。");foreach(var pose in value.Bones){var bone=pose.Path.Length==0?root:root.Find(pose.Path);if(bone!=null)entry.Bones.Add(new(bone,new(pose.Position.X,pose.Position.Y,pose.Position.Z),Quaternion.Normalize(new(pose.X,pose.Y,pose.Z,pose.W))));}entry.Offset=new(value.Offset.X,value.Offset.Y,value.Offset.Z);entry.Rotation=Quaternion.Normalize(new(value.X,value.Y,value.Z,value.W));entry.Ready=true;}catch{Release(actor,false);throw;}}
 static Transform? Root(Player actor){var root=actor.PlayerBones?.RootJoint;if(root!=null&&root.Pointer!=actor.transform.Pointer&&root.IsChildOf(actor.transform))return root;var pelvis=actor.PlayerBones?.Pelvis?.Original;return pelvis!=null&&pelvis.Pointer!=actor.transform.Pointer&&pelvis.IsChildOf(actor.transform)?pelvis:null;}
 static bool Pressed(ConfigEntry<KeyCode> key)=>key.Value!=KeyCode.None&&Input.GetKeyDown(key.Value);
 internal static void Update(GameWorld current,Player local){
  foreach(var entry in frozen.Values.ToArray()){
   try{if(entry.World!=current.Pointer||entry.Actor==null||entry.Actor.ProfileId!=entry.Id||entry.Actor.HealthController?.IsAlive!=true||entry.Ready&&(entry.Root==null||Root(entry.Actor)?.Pointer!=entry.Root.Pointer))ReleaseEntry(entry,true);}
   catch{ReleaseEntry(entry,false);}
  }
  if(InterfaceMenu.InputBlocked||Cursor.visible)return;
  if(Pressed(playerUnlock))Release(local);else if(Pressed(playerLock))Request(local);
  if(Pressed(aiUnlock))Selected(false);else if(Pressed(aiLock))Selected(true);
 }
 static void Say(string text){notice=text;FeatureToasts.Show(text);}
 internal static bool Request(Player actor){
  try{
   var current=Singleton<GameWorld>.Instance;
   if(!RaidClock.Active||current==null||actor==null||actor.HealthController?.IsAlive!=true)throw new InvalidOperationException("角色未就绪，请进入战局后重试。");
   bool ai=!actor.IsYourPlayer&&(actor.IsAI||actor.AIData?.BotOwner!=null);
   if(!actor.IsYourPlayer&&!ai)throw new InvalidOperationException("只能锁定本机玩家或选中的 AI。");
   var all=current.AllAlivePlayersList;bool present=false;if(all!=null)for(int i=0;i<all.Count;i++)if(all[i]?.Pointer==actor.Pointer&&all[i].ProfileId==actor.ProfileId){present=true;break;}
   if(!present)throw new InvalidOperationException("角色已离开当前战局，请重新选择。");
   var root=Root(actor);if(root==null)throw new InvalidOperationException("人物可视骨架尚未就绪。");
   Release(actor,false);
   var entry=new Frozen{Actor=actor,Id=actor.ProfileId,World=current.Pointer,Pointer=actor.Pointer,Root=root,Ai=ai,WasPaused=ai&&AiTestControl.IsPaused(actor)};
   frozen[actor.Pointer]=entry;
   if(ai)AiTestControl.SetPaused(new[]{actor},true);
   Say((ai?actor.Profile?.Nickname??"AI":"玩家")+"：正在记录当前动作");
   return true;
  }catch(Exception error){Say(error.Message);return false;}
 }
 internal static void Release(Player? actor,bool notify=true){
  if(actor==null||!frozen.TryGetValue(actor.Pointer,out var entry))return;
  ReleaseEntry(entry,true);if(notify)Say((entry.Ai?entry.Id==actor.ProfileId?actor.Profile?.Nickname??"AI":"AI":"玩家")+"：动作锁定已解除");
 }
 static void ReleaseEntry(Frozen entry,bool restorePause){
  Restore(entry);frozen.Remove(entry.Pointer);
  if(restorePause&&entry.Ai)try{if(Singleton<GameWorld>.Instance?.Pointer==entry.World&&entry.Actor?.ProfileId==entry.Id&&entry.Actor.HealthController?.IsAlive==true)AiTestControl.SetPaused(new[]{entry.Actor},entry.WasPaused);}catch{}
  entry.Bones.Clear();
 }
 internal static void Selected(bool value){
  var targets=AiTestControl.PoseSelection;
  if(targets.Length==0){Say("请先在 AI 管理中勾选角色，或点击查看／修改。");return;}
  int done=0;var failures=new List<string>();foreach(var actor in targets){if(value){if(Request(actor))done++;else failures.Add(notice);}else if(frozen.ContainsKey(actor.Pointer)){Release(actor,false);done++;}}
  Say((value?$"已请求锁定 {done}／{targets.Length} 名所选 AI 的动作":$"已解除 {done} 名所选 AI 的动作锁定")+(failures.Count>0?"："+string.Join("；",failures.Distinct().Take(2)):""));
 }
 internal static void Restore(Player actor){if(frozen.TryGetValue(actor.Pointer,out var entry))Restore(entry);}
 static void Restore(Frozen entry){
  for(int i=entry.Changes.Count-1;i>=0;i--)try{var pose=entry.Changes[i];var bone=pose.Transform;if(bone==null)continue;if(Quaternion.Angle(bone.localRotation,pose.AppliedRotation)<.01f)bone.localRotation=pose.Rotation;if((bone.localPosition-pose.AppliedPosition).sqrMagnitude<.00000001f)bone.localPosition=pose.Position;}catch{}
  entry.Changes.Clear();
 }
 internal static void RestoreAll(){foreach(var entry in frozen.Values)Restore(entry);}
 internal static void Apply(Player actor){
  if(!frozen.TryGetValue(actor.Pointer,out var entry))return;
  try{
   if(actor.HealthController?.IsAlive!=true||actor.ProfileId!=entry.Id||Singleton<GameWorld>.Instance?.Pointer!=entry.World){ReleaseEntry(entry,true);return;}
   var root=entry.Root;if(root==null||Root(actor)?.Pointer!=root.Pointer){ReleaseEntry(entry,true);return;}
   if(!entry.Ready){
    // Capture only the visual skeleton. Never freeze the player/controller transform.
    // The root's WORLD orientation is saved so turning the parent or camera
    // cannot turn the frozen model; position continues following the actor.
    foreach(var bone in root.GetComponentsInChildren<Transform>(true))if(bone!=null)entry.Bones.Add(new(bone,bone.localPosition,bone.localRotation));
    if(entry.Bones.Count==0)throw new InvalidOperationException("未找到可锁定的模型骨骼。");
    entry.Offset=root.position-actor.Position;entry.Rotation=root.rotation;entry.Ready=true;
    Say((entry.Ai?actor.Profile?.Nickname??"AI":"玩家")+"：动作及朝向已锁定");
   }
   Restore(entry);
   foreach(var pose in entry.Bones){var bone=pose.Transform;if(bone==null)continue;var position=bone.localPosition;var rotation=bone.localRotation;bone.localPosition=pose.Position;bone.localRotation=pose.Rotation;if(bone.Pointer==root.Pointer)bone.SetPositionAndRotation(actor.Position+entry.Offset,entry.Rotation);entry.Changes.Add(new(bone,position,rotation,bone.localPosition,bone.localRotation));}
  }catch(Exception error){ReleaseEntry(entry,true);if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+10;Say("动作锁定已解除："+error.Message);}}
 }
 internal static void ApplyAll(){foreach(var entry in frozen.Values.ToArray())Apply(entry.Actor);}
 internal static void Reset(){foreach(var entry in frozen.Values.ToArray())ReleaseEntry(entry,true);frozen.Clear();}
 internal static void Draw(Rect rect){
  scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-20,700));float x=8,y=8,w=rect.width-36,half=(w-12)/2;
  InterfaceMenu.Label(new Rect(x,y,w,30),"动作锁定",InterfaceMenu.Accent,21);y+=44;
  InterfaceMenu.Paragraph(new Rect(x,y,w,66),"先摆好动作与朝向，再点击锁定。模型保持锁定时的姿势和世界朝向，玩家位置仍随移动更新。解除后恢复实时动作。",null,13);y+=78;
  var local=Singleton<GameWorld>.Instance?.MainPlayer;bool enabled=GUI.enabled;GUI.enabled=enabled&&RaidClock.Active&&local?.HealthController?.IsAlive==true;
  if(InterfaceMenu.Button(new Rect(x,y,half,38),local!=null&&Locked(local)?"按当前视角重新锁定玩家":"锁定玩家当前模型动作"))Request(local!);
  if(InterfaceMenu.Button(new Rect(x+half+12,y,half,38),"解除玩家动作锁定"))Release(local);y+=52;
  if(InterfaceMenu.Button(new Rect(x,y,half,38),"锁定所选 AI 动作"))Selected(true);
  if(InterfaceMenu.Button(new Rect(x+half+12,y,half,38),"解除所选 AI 动作锁定"))Selected(false);y+=52;GUI.enabled=enabled;
  InterfaceMenu.Paragraph(new Rect(x,y,w,70),"AI 优先使用列表中勾选的角色；无勾选时使用查看／修改的对象。锁定时暂停其行为，解除时恢复此前的暂停设置。换局、死亡或模型更换会清除锁定。",null,12);y+=82;
  InterfaceMenu.DrawEntry(playerLock,new Rect(x,y,half,92));InterfaceMenu.DrawEntry(playerUnlock,new Rect(x+half+12,y,half,92));y+=104;
  InterfaceMenu.DrawEntry(aiLock,new Rect(x,y,half,92));InterfaceMenu.DrawEntry(aiUnlock,new Rect(x+half+12,y,half,92));y+=106;
  InterfaceMenu.Paragraph(new Rect(x,y,w,64),notice+"\n已锁定 "+frozen.Values.Count(entry=>entry.Ready)+" 个模型；按键默认不绑定。",InterfaceMenu.Accent,13);y+=78;
  GUI.enabled=enabled&&frozen.Count>0;if(InterfaceMenu.Button(new Rect(x,y,w,38),"解除全部动作锁定（玩家和 AI）")){Reset();Say("已解除全部模型动作锁定");}GUI.enabled=enabled;GUI.EndScrollView();
 }
}
