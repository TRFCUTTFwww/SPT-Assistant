using System;
namespace LocalSpt5Esp;
internal static class MenuSections {
 internal static readonly string[] Names={"射击","人物","视觉","信息","其他"};
 internal static string Group(string section){
  int dot=section.IndexOf('.');
  if(dot<0||!int.TryParse(section.AsSpan(0,dot),out int number))return "其他";
  return number switch{
   8 or 9 or 10 or 11=>"射击",
   2 or 4 or 19 or 20=>"人物",
   1 or 6 or 7 or 12 or 16=>"视觉",
   13 or 14 or 15 or 18 or 21 or 22 or 23=>"信息",
   _=>"其他"
  };
 }
 internal static string Heading(string category){int dot=category.IndexOf(". ",StringComparison.Ordinal);return dot<0?category:category[(dot+2)..];}
}
