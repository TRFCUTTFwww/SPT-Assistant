using System;
using System.Collections.Generic;
namespace LocalSpt5Esp;
internal sealed class AiPauseSelection {
 internal bool All {get;private set;}
 readonly HashSet<string> paused=new(),running=new();
 internal bool Contains(string id)=>All?!running.Contains(id):paused.Contains(id);
 internal void Set(string id,bool value){if(All){if(value)running.Remove(id);else running.Add(id);}else{if(value)paused.Add(id);else paused.Remove(id);}}
 internal void PauseAll(){All=true;paused.Clear();running.Clear();}
 internal void Clear(){All=false;paused.Clear();running.Clear();}
}
internal static class AiSpawnRules {
 internal static int Count(string text){if(!int.TryParse(text,out int count)||count<1||count>5)throw new ArgumentException("每次生成数量需为 1～5。");return count;}
 internal static float Distance(string text){if(!float.TryParse(text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float distance)||!float.IsFinite(distance)||distance<5||distance>100)throw new ArgumentException("前方距离需为 5～100 米。");return distance;}
}
