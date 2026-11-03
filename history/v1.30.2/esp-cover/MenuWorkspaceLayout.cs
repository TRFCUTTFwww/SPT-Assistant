using System;
namespace LocalSpt5Esp;
internal static class MenuWorkspaceLayout {
 internal static int Columns(float width,float minimum=112,float gap=8)=>Math.Max(1,(int)((width+gap)/(minimum+gap)));
 internal static int Rows(int count,float width,float minimum=112)=>count==0?0:(int)Math.Ceiling(count/(double)Columns(width,minimum));
 internal static float Height(int count,float width,float minimum=112)=>Rows(count,width,minimum)*40;
}
