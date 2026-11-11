using System;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.InputSystem;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class WorldEditor {
 static ConfigEntry<bool>? enabled;
 static readonly MenuInputGate gate=new();
 static LiveActorReference? actor;static LootReference? loot;
 static IntPtr world;static int frame=-1,moveFrame,dialogFrame;static string dialog="",notice="";
 static bool moving,cursorOwned,oldVisible;static CursorLockMode oldLock;
 static Vector3 point;static float distance=5;
 internal static bool Active=>enabled?.Value==true&&RaidClock.Active&&Singleton<GameWorld>.Instance?.MainPlayer?.HealthController?.IsAlive==true;
 internal static bool InputBlocked=>gate.Blocked;
 internal static bool ModalOpen=>dialog.Length>0||altCursor||dragging;
 internal static bool Holding(Player player)=>moving&&moveActors.Any(value=>value.Reference.Pointer==player.Pointer&&value.Reference.Profile==player.ProfileId);
 internal static bool MovementCommand(ECommand command)=>command is ECommand.ToggleSpeed or ECommand.DecreaseWalkSpeed or ECommand.ToggleDuck or ECommand.PressSprinting or ECommand.ToggleSprinting or ECommand.EndSprinting or ECommand.ToggleProne or ECommand.NextWalkPose or ECommand.PreviousWalkPose or ECommand.ToggleWalk or ECommand.EndWalk or ECommand.Jump or ECommand.ToggleLeanRight or ECommand.ToggleLeanLeft or ECommand.EndLeanLeft or ECommand.EndLeanRight or ECommand.RestorePose;
 internal static void Initialize(){enabled=FeatureConfig.Bind("63. 场景编辑","Editor mode","编辑模式",false,"按住 Alt 显示鼠标，左键拖框多选，右键编辑；移动时左键放下，右键或 Esc 取消，滚轮调整距离。");}
 static GameWorld Current(){var current=Singleton<GameWorld>.Instance;if(!Active||current==null)throw new ArgumentException("请进入战局且保持存活。");return current;}
 static Player Actor(){var value=actor?.Resolve()??throw new ArgumentException("请先选择 AI。");if(!Editable(value))throw new ArgumentException("只允许编辑本机控制的普通 AI。");return value;}
 static void Run(Action work){try{work();}catch(Exception e){notice=e.Message;FeatureToasts.Show(notice);}}
 static void Modal(string value){dialog=value;dialogFrame=Time.frameCount;scroll=Vector2.zero;SyncCursor();}
 static void CloseDialog(){ReleasePropertyEditors();dialog="";altCursor=!moving&&altCursor&&AltDown;SyncCursor();}
 internal static void KeepCursor(){if(cursorOwned){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}}
 internal static void Suspend(){if(moving)Run(()=>FinishMove(true));altCursor=false;dragging=false;CloseDialog();}
 internal static void Reset(){Suspend();gate.Reset();ClearSelection();LootReference.ClearCache();clipboard=Array.Empty<ClipboardEntry>();world=IntPtr.Zero;frame=-1;}
 internal static void CaptureInput(){
  if(frame==Time.frameCount)return;frame=Time.frameCount;
  gate.Tick(frame,Input.GetMouseButton(0)||Input.GetMouseButton(1)||Input.GetMouseButton(2));
  var current=Singleton<GameWorld>.Instance;
  if(!Active||current==null||world!=IntPtr.Zero&&world!=current.Pointer){if(world!=IntPtr.Zero||moving||dialog.Length>0||cursorOwned)Reset();return;}world=current.Pointer;
  if(InterfaceMenu.Open||!Application.isFocused){Suspend();return;}
  try{
   if(Input.GetKeyDown(KeyCode.Escape)){if(moving)FinishMove(true);else if(dialog.Length>0)CloseDialog();else ClearSelection();return;}
   if(dialog.Length>0){KeepCursor();if(dialog=="context")ContextInput();return;}
   if(moving){
    if(Input.GetMouseButtonDown(1)){FinishMove(true);return;}
    if(gate.Blocked||Time.frameCount<=moveFrame)return;
    distance=EditorItemRules.Distance(distance,Input.mouseScrollDelta.y);point=PointerRay(false).GetPoint(distance);PreviewMove();
    if(Input.GetMouseButtonDown(0))FinishMove(false);return;
   }
   SelectionInput(current);
  }catch(Exception error){notice=error.Message;Run(()=>FinishMove(true));altCursor=false;dragging=false;CloseDialog();FeatureToasts.Show(notice);}
 }
}
