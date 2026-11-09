using System;
using System.Linq;
namespace LocalSpt5Esp;
internal static class MenuSearchRules {
 internal static string[] Terms(string query)=>query.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
 internal static bool Matches(string[] terms,string text)=>terms.Length>0&&terms.All(term=>text.Contains(term,StringComparison.OrdinalIgnoreCase));
}
