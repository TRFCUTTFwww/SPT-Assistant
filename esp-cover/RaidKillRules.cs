using System;
using System.Collections.Generic;
using System.Linq;
namespace LocalSpt5Esp;
internal sealed record RaidKillNotice(string Victim,string Killer,string VictimName,string KillerName,string Detail,bool PlayerRelated,bool Known,double Time);
internal sealed class RaidKillBuffer {
 readonly Dictionary<string,RaidKillNotice> pending=new(StringComparer.Ordinal);readonly HashSet<string> seen=new(StringComparer.Ordinal),known=new(StringComparer.Ordinal);internal readonly List<RaidKillNotice> Entries=new();
 internal void Clear(){pending.Clear();seen.Clear();known.Clear();Entries.Clear();}
 internal void Stage(RaidKillNotice value){if(string.IsNullOrEmpty(value.Victim)||!double.IsFinite(value.Time))return;if(seen.Contains(value.Victim)){if(!value.Known||known.Contains(value.Victim))return;int index=Entries.FindIndex(n=>n.Victim==value.Victim&&!n.Known);if(index>=0){Entries[index]=value with{Time=Entries[index].Time};known.Add(value.Victim);return;}seen.Remove(value.Victim);}if(pending.TryGetValue(value.Victim,out var old)){if(value.Known||!old.Known)pending[value.Victim]=value with{Time=old.Time};}else{if(pending.Count>=256)return;pending[value.Victim]=value;}}
 internal void Update(double now,double duration,int maximum,bool aiKills,bool environment){Entries.RemoveAll(n=>now-n.Time>=duration);foreach(var pair in pending.ToArray()){var value=pair.Value;if(now-value.Time<.15)continue;pending.Remove(pair.Key);seen.Add(pair.Key);if(value.Known)known.Add(pair.Key);if((aiKills||value.PlayerRelated)&&(environment||value.Known))Entries.Add(value);}while(Entries.Count>Math.Max(1,maximum))Entries.RemoveAt(0);}
}
