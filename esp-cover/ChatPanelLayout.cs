using System;
namespace LocalSpt5Esp;
internal static class ChatPanelLayout {
 internal const int RowHeight=50;
 internal readonly record struct Hints(float Height,int Rows);
 internal static Hints Fit(float available,float syntaxHeight,int count){
  float fixedHeight=syntaxHeight+44,budget=Math.Max(0,Math.Min(available-28,Math.Max(fixedHeight+(count>0?RowHeight:0),available*.72f)));
  int rows=Math.Min(5,Math.Min(count,Math.Max(0,(int)((budget-fixedHeight)/RowHeight))));
  return new(Math.Min(budget,fixedHeight+rows*RowHeight),rows);
 }
}
