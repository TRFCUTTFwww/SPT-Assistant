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
 static Vector2 scroll;
 internal static void Initialize(Plugin plugin){Enabled=CnConfig.Bind(plugin.Config,"22. Armor analysis","Enabled",true);}
 internal static void Reset(){worldPointer=IntPtr.Zero;nextRead=0;lines=Array.Empty<string>();scroll=Vector2.zero;}
 static void Read(GameWorld world,Player local,Camera camera){
  if(worldPointer!=world.Pointer){Reset();worldPointer=world.Pointer;}
  if(Time.unscaledTime<nextRead)return;
  nextRead=Time.unscaledTime+0.25f;lines=Array.Empty<string>();
  try{
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
   string name=target.Profile?.Nickname??"目标";if(name.Length>15)name=name[..15]+"…";
   var result=new List<string>{"弹道与防护分析 · "+name};
   var weapon=target.HandsController?.TryCast<Player.FirearmController>()?.Item;
   result.Add("对方枪械："+(weapon!=null?LootDisplay.ItemName(weapon):"当前未持枪"));
   var opponent=BallisticReadout.Current(target);
   result.Add("对方膛内／下一发："+(opponent is {} round?round.Name:"弹药未知／空膛"));
   if(opponent is {} threat)result.Add(threat.Stats);
   result.Add("自身头面："+Armor(local,true));result.Add("自身躯干："+Armor(local,false));
   if(opponent is {} incoming){
    result.Add("对方弹药 → 自身头部："+BallisticReadout.Protection(local,EBodyPart.Head,incoming.Penetration));
    result.Add("对方弹药 → 自身胸部："+BallisticReadout.Protection(local,EBodyPart.Chest,incoming.Penetration));
   }
   result.Add("对方头面："+Armor(target,true));
   var ammo=BallisticReadout.Current(local);
   result.Add("自身弹药："+(ammo is {} own?own.Name:"弹药未知／空膛"));
   if(ammo is {} outgoing){result.Add(outgoing.Stats);result.Add("自身弹药 → 对方头部："+BallisticReadout.Protection(target,EBodyPart.Head,outgoing.Penetration));}
   result.Add("基伤为弹药参数；实际损血受距离、命中区域与护甲影响。");lines=result.ToArray();
  }catch(Exception error){Warn(error);}
 }
 static string Armor(Player player,bool head){
  int armorClass=0;float durability=0,maximum=0;var equipment=player.Profile?.Inventory?.Equipment;if(equipment==null)return "防护数据未知";
  foreach(var slot in head?new[]{EquipmentSlot.Headwear,EquipmentSlot.FaceCover}:new[]{EquipmentSlot.ArmorVest,EquipmentSlot.TacticalVest})Inspect(equipment.GetSlot(slot)?.ContainedItem,ref armorClass,ref durability,ref maximum,0);
  return armorClass>0?$"最高 {armorClass} 级 · 对应耐久 {durability:F0}/{maximum:F0}":"未检测到有效护甲";
 }
 static void Inspect(Item? item,ref int bestClass,ref float durability,ref float maximum,int depth){
  if(item==null||depth>4)return;
  var armor=item.GetItemComponent<ArmorComponent>();
  if(armor!=null&&armor.Repairable?.Durability>0&&armor.ArmorClass>bestClass){
   bestClass=armor.ArmorClass;
   var repair=armor.Repairable;
   durability=repair?.Durability??0;maximum=repair?.MaxDurability??0;
  }
  // Only installed parts protect the wearer; spare plates in rig grids do not.
  var slots=item.TryCast<CompoundItem>()?.Slots;
  if(slots!=null)foreach(var slot in slots)Inspect(slot?.ContainedItem,ref bestClass,ref durability,ref maximum,depth+1);
 }
 internal static void Draw(GameWorld world,Player local,Camera camera,GUIStyle source){
  if(!Enabled.Value||InterfaceMenu.Open||!InterfaceMenu.WindowVisible("armor"))return;
  Read(world,local,camera);if(lines.Length==0)return;
  if(style==null)style=new GUIStyle(source){alignment=TextAnchor.UpperLeft,wordWrap=true,richText=false,clipping=TextClipping.Clip};
  style.fontSize=Math.Min(source.fontSize,14);
  var content=new GUIContent[lines.Length];float widest=0;
  for(int i=0;i<lines.Length;i++){content[i]=new GUIContent(I18n.Text(lines[i]));widest=Math.Max(widest,style.CalcSize(content[i]).x);}
  float line=Math.Max(21,style.fontSize+7),width=Math.Min(Math.Max(320,widest+24),Math.Max(60,Screen.width-24));
  float height=10;foreach(var value in content)height+=Math.Max(line,style.CalcHeight(value,Math.Max(1,width-16))+4);
  height=Math.Min(height,Math.Max(24,Screen.height-24));float x=Screen.width-width-12,y=Math.Min(165,Math.Max(12,Screen.height-height-12));
  using var windowOpacity=InterfaceMenu.BeginWindow("armor");var panel=InterfaceMenu.Place("armor",new Rect(x,y,width,height));x=panel.x;y=panel.y;
  float textWidth=Math.Max(1,panel.width-16),total=0;var heights=new float[content.Length];
  void Measure(){total=0;for(int i=0;i<content.Length;i++){heights[i]=Math.Max(line,style!.CalcHeight(content[i],textWidth)+4);total+=heights[i];}}
  Measure();bool overflow=total>panel.height-10;if(overflow){textWidth=Math.Max(1,textWidth-18);Measure();}
  var old=GUI.color;
  try{
   InterfaceMenu.Panel(panel);
   var viewport=new Rect(x+8,y+5,Math.Max(1,panel.width-16),Math.Max(1,panel.height-10));
   scroll=GUI.BeginScrollView(viewport,overflow?scroll:Vector2.zero,new Rect(0,0,textWidth,total),false,overflow,GUIStyle.none,GUI.skin.verticalScrollbar);
   try{float top=0;for(int i=0;i<content.Length;i++){GUI.color=i==0?InterfaceMenu.Accent:InterfaceMenu.Text;GUI.Label(new Rect(0,top,textWidth,heights[i]),content[i],style);top+=heights[i];}}finally{GUI.EndScrollView();}
  }finally{GUI.color=old;}
 }
 static void Warn(Exception error){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+20;Plugin.Self.Error(error);}
}
