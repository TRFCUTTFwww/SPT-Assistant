using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.Interactive;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class WorldEditor {
 enum SelectionKind { Ai,Items }
 static readonly List<LiveActorReference> selectedActors=new();
 static readonly List<LootReference> selectedItems=new();
 static SelectionKind selectionKind;static int selectionIndex;
 static bool altCursor,dragging;static Vector2 dragStart,dragEnd;
 static bool HasSelection=>selectedActors.Count+selectedItems.Count>0;
 static int SelectionCount=>selectionKind==SelectionKind.Ai?selectedActors.Count:selectedItems.Count;
 static bool AltDown=>Input.GetKey(KeyCode.LeftAlt)||Input.GetKey(KeyCode.RightAlt);
 static bool Editable(Player? value)=>value!=null&&!value.IsYourPlayer&&value.HealthController?.IsAlive==true&&value.AIData?.BotOwner!=null&&value.Profile?.Info?.Settings?.Role!=WildSpawnType.shooterBTR;
 static void ClearSelection(){selectedActors.Clear();selectedItems.Clear();actor=null;loot=null;selectionIndex=0;dragging=false;}
 static void PruneSelection(){selectedActors.RemoveAll(reference=>{try{return !Editable(reference.Resolve());}catch{return true;}});selectedItems.RemoveAll(reference=>{try{reference.Resolve();return false;}catch{return true;}});}
 static void TargetAt(int index){selectionIndex=Math.Clamp(index,0,Math.Max(0,SelectionCount-1));actor=selectionKind==SelectionKind.Ai&&selectedActors.Count>0?selectedActors[selectionIndex]:null;loot=selectionKind==SelectionKind.Items&&selectedItems.Count>0?selectedItems[selectionIndex]:null;}
 static Player[] SelectedActors()=>selectedActors.Select(reference=>{var value=reference.Resolve();if(!Editable(value))throw new ArgumentException("只允许编辑本机控制的普通 AI。");return value;}).ToArray();
 static Camera View=>EFT.CameraControl.CameraManager.Instance?.Camera??throw new ArgumentException("镜头尚未就绪。");
 static Ray PointerRay(bool mouse){var camera=View;if(!mouse)return camera.ViewportPointToRay(new Vector3(.5f,.5f));var rect=camera.rect;if(rect.width<=0||rect.height<=0)throw new ArgumentException("镜头尚未就绪。");return camera.ViewportPointToRay(new Vector3((Input.mousePosition.x/Screen.width-rect.x)/rect.width,(Input.mousePosition.y/Screen.height-rect.y)/rect.height));}
 static void ReadPoint(GameWorld current,Ray ray){var hit=CrosshairProbe.Read(current,current.MainPlayer,ray,100);if(hit.State==CrosshairState.Overflow)throw new ArgumentException("目标过于复杂，请换一个位置重新选择。");point=hit.State==CrosshairState.Empty?ray.GetPoint(5):hit.Point;distance=hit.Distance>0?hit.Distance:5;}
 static void SyncCursor(){
  bool wanted=dialog.Length>0||altCursor;
  if(wanted&&!cursorOwned){oldLock=Cursor.lockState;oldVisible=Cursor.visible;cursorOwned=true;}
  if(gate.Open!=wanted)gate.SetOpen(wanted,Time.frameCount);
  if(wanted)KeepCursor();else if(cursorOwned){Cursor.lockState=oldLock;Cursor.visible=oldVisible;cursorOwned=false;}
 }
 static void SelectionInput(GameWorld current){
  bool nextAlt=AltDown&&!DirectorStage.FreeActive&&(altCursor||!Cursor.visible);
  if(nextAlt!=altCursor){altCursor=nextAlt;if(!altCursor)dragging=false;SyncCursor();}
  if(altCursor){
   if(Input.GetMouseButtonDown(0)){dragging=true;dragStart=dragEnd=MousePosition;}
   if(dragging){dragEnd=MousePosition;if(Input.GetMouseButtonUp(0)){dragging=false;SelectBox(current);}}
   if(Input.GetMouseButtonDown(1)){dragging=false;PruneSelection();if(HasSelection)ReadPoint(current,PointerRay(true));else SelectAt(current,PointerRay(true));OpenContext();}
   return;
  }
  if(!gate.Blocked&&!Cursor.visible&&!DirectorStage.FreeActive&&Input.GetMouseButtonDown(1)){PruneSelection();if(HasSelection)ReadPoint(current,PointerRay(false));else SelectAt(current,PointerRay(false));OpenContext();}
 }
 static Vector3 ActorSelectionPoint(Player value)=>value.PlayerBones?.Pelvis?.Original?.position??value.Position+Vector3.up*.9f;
 static bool InBox(Camera camera,Vector3 position,EditorSelectionBox box){var p=OverlayProjection.ScreenPoint(camera,position);if(!EditorSelectionRules.Contains(box,p.x,Screen.height-p.y,p.z,Screen.width,Screen.height))return false;int mask=LayerMask.GetMask("HighPolyCollider","Terrain","LowPolyCollider");var delta=position-camera.transform.position;float length=delta.magnitude;return mask!=0&&(length<=.15f||!Physics.Raycast(camera.transform.position,delta/length,Math.Max(0,length-.15f),mask,QueryTriggerInteraction.Ignore));}
 static void SelectBox(GameWorld current){
  var start=dragStart;var end=dragEnd;
  if((end-start).sqrMagnitude<25){SelectAt(current,PointerRay(true));return;}
  ClearSelection();OverlayProjection.BeginGui();var box=EditorSelectionRules.Box(start.x,start.y,end.x,end.y);var view=View;
  var all=current.AllAlivePlayersList;if(all!=null)for(int i=0;i<all.Count;i++){var value=all[i];if(Editable(value)&&InBox(view,ActorSelectionPoint(value),box))selectedActors.Add(new(current,value));}
  for(int i=0;i<current.LootItems.Count;i++){var value=current.LootItems.GetByIndex(i);if(value?.Item==null||value.TryCast<Corpse>()!=null||!value.gameObject.activeInHierarchy)continue;var center=value.BoundCollider?.bounds.center??value.transform.position;if(InBox(view,center,box))selectedItems.Add(new(current,value));}
  selectionKind=selectedActors.Count>0?SelectionKind.Ai:SelectionKind.Items;TargetAt(0);
  notice=I18n.Text("已选择")+" AI ×"+selectedActors.Count+" / "+I18n.Text("物品")+" ×"+selectedItems.Count;
 }
 static void SelectAt(GameWorld current,Ray ray){
  ClearSelection();ReadPoint(current,ray);var contact=CrosshairProbe.Read(current,current.MainPlayer,ray,100);
  if(Editable(contact.Actor))actor=new(current,contact.Actor!);
  float nearest=contact.State==CrosshairState.Empty?100:contact.Distance+.06f;
  for(int i=0;i<current.LootItems.Count;i++){var value=current.LootItems.GetByIndex(i);if(value?.Item==null||value.TryCast<Corpse>()!=null)continue;var collider=value.BoundCollider;if(collider==null||!collider.enabled||!collider.Raycast(ray,out var hit,nearest))continue;nearest=hit.distance;loot=new(current,value);actor=null;point=hit.point;distance=hit.distance;}
  if(actor!=null){selectedActors.Add(actor);selectionKind=SelectionKind.Ai;}if(loot!=null){selectedItems.Add(loot);selectionKind=SelectionKind.Items;}TargetAt(0);
  current.MainPlayer.HandsController?.TryCast<Player.FirearmController>()?.SetTriggerPressed(false);
 }
 static void DrawSelection(){
  OverlayProjection.BeginGui();
  void Mark(Vector3 position,string name,Color color){var p=OverlayProjection.ScreenPoint(View,position);if(p.z<=0||p.x<0||p.x>Screen.width||p.y<0||p.y>Screen.height)return;float x=p.x,y=Screen.height-p.y;InterfaceMenu.Surface(new Rect(x-7,y-7,14,2),color,0);InterfaceMenu.Surface(new Rect(x-7,y+5,14,2),color,0);InterfaceMenu.Surface(new Rect(x-7,y-7,2,14),color,0);InterfaceMenu.Surface(new Rect(x+5,y-7,2,14),color,0);InterfaceMenu.Label(new Rect(x+10,y-12,220,24),name,color,12);}
  foreach(var reference in selectedActors)try{var value=reference.Resolve();Mark(ActorSelectionPoint(value),value.Profile.Nickname,InterfaceMenu.Accent);}catch{}
  foreach(var reference in selectedItems)try{var value=reference.Resolve();Mark(value.BoundCollider?.bounds.center??value.transform.position,ItemSpawner.TemplateName(value.Item.TemplateId.ToString()),new Color(.4f,.9f,.65f));}catch{}
  if(dragging){var b=EditorSelectionRules.Box(dragStart.x,dragStart.y,dragEnd.x,dragEnd.y);var rect=new Rect(b.X,b.Y,b.Width,b.Height);var color=InterfaceMenu.Accent;InterfaceMenu.Surface(rect,new Color(color.r,color.g,color.b,.14f),0);InterfaceMenu.Surface(new Rect(rect.x,rect.y,rect.width,1),color,0);InterfaceMenu.Surface(new Rect(rect.x,rect.yMax-1,rect.width,1),color,0);InterfaceMenu.Surface(new Rect(rect.x,rect.y,1,rect.height),color,0);InterfaceMenu.Surface(new Rect(rect.xMax-1,rect.y,1,rect.height),color,0);}
  if(HasSelection)InterfaceMenu.Label(new Rect(24,Screen.height-84,Math.Max(1,Screen.width-48),30),I18n.Text("已选择")+" AI ×"+selectedActors.Count+" / "+I18n.Text("物品")+" ×"+selectedItems.Count+" · "+I18n.Text("右键编辑 · Ctrl+C 复制 · Ctrl+V 粘贴 · Del 删除 · Esc 清空"),InterfaceMenu.Accent,13);
 }
 static void DrawTargetNavigation(float x,ref float y,float width){
  if(SelectionCount<2)return;
  InterfaceMenu.Label(new Rect(x,y,width-156,30),I18n.Text("当前对象")+" "+(selectionIndex+1)+" / "+SelectionCount,null,12);
  if(InterfaceMenu.Button(new Rect(x+width-148,y,70,28),"上一项"))Run(()=>SwitchTarget(-1));
  if(InterfaceMenu.Button(new Rect(x+width-70,y,70,28),"下一项"))Run(()=>SwitchTarget(1));y+=38;
 }
 static void SwitchTarget(int direction){
  ReleasePropertyEditors();TargetAt((selectionIndex+direction+SelectionCount)%SelectionCount);
  if(dialog=="rename")rename=Actor().Profile.Nickname;else if(dialog=="properties")ReadProperties();scroll=Vector2.zero;notice="";
 }
 internal static void FilterMotion(Il2CppObjectBase controller,ref Vector3 motion){if(!moving)return;foreach(var value in moveActors)if(value.Player.CharacterController?.Pointer==controller.Pointer&&value.Reference.World==Comfort.Common.Singleton<GameWorld>.Instance?.Pointer&&value.Reference.Profile==value.Player.ProfileId){motion=Vector3.zero;return;}}
}
