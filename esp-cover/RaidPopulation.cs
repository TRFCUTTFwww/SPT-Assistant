using System;
using System.Text;
using BepInEx.Configuration;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class RaidPopulation {
 static ConfigEntry<bool> Enabled=null!;
 static ConfigEntry<bool> Breakdown=null!;
 static readonly PopulationTally tally=new();
 internal static void Initialize(Plugin p){
  Enabled=CnConfig.Bind(p.Config,"13. Raid population","Enabled",true);
  Breakdown=CnConfig.Bind(p.Config,"13. Raid population","Show breakdown",true);
 }
 internal static void Reset()=>tally.Clear();
 internal static void Update(GameWorld world,Player local){
  Reset();var all=world.AllAlivePlayersList;if(all==null)return;
  for(int i=0;i<all.Count;i++){
   var p=all[i];if(p==null||p.IsYourPlayer||p.Pointer==local.Pointer||p.HealthController==null||!p.HealthController.IsAlive)continue;
   var kind=BodyTint.Kind(p);var profile=p.Profile;
   string role=profile?.Info?.Settings?.Role.ToString()??"",side=profile?.Side.ToString()??"";
   string identity=string.IsNullOrEmpty(p.ProfileId)?"raid:"+p.RaidId:p.ProfileId;
   tally.Add(identity,kind,role,side,true,false);
  }
 }
 internal static void Draw(GUIStyle style){
  if(!Enabled.Value)return;
  string text=$"剩余人数：{tally.Total}";
  if(Breakdown.Value){
   var sb=new StringBuilder(text).Append(I18n.Text($"  | PMC {tally.Counts[(int)CharacterKind.Pmc]}（USEC {tally.Usec} / BEAR {tally.Bear}"+(tally.Unknown>0?$" / 未知 {tally.Unknown}":"")+")\n各阵营："));
   foreach(var kind in new[]{CharacterKind.Boss,CharacterKind.Follower,CharacterKind.Cultist,CharacterKind.Zombie,CharacterKind.Scav,CharacterKind.Raider,CharacterKind.Rogue,CharacterKind.BlackFox})sb.Append("  | ").Append(I18n.Text(RolePalette.Name(kind))).Append(' ').Append(tally.Counts[(int)kind]);
   text=sb.ToString();
  }
  var alignment=style.alignment;var font=style.fontSize;var color=GUI.color;
  try{
   style.alignment=TextAnchor.MiddleLeft;
   float panelWidth=Math.Min(880,Math.Max(350,Screen.width-660));
   if(style.CalcSize(new GUIContent(I18n.Text(text))).x>panelWidth-12)style.fontSize=Math.Max(10,font-2);
   float height=Breakdown.Value?48:27;
   var panel=InterfaceMenu.Place("population",new Rect(12,12,panelWidth,height));InterfaceMenu.Panel(panel);
   GUI.color=InterfaceMenu.Text;GUI.Label(new Rect(panel.x+6,panel.y,panel.width-12,height),I18n.Text(text),style);
  }finally{style.alignment=alignment;style.fontSize=font;GUI.color=color;}
 }
}
