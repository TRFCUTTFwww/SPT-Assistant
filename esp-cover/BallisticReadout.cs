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
 internal readonly record struct ProtectionInfo(string Text,int Defense=0,int Offense=0){
  internal int Advantage(bool defending)=>defending?Defense:Offense;
 }
 internal static ProtectionInfo Protection(Player player,EBodyPart part,float penetration){
  if(!float.IsFinite(penetration)||penetration<0)return new("弹药数据未知");
  var coverage=new BallisticCoverage();
  var colliders=player._hitColliders;if(colliders==null)return new("防护数据未知");
  foreach(var body in colliders){
   if(body==null||body.BodyPartType!=part)continue;
   bool armor=player.TryGetArmorResistData(body,penetration,out var resistance);
   float chance=armor?resistance.GetPenetrationChance(penetration):1;
   if(armor&&ArmorResistanceData.MAX_CHANCE>1)chance/=100;
   coverage.Add(armor,chance);
  }
  if(coverage.Covered==0&&coverage.Uncovered==0)return new("防护数据未知");
  if(coverage.Covered==0)return coverage.Unknown?new("防护数据不完整"):new("无有效覆盖 · 穿透 100%",coverage.Advantage(true),coverage.Advantage(false));
  string text=Math.Abs(coverage.Maximum-coverage.Minimum)<.005f?$"护甲覆盖区：估算穿透 {coverage.Maximum*100:F0}%":$"护甲覆盖区：估算穿透 {coverage.Minimum*100:F0}–{coverage.Maximum*100:F0}%";
  if(coverage.Uncovered>0)text+=" · 存在未覆盖区域";
  if(coverage.Unknown)text+=" · 部分防护数据未知";
  return new(text,coverage.Advantage(true),coverage.Advantage(false));
 }
}
