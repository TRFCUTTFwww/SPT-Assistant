using System;
using System.Numerics;
namespace LocalSpt5Esp;
internal readonly record struct EditorSelectionBox(float X,float Y,float Width,float Height);
internal static class EditorSelectionRules {
 internal static EditorSelectionBox Box(float ax,float ay,float bx,float by)=>new(Math.Min(ax,bx),Math.Min(ay,by),Math.Abs(bx-ax),Math.Abs(by-ay));
 internal static bool Contains(EditorSelectionBox box,float x,float y,float depth,float width,float height)=>float.IsFinite(x)&&float.IsFinite(y)&&float.IsFinite(depth)&&depth>0&&x>=0&&y>=0&&x<=width&&y<=height&&x>=box.X&&y>=box.Y&&x<=box.X+box.Width&&y<=box.Y+box.Height;
 internal static Vector3[] Translate(Vector3[] source,Vector3 goal){
  if(source.Length==0)throw new ArgumentException("请先选择对象。");
  bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
  if(!Finite(goal)||Array.Exists(source,v=>!Finite(v)))throw new ArgumentException("目标坐标无效。");
  // Divide first so adding a large selection cannot overflow the centroid sum.
  Vector3 center=Vector3.Zero;foreach(var value in source)center+=value/source.Length;
  var result=Array.ConvertAll(source,v=>goal+(v-center));if(Array.Exists(result,v=>!Finite(v)))throw new ArgumentException("目标坐标无效。");return result;
 }
}
