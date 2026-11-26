using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
namespace LocalSpt5Esp;

// Catalog text is authored and reviewed in English. No network translation or word replacement.
internal sealed class LocalizationCatalog {
 readonly IReadOnlyDictionary<string,string> text;
 readonly (Regex Probe,Regex Capture,string Translation,int Count,string Anchor,int[] Verbatim)[] templates;
 readonly Dictionary<string,string> cache=new(StringComparer.Ordinal);
 internal LocalizationCatalog(IReadOnlyDictionary<string,string> text,IReadOnlyDictionary<string,string> formats,IReadOnlyDictionary<string,int[]>? verbatim=null){
  this.text=text;
  templates=formats.OrderByDescending(p=>Regex.Replace(p.Key,@"\{\d+\}","").Length).Select(p=>{
   var matches=Regex.Matches(p.Key,@"\{(\d+)\}");int position=0;string pattern="\\A";string anchor="";int count=0;
   foreach(Match m in matches){string literal=p.Key[position..m.Index];if(literal.Length>anchor.Length)anchor=literal;pattern+=Regex.Escape(literal)+"(?<p"+m.Groups[1].Value+">.*?)";position=m.Index+m.Length;count=Math.Max(count,int.Parse(m.Groups[1].Value)+1);}
   string tail=p.Key[position..];if(tail.Length>anchor.Length)anchor=tail;pattern+=Regex.Escape(tail)+"\\z";
   if(count==0||anchor.Length<1)throw new ArgumentException("A localized format needs an unambiguous literal: "+p.Key);
   var expected=matches.Select(m=>m.Groups[1].Value).Distinct().Order().ToArray();var actual=Regex.Matches(p.Value,@"\{(\d+)\}").Select(m=>m.Groups[1].Value).Distinct().Order().ToArray();
   if(!expected.SequenceEqual(actual))throw new ArgumentException("Translation placeholders do not match: "+p.Key);
   // Use the linear matcher to reject inputs. The runtime's nonbacktracking engine
   // can lose group captures on trailing newlines; extract groups with the bounded matcher.
   return(new Regex(pattern,RegexOptions.CultureInvariant|RegexOptions.Singleline|RegexOptions.NonBacktracking,TimeSpan.FromMilliseconds(20)),new Regex(pattern,RegexOptions.CultureInvariant|RegexOptions.Singleline,TimeSpan.FromMilliseconds(20)),p.Value,count,anchor,verbatim!=null&&verbatim.TryGetValue(p.Key,out var indices)?indices:Array.Empty<int>());
  }).ToArray();
 }
 internal string Text(string value)=>Render(value,0);
 string Render(string value,int depth){
  if(string.IsNullOrEmpty(value)||value.Length>8192||depth>6)return value;
  if(text.TryGetValue(value,out var exact))return exact;
  if(!value.Any(c=>c is >= '\u3400' and <= '\u9fff'))return value;
  if(cache.TryGetValue(value,out var saved))return saved;
  string result=value;
  foreach(var entry in templates){
   if(!value.Contains(entry.Anchor,StringComparison.Ordinal))continue;
   Match match;try{if(!entry.Probe.IsMatch(value))continue;match=entry.Capture.Match(value);}catch(RegexMatchTimeoutException){continue;}
   if(!match.Success)continue;
   // Only catalog messages are interpreted. Unknown text is preserved verbatim.
   var arguments=new object[entry.Count];for(int i=0;i<arguments.Length;i++)arguments[i]=entry.Verbatim.Contains(i)?match.Groups["p"+i].Value:Render(match.Groups["p"+i].Value,depth+1);
   result=string.Format(System.Globalization.CultureInfo.CurrentCulture,entry.Translation,arguments);break;
  }
  if(result==value&&value.Contains('\n'))result=string.Join("\n",value.Split('\n').Select(v=>Render(v,depth+1)));
  if(cache.Count>=2048)cache.Clear();cache[value]=result;return result;
 }
}
