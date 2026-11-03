using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace LocalSpt5Esp;
internal static class RouteGeometry {
 internal readonly record struct Projection(Vector3 Point,int Segment,float Along,float Distance);
 internal readonly record struct Arrow(Vector3 Point,Vector3 Direction);
 internal static Vector3[] Clean(IEnumerable<Vector3> input){var result=new List<Vector3>();foreach(var point in input){if(!float.IsFinite(point.X)||!float.IsFinite(point.Y)||!float.IsFinite(point.Z))throw new ArgumentException("导航路径包含无效坐标。");if(result.Count==0||Vector3.DistanceSquared(result[^1],point)>.0004f)result.Add(point);if(result.Count>4096)throw new ArgumentException("导航路径过长。");}return result.ToArray();}
 internal static float Length(IReadOnlyList<Vector3> points){float result=0;for(int i=1;i<points.Count;i++)result+=Vector3.Distance(points[i-1],points[i]);return result;}
 internal static Projection Project(IReadOnlyList<Vector3> points,Vector3 origin){if(points.Count<2)throw new ArgumentException("导航至少需要两个路径点。");var best=new Projection(points[0],0,0,float.PositiveInfinity);float along=0;for(int i=0;i<points.Count-1;i++){var delta=points[i+1]-points[i];float squared=delta.LengthSquared(),length=MathF.Sqrt(squared);float t=squared>.000001f?Math.Clamp(Vector3.Dot(origin-points[i],delta)/squared,0,1):0;var candidate=points[i]+delta*t;float distance=Vector3.Distance(candidate,origin);if(distance<best.Distance-.001f)best=new(candidate,i,along+t*length,distance);along+=length;}return best;}
 internal static Vector3[] Slice(IReadOnlyList<Vector3> points,Projection start,float maximum){if(points.Count<2||maximum<=0||!float.IsFinite(maximum))return Array.Empty<Vector3>();var result=new List<Vector3>{start.Point};float left=maximum;for(int i=start.Segment+1;i<points.Count;i++){var from=result[^1];float distance=Vector3.Distance(from,points[i]);if(distance<=.001f)continue;if(distance>left){result.Add(Vector3.Lerp(from,points[i],left/distance));break;}result.Add(points[i]);left-=distance;if(left<=.001f)break;}return result.ToArray();}
 internal static IEnumerable<Arrow> Arrows(IReadOnlyList<Vector3> points,float spacing){if(!float.IsFinite(spacing)||spacing<.5f)throw new ArgumentException("箭头间距过小。");float next=1,along=0;for(int i=1;i<points.Count;i++){var delta=points[i]-points[i-1];float length=delta.Length();if(length<.001f)continue;while(next<=along+length){float offset=next-along;var horizontal=new Vector3(delta.X,0,delta.Z);if(horizontal.LengthSquared()>.01f&&Math.Abs(delta.Y)/length<.85f)yield return new(points[i-1]+delta*(offset/length),Vector3.Normalize(horizontal));next+=spacing;}along+=length;}}
 internal static bool SpecialConnection(Vector3 a,Vector3 b)=>Math.Abs(b.Y-a.Y)>1.25f&&new Vector2(b.X-a.X,b.Z-a.Z).Length()<.9f;
}
