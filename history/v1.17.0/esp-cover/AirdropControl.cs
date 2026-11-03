using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Airdrop;
using EFT.SynchronizableObjects;
using HarmonyLib;
using UnityEngine;
using Object=UnityEngine.Object;
namespace LocalSpt5Esp;
internal static partial class AirdropControl {
 static readonly string[] names={"随机种类","综合物资","武器与护甲","食品与医疗","交易材料"};
 static readonly string[] types={"random","mixed","weaponArmor","foodMedical","barter"};
 static readonly string[] qualities={"原版","精选 · 高价值倾向 ×3","高品质 · 高价值倾向 ×8"};
 static ConfigEntry<bool> markers=null!;static int type,quality;static bool custom,loaded;static string notice="";static float nextScan,cooldown,pointUntil;static IntPtr world;static Vector2 scroll;
 static readonly List<AirdropSynchronizableObject> drops=new();static readonly List<GameObject> points=new();static Vector3? pendingPosition;
 internal static void Initialize(Plugin plugin){InitializeInformation(plugin);markers=FeatureConfig.Bind("38. 空投控制","Show airdrops","定位已有空投",true,"显示当前战局已经存在的空投位置和距离。");try{plugin.Patches.Patch(typeof(AirdropManager).GetMethod("TakeAirdropPoint",new[]{typeof(bool),typeof(Vector3)}),prefix:new HarmonyMethod(typeof(AirdropControl),nameof(TakePoint)));plugin.Patches.Patch(AccessTools.Method(typeof(AirdropSynchronizableObject),"ApplyAirdropViewType"),postfix:new HarmonyMethod(typeof(AirdropControl),nameof(Track)));plugin.Log.LogInfo("Airdrop control ready: event-tracked crates with low-frequency recovery scan");}catch(Exception e){plugin.Log.LogError("Airdrop point hook: "+e);}}
 static bool TakePoint(bool __0,Vector3 __1,ref GameObject __result){if(pendingPosition is not Vector3 position||!RaidClock.Active||Singleton<GameWorld>.Instance?.Pointer!=world||Time.unscaledTime>pointUntil||Vector3.Distance(position,__1)>.5f)return true;var point=new GameObject("Local requested airdrop position");point.transform.position=position;point.AddComponent<AirdropPoint>();points.Add(point);__result=point;pendingPosition=null;return false;}
 static void Track(AirdropSynchronizableObject __instance){if(world==IntPtr.Zero||__instance==null)return;for(int i=0;i<drops.Count;i++)if(drops[i]!=null&&drops[i].Pointer==__instance.Pointer)return;drops.Add(__instance);}
 internal static void Tick(GameWorld? current){if(current==null){if(world!=IntPtr.Zero)Reset();return;}if(world!=current.Pointer){Reset();world=current.Pointer;}if((markers.Value||InterfaceMenu.AirdropPage)&&Time.unscaledTime>=nextScan){nextScan=Time.unscaledTime+15;drops.RemoveAll(d=>d==null||!d.gameObject.activeInHierarchy);foreach(var drop in Object.FindObjectsOfType<AirdropSynchronizableObject>())if(drop!=null&&drop.gameObject.activeInHierarchy)Track(drop);}if(Time.unscaledTime>pointUntil)pendingPosition=null;TickInformationSafe(current);}
 internal static void Reset(){ResetInformation();drops.Clear();pendingPosition=null;world=IntPtr.Zero;nextScan=0;foreach(var point in points)if(point!=null)Object.Destroy(point);points.Clear();}
 static void Load(){var settings=NextRaidLoot.Read();custom=settings.AirdropEnabled;type=Math.Max(0,Array.IndexOf(types,settings.AirdropType));quality=settings.AirdropQuality;loaded=true;}
 static void Save(){var settings=NextRaidLoot.Read();bool prior=settings.AirdropEnabled;settings.AirdropEnabled=custom;settings.AirdropType=types[type];settings.AirdropQuality=quality;NextRaidLoot.Save(settings);if(prior!=custom)FeatureToasts.Show("空投种类与品质设置",custom);notice="已保存，作用于随后生成的空投。";}
 static void Summon(){if(!RaidClock.Active)throw new ArgumentException("请进入战局后召唤空投。");if(Time.unscaledTime<cooldown)throw new ArgumentException("请等待当前召唤提交，稍后再试。");var current=Singleton<GameWorld>.Instance;var player=current?.MainPlayer;if(current==null||player?.HealthController?.IsAlive!=true)throw new ArgumentException("需要存活的本地角色。");
  if(custom&&!NextRaidLoot.ServerStatus.StartsWith("服务端模块已就绪",StringComparison.Ordinal))throw new ArgumentException("自定义种类与品质需要先重启服务端加载模块。");Save();var position=player.Position;if(Physics.Raycast(position+Vector3.up*2,Vector3.down,out var hit,8,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))position=hit.point;
  world=current.Pointer;pendingPosition=position;pointUntil=Time.unscaledTime+5;current.InitAirdrop(null,true,position);cooldown=Time.unscaledTime+20;notice="已请求在当前位置投送。飞机和降落需要时间；室内和地下区域请先到开阔地面。";FeatureToasts.Show("空投已请求 · 玩家当前位置");Plugin.Self.Log.LogInfo("Manual airdrop requested at local position; type="+types[type]+", quality="+quality);
 }
 static void Action(Action action){try{action();}catch(Exception e){notice=e.Message;Plugin.Self.Log.LogWarning("Airdrop control: "+e.Message);}}
 internal static void Draw(Rect rect){if(!loaded)Action(Load);float x=rect.x,y=rect.y,w=rect.width;InterfaceMenu.Label(new Rect(x,y,w,30),"空投",InterfaceMenu.Accent,21);y+=44;float half=(w-12)/2;if(InterfaceMenu.Button(new Rect(x,y,half,36),"本局空投信息",infoPage))infoPage=true;if(InterfaceMenu.Button(new Rect(x+half+12,y,half,36),"种类与品质",!infoPage))infoPage=false;y+=52;var body=new Rect(x,y,w,rect.yMax-y);if(infoPage){DrawInformation(body);return;}scroll=GUI.BeginScrollView(body,scroll,new Rect(0,0,w-20,Math.Max(body.height-1,540)));w-=38;x=8;y=8;
  if(InterfaceMenu.Button(new Rect(x,y,250,36),custom?"自定义空投 · 开启":"自定义空投 · 关闭",custom)){custom=!custom;FeatureToasts.Show("自定义空投："+(custom?"开启":"关闭")+" · 待保存");}y+=52;
  float cell=(w-16)/3;for(int i=0;i<names.Length;i++)if(InterfaceMenu.Button(new Rect(x+i%3*(cell+8),y+i/3*42,cell,34),names[i],type==i)){type=i;custom=true;}y+=98;
  for(int i=0;i<qualities.Length;i++)if(InterfaceMenu.Button(new Rect(x,y+i*42,w,34),qualities[i],quality==i)){quality=i;custom=true;}y+=144;
  if(InterfaceMenu.Button(new Rect(x,y,180,36),"保存种类与品质"))Action(Save);y+=52;
  if(InterfaceMenu.Button(new Rect(x,y,250,34),markers.Value?"已有空投定位 · 开启":"已有空投定位 · 关闭",markers.Value))markers.Value=!markers.Value;y+=50;InterfaceMenu.Paragraph(new Rect(x,y,w,88),notice+"\n"+NextRaidLoot.ServerStatus,InterfaceMenu.Accent,12);GUI.EndScrollView();
 }
 internal static void DrawMarkers(Camera camera,Player player){if(!markers.Value)return;foreach(var drop in drops){if(drop==null||!drop.gameObject.activeInHierarchy)continue;var p=camera.WorldToScreenPoint(drop.transform.position+Vector3.up*1.5f);if(p.z<=0||p.x<0||p.x>Screen.width||p.y<0||p.y>Screen.height)continue;float y=Screen.height-p.y;var color=new Color(.45f,1,.72f);InterfaceMenu.Label(new Rect(p.x-150,y-14,300,28),"空投 · "+drop.AirdropType+" · "+Vector3.Distance(player.Position,drop.transform.position).ToString("F0")+" 米",color,14);}}
}
