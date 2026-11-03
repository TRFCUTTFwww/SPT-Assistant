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
  ["1. ESP"]="01. 透视显示",["2. God mode"]="02. 玩家无敌",["3. Keys"]="03. 快捷键",
  ["4. AI ignore"]="04. 敌人忽略",["6. Loot details"]="06. 物品与容器详情",["7. Character colors"]="07. 人物颜色",
  ["8. Aim assist"]="08. 辅助瞄准",["9. Infinite launcher"]="09. 无限发射器",["10. Recoil assist"]="10. 无后座",
  ["11. Hit display"]="11. 命中与击杀提示",["12. Corpse ESP"]="12. 尸体显示",["13. Raid population"]="13. 局内存活人数",["14. Exfiltration"]="14. 撤离点状态",["15. Transit points"]="15. 转移点",["16. Night vision"]="16. 夜视",["17. Force doors"]="17. 破门强开",["18. BTR"]="18. BTR 站点",["19. Vitals"]="19. 生存状态",["20. Movement"]="20. 体力与移动",["21. Magazine"]="21. 弹夹信息",["22. Armor analysis"]="22. 头甲分析",["23. Aggro tracking"]="23. 仇恨追踪"
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
  ["Mode"]=("瞄准模式","关闭：停用自瞄；暴力：视角直接瞄向选中部位；静默：调整本地子弹方向；微自瞄：缓慢移向选中部位。"),
  ["Target body part"]=("瞄准部位","选择辅助瞄准锁定的身体部位，默认头部。"),
  ["Hold key"]=("按住激活按键","按住此键激活选中的自瞄模式；Mouse4 是鼠标侧键。"),
  ["Always active"]=("自瞄常开","无需按住激活键；关闭模式仍停用自瞄，不会自动开枪。"),
  ["Always active toggle key"]=("自瞄常开切换键","切换常开与按住激活，默认 Ctrl+F4。"),
  ["Cycle mode key"]=("瞄准模式切换键","循环切换关闭、暴力和静默模式，默认 Ctrl+F3。"),
  ["Acquisition angle"]=("目标选取角度（度）","从视野中心选取目标的最大夹角。"),
  ["Maximum distance"]=("最大距离（米）","本组功能的最大作用距离。"),
  ["Close target distance"]=("近距离优先范围（米）","此范围内优先选取近距离威胁，并扩大允许角度。"),
  ["Close target angle"]=("近距离选取角度（度）","近距离威胁允许使用的目标选取角度。"),
  ["Visible targets only"]=("仅选取可见目标","要求镜头到选中部位没有遮挡；静默穿掩体开启时，静默模式不受此项限制。"),
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
  ["BlackFox color"]=("黑色军团颜色","黑色军团及黑色军团活动变体的人物轮廓与标签颜色，默认粉色；填写 #RRGGBB。"),
  ["Pmc color"]=("PMC 颜色","PMC 的人物轮廓与标签颜色，默认蓝色；填写 #RRGGBB。"),
  ["Show breakdown"]=("显示各阵营人数","分别显示 BOSS、保镖、邪教徒、Scav、掠夺者、游荡者、黑色军团、PMC 的存活人数，PMC 进一步区分 USEC 和 BEAR。只统计已生成且仍存活的角色。"),
  ["Show conditional exits"]=("显示特殊撤离状态","显示本地图黑车、绿烟、拉闸点的实际状态，包括未分配给你的点。"),
  ["Panel mode"]=("撤离信息窗口","关闭、按住显示或常开；默认按住 + 键显示。"),
  ["Panel hold key"]=("窗口按住显示键","按住此键显示撤离／转移信息窗口，不需要 Ctrl；默认 +，也支持小键盘 +。"),
  ["Panel width"]=("窗口宽度","撤离／转移信息窗口的宽度，默认 440 像素。"),
  ["Magazine panel mode"]=("弹夹信息窗口","关闭、按住显示或常开；默认与撤离窗口一样按住 + 键显示，也可单独改键。"),
  ["Magazine hold key"]=("弹夹窗口按键","按住此键显示当前弹夹内的子弹信息，默认 +；可独立设置。"),
  ["Magazine panel width"]=("弹夹窗口宽度","左侧弹夹信息窗口的宽度。"),
  ["Aggro maximum lines"]=("最多追踪数量","窗口最多列出的 AI 数量，优先显示当前锁定你的 AI。"),
  ["Exit marker mode"]=("撤离点标记","关闭、仅显示最近可用撤离点，或显示所有已加载撤离点；同名出口合并显示，标记附带直线距离。"),
  ["Show lever markers"]=("显示拉闸位置标记","标出与撤离点关联的开关位置、距离和开启状态；可独立于撤离点标记开关控制。"),
  ["Unlimited leg stamina"]=("无限绿条（腿部体力）","仅补满本地玩家的腿部体力，关闭后恢复正常消耗。"),
  ["Unlimited arm stamina"]=("无限蓝条（手臂体力）","仅补满本地玩家的手臂体力，关闭后恢复正常消耗。"),
  ["Speed enabled"]=("人物加速","仅提高本地玩家步行与冲刺速度，关闭后恢复原速。"),
  ["Speed multiplier"]=("移动速度倍数","人物加速开启时使用；1.0 为原速，可设为 1.0～5.0 倍，默认 1.5 倍。"),
  ["Maximum exits"]=("每页撤离点","状态面板显示的最大撤离点数量，优先可用点和特殊撤离点。"),
  ["Force unassigned exits"]=("强制启用未分配出口","尝试启用地图已加载但未分配给你的本阵营普通出口，默认关闭，Ctrl+F10 切换；保留付款、钥匙、装备和拉闸条件，不生成黑车或列车，不强改秘密撤离。"),
  ["Force toggle key"]=("强制出口切换键","切换强制启用未分配普通出口，默认 Ctrl+F10。关闭后恢复原分配；已开始的正常撤离倒计时不打断。"),
  ["Ignore exit requirements"]=("无视撤离条件","跳过已经开启的普通撤离点的个人条件；电梯、列车、拉闸和暗号撤离保留原本启动流程。"),
  ["Ignore requirements toggle key"]=("无视条件切换键","默认 Ctrl+I。")
 };
 static readonly Dictionary<string,string> enabledHelp=new(){
  ["2. God mode"]="阻止本地玩家受到伤害或死亡，不阻止战局计时结束。",
  ["4. AI ignore"]="敌人忽略本地玩家，清理敌对识别、视觉跟踪和已有敌人记忆。",
  ["7. Character colors"]="为 BOSS、保镖、邪教徒、Scav、掠夺者、游荡者、黑色军团、PMC 分别着色；标签和身体颜色一致，Ctrl+F6 同时控制显示。",
  ["9. Infinite launcher"]="仅让已登记的赠送 RShG-2 重复发射，不消耗膛内火箭或丢弃武器。",
  ["11. Hit display"]="显示你造成的实际生命值损失、命中部位及归因给你的击杀，不包括护甲耐久损失。",
  ["12. Corpse ESP"]="显示尸体标记，跟随人物显示 Ctrl+F6 开关，独立于地面物品显示范围。",
  ["13. Raid population"]="在顶部显示已生成角色的当前存活人数，不含自己、尸体及尚未刷新的增援；约每半秒更新。",
  ["14. Exfiltration"]="撤离点标记可选关闭、最近或全部；信息窗口可关闭、按住 + 显示或常开，并显示列车和 BTR 信息。",
  ["18. BTR"]="在撤离／转移信息窗口显示 BTR 当前站、剩余停留时间和下一站。",
  ["19. Vitals"]="战局中常驻显示自己的总血量、水分和能量；约每 0.2 秒更新，低数值变色提醒。",
  ["20. Movement"]="分别控制腿部与手臂无限体力，并可调节本地玩家的移动速度。",
  ["21. Magazine"]="左侧显示当前武器弹夹内的弹种、数量、伤害和穿透值。",
  ["22. Armor analysis"]="瞄准敌人附近时，根据当前弹药和对方头甲显示粗略穿透与伤害估计。",
  ["23. Aggro tracking"]="按住 + 在左侧中间显示当前把你作为目标的 AI，以及仍记得你的 AI；也支持小键盘 +。"
 };
 internal static ConfigEntry<T> Bind<T>(ConfigFile file,string section,string key,T value,string description="")=>Bind(file,section,key,value,new ConfigDescription(description));
 internal static ConfigEntry<T> Bind<T>(ConfigFile file,string section,string key,T value,ConfigDescription description){
  if(!common.TryGetValue(key,out var text))throw new InvalidOperationException("缺少菜单翻译："+section+" / "+key);
  var attributes=new ConfigurationManagerAttributes{DispName=text.Name,Category=categories[section],Description=""};
  if(section=="2. God mode"&&key=="Enabled")attributes.DispName="玩家无敌（不保护 AI）";
  if(section=="8. Aim assist"&&key=="Mode")attributes.CustomDrawer=DrawAimMode;
  else if(section=="8. Aim assist"&&key=="Target body part")attributes.CustomDrawer=DrawAimTargetPart;
  else if(section=="14. Exfiltration"&&key=="Panel mode")attributes.CustomDrawer=DrawExfilPanelMode;
  else if(section=="21. Magazine"&&key=="Magazine panel mode")attributes.CustomDrawer=DrawExfilPanelMode;
  else if(section=="14. Exfiltration"&&key=="Exit marker mode")attributes.CustomDrawer=DrawExfilMarkerMode;
  else if(typeof(T)==typeof(bool))attributes.CustomDrawer=DrawBoolean;
  return file.Bind(section,key,value,ConfigLimits.Describe(section,key,value,new ConfigDescription(text.Help,description.AcceptableValues,attributes)));
 }
 static void DrawAimMode(ConfigEntryBase entry){
  int current=(int)(AimMode)entry.BoxedValue;
  int chosen=GUILayout.Toolbar(current,new[]{"关闭","暴力","静默","微自瞄"},Array.Empty<GUILayoutOption>());
  if(chosen!=current)entry.BoxedValue=(AimMode)chosen;
 }
 static void DrawAimTargetPart(ConfigEntryBase entry){
  int current=(int)(AimTargetPart)entry.BoxedValue;
  int chosen=GUILayout.Toolbar(current,new[]{"头","胸","腹","左臂","右臂","左腿","右腿"},Array.Empty<GUILayoutOption>());
  if(chosen!=current)entry.BoxedValue=(AimTargetPart)chosen;
 }
 static void DrawExfilPanelMode(ConfigEntryBase entry){
  int current=(int)(ExfilPanelMode)entry.BoxedValue;
  int chosen=GUILayout.Toolbar(current,new[]{"关闭","按住显示","常开"},Array.Empty<GUILayoutOption>());
  if(chosen!=current)entry.BoxedValue=(ExfilPanelMode)chosen;
 }
 static void DrawExfilMarkerMode(ConfigEntryBase entry){
  int current=(int)(ExfilMarkerMode)entry.BoxedValue;
  int chosen=GUILayout.Toolbar(current,new[]{"关闭","最近","全部"},Array.Empty<GUILayoutOption>());
  if(chosen!=current)entry.BoxedValue=(ExfilMarkerMode)chosen;
 }
 static void DrawBoolean(ConfigEntryBase entry){
  bool current=(bool)entry.BoxedValue;
  bool chosen=GUILayout.Toggle(current,OnOff(current),Array.Empty<GUILayoutOption>());
  if(chosen!=current)entry.BoxedValue=chosen;
 }
 internal static string OnOff(bool value)=>value?"开启":"关闭";
 internal static string AimName(AimMode mode)=>mode switch{AimMode.Snap=>"暴力",AimMode.Silent=>"静默",AimModes.Soft=>"微自瞄",_=>"关闭"};
}
