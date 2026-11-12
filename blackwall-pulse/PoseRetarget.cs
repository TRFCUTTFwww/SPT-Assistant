using System.Numerics;
namespace LocalBlackwallPulse;
internal static class PoseRetarget {
 internal static Quaternion Map(Quaternion targetReference,Quaternion sourceReference,Quaternion sourceCurrent)=>Quaternion.Normalize(targetReference*Quaternion.Inverse(sourceReference)*sourceCurrent);
 internal static float Flexion(Quaternion reference,Quaternion current,Vector3 axis,float limit=85){
  if(axis.LengthSquared()<.000001f)return 0;
  axis=Vector3.Normalize(axis);var delta=Quaternion.Normalize(Quaternion.Inverse(reference)*current);
  if(delta.W<0)delta=new(-delta.X,-delta.Y,-delta.Z,-delta.W);
  float angle=2*MathF.Atan2(Vector3.Dot(new(delta.X,delta.Y,delta.Z),axis),delta.W)*180/MathF.PI;
  return Math.Clamp(angle,-limit,limit);
 }
 internal static bool ElbowName(string name){string n=new(name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());return n.EndsWith("rforearm1",StringComparison.Ordinal)||n.EndsWith("rforearm",StringComparison.Ordinal)||n.EndsWith("rightforearm",StringComparison.Ordinal);}
}
