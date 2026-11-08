using System;
using System.Collections.Generic;
using System.Linq;
namespace LocalSpt5Esp;
internal static class QuestLocationRules {
 internal static string[] Zones(string kind,string? target,IEnumerable<string>? zones)=>
  (kind is "visit" or "trigger"?new[]{target??""}:kind is "zone" or "in-zone"?zones??Array.Empty<string>():Array.Empty<string>())
  .Where(id=>!string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
 internal static bool Eligible(bool started,bool completed)=>started&&!completed;
}
