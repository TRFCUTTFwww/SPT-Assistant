using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using EFT;
using EFT.Interactive;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class TransitDisplay {
 internal static ConfigEntry<bool> Enabled=null!;
 static ConfigEntry<float> Range=null!;
 internal readonly record struct Row(Vector3 Position,string Destination,string State,float Distance,bool Available);
 internal static readonly List<Row> Rows=new();
 internal static int Revision;
 static float nextScan,nextError;
 internal static void Initialize(Plugin p){
  Enabled=CnConfig.Bind(p.Config,"15. Transit points","Enabled",true);
  Range=CnConfig.Bind(p.Config,"15. Transit points","Maximum distance",2000f,new ConfigDescription("",new AcceptableValueRange<float>(50,5000)));
 }
 internal static void Reset(){Rows.Clear();nextScan=0;Revision++;}
 internal static void Tick(GameWorld? world,Player? player){
  if(world==null||player==null){if(Rows.Count>0)Reset();return;}
  if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+1;
  Rows.Clear();Revision++;if(!Enabled.Value)return;
  try{
   var controller=world.TransitController;var points=controller?.pointsById;if(points==null)return;
   foreach(var point in points.Values){
    if(point==null)continue;
    try{
     var parameters=point.parameters;if(parameters==null)continue;
     var collider=point.GetComponent<Collider>();var position=collider!=null&&collider.bounds.size.sqrMagnitude>0.000001f?collider.bounds.center:point.transform.position;
     float distance=Vector3.Distance(player.Position,position);if(distance>Range.Value)continue;
     string target=parameters.target??"",name=target;
     var client=controller!.TryCast<ClientTransitController>();
     if(client!=null&&client.TryGetLocation(target,out var location)&&location!=null)name=MapName(location.Id,location.Name);
     if(string.IsNullOrWhiteSpace(name)||name.Length==24)name=LocalizationExtensions.Localized(parameters.description??parameters.name,EStringCase.None);
     if(string.IsNullOrWhiteSpace(name))name="转移点";
     bool met=true;var tips=new List<string>();
     if(point.Requirements!=null)foreach(var requirement in point.Requirements){if(requirement==null||requirement.Met(player))continue;met=false;var tip=requirement.GetLocalizedTip();if(!string.IsNullOrWhiteSpace(tip))tips.Add(tip.Replace('\n',' '));}
     bool active=controller.settings!=null&&controller.settings.active&&point.Enabled&&point.IsActive;
     string state=!point.Enabled?"未启用":!active?"未开启":!met?"条件未满足":"可用";
     if(tips.Count>0)state+=" · "+string.Join(" / ",tips.Take(2));
     Rows.Add(new(position,name,state,distance,active&&met));
    }catch(Exception e){Warn(e);}
   }
   Rows.Sort((a,b)=>a.Distance.CompareTo(b.Distance));
  }catch(Exception e){Warn(e);}
 }
 static string MapName(string id,string fallback)=>id.ToLowerInvariant() switch{
  "bigmap"=>"海关","factory4_day" or "factory4_night"=>"工厂","woods"=>"森林","shoreline"=>"海岸线",
  "interchange"=>"立交桥","rezervbase"=>"储备站","lighthouse"=>"灯塔","tarkovstreets"=>"塔科夫街区",
  "sandbox" or "sandbox_high"=>"零号地带","laboratory"=>"实验室","labyrinth"=>"迷宫","terminal"=>"终端","icebreaker"=>"破冰船",
  _=>LocalizationExtensions.Localized(fallback,EStringCase.None)
 };
 internal static void Draw(Camera camera,GUIStyle style){
  if(!Enabled.Value)return;
  foreach(var row in Rows){
   var point=camera.WorldToScreenPoint(row.Position);if(point.z<=0||point.x<0||point.x>Screen.width||point.y<0||point.y>Screen.height)continue;
   var old=GUI.color;
   try{GUI.color=row.Available?Color.cyan:Color.gray;GUI.Label(new Rect(point.x-200,Screen.height-point.y-24,400,48),$"转移 → {row.Destination} [{row.Distance:F0}米]\n{row.State}",style);}finally{GUI.color=old;}
  }
 }
 static void Warn(Exception e){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+10;Plugin.Self.Error(e);}
}
