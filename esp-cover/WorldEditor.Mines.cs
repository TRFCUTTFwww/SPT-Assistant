using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class WorldEditor {
 sealed class MineReference {
  readonly IntPtr owner;readonly MineDirectional mine;
  internal MineReference(GameWorld current,MineDirectional value){owner=current.Pointer;mine=value;}
  internal MineDirectional Resolve(bool inactive=false){if(!RaidClock.Active||Singleton<GameWorld>.Instance?.Pointer!=owner||mine==null||mine._exploded||mine._cleared||!inactive&&!mine.gameObject.activeInHierarchy)throw new ArgumentException("阔剑已爆炸、删除或离开当前战局。");return mine;}
 }
 sealed record MineCopy(MineDirectional Source,Quaternion Rotation);
 sealed record MineMove(MineReference Reference,Vector3 Origin,Quaternion Rotation,bool Enabled,(Collider Collider,bool Enabled)[] Colliders);
 static readonly List<MineReference> selectedMines=new();
 static readonly List<MineMove> moveMines=new();
 static MineMove CaptureMineMove(MineReference reference){var mine=reference.Resolve();return new(reference,mine.transform.position,mine.transform.rotation,mine.enabled,mine.GetComponentsInChildren<Collider>(true).Select(c=>(c,c.enabled)).ToArray());}
 static void FreezeMine(MineMove move){move.Reference.Resolve().enabled=false;foreach(var entry in move.Colliders)if(entry.Collider!=null)entry.Collider.enabled=false;}
 static void ThawMine(MineMove move){foreach(var entry in move.Colliders)if(entry.Collider!=null)entry.Collider.enabled=entry.Enabled;var mine=move.Reference.Resolve();mine.enabled=move.Enabled;}
 static MineDirectional[] Mines(GameWorld current)=>current.MineManager?.Mines?.Where(m=>m!=null&&!m._cleared&&!m._exploded&&m.gameObject.activeInHierarchy&&m._mineData?.IsDummy==false).ToArray()??Array.Empty<MineDirectional>();
 static void RequireMineEditing(){Current();if(ChatFikaSession.Role!=0)throw new ArgumentException("阔剑场景编辑暂不支持 FIKA 同步。");}
 static MineCopy CaptureMine(MineDirectional mine)=>new(mine,mine.transform.rotation);
 static MineDirectional PlaceMine(MineCopy copy,Vector3 position){
  RequireMineEditing();if(MineLibrary.Model==null){MineLibrary.Request();throw new ArgumentException(MineLibrary.Status);}if(copy.Source==null)throw new ArgumentException("阔剑模型已失效，请重新复制。");
  // An inactive parent prevents Awake/trigger evaluation before placement.
  var staging=new GameObject("Assistant mine staging");staging.SetActive(false);GameObject? clone=null;
  try{clone=UnityEngine.Object.Instantiate(copy.Source.gameObject,staging.transform);clone.name=MineLibrary.PlacedName;clone.SetActive(false);
   var mine=clone.GetComponent<MineDirectional>()??throw new ArgumentException("阔剑模型缺少原生触发器。");
   mine.enabled=false;mine._cleared=false;mine._exploded=false;
   foreach(var trigger in clone.GetComponentsInChildren<MineDirectionalColliders>(true))trigger._parent=mine;
   clone.transform.SetPositionAndRotation(position,copy.Rotation);clone.transform.SetParent(null,true);MineLibrary.BindVisual(mine);clone.SetActive(true);mine.SetArmed(true);mine.enabled=true;
   var list=Current().MineManager?.Mines;if(list!=null&&!list.Contains(mine))list.Add(mine);
   return mine;
  }catch{if(clone!=null)UnityEngine.Object.Destroy(clone);throw;}finally{UnityEngine.Object.Destroy(staging);}
 }
 static void CreateMine(){
  RequireMineEditing();var source=MineLibrary.Model;
  if(source==null){MineLibrary.Request();FeatureToasts.Show(MineLibrary.Status);return;}
  var rotation=Quaternion.Euler(0,View.transform.eulerAngles.y,0);var mine=PlaceMine(new(source,rotation),point);var reference=new MineReference(Current(),mine);
  PushUndo("放置阔剑",()=>DeleteMine(reference));CompleteOperation();
 }
 static void DeleteMine(MineReference reference){RequireMineEditing();var mine=reference.Resolve();mine.enabled=false;mine.gameObject.SetActive(false);Current().MineManager?.Mines?.Remove(mine);}
 static void RestoreMine(MineReference reference){RequireMineEditing();var mine=reference.Resolve(true);mine.gameObject.SetActive(true);mine.enabled=true;var list=Current().MineManager?.Mines;if(list!=null&&!list.Contains(mine))list.Add(mine);}
 static void RotateMines(float degrees){RequireMineEditing();var previous=selectedMines.Select(r=>(Reference:r,Rotation:r.Resolve().transform.rotation)).ToArray();foreach(var entry in previous)entry.Reference.Resolve().transform.rotation=entry.Rotation*Quaternion.Euler(0,degrees,0);PushUndo("旋转阔剑",()=>{Current();foreach(var entry in previous)entry.Reference.Resolve().transform.rotation=entry.Rotation;});CompleteOperation();}
}
