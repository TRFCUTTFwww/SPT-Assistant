using System;
namespace LocalSpt5Esp;
internal static class MenuSections {
 internal static readonly string[] Names={"战斗与枪械","人物与移动","AI 工作台","玩具枪工具","镜头与画面","战局与物资","载具与空投","传送与导航","物品与装备","设置与热键"};
 internal static string DisplayName(string category)=>category switch{"战斗与枪械"=>"射击","人物与移动"=>"人物","AI 工作台"=>"AI 管理","镜头与画面"=>"视觉","战局与物资"=>"战局","载具与空投"=>"BTR","传送与导航"=>"传送","物品与装备"=>"装备与预设","设置与热键"=>"其他",_=>category};
 internal static int Number(string section){int dot=section.IndexOf('.');return dot>=0&&int.TryParse(section.AsSpan(0,dot),out int value)?value:0;}
 internal static string Group(string section)=>Number(section) switch{8 or 9 or 10 or 28 or 39 or 40 or 44 or 45=>"战斗与枪械",2 or 19 or 20 or 29 or 43=>"人物与移动",34 or 49=>"AI 工作台",50 or 46=>"玩具枪工具",51=>"队友",1 or 6 or 7 or 11 or 12 or 16 or 21 or 22 or 23 or 35 or 48 or 52=>"镜头与画面",13 or 26 or 27 or 30 or 31 or 37 or 42=>"战局与物资",18 or 38=>"载具与空投",14 or 15 or 33 or 41=>"传送与导航",_=>"设置与热键"};
 internal static bool ThirdPersonKey(string key)=>key.Contains("Third person",StringComparison.OrdinalIgnoreCase)||key.StartsWith("Camera ",StringComparison.OrdinalIgnoreCase)||key=="First person when aiming";
 internal static string Group(string section,string key)=>Number(section)==43&&ThirdPersonKey(key)?"镜头与画面":Number(section)==43&&key.StartsWith("Selected AI",StringComparison.OrdinalIgnoreCase)?"AI 工作台":Group(section);
 internal static string[] Pages(string category)=>category switch{
  "战斗与枪械"=>new[]{"射击辅助","枪械参数","微自瞄","瞄准排除","命中音效"},
  "人物与移动"=>new[]{"生存防护","移动与穿墙","Buff 状态","模型与动作","动作锁定","死亡保留","角色管理"},
  "AI 工作台"=>new[]{"AI 列表","换武器","生成 AI","动作与路线","装备与 Buff","位置传送","AI 模板","地图场景","尸体清理","全局控制","刷新规则"},
  "队友"=>new[]{"战前编队","队伍指令","物资与设置"},
  "玩具枪工具"=>new[]{"生成与模板","命中操作","子弹与射速","出生参数","普通枪弹着点"},
  "镜头与画面"=>new[]{"镜头控制","第三人称","人物显示","战斗信息","界面布局"},
  "战局与物资"=>new[]{"时间与环境","本局物资","刷新规则","活动控制","任务物资","战局信息"},
  "载具与空投"=>new[]{"BTR 控制","空投控制"},
  "传送与导航"=>new[]{"角色传送","路面导航","撤离与过图"},
  "物品与装备"=>new[]{"添加物品","玩家枪械预设","整套装备"},
  _=>new[]{"全部热键","配置方案","主题与布局","命令控制台","其他设置"}
 };
 internal static string Subtitle(string category)=>category switch{"队友"=>"战前选队友，入场组队，统一指挥战斗、跟随与物资交付","AI 工作台"=>"选一次角色，连续完成生成、动作、装备与场景编辑","玩具枪工具"=>"生成类型、模板、命中操作和射击设置都在这里","战斗与枪械"=>"瞄准、伤害、枪械参数与命中反馈","人物与移动"=>"生存、移动、穿墙、状态与人物动作","镜头与画面"=>"自由镜头、第三人称、HUD 与战斗显示","战局与物资"=>"本局时间、物资、任务与下局刷新规则","载具与空投"=>"BTR 路线与空投投放","传送与导航"=>"玩家或 AI 传送、地图目标与路面导航","物品与装备"=>"物品添加、枪械与整套装备方案",_=>"快捷键、配置方案、界面与命令"};
 internal static bool OnPage(string category,string page,string section,string key){int n=Number(section);return (category,page) switch{
  ("战斗与枪械","射击辅助")=>n is 8 or 9 or 10 or 28 or 39 or 40 or 44,
  ("人物与移动","生存防护")=>n is 2 or 19 or 29,
  ("人物与移动","移动与穿墙")=>n==20||n==43&&key.Contains("Crouch",StringComparison.OrdinalIgnoreCase),
  ("人物与移动","模型与动作")=>n==43&&!ThirdPersonKey(key)&&!key.Contains("Crouch",StringComparison.OrdinalIgnoreCase)&&!key.Contains("pose",StringComparison.OrdinalIgnoreCase),
  ("镜头与画面","第三人称")=>n==43&&ThirdPersonKey(key),
  ("镜头与画面","人物显示")=>n is 1 or 6 or 7 or 12 or 16,
  ("镜头与画面","战斗信息")=>n is 11 or 21 or 22 or 23 or 35 or 52,
  ("战局与物资","战局信息")=>n is 13 or 30 or 42,
  ("传送与导航","撤离与过图")=>n is 14 or 15 or 41,
  ("设置与热键","其他设置")=>n is 3 or 4 or 17 or 47||n==0,
  _=>false
 };}
 internal static string Heading(string category){int dot=category.IndexOf(". ",StringComparison.Ordinal);return dot<0?category:category[(dot+2)..];}
}
