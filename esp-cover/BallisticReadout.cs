using System;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
namespace LocalSpt5Esp;

// Read native templates directly. Never create an item or read the database in a hit hook.
internal static class BallisticReadout {
 internal readonly record struct Round(string Id,string Name,int Damage,int Penetration,int ArmorDamage,int Projectiles){
  internal string Stats=>$"基伤 {Damage} / 穿 {Penetration} / 甲伤 {ArmorDamage}%"+(Projectiles>1?$" · {Projectiles} 弹丸（单丸）":"");
 }
 internal static Round? Current(Player player){var ammo=MagazineDisplay.CurrentAmmo(player);return ammo==null?null:Resolve(ammo.StringTemplateId);}
 internal static Round? Resolve(string? id){
  if(string.IsNullOrEmpty(id)||id.Length!=24)return null;
  foreach(char c in id)if(!Uri.IsHexDigit(c))return null;
  var templates=Singleton<ItemFactory>.Instance?.ItemTemplates;
  if(templates==null||!templates.TryGetValue(new MongoID(id),out var template)||template?.TryCast<AmmoTemplate>() is not {} ammo)return null;
  return new(id,LootDisplay.TemplateName(id),ammo.Damage,ammo.PenetrationPower,ammo.ArmorDamage,ammo.ProjectileCount);
 }
 internal static string Protection(Player player,EBodyPart part,float penetration){
  if(!float.IsFinite(penetration)||penetration<0)return "弹药数据未知";
  int count=0;float minimum=1,maximum=0;bool covered=false;
  var colliders=player._hitColliders;if(colliders==null)return "防护数据未知";
  foreach(var body in colliders){
   if(body==null||body.BodyPartType!=part)continue;
   bool armor=player.TryGetArmorResistData(body,penetration,out var resistance);
   float chance=armor?resistance.GetPenetrationChance(penetration):1;
   if(armor&&ArmorResistanceData.MAX_CHANCE>1)chance/=100;
   if(!float.IsFinite(chance))continue;
   chance=Math.Clamp(chance,0,1);minimum=Math.Min(minimum,chance);maximum=Math.Max(maximum,chance);count++;covered|=armor;
  }
  if(count==0)return "防护数据未知";
  if(!covered)return "无有效覆盖 · 穿透 100%";
  return Math.Abs(maximum-minimum)<.005f?$"估算穿透 {maximum*100:F0}%":$"估算穿透 {minimum*100:F0}–{maximum*100:F0}%（覆盖区域不同）";
 }
}
