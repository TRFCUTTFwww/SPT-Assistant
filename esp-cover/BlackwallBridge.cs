using System;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Runtime.Loader;
using System.Security.Cryptography;
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
 static DateTime payloadStamp;static float nextReload;
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
  filter=FeatureConfig.Bind(section,"Blackwall filter","显示全屏红光（可关闭）",false,"只影响屏幕覆盖，不关闭手部、人物红光或击杀音效。");
  volume=FeatureConfig.Bind(section,"Blackwall volume","黑墙音量",.65f,"",new AcceptableValueRange<float>(0,1));
 }
 static Type? FindApi()=>AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name=="Local.BlackwallPulse").Select(a=>a.GetType("LocalBlackwallPulse.PulseApi")).Where(t=>t!=null&&(t.GetProperty("Active")?.GetValue(null) as bool? ?? t.Assembly.GetType("LocalBlackwallPulse.Plugin")?.GetField("Instance",BindingFlags.Static|BindingFlags.NonPublic)?.GetValue(null)!=null)).OrderByDescending(t=>System.Version.TryParse((string?)t!.GetProperty("Version")?.GetValue(null),out var v)?v:new System.Version()).FirstOrDefault();
 static void TryHotReload(){
  if(Time.unscaledTime<nextReload)return;nextReload=Time.unscaledTime+1;
  string path=Path.Combine(BepInEx.Paths.PluginPath,"BlackwallPulse","runtime","Local.BlackwallPulse.module");if(!File.Exists(path))return;
  var stamp=File.GetLastWriteTimeUtc(path);if(stamp==payloadStamp)return;
  var previous=api??FindApi();if(previous==null||(bool?)previous.GetProperty("Busy")?.GetValue(null)==true)return;payloadStamp=stamp;
  byte[] bytes=File.ReadAllBytes(path);string hash=Convert.ToHexString(SHA256.HashData(bytes));
  if(hash==(string?)previous.GetProperty("PayloadHash")?.GetValue(null)){api=previous;return;}
  object? host=previous.GetProperty("Host")?.GetValue(null)??previous.Assembly.GetType("LocalBlackwallPulse.Plugin")?.GetField("Instance",BindingFlags.Static|BindingFlags.NonPublic)?.GetValue(null);
  if(host==null)throw new InvalidOperationException("Blackwall native host is unavailable");
  var context=new BlackwallContext();using var stream=new MemoryStream(bytes,false);var candidate=context.LoadFromStream(stream).GetType("LocalBlackwallPulse.PulseApi",true)!;
  var start=candidate.GetMethod("StartHot",BindingFlags.Public|BindingFlags.Static)??throw new InvalidOperationException("Blackwall module has no hot activation API");
  if((int?)candidate.GetProperty("ApiVersion")?.GetValue(null)!=1)throw new InvalidOperationException("Blackwall module API mismatch");
  try{
   var stop=previous.GetMethod("StopForReload");if(stop!=null)stop.Invoke(null,null);else host.GetType().GetMethod("Unload")!.Invoke(host,null);
   start.Invoke(null,new object[]{host,hash});api=candidate;Call("SetProtection",(Func<long,bool>)Protected);message="黑墙能力已热更新到 "+candidate.GetProperty("Version")?.GetValue(null);
   Plugin.Self.Log.LogInfo(message);
  }catch{
   try{candidate.GetMethod("StopForReload")?.Invoke(null,null);var restore=previous.GetMethod("StartHot");if(restore!=null)restore.Invoke(null,new object[]{host,(string?)previous.GetProperty("PayloadHash")?.GetValue(null)??""});else host.GetType().GetMethod("Load")!.Invoke(host,null);api=previous;}catch(Exception rollback){Plugin.Self.Log.LogError("Blackwall rollback: "+rollback);}
   throw;
  }
 }
 sealed class BlackwallContext:AssemblyLoadContext {
  internal BlackwallContext():base("Blackwall-"+Guid.NewGuid().ToString("N"),false){}
  protected override Assembly? Load(AssemblyName name){var shared=Default.Assemblies.FirstOrDefault(a=>a.GetName().Name==name.Name);if(shared!=null)return shared;try{return Default.LoadFromAssemblyName(name);}catch(FileNotFoundException){}foreach(var folder in new[]{"core","interop"}){string file=Path.Combine(BepInEx.Paths.BepInExRootPath,folder,name.Name+".dll");if(File.Exists(file))return Default.LoadFromAssemblyPath(file);}return null;}
 }
 static object? Call(string name,params object?[] args)=>api?.GetMethod(name,BindingFlags.Public|BindingFlags.Static)?.Invoke(null,args);
 static bool Protected(long pointer){var all=Singleton<GameWorld>.Instance?.AllAlivePlayersList;if(all==null)return true;for(int i=0;i<all.Count;i++){var actor=all[i];if(actor!=null&&actor.Pointer.ToInt64()==pointer)return actor.ActiveHealthController==null||Plugin.DamageBlocked(actor.ActiveHealthController);}return true;}
 internal static void Tick(){
  try{
   TryHotReload();
   if(api==null&&Time.unscaledTime>=nextFind){nextFind=Time.unscaledTime+2;api=FindApi();if(api!=null){if((int?)api.GetProperty("ApiVersion")?.GetValue(null)!=1){api=null;message="黑墙接口版本不兼容";return;}Call("SetProtection",(Func<long,bool>)Protected);}}
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
  float buttonWidth=(rect.width-8)/2;
  if(InterfaceMenu.Button(new(rect.x,rect.y+100,buttonWidth,34),"无伤害动作预览")){previewPending=true;pendingFrame=Time.frameCount;InterfaceMenu.Close();}
  if(InterfaceMenu.Button(new(rect.x+buttonWidth+8,rect.y+100,buttonWidth,34),"试听黑墙击杀音效"))Call("PreviewDeathSound");
  var entries=new ConfigEntryBase[]{enabled,key,previewKey,range,cone,limit,walls,effects,filter,volume};var area=new Rect(rect.x,rect.y+148,rect.width,Math.Max(50,rect.height-148));scroll=GUI.BeginScrollView(area,scroll,new Rect(0,0,rect.width-22,entries.Length*100));for(int i=0;i<entries.Length;i++)InterfaceMenu.DrawEntry(entries[i],new Rect(2,i*100,rect.width-28,92));GUI.EndScrollView();
 }
 internal static void Reset(){try{Call("Disconnect");}catch{}api=null;previewPending=false;}
}
