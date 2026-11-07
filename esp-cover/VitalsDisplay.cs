using System;
using BepInEx.Configuration;
using EFT;
using EFT.HealthSystem;
using UnityEngine;

namespace LocalSpt5Esp;

internal static class VitalsDisplay {
 static ConfigEntry<bool> Enabled=null!;
 static IntPtr playerPointer;
 static float nextRead,nextError;
 static Stat health,hydration,energy;
 static bool available;
 static GUIStyle? labelStyle;
 static int fontSize;
 readonly record struct Stat(float Current,float Maximum) {
  internal bool Valid=>float.IsFinite(Current)&&float.IsFinite(Maximum)&&Maximum>0;
  internal float Ratio=>Math.Clamp(Current/Maximum,0f,1f);
 }

 internal static void Initialize(Plugin plugin){
  Enabled=CnConfig.Bind(plugin.Config,"19. Vitals","Enabled",true);
 }

 internal static void Reset(){playerPointer=IntPtr.Zero;nextRead=0;available=false;health=hydration=energy=default;}

 static void Read(Player player){
  if(playerPointer!=player.Pointer){Reset();playerPointer=player.Pointer;}
  if(Time.unscaledTime<nextRead)return;
  nextRead=Time.unscaledTime+0.2f;
  try{
   ActiveHealthController controller=player.ActiveHealthController;
   if(controller==null){available=false;return;}
   var body=controller.GetBodyPartHealth(EBodyPart.Common,false);
   var water=controller.Hydration;
   var food=controller.Energy;
   health=new Stat(body.Current,body.Maximum);
   hydration=new Stat(water.Current,water.Maximum);
   energy=new Stat(food.Current,food.Maximum);
   available=health.Valid&&hydration.Valid&&energy.Valid;
  }catch(Exception error){
   available=false;
   if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+20;Plugin.Self.Error(error);}
  }
 }

 internal static void Draw(Player player,GUIStyle source){
  if(!Enabled.Value)return;
  Read(player);
  if(!available)return;
  if(labelStyle==null||fontSize!=source.fontSize){fontSize=source.fontSize;labelStyle=new GUIStyle(source){alignment=TextAnchor.MiddleLeft,richText=false,wordWrap=false};}
  int width=Math.Min(382,Screen.width-24);
  if(width<200)return;
  var oldColor=GUI.color;
  var panel=InterfaceMenu.Place("vitals",new Rect(12,110,width,28));
  try{
   InterfaceMenu.Panel(panel);
   float unit=(width-12)/3f;
   DrawStat(new Rect(panel.x+6,panel.y+1,unit,26),"血量",health);
   DrawStat(new Rect(panel.x+6+unit,panel.y+1,unit,26),"水分",hydration);
   DrawStat(new Rect(panel.x+6+unit*2,panel.y+1,unit,26),"能量",energy);
  }finally{GUI.color=oldColor;}
 }

 static void DrawStat(Rect rect,string name,Stat value){
  GUI.color=value.Ratio<0.25f?new Color(1f,0.35f,0.3f):value.Ratio<0.5f?new Color(1f,0.86f,0.3f):InterfaceMenu.Text;
  GUI.Label(rect,I18n.Text($"{name} {Math.Ceiling(value.Current):F0}/{Math.Ceiling(value.Maximum):F0}"),labelStyle);
 }
}
