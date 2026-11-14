using System.Numerics;
namespace AllTheModium.Client;
internal static class SwordGuardRules {
 internal static bool CanRaise(bool canControl,bool idle,bool rightHeld,bool leftHeld)=>canControl&&idle&&rightHeld&&!leftHeld;
 internal static bool Frontal(Vector3 facing,Vector3 incoming){
  if(!float.IsFinite(facing.LengthSquared())||!float.IsFinite(incoming.LengthSquared())||facing.LengthSquared()<1e-6f||incoming.LengthSquared()<1e-6f)return false;
  // Direction is the projectile's travel direction, toward the victim.
  return Vector3.Dot(Vector3.Normalize(facing),-Vector3.Normalize(incoming))>0;
 }
 internal static float Damage(float amount,bool guarding,bool eligible,bool propagated,bool frontal)=>guarding&&eligible&&!propagated&&frontal&&float.IsFinite(amount)&&amount>0?amount*.5f:amount;
}
