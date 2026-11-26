using System;
using System.Collections.Generic;
namespace LocalSpt5Esp;
internal static class PanelPages {
 internal readonly record struct Page(int Start,int Count,float Height);
 internal static List<Page> Build(IReadOnlyList<float> heights,int maximumItems,float availableHeight){
  var pages=new List<Page>();maximumItems=Math.Max(1,maximumItems);availableHeight=Math.Max(1,availableHeight);
  int start=0,count=0;float height=0;
  for(int i=0;i<heights.Count;i++){
   float row=Math.Clamp(float.IsFinite(heights[i])?heights[i]:1,1,availableHeight);
   if(count>0&&(count>=maximumItems||height+row>availableHeight)){pages.Add(new(start,count,height));start=i;count=0;height=0;}
   height+=row;count++;
  }
  if(count>0)pages.Add(new(start,count,height));
  if(pages.Count==0)pages.Add(new(0,0,0));
  return pages;
 }
}
