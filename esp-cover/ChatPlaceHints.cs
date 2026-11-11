using System;
namespace LocalSpt5Esp;
internal static class ChatPlaceHints {
 internal static string Kind(string key)=>key.Split(':')[0].ToLowerInvariant() switch{"exit"=>"撤离点","room" or "valuable"=>"房间 / 高价值地点","door"=>"钥匙房门","saved"=>"收藏地点","marker"=>"本局标记","drop" or "airdrop"=>"空投箱","interaction"=>"交互点","quest"=>"任务地点","btr"=>"BTR 车辆",_=>"地点"};
 internal static string Description(string token)=>token switch{"edit"=>"打开可视化物品编辑：参数、附件、内容物、装弹、枪械与预设","facing"=>"指定传送后的朝向目标或坐标","@s"=>"自己 / 本地玩家","@a"=>"全部人类玩家","@p"=>"最近的其他人类玩家","@e[type=ai]"=>"本局存活 AI","@e[type=boss]"=>"本局存活 Boss","@selected"=>"编辑器当前选择",_=>token.Contains(':')?Kind(token):""};
}
