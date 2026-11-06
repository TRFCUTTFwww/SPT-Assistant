using System.Numerics;
namespace LocalSpt5Esp;
internal static class OverlayProjectionRules {
 internal static Vector2 Pixels(float x,float y,float rectX,float rectY,float rectWidth,float rectHeight,int width,int height)
  =>new((rectX+x*rectWidth)*width,(rectY+y*rectHeight)*height);
}
