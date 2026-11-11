using System;
using System.Linq;
namespace LocalSpt5Esp;
internal static class EditorContextRules {
 internal static readonly (string Id,string Name)[] Options={
  ("create","创建"),("move","移动"),("copy","复制"),("paste","粘贴"),("duplicate","复制并粘贴"),
  ("rename","重命名"),("kill","杀死"),("release-air","解除空中固定"),("delete","删除"),("revive","复活"),("properties","属性"),
  ("remove-weapons","删除所有武器"),("team-a","设为A方"),("team-b","设为B方"),("team-clear","清除队伍标记"),("pause-toggle","暂停／恢复活动"),
  ("ab-toggle","A ↔ B 敌对／停战"),("player-toggle","对玩家敌对／停战"),("player-default","恢复对玩家默认关系"),
  ("invincible-toggle","切换无敌"),("clear-effects","清除异常状态")
 };
 internal static string Default=>string.Join(",",Options.Select(o=>o.Id));
 static string Canonical(string id)=>id switch{"pause" or "resume"=>"pause-toggle","ab-hostile" or "ab-peace"=>"ab-toggle","player-peace" or "player-hostile"=>"player-toggle","invincible-on" or "invincible-off"=>"invincible-toggle",_=>id};
 internal static string[] Read(string value)=>value.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(Canonical).Where(id=>Options.Any(o=>o.Id==id)).Distinct(StringComparer.Ordinal).ToArray();
 // Uniform selections offer the reverse operation; mixed selections can offer both explicit operations.
 internal static bool[] NextActions(bool[] states,bool splitMixed){
  if(states.Length==0)return Array.Empty<bool>();
  bool all=states.All(value=>value),any=states.Any(value=>value);
  return splitMixed&&any&&!all?new[]{true,false}:new[]{!all};
 }
 internal static string Toggle(string value,string id){var order=Read(value);return string.Join(",",order.Contains(id)?order.Where(o=>o!=id):order.Append(id));}
 internal static string Move(string value,string id,int direction){var order=Read(value);int index=Array.IndexOf(order,id),next=index+Math.Sign(direction);if(index>=0&&next>=0&&next<order.Length)(order[index],order[next])=(order[next],order[index]);return string.Join(",",order);}
}
