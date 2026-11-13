using System;
using System.Linq;
using System.Reflection;
using System.IO;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using HarmonyLib;
using UnityEngine;
using LocalBlackwallPulse;
using Shortcut=BepInEx.Unity.IL2CPP.Configuration.KeyboardShortcut;
namespace LocalSpt5Esp;
internal static class BlackwallBridge {
 static ConfigEntry<bool> enabled=null!,walls=null!,effects=null!,filter=null!;
 static ConfigEntry<float> range=null!,cone=null!,volume=null!;static ConfigEntry<int> limit=null!;
 static ConfigEntry<Shortcut> key=null!,previewKey=null!;
 static string message="黑墙尚未初始化";static Vector2 scroll;static bool previewPending,active;static int pendingFrame;
 internal static void Initialize(){
  const string section="65. 黑墙脉冲";
  enabled=FeatureConfig.Bind(section,"Blackwall pulse enabled","启用黑墙脉冲",true,"由辅助 Mod 执行动作、特效与伤害。关闭后停止施放。");
  key=FeatureConfig.Bind(section,"Blackwall pulse shortcut","黑墙脉冲施放键",new Shortcut(KeyCode.F11),"单击施放；无冷却，收手结束即可再次施放。支持组合键与鼠标键。");
  previewKey=FeatureConfig.Bind(section,"Blackwall preview shortcut","黑墙无伤害预览键",new Shortcut(KeyCode.F11,KeyCode.LeftAlt),"只播放玩家动作、特效和滤镜，不选择或伤害 AI；预览静音。");
  range=FeatureConfig.Bind(section,"Blackwall range","黑墙最大距离（米）",60f,"",new AcceptableValueRange<float>(1,300));
  cone=FeatureConfig.Bind(section,"Blackwall cone","黑墙前方范围（度）",70f,"镜头正前方扇形范围；数值越大可覆盖越多目标。",new AcceptableValueRange<float>(1,180));
  limit=FeatureConfig.Bind(section,"Blackwall target limit","每次最多目标数",8,"范围内优先选择距离较近的 AI。",new AcceptableValueRange<int>(1,24));
  walls=FeatureConfig.Bind(section,"Blackwall through walls","黑墙穿墙",false,"允许选择遮挡后的 AI。默认关闭。仍保留辅助 Mod 的 AI 无敌保护。");
  effects=FeatureConfig.Bind(section,"Blackwall effects","黑墙手部与人物特效",true);
  filter=FeatureConfig.Bind(section,"Blackwall filter","显示全屏红光（可关闭）",false,"只影响屏幕覆盖，不关闭手部、人物红光或击杀音效。");
  volume=FeatureConfig.Bind(section,"Blackwall volume","黑墙音量",.65f,"",new AcceptableValueRange<float>(0,1));

  StopLegacyRuntime();
  string home=Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","Blackwall");
  Directory.CreateDirectory(home);
  if(!File.Exists(Path.Combine(home,"Assets","station-arms.glb"))){message="缺少黑墙资源，请安装完整的辅助 2.2 安装包";return;}
  try{
   PulseRuntime.Initialize(home,Plugin.Self.Log);PulseRuntime.Protection=Protected;
   Plugin.Self.Patches.Patch(AccessTools.Method(typeof(Player.FirearmController),"SetTriggerPressed"),prefix:new HarmonyMethod(typeof(BlackwallBridge),nameof(BeforeTrigger)));
   active=true;message="黑墙脉冲已内置于辅助 Mod";
  }catch{PulseRuntime.Shutdown();throw;}
 }
 static bool BeforeTrigger(Player.FirearmController __instance,bool __0)=>!__0||!PulseRuntime.Owns(__instance);
 static void StopLegacyRuntime(){
  foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name=="Local.BlackwallPulse")){
   var api=assembly.GetType("LocalBlackwallPulse.PulseApi");
   if((bool?)api?.GetProperty("Active")?.GetValue(null)!=true)continue;
   (api!.GetMethod("StopForReload")??throw new InvalidOperationException("Cannot stop legacy Blackwall runtime")).Invoke(null,null);
   if((bool?)api.GetProperty("Active")?.GetValue(null)==true)throw new InvalidOperationException("Legacy Blackwall is still active");
  }
 }
 static bool Protected(long pointer){var all=Singleton<GameWorld>.Instance?.AllAlivePlayersList;if(all==null)return true;for(int i=0;i<all.Count;i++){var actor=all[i];if(actor!=null&&actor.Pointer.ToInt64()==pointer)return actor.ActiveHealthController==null||Plugin.DamageBlocked(actor.ActiveHealthController);}return true;}
 internal static void Tick(){
  if(!active)return;
  try{
   bool blocked=InterfaceMenu.InputBlocked||WorldEditor.Active||DirectorStage.FreeActive||!RaidClock.Active;
   PulseRuntime.Configure(enabled.Value,range.Value,cone.Value,limit.Value,walls.Value,effects.Value,filter.Value,volume.Value);PulseRuntime.AssistantInput(blocked);PulseRuntime.Tick();
   if(previewPending&&Time.frameCount>pendingFrame+180)previewPending=false;
   if(previewPending&&Time.frameCount>pendingFrame+2&&!blocked&&!Cursor.visible){previewPending=false;PulseRuntime.Cast(true);}
   if(!blocked&&Hotkeys.Ready){if(ShortcutBindings.Take(previewKey))PulseRuntime.Cast(true);else if(ShortcutBindings.Take(key))PulseRuntime.Cast();}
   message=PulseRuntime.Status;
  }catch(Exception error){message="黑墙调用失败："+error.Message;}
 }
 internal static object Diagnostics()=>new{installed=active,integrated=true,status=message,plugin=active?PulseRuntime.Diagnostics():null};
 internal static object? CheckArms()=>active?ArmAnimation.Check(Singleton<GameWorld>.Instance?.MainPlayer,EFT.CameraControl.CameraManager.Instance?.Camera):null;
 internal static void Late(){if(!active)return;try{PulseRuntime.Late();}catch(Exception error){message="黑墙更新失败："+error.Message;}}
 internal static void DrawEffects(){if(active)PulseRuntime.Draw();}
 internal static void Draw(Rect rect){
  InterfaceMenu.Label(new(rect.x,rect.y,rect.width,28),"黑墙脉冲",InterfaceMenu.Accent,20);
  InterfaceMenu.Paragraph(new(rect.x,rect.y+38,rect.width,54),message,null,13);
  float buttonWidth=(rect.width-8)/2;
  if(InterfaceMenu.Button(new(rect.x,rect.y+100,buttonWidth,34),"无伤害动作预览")){previewPending=true;pendingFrame=Time.frameCount;InterfaceMenu.Close();}
  if(InterfaceMenu.Button(new(rect.x+buttonWidth+8,rect.y+100,buttonWidth,34),"试听黑墙击杀音效")&&active)PulseRuntime.SoundPreview();
  var entries=new ConfigEntryBase[]{enabled,key,previewKey,range,cone,limit,walls,effects,filter,volume};var area=new Rect(rect.x,rect.y+148,rect.width,Math.Max(50,rect.height-148));scroll=GUI.BeginScrollView(area,scroll,new Rect(0,0,rect.width-22,entries.Length*100));for(int i=0;i<entries.Length;i++)InterfaceMenu.DrawEntry(entries[i],new Rect(2,i*100,rect.width-28,92));GUI.EndScrollView();
 }
 internal static void Reset(){try{if(active)PulseRuntime.Shutdown();}finally{active=false;previewPending=false;}}
}
