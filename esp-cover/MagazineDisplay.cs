using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;

namespace LocalSpt5Esp;

internal static class MagazineDisplay {
 static ConfigEntry<ExfilPanelMode> Mode=null!;
 static ConfigEntry<KeyCode> HoldKey=null!;
 static ConfigEntry<int> Width=null!;
 static readonly List<(string Text,Color Color)> lines=new();
 static GUIStyle? style;
 static float nextRead,nextError;
 static IntPtr playerPointer;

 internal static void Initialize(Plugin plugin){
  Mode=CnConfig.Bind(plugin.Config,"21. Magazine","Magazine panel mode",ExfilPanelMode.Hold);
  HoldKey=CnConfig.Bind(plugin.Config,"21. Magazine","Magazine hold key",KeyCode.Plus);
  Width=CnConfig.Bind(plugin.Config,"21. Magazine","Magazine panel width",310,new ConfigDescription("",new AcceptableValueRange<int>(240,450)));
 }
 internal static void Reset(){lines.Clear();nextRead=0;playerPointer=IntPtr.Zero;}
 static bool Visible(){
  if(Mode.Value==ExfilPanelMode.Off)return false;
  if(Mode.Value==ExfilPanelMode.Always)return true;
  if(Input.GetKey(HoldKey.Value))return true;
  return HoldKey.Value==KeyCode.Plus&&(Input.GetKey(KeyCode.KeypadPlus)||
   Input.GetKey(KeyCode.Equals)&&(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)));
 }
 internal static Ammo? CurrentAmmo(Player player){
  var weapon=player.HandsController?.TryCast<Player.FirearmController>()?.Item;
  if(weapon==null)return null;
  var chambers=weapon.Chambers;
  if(chambers!=null)foreach(var chamber in chambers){var ammo=chamber?.ContainedItem?.TryCast<Ammo>();if(ammo!=null)return ammo;}
  return weapon.GetCurrentMagazine()?.FirstRealAmmo()?.TryCast<Ammo>();
 }
 static void Read(Player player){
  if(playerPointer!=player.Pointer){Reset();playerPointer=player.Pointer;}
  if(Time.unscaledTime<nextRead)return;
  nextRead=Time.unscaledTime+0.25f;lines.Clear();
  try{
   var weapon=player.HandsController?.TryCast<Player.FirearmController>()?.Item;
   if(weapon==null){lines.Add(("当前未持枪",Color.gray));return;}
   lines.Add((LootDisplay.ItemName(weapon),Color.cyan));
   var magazine=weapon.GetCurrentMagazine();
   int chamberCount=0;
   if(weapon.Chambers!=null)foreach(var chamber in weapon.Chambers)if(chamber?.ContainedItem?.TryCast<Ammo>()!=null)chamberCount++;
   lines.Add(($"弹夹 {magazine?.Count??0}/{magazine?.MaxCount??0} · 膛内 {chamberCount}",Color.white));
   var next=CurrentAmmo(player);
   if(next!=null)lines.Add(($"下一发：{LootDisplay.ItemName(next)} · 伤 {next.Damage} / 穿 {next.PenetrationPower}",Color.yellow));
   if(magazine==null){lines.Add(("未装弹夹",Color.gray));return;}
   var grouped=new Dictionary<string,(Ammo Ammo,int Count)>();
   var cartridges=magazine.Cartridges;
   if(cartridges!=null)foreach(var item in cartridges.Items){
    var ammo=item?.TryCast<Ammo>();if(ammo==null)continue;
    var key=ammo.StringTemplateId;
    grouped.TryGetValue(key,out var old);
    grouped[key]=(ammo,old.Count+Math.Max(1,ammo.StackObjectsCount));
   }
   if(grouped.Count==0)lines.Add(("弹夹为空",Color.gray));
   foreach(var entry in grouped.Values.OrderByDescending(x=>x.Count).Take(6))
    lines.Add(($"{LootDisplay.ItemName(entry.Ammo)} ×{entry.Count} · 伤 {entry.Ammo.Damage} / 穿 {entry.Ammo.PenetrationPower}",Color.white));
   if(grouped.Count>6)lines.Add(($"另有 {grouped.Count-6} 种弹药",Color.gray));
  }catch(Exception error){lines.Clear();lines.Add(("弹夹数据读取中",Color.gray));Warn(error);}
 }
 internal static void Draw(Player player,GUIStyle source){
  if(!Visible()&&!InterfaceMenu.Editing)return;
  Read(player);
  if(lines.Count==0)return;
  if(style==null)style=new GUIStyle(source){alignment=TextAnchor.MiddleLeft,wordWrap=false,richText=false,clipping=TextClipping.Clip};
  style.fontSize=Math.Min(source.fontSize,14);
  float line=Math.Max(21,style.fontSize+7),width=Math.Min(Width.Value,Screen.width-24);
  float height=lines.Count*line+10,x=12,y=Math.Min(145,Math.Max(8,Screen.height-height-12));
  var panel=InterfaceMenu.Place("magazine",new Rect(x,y,width,height));x=panel.x;y=panel.y;
  var old=GUI.color;
  try{
   InterfaceMenu.Panel(panel);
   for(int i=0;i<lines.Count;i++){GUI.color=InterfaceMenu.Tint(lines[i].Color);GUI.Label(new Rect(x+8,y+5+i*line,width-16,line),I18n.Text(lines[i].Text),style);}
  }finally{GUI.color=old;}
 }
 static void Warn(Exception error){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+20;Plugin.Self.Error(error);}
}
