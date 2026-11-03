using System;
using System.Collections.Generic;
using System.Numerics;
namespace LocalSpt5Esp;
internal sealed class TrajectoryPath {
 internal const int Capacity=48;internal readonly List<Vector3> Points=new();
 internal void Add(Vector3 point){if(!float.IsFinite(point.X)||!float.IsFinite(point.Y)||!float.IsFinite(point.Z)||Points.Count>0&&Vector3.DistanceSquared(Points[^1],point)<.0001f)return;Points.Add(point);if(Points.Count<=Capacity)return;int remove=1;float best=float.MaxValue;for(int i=1;i<Points.Count-1;i++){var a=Points[i]-Points[i-1];var b=Points[i+1]-Points[i-1];float error=b.LengthSquared()<1e-10f?a.LengthSquared():Vector3.Cross(a,b).LengthSquared()/b.LengthSquared();if(error<best){best=error;remove=i;}}Points.RemoveAt(remove);}
}
