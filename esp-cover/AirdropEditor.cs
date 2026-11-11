using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.Airdrop;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.SynchronizableObjects;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class AirdropControl {
 internal static ServerAirDrop? EditorServer(AirdropSynchronizableObject drop)=>drop.Logic?.TryCast<ServerAirDrop>()??drop.Logic?.TryCast<ClientAirDrop>()?.OfflineServerLogic;
 internal static bool EditorReady(AirdropSynchronizableObject drop){
  if(!Active(drop))return false;var stage=EditorServer(drop)?._fallingStage??drop.Logic?.TryCast<ClientAirDrop>()?._fallingStage;
  return stage is EAirdropFallingStage.Landed or EAirdropFallingStage.Contact;
 }
 internal static AirdropSynchronizableObject[] EditorDrops(GameWorld current){
  EnsureInformation();var result=new List<AirdropSynchronizableObject>();var objects=current.SynchronizableObjectLogicProcessor?.GetSynchronizableObjects();
  if(objects!=null)foreach(var item in objects)if(item?.TryCast<AirdropSynchronizableObject>() is {} drop&&EditorReady(drop))result.Add(drop);
  foreach(var drop in drops)if(EditorReady(drop)&&!result.Any(d=>d.Pointer==drop.Pointer))result.Add(drop);
  foreach(var drop in UnityEngine.Object.FindObjectsByType<AirdropSynchronizableObject>(FindObjectsSortMode.None))if(EditorReady(drop)&&!result.Any(d=>d.Pointer==drop.Pointer))result.Add(drop);
  return result.ToArray();
 }
 internal static LootableContainer EditorContainer(AirdropSynchronizableObject drop)=>drop.GetComponentsInChildren<LootableContainer>(true).FirstOrDefault(c=>c!=null&&c.gameObject.activeInHierarchy&&c.ItemOwner?.RootItem!=null)??throw new ArgumentException("空投箱物资尚未准备好。");
 internal static void EditorRemove(GameWorld current,AirdropSynchronizableObject drop){
  var processor=current.SynchronizableObjectLogicProcessor??throw new ArgumentException("空投系统尚未就绪。");
  foreach(var container in drop.GetComponentsInChildren<LootableContainer>(true))if(container?.ItemOwner is {} owner)current.ItemOwners.Remove(owner.Cast<IItemOwner>());
  processor.ReturnToPool(drop);drops.RemoveAll(d=>d==null||d.Pointer==drop.Pointer);
 }
 internal static AirdropSynchronizableObject EditorCreate(GameWorld current,Item root,EAirdropType type,AirdropSynchronizableObject.EAirdropViewType view,Vector3 position,Quaternion rotation){
  EnsureInformation();var processor=current.SynchronizableObjectLogicProcessor??throw new ArgumentException("空投系统尚未就绪。");
  var drop=processor.TakeFromPool(SynchronizableObjectType.AirDrop)?.TryCast<AirdropSynchronizableObject>()??throw new ArgumentException("未能取得空投箱副本。");
  try{
   processor.InitSyncObject(drop,position,rotation.eulerAngles,processor.TakeSynchronizedObjectId);drop.AirdropType=type;drop.ApplyAirdropViewType(view);drop.Logic?.TryCast<ClientAirDrop>()?.UpdateContainerType(type);
   var container=drop.GetComponentsInChildren<LootableContainer>(true).FirstOrDefault(c=>c!=null&&c.gameObject.activeInHierarchy)??throw new ArgumentException("空投箱缺少可交互容器。");
   if(container.ItemOwner is {} prior)current.ItemOwners.Remove(prior.Cast<IItemOwner>());
   string id=root.Id.ToString();container.Id=id;container.Template=root.TemplateId.ToString();
   var owner=new ItemController(root,id,"editor-airdrop",false,EOwnerType.Profile);container.Init(owner);
   var key=owner.Cast<IItemOwner>();current.ItemOwners[key]=new GameWorld.ItemOwnerWorldData(container.transform,false,key);
   EditorPlace(drop,position);EditorPosition(drop,position,rotation);drop.IsActive=true;Track(drop);ledger.Released(Key(drop),Elapsed);
   if(container.ItemOwner?.RootItem?.Id.ToString()!=id)throw new ArgumentException("空投箱副本未完成物资绑定。");return drop;
  }catch{try{EditorRemove(current,drop);}catch{}throw;}
 }
 internal static void EditorPlace(AirdropSynchronizableObject drop,Vector3 position){
  EditorPosition(drop,position,drop.transform.rotation);drop.IsStatic=true;drop.InitStaticObject();var server=EditorServer(drop);
  if(server!=null){server._fallingStage=EAirdropFallingStage.Landed;server._descending=false;server._velocity=Vector3.zero;var body=server.SyncObjectRigidbody;if(body!=null){if(!body.isKinematic){body.velocity=Vector3.zero;body.angularVelocity=Vector3.zero;}body.isKinematic=true;body.position=position;}}
  if(drop.Logic?.TryCast<ClientAirDrop>() is {} client){client._fallingStage=EAirdropFallingStage.Landed;client._startFallingStage=EAirdropFallingStage.Landed;client.InitializeLandedAirdrop();client.CloseParachute();}
 }
 internal static void EditorPosition(AirdropSynchronizableObject drop,Vector3 position,Quaternion rotation){
  drop.transform.SetPositionAndRotation(position,rotation);
  if(drop.Logic?.TryCast<ClientAirDrop>() is {} client){client._destinationPosition=position;client._destinationRotation=rotation;}
  if(EditorServer(drop)?.SyncObjectRigidbody is {} body){body.position=position;body.rotation=rotation;}
 }
}
