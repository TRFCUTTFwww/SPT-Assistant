using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class AirdropControl {
 internal sealed record TeleportRow(string Key,string Name,Vector3 Position);
 internal static IEnumerable<TeleportRow> TeleportEntries=>drops.Where(Active).Select(drop=>new TeleportRow(Key(drop),"空投 #"+drop.ObjectId+" · "+drop.AirdropType,drop.transform.position));
 internal static bool TryTeleportTarget(string key,out Vector3 position,out string name){position=default;name="空投";if(!RaidClock.Active||Singleton<GameWorld>.Instance?.Pointer!=world)return false;var drop=drops.FirstOrDefault(d=>d!=null&&Key(d)==key&&Active(d));if(drop==null)return false;name="空投 #"+drop.ObjectId;position=drop.transform.position;int mask=LayerMask.GetMask("HighPolyCollider","Terrain","LowPolyCollider");if(mask==0)return false;foreach(var offset in new[]{new Vector3(2,0,0),new Vector3(-2,0,0),new Vector3(0,0,2),new Vector3(0,0,-2)}){var sample=position+offset;if(Physics.Raycast(sample+Vector3.up*2,Vector3.down,out var floor,2000,mask,QueryTriggerInteraction.Ignore)&&floor.normal.y>.45f){position=floor.point;return true;}}return false;}
}
