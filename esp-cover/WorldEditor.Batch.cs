using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using EFT;
using UnityEngine;
using NVector=System.Numerics.Vector3;
namespace LocalSpt5Esp;
internal static partial class WorldEditor {
 sealed record ClipboardEntry(SceneTemplate? Ai,ItemCopy? Item,Vector3 Position,CorpseCopy? Corpse=null);
 sealed record ActorMove(LiveActorReference Reference,Player Player,Vector3 Origin,bool Paused,bool Pinned);
 sealed record ItemMove(LootReference Reference,Vector3 Origin,Quaternion Rotation,Rigidbody? Body,bool Kinematic,bool Gravity,Vector3 Velocity,Vector3 Angular);
 static ClipboardEntry[] clipboard=Array.Empty<ClipboardEntry>();
 static readonly List<ActorMove> moveActors=new();static readonly List<ItemMove> moveItems=new();
 static Vector3[] moveOrigins=Array.Empty<Vector3>();
 static Vector3[] Translated(Vector3[] source,Vector3 goal)=>Array.ConvertAll(EditorSelectionRules.Translate(Array.ConvertAll(source,v=>new NVector(v.x,v.y,v.z)),new(goal.x,goal.y,goal.z)),v=>new Vector3(v.X,v.Y,v.Z));
 static void BeginMove(){
  // Resolve and validate the full batch before pausing or changing any object.
  var actors=selectionKind==SelectionKind.Ai?SelectedActors():Array.Empty<Player>();
  var items=selectionKind==SelectionKind.Items?selectedItems.Select(r=>(Reference:r,Item:r.Resolve())).ToArray():Array.Empty<(LootReference Reference,EFT.Interactive.LootItem Item)>();
  foreach(var target in actors)if(ModelPoseLock.Locked(target)||SceneTemplates.IsStaging(target))throw new ArgumentException("请先解除动作锁定或等待场景载入完成。");
  if(actors.Length+items.Length+(selectionKind==SelectionKind.Corpses?selectedCorpses.Count:0)==0)throw new ArgumentException("请先选择对象。");
  moveActors.Clear();moveItems.Clear();moveCorpses.Clear();
  foreach(var target in actors)moveActors.Add(new(new(Current(),target),target,target.Position,AiFreePlacement.PriorPaused(target),AiFreePlacement.Pinned(target)));
  foreach(var entry in items){var target=entry.Item;var body=target.RigidBody;moveItems.Add(new(entry.Reference,target.transform.position,target.transform.rotation,body,body?.isKinematic??true,body?.useGravity??false,body?.velocity??Vector3.zero,body?.angularVelocity??Vector3.zero));}
  if(selectionKind==SelectionKind.Corpses)foreach(var reference in selectedCorpses)moveCorpses.Add(CaptureCorpseMove(reference));
  moveOrigins=moveActors.Select(v=>v.Origin).Concat(moveItems.Select(v=>v.Origin)).Concat(moveCorpses.Select(v=>v.Origin)).ToArray();
  point=moveOrigins.Aggregate(Vector3.zero,(sum,v)=>sum+v/moveOrigins.Length);distance=EditorItemRules.Distance(Vector3.Distance(View.transform.position,point),0);
  moving=true;moveFrame=Time.frameCount;altCursor=false;CloseDialog();
  try{foreach(var value in moveCorpses)FreezeCorpse(value);foreach(var value in moveActors)AiTestControl.SetPaused(new[]{value.Reference.Resolve()},true);foreach(var value in moveItems)if(value.Body!=null){value.Body.isKinematic=true;value.Body.useGravity=false;}}
  catch{FinishMove(true);throw;}
 }
 static void PreviewMove(){
  var positions=Translated(moveOrigins,point);int i=0;
  foreach(var value in moveActors){var target=value.Reference.Resolve();if(!AiTestControl.IsPaused(target))AiTestControl.SetPaused(new[]{target},true);target.Teleport(positions[i++],true);}
  foreach(var value in moveItems){var target=value.Reference.Resolve();var pos=positions[i++];target.transform.position=pos;if(value.Body!=null)value.Body.position=pos;}
  foreach(var value in moveCorpses)MoveCorpse(value,positions[i++]);
 }
 static void FinishMove(bool cancel){
  if(!moving)return;var errors=new List<string>();
  // Restoration visits every snapshot even if another entity vanished.
  try{
   int i=0;var positions=cancel?moveOrigins:Translated(moveOrigins,point);
   foreach(var value in moveActors){var goal=positions[i++];try{var target=value.Reference.Resolve();if(cancel){target.Teleport(value.Origin,true);AiTestControl.SetPaused(new[]{target},value.Pinned||value.Paused);}else AiFreePlacement.Place(Current(),target,goal,value.Paused);}catch(Exception e){errors.Add(e.Message);try{var target=value.Reference.Resolve();target.Teleport(value.Origin,true);AiTestControl.SetPaused(new[]{target},value.Pinned||value.Paused);}catch{}}}
   foreach(var value in moveItems){try{i++;if(cancel){var target=value.Reference.Resolve();target.transform.SetPositionAndRotation(value.Origin,value.Rotation);if(value.Body!=null)value.Body.position=value.Origin;}}catch(Exception e){errors.Add(e.Message);}finally{if(value.Body!=null)try{value.Body.isKinematic=value.Kinematic;value.Body.useGravity=value.Gravity;if(!value.Kinematic){value.Body.velocity=cancel?value.Velocity:Vector3.zero;value.Body.angularVelocity=cancel?value.Angular:Vector3.zero;}}catch(Exception e){errors.Add(e.Message);}}}
  foreach(var value in moveCorpses){try{MoveCorpse(value,cancel?value.Origin:positions[i]);}catch(Exception e){errors.Add(e.Message);}finally{RestoreCorpsePhysics(value,cancel);}i++;}
  }finally{moving=false;moveActors.Clear();moveItems.Clear();moveCorpses.Clear();moveOrigins=Array.Empty<Vector3>();gate.SetOpen(false,Time.frameCount);ClearSelection();}
  if(errors.Count>0)throw new ArgumentException(string.Join("；",errors.Distinct().Take(3)));
 }
 static void Copy(){
  ClipboardEntry[] next;
  if(selectionKind==SelectionKind.Ai)next=SelectedActors().Select(target=>{var snapshot=SceneTemplates.Capture(target,target.Profile.Nickname);return new ClipboardEntry(JsonSerializer.Deserialize<SceneTemplate>(JsonSerializer.Serialize(snapshot))!,null,target.Position);}).ToArray();
  else if(selectionKind==SelectionKind.Corpses)next=selectedCorpses.Select(reference=>{var target=reference.Resolve();return new ClipboardEntry(null,null,CorpseDisplay.Position(target),CaptureCorpse(target));}).ToArray();
  else next=selectedItems.Select(reference=>{var target=reference.Resolve();return new ClipboardEntry(null,CaptureItem(target.Item),target.transform.position);}).ToArray();
  if(next.Length==0)throw new ArgumentException("请先选择对象。");clipboard=next;notice=I18n.Text("已复制独立快照。")+" ×"+next.Length;CompleteOperation();
 }
 static void Paste(){
  if(clipboard.Length==0)throw new ArgumentException("剪贴板为空。");var positions=Translated(clipboard.Select(v=>v.Position).ToArray(),point);var current=Current();int count=0;
  try{for(int i=0;i<clipboard.Length;i++){var value=clipboard[i];if(value.Ai!=null)SceneTemplates.Spawn(current,positions[i],value.Ai with{Id=Guid.NewGuid().ToString("N"),Count=1});else if(value.Corpse!=null)PlaceCorpse(value.Corpse,positions[i]);else if(value.Item!=null)PlaceItem(CloneItem(value.Item),positions[i]);count++;}}
  catch(Exception e){throw new ArgumentException(I18n.Text("已提交")+" "+count+" / "+clipboard.Length+"；"+e.Message);}
  CompleteOperation();
 }
 static void Delete(){
  if(selectionKind==SelectionKind.Ai)AiActorControl.Delete(SelectedActors());
  else if(selectionKind==SelectionKind.Corpses){var bodies=selectedCorpses.Select(r=>r.Resolve()).ToArray();foreach(var body in bodies)Current().DestroyLoot(body.Cast<IKillable>());selectedCorpses.Clear();CorpseDisplay.Reset();RaidLoot.Invalidate();}
  else{var items=selectedItems.Select(r=>r.Resolve()).ToArray();var errors=new List<string>();foreach(var value in items)try{Current().DestroyLoot(value.Cast<IKillable>());}catch(Exception e){errors.Add(e.Message);}if(errors.Count>0)throw new ArgumentException(string.Join("；",errors.Distinct()));}
  if(selectionKind==SelectionKind.Ai)selectedActors.Clear();else selectedItems.Clear();actor=null;loot=null;PruneSelection();CompleteOperation();
 }
 static void Kill(){var targets=SelectedActors();var errors=new List<string>();foreach(var target in targets)try{target.ActiveHealthController.Kill(EDamageType.Bullet);if(target.HealthController.IsAlive)throw new ArgumentException("AI 仍然存活，请检查无敌或伤害保护设置。");}catch(Exception e){errors.Add(e.Message);}PruneSelection();if(errors.Count>0)throw new ArgumentException(string.Join("；",errors.Distinct()));CompleteOperation();}
 static void ReleaseAir(){foreach(var target in SelectedActors())AiFreePlacement.Release(target);CompleteOperation();}
}
