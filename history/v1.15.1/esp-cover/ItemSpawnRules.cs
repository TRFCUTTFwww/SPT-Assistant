using System;
using System.Globalization;
using System.Text;

namespace LocalSpt5Esp;
internal static class ItemSpawnRules {
 internal static string NormalizeSearch(string value){var result=new StringBuilder(value.Length);foreach(char c in value){if(char.IsWhiteSpace(c))continue;result.Append(c is '*' or '×'?'x':char.ToLowerInvariant(c));}return result.ToString();}
 internal static float Number(string input,float minimum,float maximum,string name){
  if(!float.TryParse(input,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)||!float.IsFinite(value)||value<minimum||value>maximum)
   throw new ArgumentException($"{name}请输入 {minimum:0.##}～{maximum:0.##}。 ");
  return value;
 }
 internal static int Integer(string input,int minimum,int maximum,string name){
  if(!int.TryParse(input,NumberStyles.Integer,CultureInfo.InvariantCulture,out var value)||value<minimum||value>maximum)
   throw new ArgumentException($"{name}请输入 {minimum}～{maximum} 的整数。 ");
  return value;
 }
 internal static bool MatchesFilter(string id,Func<string,string?> parent,System.Collections.Generic.HashSet<string> allowed,System.Collections.Generic.HashSet<string> excluded){
  bool matches=false;
  var visited=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
  while(!string.IsNullOrEmpty(id)&&visited.Add(id)){
   if(excluded.Contains(id))return false;
   if(allowed.Contains(id))matches=true;
   id=parent(id)??"";
  }
  return matches;
 }
}
