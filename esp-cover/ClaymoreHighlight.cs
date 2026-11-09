using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class ClaymoreHighlight {
 sealed record Entry(MineDirectional Mine,Vector3 Position,float Distance);
 static ConfigEntry<bool> enabled=null!,throughWalls=null!,labels=null!;
 static ConfigEntry<float> range=null!;
 static readonly List<Entry> entries=new();
 static IntPtr world;
 static float nextScan,nextError;
 static int mask;
 internal static void Initialize(){
  const string section="62. 阔剑地雷";
  enabled=FeatureConfig.Bind(section,"Enabled","高亮阔剑地雷",true);
  throughWalls=FeatureConfig.Bind(section,"Through walls","隔墙显示阔剑地雷",true);
  labels=FeatureConfig.Bind(section,"Show distance","显示地雷名称与距离",true);
  range=FeatureConfig.Bind(section,"Range","阔剑地雷高亮距离（米）",150f,"",new AcceptableValueRange<float>(10,1000));
 }
 internal static void Reset(){entries.Clear();world=IntPtr.Zero;nextScan=0;mask=0;}
 static bool Finite(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
 static bool Live(MineDirectional mine)=>mine!=null&&mine.isActiveAndEnabled&&mine.gameObject.activeInHierarchy&&!mine._cleared&&!mine._exploded&&mine._mineData!=null&&!mine._mineData.IsDummy;
 internal static void Tick(GameWorld? current,Player? local){
  if(!enabled.Value||current==null||local==null||!RaidClock.Active){if(world!=IntPtr.Zero||entries.Count>0)Reset();return;}
  if(world!=current.Pointer){Reset();world=current.Pointer;}
  if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+.25f;entries.Clear();
  try{
   var mines=current.MineManager?.Mines;if(mines==null)return;
   foreach(var mine in mines){if(!Live(mine))continue;var position=mine.transform.position;if(!Finite(position))continue;float distance=Vector3.Distance(position,local.Position);if(distance<=range.Value)entries.Add(new(mine,position,distance));}
   entries.Sort((a,b)=>a.Distance.CompareTo(b.Distance));
  }catch(Exception error){Warn(error);}
 }
 static bool Visible(Camera camera,Vector3 point){
  if(throughWalls.Value)return true;
  if(mask==0)mask=LayerMask.GetMask("HighPolyCollider","Terrain","LowPolyCollider");if(mask==0)return false;
  var delta=point-camera.transform.position;return !Physics.Raycast(camera.transform.position,delta.normalized,Math.Max(0,delta.magnitude-.15f),mask,QueryTriggerInteraction.Ignore);
 }
 internal static void Draw(Camera camera){
  if(!enabled.Value||!RaidClock.Active||Event.current.type!=EventType.Repaint)return;
  try{int budget=128;foreach(var entry in entries){
   if(budget--<=0)break;if(entry.Distance>range.Value||!Live(entry.Mine))continue;
   var position=entry.Mine.transform.position+Vector3.up*.12f;if(!Finite(position)||!Visible(camera,position))continue;
   var point=OverlayProjection.ScreenPoint(camera,position);if(!Finite(point)||point.z<=.05f||point.x<0||point.x>Screen.width||point.y<0||point.y>Screen.height)continue;
   var center=new Vector2(point.x,Screen.height-point.y);var color=new Color(1,.25f,.12f);float size=Math.Clamp(180f/Math.Max(1,entry.Distance),8,22);
   var corners=new[]{center+new Vector2(0,-size),center+new Vector2(size,0),center+new Vector2(0,size),center+new Vector2(-size,0)};
   for(int i=0;i<4;i++){RaidLoot.Line(corners[i],corners[(i+1)%4],new Color(0,0,0,.9f),5);RaidLoot.Line(corners[i],corners[(i+1)%4],color,2);}
   RaidLoot.Line(center+new Vector2(0,-4),center+new Vector2(0,2),color,2);RaidLoot.Line(center+new Vector2(-1,5),center+new Vector2(1,5),color,2);
   if(labels.Value)InterfaceMenu.Label(new Rect(center.x-150,center.y-size-30,300,26),"阔剑地雷 · "+entry.Distance.ToString("F0")+" 米",color,14);
  }}catch(Exception error){Warn(error);}
 }
 static void Warn(Exception error){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+20;Plugin.Self.Log.LogWarning("Claymore highlight: "+error.Message);}
}
