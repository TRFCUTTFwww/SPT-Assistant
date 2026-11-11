using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.Interactive;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class WorldEditor {
 sealed record CorpseCopy(ItemCopy Equipment,Dictionary<int,string> Appearance,EPlayerSide Side,JsonType.TransformSync[] Bones,Vector3 RootPosition,Vector3 RootRotation,string OriginalProfile);
 sealed record BodyMove(Rigidbody Body,bool Kinematic,bool Gravity,Vector3 Velocity,Vector3 Angular);
 sealed record TransformMove(Transform Transform,Vector3 Position,Quaternion Rotation);
 sealed record CorpseMove(CorpseReference Reference,Vector3 Origin,TransformMove[] Transforms,BodyMove[] Bodies);
 static readonly List<CorpseMove> moveCorpses=new();
 static CorpseCopy CaptureCorpse(Corpse body){
  var root=body.ItemOwner?.RootItem??throw new ArgumentException("尸体装备尚未就绪。");
  var appearance=new Dictionary<int,string>();if(body.Customization!=null)foreach(var pair in body.Customization)appearance[(int)pair.Key]=pair.Value.ToString();
  var bones=body.GetTransformSync();var copy=new JsonType.TransformSync[bones.Length];for(int i=0;i<copy.Length;i++)copy[i]=bones[i];
  return new(CaptureItem(root),appearance,body.Side,copy,body.transform.position,body.transform.eulerAngles,body.PlayerProfileID??"");
 }
 static void PlaceCorpse(CorpseCopy copy,Vector3 destination){
  var current=Current();var equipment=CloneItem(copy.Equipment);var appearance=new BodyCustomization();foreach(var pair in copy.Appearance)appearance[(EBodyModelPart)pair.Key]=new MongoID(pair.Value);
  var bones=new Il2CppStructArray<JsonType.TransformSync>(copy.Bones.Length);for(int i=0;i<copy.Bones.Length;i++)bones[i]=copy.Bones[i];
  // Recreate the original pose first, then translate the complete hierarchy.
  // Never reuse the source's profile ID, inventory tree, physics or callbacks.
  var data=new JsonType.JsonCorpse{Id=equipment.Id.ToString(),Item=equipment,Position=copy.RootPosition,Rotation=copy.RootRotation,Customization=appearance,Side=copy.Side,Bones=bones,ProfileID="editor-corpse-"+Guid.NewGuid().ToString("N"),PlatformId=-1,useGravity=false,randomRotation=false};
  var body=Corpse.CreateStillCorpse<Corpse>(current,data,null);
  if(body==null)throw new ArgumentException("尸体副本创建失败。");
  try{
   bool registered=false;for(int i=0;i<current.LootItems.Count;i++)if(current.LootItems.GetByIndex(i)?.Pointer==body.Pointer){registered=true;break;}
   if(!registered)current.RegisterLoot(body);
   CorpseRevival.CopyHistory(current,copy.OriginalProfile,body.PlayerProfileID);
   var move=CaptureCorpseMove(new(current,body));try{FreezeCorpse(move);MoveCorpse(move,destination);}finally{RestoreCorpsePhysics(move,false);}
  }catch{try{current.DestroyLoot(body.Cast<IKillable>());}catch{}throw;}
  CorpseDisplay.Reset();RaidLoot.Invalidate();
 }
 static CorpseMove CaptureCorpseMove(CorpseReference reference){
  var body=reference.Resolve();
  var transforms=body.GetComponentsInChildren<Transform>().Select(t=>new TransformMove(t,t.position,t.rotation)).ToArray();
  var physics=body.GetComponentsInChildren<Rigidbody>().Select(b=>new BodyMove(b,b.isKinematic,b.useGravity,b.velocity,b.angularVelocity)).ToArray();
  return new(reference,CorpseDisplay.Position(body),transforms,physics);
 }
 static void FreezeCorpse(CorpseMove move){foreach(var value in move.Bodies){value.Body.isKinematic=true;value.Body.useGravity=false;}}
 static void MoveCorpse(CorpseMove move,Vector3 goal){
  move.Reference.Resolve();var offset=goal-move.Origin;
  // Parent-first assignment uses absolute snapshots, avoiding accumulated
  // offsets when the pelvis and other bones have their own rigidbodies.
  foreach(var value in move.Transforms)if(value.Transform!=null)value.Transform.SetPositionAndRotation(value.Position+offset,value.Rotation);
  foreach(var value in move.Bodies)if(value.Body!=null){value.Body.position=value.Body.transform.position;value.Body.rotation=value.Body.transform.rotation;}
 }
 static void RestoreCorpsePhysics(CorpseMove move,bool cancel){foreach(var value in move.Bodies)if(value.Body!=null)try{value.Body.isKinematic=value.Kinematic;value.Body.useGravity=value.Gravity;if(!value.Kinematic){value.Body.velocity=cancel?value.Velocity:Vector3.zero;value.Body.angularVelocity=cancel?value.Angular:Vector3.zero;}}catch(Exception error){Plugin.Self.Log.LogWarning("Editor corpse physics: "+error.Message);}}
 static void ReviveCorpses(){var current=Current();var references=selectedCorpses.ToArray();foreach(var reference in references)reference.Resolve();foreach(var reference in references)CorpseRevival.Enqueue(current,reference);CloseDialog();}
}
