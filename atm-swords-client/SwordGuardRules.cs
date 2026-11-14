using System.Numerics;
namespace AllTheModium.Client;
internal static class SwordGuardRules {
 internal static bool CanRaise(bool canControl,bool idle,bool rightHeld,bool leftHeld)=>canControl&&idle&&rightHeld&&!leftHeld;
 internal static bool Frontal(Vector3 facing,Vector3 incoming){
  if(!float.IsFinite(facing.LengthSquared())||!float.IsFinite(incoming.LengthSquared())||facing.LengthSquared()<1e-6f||incoming.LengthSquared()<1e-6f)return false;
  // Direction is the projectile's travel direction, toward the victim.
  return Vector3.Dot(Vector3.Normalize(facing),-Vector3.Normalize(incoming))>0;
 }
 internal static Vector3 GuardBlade(Vector3 cameraRight,Vector3 cameraUp)=>Vector3.Normalize(cameraUp-cameraRight*.75f);
 internal static bool Eligible(bool bullet,bool melee,bool blockBullets)=>melee||bullet&&blockBullets;
 internal static float Damage(float amount,bool guarding,bool eligible,bool propagated,bool frontal,float reductionPercent=50)=>guarding&&eligible&&!propagated&&frontal&&float.IsFinite(amount)&&amount>0&&float.IsFinite(reductionPercent)?amount*(1-Math.Clamp(reductionPercent,0,100)/100):amount;
}
