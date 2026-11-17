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
  var samples=new[]{"日志与诊断","调试","部位血量","像素剑","右键菜单","⚠ 调试专用 · 错误保护测试","物理警告调用栈采集","热更新失败与回退（受控模拟）","行动前设置地图游戏时刻","已停用 · 启用","列车已到站 · 发车倒计时 00:03:20","到站倒计时 00:01:10 · 发车倒计时 00:05:00","到站倒计时已设为 00:01:00；原停站时长保持 00:04:00。","发车倒计时已设为 00:02:00。","日志已导出：D:/logs/test.zip","✓  暂停／恢复活动","○  删除所有武器","已同步 3 项制作，可前往领取。","当前地图匹配 2 / 5 个地点目标；其他目标可能位于别的地图，或没有原生坐标。"};
  foreach(string source in samples){string text=catalog.Text(source);if(text==source||text.Any(c=>c is >= '\u3400' and <= '\u9fff'))throw new Exception("Untranslated recent UI: "+source+" => "+text);}
  string preview=catalog.Text("来源：开启 → 当前角色：取消\n还原后 PMC 物品总数：42。还原将替换所选类别，执行前自动备份当前存档。");
  if(!preview.StartsWith("Source: 开启 → Current character: 取消\nPMC items after restore: 42."))throw new Exception("Backup preview translated character names or lost counts");
  if(catalog.Text("BTR 站点 · 搜索")!="BTR stop · 搜索"||catalog.Text("已添加武器：开启")!="Added weapon: 开启")throw new Exception("Named arguments were translated");
  Console.WriteLine("PASS recent panel English, train/status formats, checked actions and verbatim backup/stop/item names");
 }
}
