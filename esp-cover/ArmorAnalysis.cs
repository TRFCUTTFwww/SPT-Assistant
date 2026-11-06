using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;

namespace LocalSpt5Esp;

internal static class ArmorAnalysis {
 static ConfigEntry<bool> Enabled=null!;
 static float nextRead,nextError;
 static IntPtr worldPointer;
 static string[] lines=Array.Empty<string>();
 static GUIStyle? style;
 internal static void Initialize(Plugin plugin){Enabled=CnConfig.Bind(plugin.Config,"22. Armor analysis","Enabled",true);}
 internal static void Reset(){worldPointer=IntPtr.Zero;nextRead=0;lines=Array.Empty<string>();}
 static void Read(GameWorld world,Player local,Camera camera){
  if(worldPointer!=world.Pointer){Reset();worldPointer=world.Pointer;}
  if(Time.unscaledTime<nextRead)return;
  nextRead=Time.unscaledTime+0.25f;lines=Array.Empty<string>();
  try{
   var ammo=MagazineDisplay.CurrentAmmo(local);if(ammo==null)return;
   Player? target=null;float best=float.MaxValue;
   var players=world.AllAlivePlayersList;if(players==null)return;
   for(int i=0;i<players.Count;i++){
    var candidate=players[i];if(candidate==null||candidate.IsYourPlayer||candidate.HealthController==null||!candidate.HealthController.IsAlive)continue;
    var head=candidate.PlayerBones?.Head!=null?candidate.PlayerBones.Head.position:candidate.Position+Vector3.up*1.6f;
    var screen=OverlayProjection.ScreenPoint(camera,head);if(screen.z<=0)continue;
    float offset=Vector2.Distance(new Vector2(screen.x,screen.y),new Vector2(Screen.width/2f,Screen.height/2f));
    if(offset>Math.Min(170f,Screen.height*0.18f)||offset>=best)continue;
    best=offset;target=candidate;
   }
   if(target==null)return;
   int armorClass=0;float durability=0,maximum=0;
   var equipment=target.Profile?.Inventory?.Equipment;
   if(equipment!=null){
    Inspect(equipment.GetSlot(EquipmentSlot.Headwear)?.ContainedItem,ref armorClass,ref durability,ref maximum,0);
    Inspect(equipment.GetSlot(EquipmentSlot.FaceCover)?.ContainedItem,ref armorClass,ref durability,ref maximum,0);
   }
   string armor=armorClass>0?$"头/面最高护甲 {armorClass} 级"+(maximum>0?$" · 耐久 {durability:F0}/{maximum:F0}":""):"未检测到头面护甲";
   int delta=ammo.PenetrationPower-armorClass*10;
   string penetration=armorClass==0?"无遮挡":delta>=10?"高":delta>=0?"中":delta>=-10?"低":"极低";
   int damage=ammo.Damage;
   string prediction=armorClass==0?$"粗估头部伤害 ≤{damage}（受距离影响）":$"粗估穿甲后 {Math.Round(damage*0.7f):F0}–{damage} · 未穿 0–{Math.Round(damage*0.15f):F0}";
   string name=target.Profile?.Nickname??"目标";if(name.Length>15)name=name[..15]+"…";
   lines=new[]{"头甲分析 · "+name,armor,$"{LootDisplay.ItemName(ammo)} · 伤 {damage} / 穿 {ammo.PenetrationPower}",$"穿透倾向：{penetration}（穿甲差 {delta:+0;-0;0}）",prediction};
  }catch(Exception error){Warn(error);}
 }
 static void Inspect(Item? item,ref int bestClass,ref float durability,ref float maximum,int depth){
  if(item==null||depth>4)return;
  var armor=item.GetItemComponent<ArmorComponent>();
  if(armor!=null&&armor.ArmorClass>bestClass){
   bestClass=armor.ArmorClass;
   var repair=item.GetItemComponent<RepairableComponent>();
   durability=repair?.Durability??0;maximum=repair?.MaxDurability??0;
  }
  foreach(var child in LootDisplay.Children(item))Inspect(child,ref bestClass,ref durability,ref maximum,depth+1);
 }
 internal static void Draw(GameWorld world,Player local,Camera camera,GUIStyle source){
  if(!Enabled.Value)return;
  Read(world,local,camera);if(lines.Length==0)return;
  if(style==null)style=new GUIStyle(source){alignment=TextAnchor.MiddleLeft,wordWrap=false,richText=false,clipping=TextClipping.Clip};
  style.fontSize=Math.Min(source.fontSize,14);
  float line=Math.Max(21,style.fontSize+7),width=Math.Min(385,Screen.width-24),height=lines.Length*line+10,x=Screen.width-width-12,y=165;
  var panel=InterfaceMenu.Place("armor",new Rect(x,y,width,height));x=panel.x;y=panel.y;
  var old=GUI.color;
  try{
   InterfaceMenu.Panel(panel);
   for(int i=0;i<lines.Length;i++){GUI.color=i==3?Color.yellow:i==0?InterfaceMenu.Accent:InterfaceMenu.Text;GUI.Label(new Rect(x+8,y+5+i*line,width-16,line),lines[i],style);}
  }finally{GUI.color=old;}
 }
 static void Warn(Exception error){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+20;Plugin.Self.Error(error);}
}
