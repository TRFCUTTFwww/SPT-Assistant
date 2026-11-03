using System;
using System.Collections.Generic;
using System.Numerics;
namespace LocalSpt5Esp;
internal static class DamageDirection {
 internal static float Angle(Vector3 sourceDirection,Vector3 forward){
  sourceDirection.Y=0;forward.Y=0;
  if(sourceDirection.LengthSquared()<.0001f||forward.LengthSquared()<.0001f)return float.NaN;
  forward=Vector3.Normalize(forward);var right=new Vector3(forward.Z,0,-forward.X);
  return MathF.Atan2(Vector3.Dot(sourceDirection,right),Vector3.Dot(sourceDirection,forward))*180/MathF.PI;
 }
}
internal sealed class IncomingDamageFeed {
 internal sealed class Entry {
  internal string Key="",Name="",Part="";
  internal Vector3? Origin,Direction;
  internal float? Distance;
  internal float Damage,Time;
  internal bool Preview,GodBlocked;
 }
 internal readonly List<Entry> Entries=new();
 internal void Add(Entry entry,int maximum){
  entry.Damage=float.IsFinite(entry.Damage)?Math.Max(0,entry.Damage):0;
  var prior=Entries.Count>0?Entries[^1]:null;
  if(prior!=null&&prior.Key==entry.Key&&prior.Part==entry.Part&&prior.Preview==entry.Preview&&entry.Time>=prior.Time&&entry.Time-prior.Time<=.12f){entry.Damage+=prior.Damage;Entries.RemoveAt(Entries.Count-1);}
  Entries.Add(entry);while(Entries.Count>Math.Clamp(maximum,1,12))Entries.RemoveAt(0);
 }
 internal void Prune(float time,float duration,int maximum){Entries.RemoveAll(e=>time-e.Time>=duration||time<e.Time);while(Entries.Count>Math.Clamp(maximum,1,12))Entries.RemoveAt(0);}
 internal void Clear()=>Entries.Clear();
}
