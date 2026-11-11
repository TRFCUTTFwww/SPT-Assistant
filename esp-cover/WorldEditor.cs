using System;
using System.Linq;
using System.Text.Json;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.InputSystem;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class WorldEditor {
 static ConfigEntry<bool>? enabled;
 static readonly MenuInputGate gate=new();
 static LiveActorReference? actor;static LootReference? loot;
 static SceneTemplate? aiCopy;static ItemCopy? itemCopy;
 static IntPtr world;static int frame=-1,moveFrame;static string dialog="",notice="";
 static bool moving,wasPaused,cursorOwned,oldVisible;static CursorLockMode oldLock;
 static Vector3 origin,point;static Quaternion rotation;static float distance=5;
 static Rigidbody? body;static bool kinematic,gravity;static Vector3 velocity,angular;
 internal static bool Active=>enabled?.Value==true&&RaidClock.Active&&Singleton<GameWorld>.Instance?.MainPlayer?.HealthController?.IsAlive==true;
 internal static bool InputBlocked=>gate.Blocked;
 internal static bool ModalOpen=>dialog.Length>0;
 internal static bool Holding(Player player)=>moving&&actor?.Pointer==player.Pointer;
 internal static bool MovementCommand(ECommand command)=>command is ECommand.ToggleSpeed or ECommand.DecreaseWalkSpeed or ECommand.ToggleDuck or ECommand.PressSprinting or ECommand.ToggleSprinting or ECommand.EndSprinting or ECommand.ToggleProne or ECommand.NextWalkPose or ECommand.PreviousWalkPose or ECommand.ToggleWalk or ECommand.EndWalk or ECommand.Jump or ECommand.ToggleLeanRight or ECommand.ToggleLeanLeft or ECommand.EndLeanLeft or ECommand.EndLeanRight or ECommand.RestorePose;
 internal static void Initialize(){enabled=FeatureConfig.Bind("63. 场景编辑","Editor mode","编辑模式",false,"右键打开场景菜单；移动时左键放下，右键或 Esc 取消，滚轮调整距离。");}
 static GameWorld Current(){var current=Singleton<GameWorld>.Instance;if(!Active||current==null)throw new ArgumentException("请进入战局且保持存活。");return current;}
 static Player Actor(){var value=actor?.Resolve()??throw new ArgumentException("请先选择 AI。");if(value.IsYourPlayer||value.AIData?.BotOwner==null||value.Profile?.Info?.Settings?.Role==WildSpawnType.shooterBTR)throw new ArgumentException("只允许编辑本机控制的普通 AI。");return value;}
 static void Run(Action work){try{work();}catch(Exception e){notice=e.Message;FeatureToasts.Show(notice);}}
 static void Modal(string value){if(!cursorOwned){oldLock=Cursor.lockState;oldVisible=Cursor.visible;cursorOwned=true;}dialog=value;gate.SetOpen(true,Time.frameCount);scroll=Vector2.zero;KeepCursor();}
 static void CloseDialog(){ReleasePropertyEditors();dialog="";gate.SetOpen(false,Time.frameCount);if(cursorOwned){Cursor.lockState=oldLock;Cursor.visible=oldVisible;cursorOwned=false;}}
 internal static void KeepCursor(){if(cursorOwned){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}}
 internal static void Suspend(){if(moving)Run(()=>FinishMove(true));if(dialog.Length>0)CloseDialog();}
 internal static void Reset(){Run(()=>FinishMove(true));CloseDialog();gate.Reset();actor=null;loot=null;aiCopy=null;itemCopy=null;world=IntPtr.Zero;frame=-1;}
 internal static void CaptureInput(){
  if(frame==Time.frameCount)return;frame=Time.frameCount;
  gate.Tick(frame,Input.GetMouseButton(0)||Input.GetMouseButton(1)||Input.GetMouseButton(2));
  var current=Singleton<GameWorld>.Instance;
  if(!Active||current==null||world!=IntPtr.Zero&&world!=current.Pointer){if(world!=IntPtr.Zero||moving||dialog.Length>0)Reset();return;}world=current.Pointer;
  if(InterfaceMenu.Open){Suspend();return;}
  if(!Application.isFocused){Suspend();return;}
  try{
   if(Input.GetKeyDown(KeyCode.Escape)){if(moving)FinishMove(true);else if(dialog.Length>0)CloseDialog();return;}
   if(dialog.Length>0){KeepCursor();if(dialog=="context")ContextInput();return;}
   if(moving){
    if(Input.GetMouseButtonDown(1)){FinishMove(true);return;}
    if(gate.Blocked||Time.frameCount<=moveFrame)return;
    distance=EditorItemRules.Distance(distance,Input.mouseScrollDelta.y);var camera=EFT.CameraControl.CameraManager.Instance?.Camera??throw new ArgumentException("镜头尚未就绪。");var ray=camera.ViewportPointToRay(new Vector3(.5f,.5f));point=ray.GetPoint(distance);
    if(actor!=null){var target=Actor();AiTestControl.SetPaused(new[]{target},true);target.Teleport(point,true);}else{var target=loot!.Resolve();target.transform.position=point;if(body!=null)body.position=point;}
    if(Input.GetMouseButtonDown(0))FinishMove(false);return;
   }
   if(!gate.Blocked&&!Cursor.visible&&!DirectorStage.FreeActive&&Input.GetMouseButtonDown(1))Select(current);
  }catch(Exception error){notice=error.Message;Run(()=>FinishMove(true));CloseDialog();FeatureToasts.Show(notice);}
 }
 static void Select(GameWorld current){
  var local=current.MainPlayer;var camera=EFT.CameraControl.CameraManager.Instance?.Camera??throw new ArgumentException("镜头尚未就绪。");var ray=camera.ViewportPointToRay(new Vector3(.5f,.5f));var contact=CrosshairProbe.Read(current,local,ray,100);actor=null;loot=null;
  if(contact.State==CrosshairState.Overflow)throw new ArgumentException("目标过于复杂，请换一个位置重新选择。");
  point=contact.State==CrosshairState.Empty?ray.GetPoint(5):contact.Point;distance=contact.Distance>0?contact.Distance:5;
  if(contact.Actor!=null&&contact.Actor.AIData?.BotOwner!=null&&contact.Actor.Profile?.Info?.Settings?.Role!=WildSpawnType.shooterBTR)actor=new(current,contact.Actor);
  float nearest=contact.State==CrosshairState.Empty?100:contact.Distance+.06f;
  for(int i=0;i<current.LootItems.Count;i++){var value=current.LootItems.GetByIndex(i);if(value==null||value.Item==null||value.TryCast<Corpse>()!=null)continue;var collider=value.BoundCollider;if(collider==null||!collider.enabled||!collider.Raycast(ray,out var hit,nearest))continue;nearest=hit.distance;loot=new(current,value);actor=null;point=hit.point;distance=hit.distance;}
  current.MainPlayer.HandsController?.TryCast<Player.FirearmController>()?.SetTriggerPressed(false);OpenContext();
 }
 static void BeginMove(){
  if(actor!=null){var target=Actor();if(ModelPoseLock.Locked(target)||SceneTemplates.IsStaging(target))throw new ArgumentException("请先解除动作锁定或等待场景载入完成。");origin=target.Position;rotation=target.transform.rotation;wasPaused=AiTestControl.IsPaused(target);AiTestControl.SetPaused(new[]{target},true);}
  else{var target=loot!.Resolve();origin=target.transform.position;rotation=target.transform.rotation;body=target.RigidBody;if(body!=null){kinematic=body.isKinematic;gravity=body.useGravity;velocity=body.velocity;angular=body.angularVelocity;body.isKinematic=true;body.useGravity=false;}}
  moving=true;moveFrame=Time.frameCount;distance=EditorItemRules.Distance(distance,0);CloseDialog();
 }
 static void FinishMove(bool cancel){
  if(!moving)return;
  try{
   if(actor!=null){var target=Actor();try{if(cancel)target.Teleport(origin,true);else TeleportControl.DirectorMove(new[]{target},point);}catch{target.Teleport(origin,true);throw;}finally{AiTestControl.SetPaused(new[]{target},wasPaused);}}
   else{var target=loot!.Resolve();if(cancel){target.transform.SetPositionAndRotation(origin,rotation);if(body!=null)body.position=origin;}}
  }finally{if(body!=null)try{body.isKinematic=kinematic;body.useGravity=gravity;if(!kinematic){body.velocity=cancel?velocity:Vector3.zero;body.angularVelocity=cancel?angular:Vector3.zero;}}catch{}body=null;moving=false;gate.SetOpen(false,Time.frameCount);}
 }
 static void Copy(){if(actor!=null){var target=Actor();var snapshot=SceneTemplates.Capture(target,target.Profile.Nickname);aiCopy=JsonSerializer.Deserialize<SceneTemplate>(JsonSerializer.Serialize(snapshot))!;itemCopy=null;}else{itemCopy=CaptureItem(loot!.Resolve().Item);aiCopy=null;}notice="已复制独立快照。";CloseDialog();}
 static void Paste(){if(aiCopy!=null)SceneTemplates.Spawn(Current(),point,aiCopy with{Id=Guid.NewGuid().ToString("N"),Count=1});else if(itemCopy!=null)PlaceItem(CloneItem(itemCopy));CloseDialog();}
 static void Delete(){if(actor!=null)AiActorControl.Delete(new[]{Actor()});else{var value=loot!.Resolve();Current().DestroyLoot(value.Cast<IKillable>());}CloseDialog();}
 static void Kill(){var target=Actor();target.ActiveHealthController.Kill(EDamageType.Bullet);if(target.HealthController.IsAlive)throw new ArgumentException("AI 仍然存活，请检查无敌或伤害保护设置。");CloseDialog();}
}
