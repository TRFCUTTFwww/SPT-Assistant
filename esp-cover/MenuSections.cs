using System;
namespace LocalSpt5Esp;
internal static class MenuSections {
 internal static readonly string[] Names={"镜头与画面","传送与导航","战斗与枪械","人物与移动","战局与物资","载具与空投","物品与装备","剧情","数据","服务端","AI 工作台","枪械编辑","模型与镜头","玩具枪工具","开发工具","设置与热键"};
 internal static string Lane(string category)=>category is "AI 工作台" or "枪械编辑" or "模型与镜头" or "玩具枪工具" or "开发工具"?"开发工具":category=="设置与热键"?"设置":"常用辅助";
 internal static string DisplayName(string category)=>category switch{"战斗与枪械"=>"射击","人物与移动"=>"人物","镜头与画面"=>"视觉","传送与导航"=>"导航","战局与物资"=>"战局","载具与空投"=>"载具","物品与装备"=>"物品","AI 工作台"=>"AI 开发","玩具枪工具"=>"玩具枪开发","设置与热键"=>"设置与热键",_=>category};
 internal static int Number(string section){int dot=section.IndexOf('.');return dot>=0&&int.TryParse(section.AsSpan(0,dot),out int value)?value:0;}
 internal static string Group(string section)=>Number(section) switch{8 or 9 or 10 or 28 or 39 or 40 or 44 or 53 or 54=>"战斗与枪械",45=>"枪械编辑",2 or 19 or 20 or 29=>"人物与移动",43=>"模型与镜头",34 or 49=>"AI 工作台",50 or 46=>"玩具枪工具",1 or 6 or 7 or 11 or 12 or 16 or 21 or 22 or 23 or 35 or 48 or 52 or 58=>"镜头与画面",55 or 57 or 59=>"战局与物资",56=>"物品与装备",13 or 26 or 27 or 30 or 31 or 37 or 42=>"战局与物资",18 or 38=>"载具与空投",14 or 15 or 33 or 41=>"传送与导航",_=>"设置与热键"};
 internal static bool ThirdPersonKey(string key)=>key.Contains("Third person",StringComparison.OrdinalIgnoreCase)||key.StartsWith("Camera ",StringComparison.OrdinalIgnoreCase)||key=="First person when aiming";
 internal static string Group(string section,string key)=>Number(section)==43&&ThirdPersonKey(key)?"镜头与画面":Number(section)==43&&key.StartsWith("Selected AI",StringComparison.OrdinalIgnoreCase)?"AI 工作台":Number(section)==43&&key.Contains("Crouch",StringComparison.OrdinalIgnoreCase)?"人物与移动":Group(section);
 internal static string[] Pages(string category)=>category switch{
  "战斗与枪械"=>new[]{"射击辅助","微自瞄","瞄准排除","命中音效","击杀特效","投掷物轨迹"},
  "人物与移动"=>new[]{"生存防护","移动与穿墙","Buff 状态","死亡保留"},
  "枪械编辑"=>new[]{"枪械参数"},
  "模型与镜头"=>new[]{"自由镜头","人物模型","动作锁定"},
  "服务端"=>new[]{"配置参数","业力","Scav 装备","刷新规则"},
  "剧情"=>new[]{"主线进度","商人任务","剧情走向","结局","等待时间"},
  "数据"=>new[]{"技能","成就","钱包","角色管理"},
  "开发工具"=>new[]{"坐标读取","角色管理","命令控制台"},
  "AI 工作台"=>new[]{"AI 列表","换武器","生成 AI","动作与路线","装备与 Buff","位置传送","AI 模板","地图场景","尸体清理","全局控制","刷新规则"},
  "玩具枪工具"=>new[]{"生成与模板","命中操作","子弹与射速","出生参数","普通枪弹着点"},
  "镜头与画面"=>new[]{"人物显示","战斗信息","第三人称","性能监测","界面布局"},
  "战局与物资"=>new[]{"地图模式","时间与环境","本局物资","范围拾取","刷新规则","活动控制","任务物资","战局信息"},
  "载具与空投"=>new[]{"BTR 控制","空投控制"},
  "传送与导航"=>new[]{"撤离定位","撤离控制","地图过图","路面导航","角色传送"},
  "物品与装备"=>new[]{"添加物品","玩家枪械预设","整套装备","修理与耐久"},
  _=>new[]{"全部热键","配置方案","主题与布局","其他设置"}
 };
 internal static string Subtitle(string category)=>category switch{"服务端"=>"服务端配置、PMC 与 Scav 业力，以及 Scav 出击装备","AI 工作台"=>"选一次角色，连续完成生成、动作、装备与场景编辑","玩具枪工具"=>"生成类型、模板、命中操作和射击设置都在这里","战斗与枪械"=>"瞄准、伤害与命中反馈","人物与移动"=>"生存、移动、穿墙与状态","镜头与画面"=>"人物标记、第三人称、HUD 与战斗显示","战局与物资"=>"本局时间、物资、任务与下局刷新规则","载具与空投"=>"BTR 路线与空投投放","传送与导航"=>"撤离点定位、撤离控制、地图过图与路面导航","物品与装备"=>"物品添加、枪械与整套装备方案","枪械编辑"=>"当前武器与配件参数编辑","模型与镜头"=>"自由镜头、人物模型与动作锁定","剧情"=>"主线进度、任务重做与剧情走向","数据"=>"技能、成就、商城钱包与角色管理","开发工具"=>"角色管理与命令控制台",_=>"快捷键、配置方案与界面设置"};
 internal static bool OnPage(string category,string page,string section,string key){int n=Number(section);return (category,page) switch{
  ("战斗与枪械","投掷物轨迹")=>n==54,
  ("战局与物资","范围拾取")=>n==55,
  ("战局与物资","本局物资")=>n==59,
  ("战局与物资","地图模式")=>n==57,
  ("物品与装备","修理与耐久")=>n==56,
  ("战斗与枪械","击杀特效")=>n==53,
  ("战斗与枪械","射击辅助")=>n is 8 or 9 or 10 or 28 or 39 or 40 or 44,
  ("人物与移动","生存防护")=>n is 2 or 19 or 29,
  ("人物与移动","移动与穿墙")=>n==20||n==43&&key.Contains("Crouch",StringComparison.OrdinalIgnoreCase),
  ("模型与镜头","人物模型")=>n==43&&!ThirdPersonKey(key)&&!key.Contains("Crouch",StringComparison.OrdinalIgnoreCase)&&!key.Contains("pose",StringComparison.OrdinalIgnoreCase)&&!key.StartsWith("Selected AI",StringComparison.OrdinalIgnoreCase),
  ("镜头与画面","第三人称")=>n==43&&ThirdPersonKey(key),
  ("镜头与画面","人物显示")=>n is 1 or 6 or 7 or 12 or 16,
  ("镜头与画面","战斗信息")=>n is 11 or 21 or 22 or 23 or 35 or 52,
  ("镜头与画面","性能监测")=>n==58,
  ("战局与物资","战局信息")=>n is 13 or 30 or 42,
  ("传送与导航","撤离定位")=>n==14&&!key.Contains("Force",StringComparison.OrdinalIgnoreCase)&&!key.Contains("Ignore",StringComparison.OrdinalIgnoreCase),
  ("传送与导航","撤离控制")=>n==14&&(key.Contains("Force",StringComparison.OrdinalIgnoreCase)||key.Contains("Ignore",StringComparison.OrdinalIgnoreCase)),
  ("传送与导航","地图过图")=>n is 15 or 41,
  ("设置与热键","其他设置")=>n is 3 or 4 or 17 or 47||n==0,
  _=>false
 };}
 internal static string Heading(string category){int dot=category.IndexOf(". ",StringComparison.Ordinal);return dot<0?category:category[(dot+2)..];}
}
