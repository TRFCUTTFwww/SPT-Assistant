using System;
using System.Collections.Generic;
namespace LocalSpt5Esp;
internal sealed class HitFeed {
 internal sealed record Entry(string Victim,string Name,string Part,float? Damage,bool Killed,float Time);
 readonly List<Entry> entries=new();
 internal IReadOnlyList<Entry> Entries=>entries;
 internal void Clear()=>entries.Clear();
 internal void Prune(float now,float duration,int limit){
  entries.RemoveAll(e=>now-e.Time>=duration);
  if(entries.Count>limit)entries.RemoveRange(0,entries.Count-limit);
 }
 internal void Add(string victim,string name,string part,float damage,bool killed,float now,int limit){
  if(!float.IsFinite(damage)||damage<0)return;
  entries.Add(new(victim,name,part,damage,killed,now));
  if(entries.Count>limit)entries.RemoveRange(0,entries.Count-limit);
 }
 internal void Kill(string victim,string name,string part,float now,float duration,int limit){
  Prune(now,duration,limit);
  for(int i=entries.Count-1;i>=0;i--)if(entries[i].Victim==victim){
   if(entries[i].Killed)return;
   entries[i]=entries[i] with {Killed=true,Time=now};return;
  }
  entries.Add(new(victim,name,part,null,true,now));
  if(entries.Count>limit)entries.RemoveRange(0,entries.Count-limit);
 }
}
