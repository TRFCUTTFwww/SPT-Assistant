using System;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using UnityEngine;
using Shortcut=BepInEx.Unity.IL2CPP.Configuration.KeyboardShortcut;

namespace LocalSpt5Esp;
internal static class BlackwallBridge {
 static ConfigEntry<bool> enabled=null!,walls=null!,effects=null!,filter=null!;
 static ConfigEntry<float> range=null!,cone=null!,volume=null!;static ConfigEntry<int> limit=null!;
 static ConfigEntry<Shortcut> key=null!,previewKey=null!;
 static Type? api;static float nextFind;static string message="等待独立黑墙 Mod";static Vector2 scroll;static bool previewPending;static int pendingFrame;
 internal static void Initialize(){
  const string section="65. 黑墙脉冲";
  enabled=FeatureConfig.Bind(section,"Blackwall pulse enabled","启用黑墙脉冲",true,"由独立 Blackwall Pulse Mod 执行动作、特效与伤害。关闭后停止施放。");
  key=FeatureConfig.Bind(section,"Blackwall pulse shortcut","黑墙脉冲施放键",new Shortcut(KeyCode.F11),"单击施放；无冷却，收手结束即可再次施放。支持组合键与鼠标键。");
  previewKey=FeatureConfig.Bind(section,"Blackwall preview shortcut","黑墙无伤害预览键",new Shortcut(KeyCode.F11,KeyCode.LeftAlt),"只播放玩家动作、音效和滤镜，不选择或伤害 AI。");
  range=FeatureConfig.Bind(section,"Blackwall range","黑墙最大距离（米）",60f,"",new AcceptableValueRange<float>(1,300));
  cone=FeatureConfig.Bind(section,"Blackwall cone","黑墙前方范围（度）",70f,"镜头正前方扇形范围；数值越大可覆盖越多目标。",new AcceptableValueRange<float>(1,180));
  limit=FeatureConfig.Bind(section,"Blackwall target limit","每次最多目标数",8,"范围内优先选择距离较近的 AI。",new AcceptableValueRange<int>(1,24));
  walls=FeatureConfig.Bind(section,"Blackwall through walls","黑墙穿墙",false,"允许选择遮挡后的 AI。默认关闭。仍保留辅助 Mod 的 AI 无敌保护。");
  effects=FeatureConfig.Bind(section,"Blackwall effects","黑墙手部与人物特效",true);
  filter=FeatureConfig.Bind(section,"Blackwall filter","黑墙施放滤镜",true);
  volume=FeatureConfig.Bind(section,"Blackwall volume","黑墙音量",.65f,"",new AcceptableValueRange<float>(0,1));
 }
 static object? Call(string name,params object?[] args)=>api?.GetMethod(name,BindingFlags.Public|BindingFlags.Static)?.Invoke(null,args);
 static bool Protected(long pointer){var all=Singleton<GameWorld>.Instance?.AllAlivePlayersList;if(all==null)return true;for(int i=0;i<all.Count;i++){var actor=all[i];if(actor!=null&&actor.Pointer.ToInt64()==pointer)return actor.ActiveHealthController==null||Plugin.DamageBlocked(actor.ActiveHealthController);}return true;}
 internal static void Tick(){
  try{
   if(api==null&&Time.unscaledTime>=nextFind){nextFind=Time.unscaledTime+2;api=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name=="Local.BlackwallPulse")?.GetType("LocalBlackwallPulse.PulseApi");if(api!=null){if((int?)api.GetProperty("ApiVersion")?.GetValue(null)!=1){api=null;message="黑墙接口版本不兼容";return;}Call("SetProtection",(Func<long,bool>)Protected);}}
   if(api==null)return;
   bool blocked=InterfaceMenu.InputBlocked||WorldEditor.Active||DirectorStage.FreeActive||!RaidClock.Active;
   Call("Configure",enabled.Value,range.Value,cone.Value,limit.Value,walls.Value,effects.Value,filter.Value,volume.Value);Call("SetInputBlocked",blocked);
   if(previewPending&&Time.frameCount>pendingFrame+180)previewPending=false;
   if(previewPending&&Time.frameCount>pendingFrame+2&&!blocked&&!Cursor.visible){previewPending=false;Call("TryPreview");}
   if(!blocked&&Hotkeys.Ready){if(ShortcutBindings.Take(previewKey))Call("TryPreview");else if(ShortcutBindings.Take(key))Call("TryCast");}
   message=(string?)api.GetProperty("Status")?.GetValue(null)??"独立黑墙 Mod 已连接";
  }catch(Exception error){message="黑墙调用失败："+(error.InnerException??error).Message;}
 }
 internal static object Diagnostics()=>new{installed=api!=null,status=message,plugin=api==null?null:Call("Diagnostics")};
 internal static void Draw(Rect rect){
  InterfaceMenu.Label(new(rect.x,rect.y,rect.width,28),"黑墙脉冲 · 独立 Mod",InterfaceMenu.Accent,20);
  InterfaceMenu.Paragraph(new(rect.x,rect.y+38,rect.width,54),api==null?"尚未连接 Blackwall Pulse。安装独立插件后启动客户端；辅助 Mod 负责调用和快捷键设置。":message,null,13);
  if(InterfaceMenu.Button(new(rect.x,rect.y+100,rect.width,34),"无伤害预览（关闭菜单后播放）")){previewPending=true;pendingFrame=Time.frameCount;InterfaceMenu.Close();}
  var entries=new ConfigEntryBase[]{enabled,key,previewKey,range,cone,limit,walls,effects,filter,volume};var area=new Rect(rect.x,rect.y+148,rect.width,Math.Max(50,rect.height-148));scroll=GUI.BeginScrollView(area,scroll,new Rect(0,0,rect.width-22,entries.Length*100));for(int i=0;i<entries.Length;i++)InterfaceMenu.DrawEntry(entries[i],new Rect(2,i*100,rect.width-28,92));GUI.EndScrollView();
 }
 internal static void Reset(){try{Call("Disconnect");}catch{}api=null;previewPending=false;}
}
