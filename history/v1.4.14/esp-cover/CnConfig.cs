using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
namespace LocalSpt5Esp;

// Translate the display metadata while preserving existing section/key identifiers on disk.
internal static class CnConfig {
 public sealed class ConfigurationManagerAttributes {
  public string DispName="",Category="",Description="";
  public Action<ConfigEntryBase>? CustomDrawer;
 }
 static readonly Dictionary<string,string> categories=new(){
  ["1. ESP"]="01. 透视显示",["2. God mode"]="02. 无敌模式",["3. Keys"]="03. 快捷键",
  ["4. AI ignore"]="04. 敌人忽略",["6. Loot details"]="06. 物品与容器详情",["7. Character colors"]="07. 人物颜色",
  ["8. Aim assist"]="08. 辅助瞄准",["9. Infinite launcher"]="09. 无限发射器",["10. Recoil assist"]="10. 无后座",
  ["11. Hit display"]="11. 命中与击杀提示",["12. Corpse ESP"]="12. 尸体显示",["13. Raid population"]="13. 局内存活人数",["14. Exfiltration"]="14. 撤离点状态",["15. Transit points"]="15. 转移点",["16. Night vision"]="16. 夜视",["17. Force doors"]="17. 破门强开"
 };
 static readonly Dictionary<string,(string Name,string Help)> common=new(){
  ["Enabled"]=("启用","开启或关闭本组功能。"),
  ["People"]=("人物透视","显示存活人物的位置、身份与生命值，Ctrl+F6 切换；尸体显示也受 Ctrl+F6 控制。"),
  ["Show health"]=("显示生命值","显示当前／最大生命值和血条。"),
  ["Items"]=("物品透视","显示地面物品、容器和尸体内物品，Ctrl+F7 切换。"),
  ["Containers"]=("显示容器","显示可搜索的容器；看向标记时在固定面板查看详情。"),
  ["People range"]=("人物显示范围（米）","存活人物标记的最大显示距离，尸体范围可在尸体显示分组单独设置。"),
  ["Loot range"]=("物品与容器范围（米）","地面物品及容器标记的最大距离，不限制尸体定位距离。"),
  ["Maximum loot labels"]=("最多物品标签","超过数量时优先显示近处物品。"),
  ["Font size"]=("字体大小","调整本组文字显示大小。"),
  ["People toggle"]=("人物／尸体显示按键","切换人物及尸体标记，默认 Ctrl+F6。"),
  ["Loot toggle"]=("物品显示按键","切换物品、容器及尸体物品详情，默认 Ctrl+F7。"),
  ["Items toggle"]=("物品显示按键","切换物品、容器及尸体物品详情，默认 Ctrl+F7。"),
  ["God mode toggle"]=("无敌切换按键","切换本地玩家的无敌状态，默认 Ctrl+F8。"),
  ["AI ignore toggle"]=("敌人忽略切换按键","切换敌人是否忽略你，默认 Ctrl+F9。"),
  ["Opacity percent"]=("显示不透明度（%）","文字、阴影、底板、边框和标记的统一不透明度。0 为隐藏，50 为半透明，100 为完整显示。"),
  ["Show item values"]=("显示物品估值","按价格快照计算堆叠和附件估值，不是实时成交价。"),
  ["Show container contents"]=("显示容器内物品","看向容器标记时展开容器名称、物品和估值。"),
  ["Contents distance"]=("物品详情范围（米）","此距离内显示容器和尸体中的物品详情。"),
  ["Maximum contents lines"]=("最多物品详情行数","优先列出高价物品；未列出的物品仍计入总估值。"),
  ["Maximum containers"]=("最多容器数量","每次扫描评估最近的这些容器，相邻同类容器合并为一组。"),
  ["High value threshold"]=("高价物品阈值（卢布）","物品或堆叠估值达到此金额时显示橙色。"),
  ["Through walls"]=("穿墙显示轮廓","即使被墙壁遮挡，也显示人物着色轮廓。"),
  ["Opacity"]=("人物轮廓不透明度","调整人物着色轮廓的不透明程度。"),
  ["Mode"]=("瞄准模式","关闭：停用自瞄；暴力：视角直接瞄向头部；静默：调整本地子弹方向，不移动视角。"),
  ["Hold key"]=("按住激活按键","按住此键激活选中的自瞄模式；Mouse4 是鼠标侧键。"),
  ["Always active"]=("自瞄常开","无需按住激活键；关闭模式仍停用自瞄，不会自动开枪。"),
  ["Always active toggle key"]=("自瞄常开切换键","切换常开与按住激活，默认 Ctrl+F4。"),
  ["Cycle mode key"]=("瞄准模式切换键","循环切换关闭、暴力和静默模式，默认 Ctrl+F3。"),
  ["Acquisition angle"]=("目标选取角度（度）","从视野中心选取目标的最大夹角。"),
  ["Maximum distance"]=("最大距离（米）","本组功能的最大作用距离。"),
  ["Close target distance"]=("近距离优先范围（米）","此范围内优先选取近距离威胁，并扩大允许角度。"),
  ["Close target angle"]=("近距离选取角度（度）","近距离威胁允许使用的目标选取角度。"),
  ["Visible targets only"]=("仅选取可见目标","要求镜头到敌人头部没有遮挡；静默穿掩体开启时，静默模式不受此项限制。"),
  ["Silent through cover"]=("静默穿掩体","静默子弹可选取被掩体遮挡的 AI；遮挡时修正子弹起点。火箭仍有碰撞。"),
  ["Shot interval"]=("发射间隔（秒）","按住正常开火键时，两发火箭之间的最短时间。"),
  ["No recoil"]=("无后座","抑制本地玩家开枪时的枪械后坐力和相关镜头上跳，独立于自瞄模式；关闭后后续射击恢复。"),
  ["Toggle key"]=("切换按键","切换无后座功能，默认 Ctrl+F5。"),
  ["Night vision toggle"]=("夜视切换按键","默认 Ctrl+N。"),
  ["Duration"]=("提示持续时间（秒）","命中与击杀提示保留时间。"),
  ["Maximum lines"]=("最多提示条数","保留最近的这些命中／击杀提示。"),
  ["Show contents"]=("显示尸体内物品","看向尸体标记时显示装备、背包及口袋内物品，需要开启 Ctrl+F7 物品显示。"),
  ["Boss color"]=("BOSS 颜色","BOSS 的人物轮廓与标签颜色，默认红色；填写 #RRGGBB 十六进制颜色。"),
  ["Follower color"]=("保镖颜色","BOSS 随从的人物轮廓与标签颜色，默认紫色；填写 #RRGGBB。"),
  ["Cultist color"]=("邪教徒颜色","邪教徒的人物轮廓与标签颜色，默认黄色；填写 #RRGGBB。"),
  ["Scav color"]=("Scav 颜色","普通 Scav 的人物轮廓与标签颜色，默认绿色；填写 #RRGGBB。"),
  ["Raider color"]=("掠夺者颜色","掠夺者的人物轮廓与标签颜色，默认橙色；填写 #RRGGBB。"),
  ["Rogue color"]=("游荡者颜色","游荡者的人物轮廓与标签颜色，默认青色；填写 #RRGGBB。"),
  ["BlackFox color"]=("黑狐颜色","黑狐及黑狐活动变体的人物轮廓与标签颜色，默认粉色；填写 #RRGGBB。"),
  ["Pmc color"]=("PMC 颜色","PMC 的人物轮廓与标签颜色，默认蓝色；填写 #RRGGBB。"),
  ["Show breakdown"]=("显示各阵营人数","分别显示 BOSS、保镖、邪教徒、Scav、掠夺者、游荡者、黑狐、PMC 的存活人数，PMC 进一步区分 USEC 和 BEAR。只统计已生成且仍存活的角色。"),
  ["Show conditional exits"]=("显示特殊撤离状态","显示本地图黑车、绿烟、拉闸点的实际状态，包括未分配给你的点。"),
  ["Maximum exits"]=("每页撤离点","状态面板显示的最大撤离点数量，优先可用点和特殊撤离点。"),
  ["Force unassigned exits"]=("强制启用未分配出口","尝试启用地图已加载但未分配给你的本阵营普通出口，跨阵营需另开 Ctrl+F11。默认关闭，Ctrl+F10 切换；保留付款、钥匙、装备和拉闸条件，不生成黑车或列车，不强改秘密撤离。"),
  ["Force toggle key"]=("强制出口切换键","切换强制启用未分配普通出口，默认 Ctrl+F10。关闭后恢复原分配；已开始的正常撤离倒计时不打断。"),
  ["Cross faction exits"]=("允许跨阵营撤离","PMC 可使用地图已加载的 Scav 出口，Scav 可使用 PMC 出口。默认关闭，Ctrl+F11 切换；仅放开阵营／分配限制，保留关闭、付款、钥匙、装备和拉闸条件，不生成车辆。"),
  ["Cross faction toggle key"]=("跨阵营撤离切换键","切换 PMC／Scav 出口互通，默认 Ctrl+F11；关闭后恢复原分配。"),
  ["Ignore exit requirements"]=("无视撤离条件","允许本地角色无视已存在撤离点的钥匙、付款、装备、合作及启动条件。"),
  ["Ignore requirements toggle key"]=("无视条件切换键","默认 Ctrl+I。")
 };
 static readonly Dictionary<string,string> enabledHelp=new(){
  ["2. God mode"]="阻止本地玩家受到伤害或死亡，不阻止战局计时结束。",
  ["4. AI ignore"]="敌人忽略本地玩家，清理敌对识别、视觉跟踪和已有敌人记忆。",
  ["7. Character colors"]="为 BOSS、保镖、邪教徒、Scav、掠夺者、游荡者、黑狐、PMC 分别着色；标签和身体颜色一致，Ctrl+F6 同时控制显示。",
  ["9. Infinite launcher"]="仅让已登记的赠送 RShG-2 重复发射，不消耗膛内火箭或丢弃武器。",
  ["11. Hit display"]="显示你造成的实际生命值损失、命中部位及归因给你的击杀，不包括护甲耐久损失。",
  ["12. Corpse ESP"]="显示尸体标记，跟随人物显示 Ctrl+F6 开关，独立于地面物品显示范围。",
  ["13. Raid population"]="在顶部显示已生成角色的当前存活人数，不含自己、尸体及尚未刷新的增援；约每半秒更新。",
  ["14. Exfiltration"]="显示最近已满足条件的本局撤离点及特殊撤离状态，距离为直线距离；约每半秒更新。"
 };
 internal static ConfigEntry<T> Bind<T>(ConfigFile file,string section,string key,T value,string description="")=>Bind(file,section,key,value,new ConfigDescription(description));
 internal static ConfigEntry<T> Bind<T>(ConfigFile file,string section,string key,T value,ConfigDescription description){
  if(!common.TryGetValue(key,out var text))throw new InvalidOperationException("缺少菜单翻译："+section+" / "+key);
  var attributes=new ConfigurationManagerAttributes{DispName=text.Name,Category=categories[section],Description=""};
  if(section=="8. Aim assist"&&key=="Mode")attributes.CustomDrawer=DrawAimMode;
  else if(typeof(T)==typeof(bool))attributes.CustomDrawer=DrawBoolean;
  return file.Bind(section,key,value,new ConfigDescription("",description.AcceptableValues,attributes));
 }
 static void DrawAimMode(ConfigEntryBase entry){
  int current=(int)(AimMode)entry.BoxedValue;
  int chosen=GUILayout.Toolbar(current,new[]{"关闭","暴力","静默"},Array.Empty<GUILayoutOption>());
  if(chosen!=current)entry.BoxedValue=(AimMode)chosen;
 }
 static void DrawBoolean(ConfigEntryBase entry){
  bool current=(bool)entry.BoxedValue;
  bool chosen=GUILayout.Toggle(current,OnOff(current),Array.Empty<GUILayoutOption>());
  if(chosen!=current)entry.BoxedValue=chosen;
 }
 internal static string OnOff(bool value)=>value?"开启":"关闭";
 internal static string AimName(AimMode mode)=>mode switch{AimMode.Snap=>"暴力",AimMode.Silent=>"静默",_=>"关闭"};
}
