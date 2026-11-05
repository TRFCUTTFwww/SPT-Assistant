using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using UnityEngine;
namespace LocalSpt5Esp;
internal sealed class CorpseReference {
 internal readonly IntPtr World,Pointer;internal readonly string Profile,Name;
 internal CorpseReference(GameWorld world,Corpse body){World=world.Pointer;Pointer=body.Pointer;Profile=body.PlayerProfileID??"";Name=BossLabels.Cached(Profile,body.Side.ToString());}
 internal static IEnumerable<Corpse> Read(GameWorld world){var loot=world.LootItems;if(loot==null)yield break;for(int i=0;i<loot.Count;i++)if(loot.GetByIndex(i)?.TryCast<Corpse>() is {} body&&body.gameObject.activeInHierarchy)yield return body;}
 internal Corpse Resolve(){var world=Singleton<GameWorld>.Instance;if(!RaidClock.Active||world?.Pointer!=World)throw new ArgumentException("战局已改变，请重新选择尸体。");foreach(var body in Read(world))if(body.Pointer==Pointer&&(body.PlayerProfileID??"")==Profile)return body;throw new ArgumentException("尸体已被清除、复活或替换，请刷新列表。");}
 internal static CorpseReference? Hit(GameWorld world,EFT.Ballistics.Shot shot){var collider=shot.HitCollider;if(collider==null)return null;var direct=collider.GetComponentInParent<Corpse>();var part=collider.GetComponent<BodyPartCollider>()??collider.GetComponentInParent<BodyPartCollider>();string id=part?.PlayerProfileID??part?.Player?.ProfileId??"";foreach(var body in Read(world)){if(direct!=null?body.Pointer==direct.Pointer:id.Length>0&&body.PlayerProfileID==id&&Vector3.Distance(CorpseDisplay.Position(body),shot.HitPoint)<4)return new(world,body);}return null;}
}
