using System;
using System.Linq;
namespace LocalSpt5Esp;
internal static class EditorContextRules {
 internal static readonly (string Id,string Name)[] Options={
  ("create","创建"),("move","移动"),("copy","复制"),("paste","粘贴"),("duplicate","复制并粘贴"),
  ("rename","重命名"),("kill","杀死"),("release-air","解除空中固定"),("delete","删除"),("revive","复活"),("properties","属性"),
  ("remove-weapons","删除所有武器"),("team-a","设为A方"),("team-b","设为B方"),("team-clear","清除队伍标记"),("pause","暂停活动"),("resume","恢复活动"),
  ("ab-hostile","A ↔ B 双方敌对"),("ab-peace","A ↔ B 双方停战"),("player-peace","对玩家停战"),("player-hostile","对玩家敌对"),("player-default","恢复对玩家默认关系"),
  ("invincible-on","开启无敌"),("invincible-off","关闭无敌"),("clear-effects","清除异常状态")
 };
 internal static string Default=>string.Join(",",Options.Select(o=>o.Id));
 internal static string[] Read(string value)=>value.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Where(id=>Options.Any(o=>o.Id==id)).Distinct(StringComparer.Ordinal).ToArray();
 internal static string Toggle(string value,string id){var order=Read(value);return string.Join(",",order.Contains(id)?order.Where(o=>o!=id):order.Append(id));}
 internal static string Move(string value,string id,int direction){var order=Read(value);int index=Array.IndexOf(order,id),next=index+Math.Sign(direction);if(index>=0&&next>=0&&next<order.Length)(order[index],order[next])=(order[next],order[index]);return string.Join(",",order);}
}
