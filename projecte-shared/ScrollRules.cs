namespace ProjectE;
public static class ScrollRules {
 public static float Maximum(int count,int columns,float rowHeight,float viewport)=>Math.Max(0,((Math.Max(0,count)+columns-1)/columns)*rowHeight-viewport);
 public static float Step(float offset,float wheel,int count,int visible)=>Math.Clamp(offset+wheel*3,0,Math.Max(0,count-visible));
}
