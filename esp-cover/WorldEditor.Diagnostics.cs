using System;
using System.Linq;
using Comfort.Common;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class WorldEditor {
 internal static object Diagnostics(){
  var current=Singleton<GameWorld>.Instance;var local=current?.MainPlayer;
  return new{active=Active,moving,moveIgnored=moveIgnored?.Value,moveInvincible=moveInvincible?.Value,selectedAirdrops=selectedAirdrops.Count,airdrops=current==null?0:AirdropControl.EditorDrops(current).Length,sniperBlind=MiscPanel.Options.SniperBlind,sniperProtected=MiscPanel.SniperProtected(local),sniperDamageBlocked=local?.ActiveHealthController is {} health&&MiscPanel.SniperDamage(health,EDamageType.Sniper),ordinaryBulletBlocked=local?.ActiveHealthController is {} h&&MiscPanel.SniperDamage(h,EDamageType.Bullet)};
 }
 internal static object CheckAirdrop(){
  var current=Current();if(moving||HasSelection||dialog.Length>0)throw new ArgumentException("请先结束编辑操作。");
  var source=AirdropControl.EditorDrops(current).FirstOrDefault()??throw new ArgumentException("本局没有已落地且准备好物资的空投箱。");
  var snapshot=CaptureAirdrop(source);var origin=source.transform.position;var before=CaptureItem(AirdropControl.EditorContainer(source).ItemOwner.RootItem);
  EFT.SynchronizableObjects.AirdropSynchronizableObject? copy=null;
  try{
   copy=AirdropControl.EditorCreate(current,CloneItem(snapshot.Contents),snapshot.Type,snapshot.View,origin+Vector3.up*30,snapshot.Rotation);
   var reference=new AirdropReference(current,copy);var move=CaptureAirdropMove(reference);FreezeAirdrop(move);MoveAirdrop(move,origin+Vector3.up*32);FinishAirdropMove(move,origin+Vector3.up*32,false);
   var clone=CaptureItem(AirdropControl.EditorContainer(copy).ItemOwner.RootItem);var after=CaptureItem(AirdropControl.EditorContainer(source).ItemOwner.RootItem);
   bool sourceUnchanged=before==after&&source.transform.position==origin;bool newRoot=clone.Root!=before.Root;bool moved=Vector3.Distance(copy.transform.position,origin+Vector3.up*32)<.01f;
   AirdropControl.EditorRemove(current,copy);bool removed=!copy.IsActive||!copy.gameObject.activeInHierarchy;copy=null;
   if(!sourceUnchanged||!newRoot||!moved||!removed)throw new InvalidOperationException("空投编辑诊断不通过。");
   return new{sourceUnchanged,newRoot,moved,removed};
  }finally{if(copy!=null)AirdropControl.EditorRemove(current,copy);}
 }
}
