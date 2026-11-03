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
  string text=$"存活：{tally.Total}";
  if(Breakdown.Value){
   var sb=new StringBuilder(text).Append($"  | PMC {tally.Counts[(int)CharacterKind.Pmc]}（USEC {tally.Usec} / BEAR {tally.Bear}"+(tally.Unknown>0?$" / 未知 {tally.Unknown}":"")+")\n各阵营：");
   foreach(var kind in new[]{CharacterKind.Boss,CharacterKind.Follower,CharacterKind.Cultist,CharacterKind.Scav,CharacterKind.Raider,CharacterKind.Rogue,CharacterKind.BlackFox})sb.Append("  | ").Append(RolePalette.Name(kind)).Append(' ').Append(tally.Counts[(int)kind]);
   text=sb.ToString();
  }
  var alignment=style.alignment;var font=style.fontSize;var color=GUI.color;
  try{
   style.alignment=TextAnchor.MiddleLeft;
   if(style.CalcSize(new GUIContent(text)).x>Screen.width-36)style.fontSize=Math.Max(10,font-2);
   float height=Breakdown.Value?48:27;
   GUI.color=new Color(0,0,0,0.55f);GUI.DrawTexture(new Rect(12,57,Screen.width-24,height),Texture2D.whiteTexture);
   GUI.color=Color.white;GUI.Label(new Rect(18,57,Screen.width-36,height),text,style);
  }finally{style.alignment=alignment;style.fontSize=font;GUI.color=color;}
 }
}
