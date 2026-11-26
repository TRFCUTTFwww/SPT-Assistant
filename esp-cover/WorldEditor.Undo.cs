using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class WorldEditor {
 sealed record UndoEntry(string Label,Action Apply);
 const int UndoLimit=20;
 static readonly List<UndoEntry> undoStack=new();
 internal static bool CanUndo=>undoStack.Count>0;
 internal static string UndoLabel=>undoStack.Count>0?undoStack[^1].Label:"";
 internal static void ResetUndo()=>undoStack.Clear();
 static void PushUndo(string label,Action apply){if(undoStack.Count>=UndoLimit)undoStack.RemoveAt(0);undoStack.Add(new(label,apply));}
 internal static void Undo(){
  if(undoStack.Count==0)throw new ArgumentException("没有可撤销的操作。");
  var step=undoStack[^1];undoStack.RemoveAt(undoStack.Count-1);
  step.Apply();notice=I18n.Text("已撤销：")+I18n.Text(step.Label);FeatureToasts.Show(notice);
 }
 static Action? CaptureMoveUndo(){
  if(moveActors.Count+moveItems.Count+moveCorpses.Count+moveAirdrops.Count+moveContainers.Count+moveMines.Count==0)return null;
  var mines=moveMines.ToArray();
  var actors=moveActors.Select(v=>(v.Reference,v.Origin,v.Paused,v.Pinned)).ToArray();
  var items=moveItems.Select(v=>(v.Reference,v.Origin,v.Rotation)).ToArray();
  var corpses=moveCorpses.Select(v=>(v.Reference,v.Origin)).ToArray();
  var drops=moveAirdrops.Select(v=>(v.Reference,v.Origin,v.Rotation)).ToArray();
  var containers=moveContainers.Select(v=>(v.Reference,v.Origin,v.Rotation)).ToArray();
  return ()=>{
   var worldNow=Current();
   foreach(var entry in mines){var current=CaptureMineMove(entry.Reference);FreezeMine(current);try{entry.Reference.Resolve().transform.SetPositionAndRotation(entry.Origin,entry.Rotation);}finally{ThawMine(current);}}
   foreach(var entry in actors)try{var target=entry.Reference.Resolve();AiFreePlacement.Place(worldNow,target,entry.Origin,entry.Paused,entry.Pinned);}catch{}
   foreach(var entry in items)try{var item=entry.Reference.Resolve();item.transform.SetPositionAndRotation(entry.Origin,entry.Rotation);if(item.RigidBody!=null){item.RigidBody.position=entry.Origin;item.RigidBody.velocity=Vector3.zero;item.RigidBody.angularVelocity=Vector3.zero;}}catch{}
   foreach(var entry in corpses)try{var move=CaptureCorpseMove(entry.Reference);try{FreezeCorpse(move);MoveCorpse(move,entry.Origin);}finally{RestoreCorpsePhysics(move,true);}}catch{}
   foreach(var entry in drops)try{var drop=entry.Reference.Resolve();AirdropControl.EditorPosition(drop,entry.Origin,entry.Rotation);}catch{}
   foreach(var entry in containers)try{ContainerScene.Root(entry.Reference.Resolve()).SetPositionAndRotation(entry.Origin,entry.Rotation);}catch{}
  };
 }
 static void PushRotateUndo(IEnumerable<ContainerMove> entries){
  var snapshot=entries.Select(v=>(v.Reference,v.Origin,v.Rotation)).ToArray();
  if(snapshot.Length==0)return;
  PushUndo("旋转容器",()=>{Current();foreach(var entry in snapshot)try{ContainerScene.Root(entry.Reference.Resolve()).SetPositionAndRotation(entry.Origin,entry.Rotation);}catch{}});
 }
 static void PushRenameUndo(SelectionKind kind,int index,string previousName){
  if(kind==SelectionKind.Containers){
   var target=selectedContainers[index];
   PushUndo("重命名",()=>{Current();ContainerScene.Rename(target.Resolve(),previousName);});
  }else{
   var target=actor!;
   PushUndo("重命名",()=>{Current();target.Resolve().Profile.Info.Nickname=previousName;});
  }
 }
 static Action? CaptureDeleteUndo(){
  var current=Current();
  if(selectionKind==SelectionKind.Mines){var mines=selectedMines.ToArray();return ()=>{foreach(var reference in mines)RestoreMine(reference);};}
  if(selectionKind==SelectionKind.Ai){
   var saved=SelectedActors().Select(t=>(Pos:t.Position,Template:SceneTemplates.Capture(t,t.Profile.Nickname))).ToArray();
   return saved.Length==0?null:()=>{var w=Current();foreach(var entry in saved)SceneTemplates.Spawn(w,entry.Pos,entry.Template with{Id=Guid.NewGuid().ToString("N"),Count=1});};
  }
  if(selectionKind==SelectionKind.Containers){
   var saved=selectedContainers.Select(r=>{var c=r.Resolve();return(Copy:CaptureContainer(c),Pos:ContainerScene.Root(c).position);}).ToArray();
   return saved.Length==0?null:()=>{Current();foreach(var entry in saved)PlaceContainerCopy(entry.Copy,entry.Pos);};
  }
  if(selectionKind==SelectionKind.Items){
   var saved=selectedItems.Select(r=>{var l=r.Resolve();return(Copy:CaptureItem(l.Item),Pos:l.transform.position);}).ToArray();
   return saved.Length==0?null:()=>{Current();foreach(var entry in saved)PlaceItem(CloneItem(entry.Copy),entry.Pos);};
  }
  if(selectionKind==SelectionKind.Corpses){
   var saved=selectedCorpses.Select(r=>{var b=r.Resolve();return(Copy:CaptureCorpse(b),Pos:CorpseDisplay.Position(b));}).ToArray();
   return saved.Length==0?null:()=>{Current();foreach(var entry in saved)PlaceCorpse(entry.Copy,entry.Pos);};
  }
  if(selectionKind==SelectionKind.Airdrops){
   var saved=selectedAirdrops.Select(r=>{var d=r.Resolve();return(Copy:CaptureAirdrop(d),Pos:d.transform.position);}).ToArray();
   return saved.Length==0?null:()=>{Current();foreach(var entry in saved)PlaceAirdrop(entry.Copy,entry.Pos);};
  }
  return null;
 }
}
