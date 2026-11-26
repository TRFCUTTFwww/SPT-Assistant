using UnityEngine;

namespace LocalSpt5Esp;

internal static class ActionButtons {
 internal static int Draw(Rect area,string[] labels,out float height,int maximumColumns=4){
  int columns=Math.Max(1,Math.Min(maximumColumns,Math.Min(labels.Length,(int)((area.width+6)/126))));
  float width=(area.width-(columns-1)*6)/columns;
  height=(int)Math.Ceiling(labels.Length/(double)columns)*34;
  int result=-1;
  for(int i=0;i<labels.Length;i++)if(InterfaceMenu.Button(new(area.x+i%columns*(width+6),area.y+i/columns*34,width,28),labels[i]))result=i;
  return result;
 }
}
