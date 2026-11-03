using System;
using System.Collections.Generic;
using System.Linq;
namespace LocalSpt5Esp;
internal sealed class AiPairSelection {
 readonly HashSet<string> sources=new(StringComparer.Ordinal);internal IEnumerable<string> Sources=>sources;internal int Count=>sources.Count;internal string Target{get;private set;}="";
 internal bool Ready=>sources.Count>0&&Target.Length>0&&!sources.Contains(Target);
 internal bool Contains(string key)=>sources.Contains(key);
 internal void ToggleSource(string key){if(key.Length==0||key==Target)return;if(!sources.Remove(key))sources.Add(key);}
 internal void SelectTarget(string key){Target=key;sources.Remove(key);}
 internal void ClearSources()=>sources.Clear();
 internal void Clear(){sources.Clear();Target="";}
 internal bool Prune(ISet<string> available){int count=sources.Count;string target=Target;sources.RemoveWhere(key=>!available.Contains(key));if(!available.Contains(Target))Target="";return count!=sources.Count||target!=Target;}
}
