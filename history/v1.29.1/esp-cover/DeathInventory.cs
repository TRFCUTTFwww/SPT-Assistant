using System;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class DeathInventory {
 static string notice="";static bool loaded,enabled;
 static void Load(){enabled=NextRaidLoot.Read().KeepOnDeath;loaded=true;}
 internal static void Draw(Rect rect){try{if(!loaded)Load();if(InterfaceMenu.Button(new Rect(rect.x,rect.y,Math.Min(390,rect.width),34),enabled?"PMC 死亡保留装备与背包 · 开启":"PMC 死亡保留装备与背包 · 关闭",enabled)){var settings=NextRaidLoot.Read();settings.KeepOnDeath=!enabled;NextRaidLoot.Save(settings);enabled=settings.KeepOnDeath;notice="已保存；死亡仍按死亡结算，物品按保留设置处理。";FeatureToasts.Show("死亡保留装备与背包",enabled);}InterfaceMenu.Paragraph(new Rect(rect.x,rect.y+46,rect.width,64),notice+"\n"+NextRaidLoot.ServerStatus,InterfaceMenu.Accent,12);}catch(Exception e){notice=e.Message;InterfaceMenu.Label(rect,notice,null,12);}}
}
