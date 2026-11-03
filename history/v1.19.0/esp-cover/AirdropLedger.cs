using System;
using System.Collections.Generic;
using System.Linq;
namespace LocalSpt5Esp;
internal sealed class AirdropLedger {
 internal sealed class Entry {internal string Key="";internal double? ReleasedAt;internal bool Present;}
 readonly Dictionary<string,Entry> entries=new();
 internal IEnumerable<Entry> Entries=>entries.Values;
 internal int ReleasedCount=>entries.Values.Count(e=>e.ReleasedAt.HasValue);
 internal Entry Observe(string key,bool present){if(!entries.TryGetValue(key,out var entry)){entry=new(){Key=key};entries[key]=entry;}entry.Present=present;return entry;}
 internal void Released(string key,double? elapsed){var entry=Observe(key,true);entry.ReleasedAt??=elapsed;}
 internal void Clear()=>entries.Clear();
 internal static double? Remaining(double due,double elapsed)=>double.IsFinite(due)&&double.IsFinite(elapsed)&&due>=0&&elapsed>=0?Math.Max(0,due-elapsed):null;
}
