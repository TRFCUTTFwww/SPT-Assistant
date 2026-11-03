using System;
using System.Collections.Generic;
using System.Linq;
namespace LocalSpt5Esp;
internal enum CompanionOrder { Follow,Hunt,Loot,Deliver,Hold,Autonomous }
internal static class CompanionRules {
 internal const int Maximum=32;
 internal static void Validate(IEnumerable<(string Role,string Side,int Difficulty,int Count)> rows){
  int total=0;foreach(var row in rows){if(!AiSpawnCatalog.Options.Any(o=>o.Role==row.Role&&o.Side==row.Side)||row.Count is <1 or >Maximum||row.Difficulty is <0 or >3)throw new ArgumentException("队友类型、阵营、难度或数量无效。");total=checked(total+row.Count);if(total>Maximum)throw new ArgumentException("每支编队最多 32 名队友。");}
 }
 internal static bool HuntTarget(bool alive,bool ai,bool local,bool teammate,bool btr)=>alive&&ai&&!local&&!teammate&&!btr;
 internal static bool Deliverable(string id,IReadOnlySet<string> original,IReadOnlySet<string> delivered,bool held,bool worn,bool hasOriginalChild)=>!string.IsNullOrEmpty(id)&&!original.Contains(id)&&!delivered.Contains(id)&&!held&&!worn&&!hasOriginalChild;
 internal static bool SameIdentity(long expectedWorld,long expectedActor,string expectedProfile,long world,long actor,string profile)=>expectedWorld!=0&&expectedActor!=0&&expectedWorld==world&&expectedActor==actor&&expectedProfile==profile;
}
internal sealed class CompanionSpawnLedger {
 readonly Dictionary<string,string?> tickets=new(StringComparer.Ordinal);
 internal void Add(string key){if(string.IsNullOrEmpty(key)||!tickets.TryAdd(key,null))throw new ArgumentException("重复的队友生成请求。");}
 internal bool Confirm(string key,string profile){if(string.IsNullOrEmpty(profile)||!tickets.ContainsKey(key)||tickets[key]!=null||tickets.Values.Contains(profile))return false;tickets[key]=profile;return true;}
 internal int Confirmed=>tickets.Count(p=>p.Value!=null);
 internal bool Owns(string key)=>tickets.ContainsKey(key);
 internal bool HasActor(string key)=>tickets.TryGetValue(key,out var value)&&value!=null;
}
