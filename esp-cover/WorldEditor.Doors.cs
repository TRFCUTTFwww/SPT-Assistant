using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using UnityEngine;

namespace LocalSpt5Esp;

internal static partial class WorldEditor
{
 sealed class DoorReference
 {
  readonly IntPtr world,pointer; readonly string id; readonly Door door;
  internal DoorReference(GameWorld current,Door value){world=current.Pointer;pointer=value.Pointer;id=value.Id??"";door=value;}
  internal Door Resolve(){var current=Singleton<GameWorld>.Instance;if(!RaidClock.Active||current?.Pointer!=world||door==null||door.Pointer!=pointer||door.Id!=id||!door.gameObject.activeInHierarchy)throw new ArgumentException("门已离开或被替换，请重新选择。");return door;}
  internal Transform transform=>door.transform;
  internal static Door[] Read(GameWorld current)=>System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(UnityEngine.Object.FindObjectsOfType<Door>(),d=>d!=null&&d.gameObject.activeInHierarchy));
  internal static string State(Door value)=>value.DoorState switch{EDoorState.Locked=>"上锁",EDoorState.Shut=>"关闭",EDoorState.Open=>"打开",_=>"切换中"};
  internal static void RequireIdle(Door value){if(value==null)throw new ArgumentException("门不可用。");if(value.DoorState is not (EDoorState.Open or EDoorState.Shut or EDoorState.Locked))throw new ArgumentException("门正在交互或被封锁，请等待后重试。");}
  internal static void Set(Door value,EDoorState state){RequireIdle(value);if(value.DoorState==state)return;value.SetDoorState(state,true);}
 }
}
