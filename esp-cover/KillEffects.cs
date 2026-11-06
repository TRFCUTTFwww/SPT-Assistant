using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using UnityEngine;
namespace LocalSpt5Esp;
internal enum KillEffectStyle { Pulse,Sparks,PulseAndSparks }
internal static class KillEffects {
 sealed record Burst(IntPtr World,string Victim,Vector3 Position,bool Headshot,float Started,bool Preview=false);
 static ConfigEntry<bool> enabled=null!,center=null!;static ConfigEntry<float> duration=null!,scale=null!;static ConfigEntry<KillEffectStyle> style=null!;
 static readonly List<Burst> bursts=new();static readonly HashSet<(IntPtr,string)> seen=new();static IntPtr world;
 internal static void Initialize(){const string section="53. 击杀特效";enabled=FeatureConfig.Bind(section,"Confirmed firearm kill effects","枪械击杀特效",true,"仅在玩家枪械确认击杀后播放，不影响伤害、尸体或物品。爆头使用金色，其余使用主题强调色。");center=FeatureConfig.Bind(section,"Kill center marker","准星击杀反馈",true);style=FeatureConfig.Bind(section,"Kill effect style","击杀特效样式",KillEffectStyle.PulseAndSparks);duration=FeatureConfig.Bind(section,"Kill effect duration","特效持续时间（秒）",.75f,"",new AcceptableValueRange<float>(.2f,2));scale=FeatureConfig.Bind(section,"Kill effect scale","特效大小",1f,"",new AcceptableValueRange<float>(.3f,2.5f));}
 internal static void Record(Player victim,IPlayer? aggressor,DamageInfo? damage,EBodyPart part){var current=Singleton<GameWorld>.Instance;var local=current?.MainPlayer;if(!enabled.Value||!RaidClock.Active||current==null||local?.IsYourPlayer!=true||aggressor?.ProfileId!=local.ProfileId||victim.IsYourPlayer||victim.HealthController?.IsAlive!=false||damage?.DamageType!=EDamageType.Bullet||damage.Weapon==null)return;SyncWorld(current.Pointer);if(!seen.Add((current.Pointer,victim.ProfileId)))return;var point=damage.HitPoint;if(!float.IsFinite(point.x)||!float.IsFinite(point.y)||!float.IsFinite(point.z)||Vector3.Distance(point,victim.Position)>5)point=victim.Position+Vector3.up;bursts.Add(new(current.Pointer,victim.ProfileId,point,part==EBodyPart.Head,Time.unscaledTime));Trim();}
 static void SyncWorld(IntPtr pointer){if(world==pointer)return;bursts.Clear();seen.Clear();world=pointer;}
 static void Trim(){if(bursts.Count>12)bursts.RemoveRange(0,bursts.Count-12);}
 internal static void Tick(GameWorld? current){SyncWorld(current?.Pointer??IntPtr.Zero);bursts.RemoveAll(b=>Time.unscaledTime-b.Started>duration.Value||!enabled.Value&&!b.Preview);}
 internal static void Preview(){bursts.Add(new(world,"preview",Vector3.zero,true,Time.unscaledTime,true));Trim();}
 internal static void Reset(){bursts.Clear();seen.Clear();world=IntPtr.Zero;}
 internal static void Draw(Camera? camera){if(Event.current.type!=EventType.Repaint)return;foreach(var burst in bursts){float t=Math.Clamp((Time.unscaledTime-burst.Started)/duration.Value,0,1);if(t>=1)continue;Vector2 point;if(burst.Preview)point=new(Screen.width*.5f,Screen.height*.48f);else{if(camera==null)continue;var projected=OverlayProjection.ScreenPoint(camera,burst.Position);if(projected.z<=0||projected.x<0||projected.x>Screen.width||projected.y<0||projected.y>Screen.height)continue;point=new(projected.x,Screen.height-projected.y);}var color=burst.Headshot?new Color(1,.78f,.35f):InterfaceMenu.Accent;color.a=(1-t)*(1-t);float size=scale.Value;
   if(style.Value is KillEffectStyle.Pulse or KillEffectStyle.PulseAndSparks){float radius=(10+42*(1-(1-t)*(1-t)))*size;for(int i=0;i<36;i++){float a=i*Mathf.PI*2/36,b=(i+1)*Mathf.PI*2/36;RaidLoot.Line(point+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,point+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,color,1.8f*size);}}
   if(style.Value is KillEffectStyle.Sparks or KillEffectStyle.PulseAndSparks)for(int i=0;i<10;i++){float angle=i*Mathf.PI*.2f+(burst.Headshot ? .17f : 0);var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));float spread=(15+68*t)*size;var fall=new Vector2(0,18*t*t*size);RaidLoot.Line(point+direction*spread+fall,point+direction*(spread+Math.Max(1,12*(1-t))*size)+fall,color,2*size);}
   if(center.Value){var c=new Vector2(Screen.width*.5f,Screen.height*.5f);float offset=(5+6*t)*size;for(int i=0;i<4;i++){float a=(45+i*90)*Mathf.Deg2Rad;var d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));RaidLoot.Line(c+d*offset,c+d*(offset+8*size),color,2*size);}}
  }}
 internal static void DrawSettings(Rect rect){float y=rect.y;InterfaceMenu.Label(new(rect.x,y,rect.width,30),"枪械击杀反馈",InterfaceMenu.Accent,20);y+=44;foreach(var entry in new ConfigEntryBase[]{enabled,style,duration,scale,center}){InterfaceMenu.DrawEntry(entry,new(rect.x,y,rect.width,84));y+=94;}if(InterfaceMenu.Button(new(rect.x,y,rect.width,36),"预览击杀特效")){Preview();InterfaceMenu.Close();}InterfaceMenu.Paragraph(new(rect.x,y+48,rect.width,58),"确认枪械击杀后，在目标位置播放脉冲与碎光；爆头显示金色。",null,13);}
}
