using System;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class ImpactAiSpawner {
 const string Toy="66015072e9f84d5680039678";
 static ConfigEntry<bool> enabled=null!,toyOnly=null!;static ConfigEntry<string> role=null!,count=null!;static ConfigEntry<int> difficulty=null!;static ConfigEntry<float> cooldown=null!;
 static readonly ImpactGate gate=new();static bool choosing;static string query="",notice="";static Vector2 scroll,rolesScroll;static float nextError;
 internal static void Initialize(Plugin plugin){
  const string section="46. 弹着点生成 AI";
  enabled=FeatureConfig.Bind(section,"Impact AI spawn","弹着点生成 AI",false,"玩家子弹真正碰撞后在弹着点附近的有效导航地面生成指定 AI。每次开枪最多触发一次，穿透、碎片及霰弹不会重复触发。");
  toyOnly=FeatureConfig.Bind(section,"Toy gun only","仅绑定玩具枪",true,"仅 20×1 毫米玩具枪触发；关闭后所有玩家枪械均可触发。");
  role=FeatureConfig.Bind(section,"Impact AI role","弹着点 AI 类型","assault");count=FeatureConfig.Bind(section,"Impact AI count","每次弹着点生成数量","1");
  difficulty=FeatureConfig.Bind(section,"Impact AI difficulty","弹着点 AI 难度",1,"0 简单／1 普通／2 困难／3 极难",new AcceptableValueRange<int>(0,3));
  cooldown=FeatureConfig.Bind(section,"Impact spawn interval","弹着点生成间隔（秒）",1f,"限制连射产生的请求数量；生成队列最多等待 16 个请求。",new AcceptableValueRange<float>(.1f,30));
  plugin.Patches.Patch(AccessTools.Method(typeof(Shot),"HandleCollision"),postfix:new HarmonyMethod(typeof(ImpactAiSpawner),nameof(Collided)));
  plugin.Log.LogInfo("Impact AI spawning ready: real local bullet collision, toy-only option, per-trigger deduplication and queued native activation");
 }
 internal static void Reset(){gate.Reset();notice="";}
 internal static AiSpawnOption CurrentOption=>Option();internal static int CurrentDifficulty=>difficulty.Value;internal static int CurrentCount=>AiSpawnRules.Count(count.Value);internal static void SpawnAt(GameWorld current,Vector3 point,AiBirthPreset birth)=>AiSpawnService.Enqueue(current,point,Option(),difficulty.Value,AiSpawnRules.Count(count.Value),birth,"玩具枪开发者工具"); static AiSpawnOption Option()=>AiSpawnCatalog.Options.FirstOrDefault(option=>option.Role==role.Value)??throw new ArgumentException("请选择可用的 AI 类型。");
 static void Collided(Shot __instance){
  if(__instance==null||!RaidClock.Active)return;if(ToyDeveloper.Handle(__instance))return;if(!enabled.Value)return;
  try{
   var current=Singleton<GameWorld>.Instance;var local=current?.MainPlayer;var weapon=__instance.Weapon;
   if(local?.HealthController?.IsAlive!=true||__instance.PlayerRaidId!=local.RaidId||__instance.Parent!=null||weapon==null||toyOnly.Value&&weapon.StringTemplateId!=Toy||__instance.HitCollider==null)return;
   var point=__instance.HitPoint;if(!float.IsFinite(point.x)||!float.IsFinite(point.y)||!float.IsFinite(point.z))return;
   if(!gate.Accept(current!.Pointer.ToInt64(),weapon.Id,__instance.FireIndex,Time.unscaledTime,cooldown.Value))return;
   AiSpawnService.Enqueue(current,point,Option(),difficulty.Value,AiSpawnRules.Count(count.Value),AiSpawnPresets.Snapshot(),"弹着点");notice="已提交弹着点生成："+Option().Name;
  }catch(Exception error){notice=error.Message;if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+3;FeatureToasts.Show("弹着点生成："+error.Message);Plugin.Self.Log.LogWarning("Impact AI: "+error.Message);}}
 }
 static void Action(Action action){try{action();}catch(Exception error){notice=error.Message;FeatureToasts.Show(notice);}}
 internal static void Draw(Rect rect){
  scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-20,choosing?2100:1900));float x=8,y=8,w=rect.width-38,half=(w-12)/2;
  InterfaceMenu.Label(new Rect(x,y,w,30),"弹着点生成 AI",InterfaceMenu.Accent,21);y+=46;
  if(InterfaceMenu.Button(new Rect(x,y,half,36),enabled.Value?"弹着点生成 · 开":"弹着点生成 · 关",enabled.Value))enabled.Value=!enabled.Value;
  if(InterfaceMenu.Button(new Rect(x+half+12,y,half,36),toyOnly.Value?"仅玩具枪 · 开":"所有玩家枪械",toyOnly.Value))toyOnly.Value=!toyOnly.Value;y+=50;
  if(InterfaceMenu.Button(new Rect(x,y,w,36),"生成类型："+(AiSpawnCatalog.Options.FirstOrDefault(option=>option.Role==role.Value)?.Name??"请选择")+"  ›"))choosing=!choosing;y+=48;
  if(choosing){query=InterfaceMenu.Field(new Rect(x,y,w,32),query);y+=42;var options=AiSpawnCatalog.Options.Where(option=>(option.Name+" "+option.Role+" "+option.Group).Contains(query,StringComparison.OrdinalIgnoreCase)).ToArray();rolesScroll=GUI.BeginScrollView(new Rect(x,y,w,166),rolesScroll,new Rect(0,0,w-20,options.Length*40));for(int i=0;i<options.Length;i++)if(InterfaceMenu.Button(new Rect(0,i*40,w-26,32),options[i].Group+" · "+options[i].Name,options[i].Role==role.Value)){role.Value=options[i].Role;choosing=false;}GUI.EndScrollView();y+=180;}
  string[] difficulties={"简单","普通","困难","极难"};float cell=(w-24)/4;for(int i=0;i<4;i++)if(InterfaceMenu.Button(new Rect(x+i*(cell+8),y,cell,32),difficulties[i],difficulty.Value==i))difficulty.Value=i;y+=46;
  InterfaceMenu.Label(new Rect(x,y,half,24),"每次生成数量",null,13);InterfaceMenu.Label(new Rect(x+half+12,y,half,24),"最短触发间隔（秒）",null,13);y+=30;
  count.Value=InterfaceMenu.Field(new Rect(x,y,half,34),count.Value);string delay=InterfaceMenu.Field(new Rect(x+half+12,y,half,34),cooldown.Value.ToString("G4",System.Globalization.CultureInfo.InvariantCulture));if(float.TryParse(delay,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var value)&&float.IsFinite(value)&&value>=.1f&&value<=30)cooldown.Value=value;y+=48;
  if(InterfaceMenu.Button(new Rect(x,y,half,34),"预加载所选 AI"))Action(()=>AiSpawnService.Preload(Option(),difficulty.Value,AiSpawnRules.Count(count.Value)));
  if(InterfaceMenu.Button(new Rect(x+half+12,y,half,34),"取消剩余生成"))AiSpawnService.Cancel(true);y+=48;
  InterfaceMenu.Paragraph(new Rect(x,y,w,82),AiSpawnService.Status+"\n"+notice,InterfaceMenu.Accent,12);y+=96;
  AiSpawnPresets.Draw(new Rect(x,y,w,AiSpawnPresets.Height));GUI.EndScrollView();
 }
}
