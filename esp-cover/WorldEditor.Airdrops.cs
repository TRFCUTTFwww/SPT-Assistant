using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Airdrop;
using EFT.SynchronizableObjects;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class WorldEditor {
 sealed class AirdropReference {
  readonly IntPtr world,pointer;readonly int id;readonly string root;readonly AirdropSynchronizableObject drop;
  internal AirdropReference(GameWorld current,AirdropSynchronizableObject value){world=current.Pointer;drop=value;pointer=value.Pointer;id=value.ObjectId;root=AirdropControl.EditorContainer(value).ItemOwner.RootItem.Id.ToString();}
  internal AirdropSynchronizableObject Resolve(){var current=Singleton<GameWorld>.Instance;if(!RaidClock.Active||current?.Pointer!=world||drop==null||drop.Pointer!=pointer||drop.ObjectId!=id||!AirdropControl.EditorReady(drop)||AirdropControl.EditorContainer(drop).ItemOwner.RootItem.Id.ToString()!=root)throw new ArgumentException("空投箱已离开、被删除或替换，请重新选择。");return drop;}
 }
 sealed record AirdropCopy(ItemCopy Contents,EAirdropType Type,AirdropSynchronizableObject.EAirdropViewType View,Quaternion Rotation);
 sealed record AirdropMove(AirdropReference Reference,Vector3 Origin,Quaternion Rotation,Rigidbody? Body,bool Kinematic,bool Gravity,Vector3 Velocity,Vector3 Angular);
 static readonly List<AirdropReference> selectedAirdrops=new();static readonly List<AirdropMove> moveAirdrops=new();
 static string SelectionName=>selectionKind switch{SelectionKind.Ai=>"AI",SelectionKind.Items=>I18n.Text("物品"),SelectionKind.Corpses=>I18n.Text("尸体"),_=>I18n.Text("空投箱")};
 static Vector3 AirdropPoint(AirdropSynchronizableObject drop)=>drop.CollisionCollider?.bounds.center??drop.transform.position+Vector3.up*.6f;
 static AirdropCopy CaptureAirdrop(AirdropSynchronizableObject drop){var view=AirdropSynchronizableObject.EAirdropViewType.Default;foreach(var entry in drop.AirdropViews)if(entry?.LootContainerCollider?.gameObject.activeInHierarchy==true){view=entry.ViewType;break;}return new(CaptureItem(AirdropControl.EditorContainer(drop).ItemOwner.RootItem),drop.AirdropType,view,drop.transform.rotation);}
 static void PlaceAirdrop(AirdropCopy copy,Vector3 position)=>AirdropControl.EditorCreate(Current(),CloneItem(copy.Contents),copy.Type,copy.View,position,copy.Rotation);
 static AirdropMove CaptureAirdropMove(AirdropReference reference){var drop=reference.Resolve();var body=AirdropControl.EditorServer(drop)?.SyncObjectRigidbody;return new(reference,drop.transform.position,drop.transform.rotation,body,body?.isKinematic??true,body?.useGravity??false,body?.velocity??Vector3.zero,body?.angularVelocity??Vector3.zero);}
 static void FreezeAirdrop(AirdropMove value){if(value.Body!=null){value.Body.isKinematic=true;value.Body.useGravity=false;}}
 static void MoveAirdrop(AirdropMove value,Vector3 position){var drop=value.Reference.Resolve();AirdropControl.EditorPosition(drop,position,value.Rotation);}
 static void FinishAirdropMove(AirdropMove value,Vector3 position,bool cancel){
  try{var drop=value.Reference.Resolve();if(cancel)AirdropControl.EditorPosition(drop,value.Origin,value.Rotation);else AirdropControl.EditorPlace(drop,position);}
  finally{if(value.Body!=null){value.Body.isKinematic=value.Kinematic;value.Body.useGravity=value.Gravity;if(!value.Kinematic){value.Body.velocity=cancel?value.Velocity:Vector3.zero;value.Body.angularVelocity=cancel?value.Angular:Vector3.zero;}}}
 }
}
