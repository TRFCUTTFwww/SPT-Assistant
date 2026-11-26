using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using EFT;
using UnityEngine;

namespace LocalSpt5Esp;

internal static class AggroDisplay {
 static ConfigEntry<bool> Enabled=null!;
 static ConfigEntry<int> MaximumLines=null!;
 static readonly List<(string Label,float Distance,bool Targeting)> threats=new();
 static IntPtr worldPointer;
 static float nextRead,nextError;
 static GUIStyle? style;

 internal static void Initialize(Plugin plugin){
  Enabled=CnConfig.Bind(plugin.Config,"23. Aggro tracking","Enabled",true);
  MaximumLines=CnConfig.Bind(plugin.Config,"23. Aggro tracking","Aggro maximum lines",6,new ConfigDescription("",new AcceptableValueRange<int>(1,12)));
 }
 internal static void Reset(){threats.Clear();worldPointer=IntPtr.Zero;nextRead=0;}
 static bool IsLocal(IPlayer? person,Player local)=>person!=null&&person.ProfileId==local.ProfileId;
 static void Read(GameWorld world,Player local){
  if(worldPointer!=world.Pointer){Reset();worldPointer=world.Pointer;}
  if(Time.unscaledTime<nextRead)return;
  nextRead=Time.unscaledTime+0.25f;threats.Clear();
  try{
   var all=world.AllAlivePlayersList;if(all==null)return;
   for(int i=0;i<all.Count;i++){
    var actor=all[i];if(actor==null||actor.IsYourPlayer||actor.HealthController==null||!actor.HealthController.IsAlive)continue;
    var owner=actor.AIData?.BotOwner;var memory=owner?.Memory;if(memory==null)continue;
    bool targeting=IsLocal(memory.GoalEnemy?.Person,local);
    bool remembered=targeting||IsLocal(memory.LastEnemy?.Person,local);
    if(!remembered&&owner?.EnemiesController?.EnemyInfos!=null)remembered=owner.EnemiesController.EnemyInfos.ContainsKey(local);
    if(!remembered)continue;
    string name=actor.Profile?.Nickname??"AI";
    if(name.Length>16)name=name[..16]+"…";
    threats.Add(($"{BodyTint.KindName(actor)} · {name}",Vector3.Distance(actor.Position,local.Position),targeting));
   }
   threats.Sort((a,b)=>a.Targeting!=b.Targeting?(a.Targeting?-1:1):a.Distance.CompareTo(b.Distance));
  }catch(Exception error){Warn(error);}
 }
 internal static void Draw(GameWorld world,Player local,GUIStyle source){
  if(!Enabled.Value||!PlusHeld()&&!InterfaceMenu.Editing)return;
  Read(world,local);
  if(style==null)style=new GUIStyle(source){alignment=TextAnchor.MiddleLeft,wordWrap=false,richText=false,clipping=TextClipping.Clip};
  style.fontSize=Math.Min(source.fontSize,14);
  int shown=Math.Min(MaximumLines.Value,threats.Count);
  float line=Math.Max(21,style.fontSize+7),width=Math.Min(340,Screen.width-24),height=(shown+1)*line+10;
  float x=12,y=Math.Clamp((Screen.height-height)*0.5f,12f,Math.Max(12f,Screen.height-height-12));
  using var windowOpacity=InterfaceMenu.BeginWindow("aggro");var panel=InterfaceMenu.Place("aggro",new Rect(x,y,width,height));x=panel.x;y=panel.y;
  var old=GUI.color;
  try{
   InterfaceMenu.Panel(panel);
   int locked=threats.Count(t=>t.Targeting);
   GUI.color=locked>0?new Color(1f,0.45f,0.35f):InterfaceMenu.Accent;
   GUI.Label(new Rect(x+8,y+5,width-16,line),I18n.Text($"仇恨追踪 · 当前锁定 {locked} · 记住你 {threats.Count-locked}"),style);
   for(int i=0;i<shown;i++){
    var threat=threats[i];GUI.color=threat.Targeting?new Color(1f,0.65f,0.45f):InterfaceMenu.Text;
    GUI.Label(new Rect(x+8,y+5+(i+1)*line,width-16,line),I18n.Text($"{(threat.Targeting?"锁定":"记忆")} · {threat.Label} · {threat.Distance:F0}米"),style);
   }
  }finally{GUI.color=old;}
 }
 static bool PlusHeld()=>Input.GetKey(KeyCode.Plus)||Input.GetKey(KeyCode.KeypadPlus)||
  Input.GetKey(KeyCode.Equals)&&(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift));
 static void Warn(Exception error){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+20;Plugin.Self.Error(error);}
}
