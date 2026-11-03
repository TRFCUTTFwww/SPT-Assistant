using System;
using System.Collections.Generic;
namespace LocalSpt5Esp;
internal static class AiSelectionRules {
 internal static bool Checked(bool sameWorld,bool local,bool ai,bool alive,string id,IReadOnlySet<string> selected)=>sameWorld&&!local&&ai&&alive&&id.Length>0&&selected.Contains(id);
}
internal sealed class AiPauseSelection {
 internal bool All {get;private set;}
 internal bool Any=>All||paused.Count>0;
 readonly HashSet<string> paused=new(),running=new();
 internal bool Contains(string id)=>All?!running.Contains(id):paused.Contains(id);
 internal void Set(string id,bool value){if(All){if(value)running.Remove(id);else running.Add(id);}else{if(value)paused.Add(id);else paused.Remove(id);}}
 internal void PauseAll(){All=true;paused.Clear();running.Clear();}
 internal void Clear(){All=false;paused.Clear();running.Clear();}
}
internal static class AiSpawnRules {
 internal static int Count(string text){if(!int.TryParse(text,out int count)||count<1)throw new ArgumentException("数量须为正整数；游戏按当前 AI 区域的实际空位接受请求。");return count;}
 internal static float Distance(string text){if(!float.TryParse(text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float distance)||!float.IsFinite(distance)||distance<5)throw new ArgumentException("前方距离须为至少 5 米的有限数字，位置仍须位于有效导航区域。");return distance;}
}
