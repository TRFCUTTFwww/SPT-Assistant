using System;
namespace LocalSpt5Esp;
internal static class MenuSections {
 internal static readonly string[] Names={"射击","枪械","人物","娱乐","视觉","战局","AI 测试","物资刷新","空投","信息","装备与预设","传送","Commando","其他"};
 internal static string Group(string section){
  int dot=section.IndexOf('.');
  if(dot<0||!int.TryParse(section.AsSpan(0,dot),out int number))return "其他";
  return number switch{
   8 or 9 or 10 or 28 or 39 or 40 or 44 or 45=>"射击",
   2 or 4 or 19 or 20 or 29=>"人物",
   1 or 6 or 7 or 12 or 16=>"视觉",
   13 or 14 or 15 or 18 or 26 or 27 or 30 or 31 or 33 or 41 or 42=>"战局",
   34=>"AI 测试",38=>"空投",37=>"物资刷新",43 or 46=>"娱乐",
   11 or 21 or 22 or 23 or 35=>"信息",
   _=>"其他"
  };
 }
 internal static string Heading(string category){int dot=category.IndexOf(". ",StringComparison.Ordinal);return dot<0?category:category[(dot+2)..];}
}
