using System;
using System.Collections.Generic;
using System.Linq;
namespace LocalSpt5Esp;
internal sealed class MenuRowLayout {
 internal float[] Tops {get;private set;}=Array.Empty<float>();
 internal float[] Heights {get;private set;}=Array.Empty<float>();
 internal float Height {get;private set;}
 internal void Build(IEnumerable<float> heights,float gap=4){Heights=heights.ToArray();Tops=new float[Heights.Length];float y=0;for(int i=0;i<Heights.Length;i++){Tops[i]=y;y+=Heights[i]+gap;}Height=Math.Max(0,y-(Heights.Length>0?gap:0));}
 internal (int First,int End) Visible(float offset,float viewport){
  if(Tops.Length==0)return(0,0);
  int index=Array.BinarySearch(Tops,Math.Max(0,offset));int first=index>=0?index:Math.Max(0,~index-1);
  while(first<Tops.Length&&Tops[first]+Heights[first]<=offset)first++;
  int end=first;while(end<Tops.Length&&Tops[end]<offset+viewport)end++;
  return(first,end);
 }
}
