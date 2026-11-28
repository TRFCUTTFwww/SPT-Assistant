using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using LocalSpt5Esp;
internal static class RecentLocalizationChecks {
 internal static void Run(string sourceRoot){
  Dictionary<string,string> Read(string name)=>JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(sourceRoot,"esp-cover/Locales/"+name)))!;
  var catalog=new LocalizationCatalog(Read("en.json"),Read("en-formats.json"),JsonSerializer.Deserialize<Dictionary<string,int[]>>(File.ReadAllText(Path.Combine(sourceRoot,"esp-cover/Locales/en-verbatim.json"))));
  var samples=new[]{
   "历史战局","战局列表","入场装备","带出与保留","新增物品","击杀名单","成功撤离","进行中／未结算","每秒自动同步 O 键时间","保存并立即刷新","动态 JavaScript · 开启","已更新 · 00:04:20","执行失败，保留上次文字：请使用 return 返回要显示的文字。","日志与诊断","调试","部位血量","像素剑","右键菜单","⚠ 调试专用 · 错误保护测试","物理警告调用栈采集","热更新失败与回退（受控模拟）","行动前设置地图游戏时刻","已停用 · 启用","列车已到站 · 发车倒计时 00:03:20","到站倒计时 00:01:10 · 发车倒计时 00:05:00","到站倒计时已设为 00:01:00；原停站时长保持 00:04:00。","发车倒计时已设为 00:02:00。","日志已导出：D:/logs/test.zip","✓  暂停／恢复活动","○  删除所有武器","已同步 3 项制作，可前往领取。","当前地图匹配 2 / 5 个地点目标；其他目标可能位于别的地图，或没有原生坐标。",
   "清除 BTR 对玩家的敌对状态","BTR 始终友好（忽略误伤）","已清除 BTR 对玩家的敌对状态。",
   "动态颜色变化","变色 · 关","彩虹渐变","双色渐变","呼吸脉冲","变色速度","精确速度","着色方式 · 纯色剪影（适合黑线稿）","着色方式 · 原图叠色（保留明暗）",
   "在此格添加物品…","在此格用 JSON 添加…","粘贴剪贴板 JSON 到此格","目录选择添加","JSON / 预设添加","空格位 (2, 4)","在格位 (2, 4) 添加物品","已向格位 (2, 4) 添加 3 件物品。","展开完整文本","整树带钩","整树满耐久"
  };
  foreach(string source in samples){string text=catalog.Text(source);if(text==source||text.Any(c=>c is >= '\u3400' and <= '\u9fff'))throw new Exception("Untranslated recent UI: "+source+" => "+text);}
  if(catalog.Text("当前显示：开启")!="Current text: 开启")throw new Exception("User script text was translated");
  string preview=catalog.Text("来源：开启 → 当前角色：取消\n还原后 PMC 物品总数：42。还原将替换所选类别，执行前自动备份当前存档。");
  if(!preview.StartsWith("Source: 开启 → Current character: 取消\nPMC items after restore: 42."))throw new Exception("Backup preview translated character names or lost counts");
  if(catalog.Text("BTR 站点 · 搜索")!="BTR stop · 搜索"||catalog.Text("已添加武器：开启")!="Added weapon: 开启")throw new Exception("Named arguments were translated");
  const string build="20261120-155930.1303744Z";
  var additions=new Dictionary<string,string>{
   ["开发版 "+build]="Dev "+build,
   ["开发版本 "+build]="Development build "+build,
   ["统计：4 场 · 撤离 3（75%）· 累计击杀 10"]="Raids: 4 · Extractions: 3 (75%) · Kills: 10",
   ["估值：入场 ₽2,000 · 带出 ₽3,500 · 新增 ₽1,500"]="Estimated value: Starting ₽2,000 · Extracted ₽3,500 · Gained ₽1,500",
   ["门正在交互或被封锁，请等待后重试。"]="The door is in use or blocked. Wait and try again.",
   ["测试成功 · 15 ms · 12:00:01.234"]="Test passed · 15 ms · 12:00:01.234",
   ["已载入预设：开启"]="Preset loaded: 开启",
   ["所有文件"]="All files",
   ["JSON 编辑"]="JSON editor",
   ["准星测距悬浮窗"]="Crosshair rangefinder",
   ["物品树 JSON 必须是数组。"]="Item tree JSON must be an array.",
   ["不能在此添加或删除物品。"]="Items cannot be added or removed here.",
   ["物品树包含重复编号或循环引用。"]="The item tree contains duplicate IDs or a cycle.",
   ["存档 JSON 编辑器 · 开启 · 803 KB · 修订 A1B2C3D4"]="Profile JSON editor · 开启 · 803 KB · Rev A1B2C3D4",
   ["市场规则"]="Market rules",
   ["全物品解禁"]="Unban all items",
   ["解除等级限制"]="Remove level locks",
   ["跳蚤购买带战局勾"]="Found-in-raid purchases",
   ["纯卢布市场"]="Rouble-only market",
   ["屏蔽以物易物单"]="Hide barter offers",
   ["直购到背包"]="Buy to bag",
   ["商人最高：Therapist ₽42,500  |  税后净得：₽51,200（+₽8,700）"]="Best trader: Therapist ₽42,500  |  Flea net: ₽51,200 (+₽8,700)",
   ["已直购到背包：M995 ×60（-₽48,000）"]="Bought to bag: M995 ×60 (-₽48,000)"
  };
  foreach(var pair in additions)if(catalog.Text(pair.Key)!=pair.Value)throw new Exception("Incorrect translation: "+pair.Key);
  Console.WriteLine("PASS recent panel English, train/status formats, checked actions and verbatim backup/stop/item names");
 }
}
