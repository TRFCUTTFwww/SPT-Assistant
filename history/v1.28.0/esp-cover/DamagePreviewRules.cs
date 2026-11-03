using System;
namespace LocalSpt5Esp;
internal static class DamagePreviewRules {
 internal static float Upper(float baseDamage,double factor,float health,float coefficient,float buffMultiplier){if(!float.IsFinite(baseDamage)||baseDamage<=0||!double.IsFinite(factor)||factor<=0||!float.IsFinite(health)||health<=0||!float.IsFinite(coefficient)||coefficient<=0||!float.IsFinite(buffMultiplier)||buffMultiplier<=0)return 0;double scaled=Math.Min(health,(double)baseDamage*factor);return (float)Math.Min(health,scaled*coefficient*buffMultiplier);}
 internal static bool PotentiallyLethal(bool vital,float partHealth,float totalHealth,float upper)=>upper>0&&(vital&&partHealth>0&&upper>=partHealth||totalHealth>0&&upper>=totalHealth);
}
