using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class EventLootDiagnostics {
 internal static object Anchor(){var player=Singleton<GameWorld>.Instance?.MainPlayer??throw new InvalidOperationException("No player");var camera=UnityEngine.Object.FindObjectsOfType<Camera>().FirstOrDefault(c=>c.enabled&&c.name=="FPS Camera")??Camera.main??throw new InvalidOperationException("No main camera");var ray=camera.ViewportPointToRay(new Vector3(.5f,.5f,0));var hits=Physics.RaycastAll(ray,8f,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider!=null&&!h.collider.transform.IsChildOf(player.transform)).OrderBy(h=>h.distance).Take(16).Select(h=>new{distance=h.distance,name=h.collider.name,layer=h.collider.gameObject.layer,point=new{x=h.point.x,y=h.point.y,z=h.point.z},normal=new{x=h.normal.x,y=h.normal.y,z=h.normal.z}}).ToArray();return new{player=new{x=player.Position.x,y=player.Position.y,z=player.Position.z},camera=camera.name,hits};}
 internal static object Read(){
  var world=Singleton<GameWorld>.Instance??throw new InvalidOperationException("No active raid");var bodies=new List<object>();var loot=world.LootItems;
  if(loot!=null)for(int i=0;i<loot.Count;i++){var body=loot.GetByIndex(i)?.TryCast<Corpse>();if(body==null)continue;var root=body.ItemOwner?.RootItem;var equipment=root?.TryCast<InventoryEquipment>();var bag=equipment?.GetSlot(EquipmentSlot.Backpack)?.ContainedItem;var gear=root==null?Array.Empty<object>():LootDisplay.Children(root).Select(item=>(object)new{template=item.StringTemplateId,name=LootDisplay.ItemName(item),count=item.StackObjectsCount}).ToArray();var contents=bag==null?Array.Empty<object>():LootDisplay.Children(bag).Take(20).Select(item=>(object)new{template=item.StringTemplateId,name=LootDisplay.ItemName(item),count=item.StackObjectsCount}).ToArray();bodies.Add(new{profile=body.PlayerProfileID,label=BossLabels.Cached(body.PlayerProfileID??"",""),bag=bag?.StringTemplateId,gear,contents});}
  var wallets=new List<object>();foreach(var container in UnityEngine.Object.FindObjectsOfType<LootableContainer>(true)){if(container.Template!="6ad0ec26a41fb8f9ce0177e8"&&container.name!="item_wallet_LOOTABLE")continue;var root=container.ItemOwner?.RootItem;wallets.Add(new{id=container.Id,container.Template,container.IsInitialized,active=container.gameObject.activeInHierarchy,root=root?.StringTemplateId,contents=root==null?0:LootDisplay.Children(root).Count(),state=container.DoorState.ToString()});}
  return new{bodies,wallets};
 }
}
