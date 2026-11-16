using System;
using System.Linq;
using EFT;
using BepInEx.Configuration;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class QuickhackSettingsBridge {
 static Vector2 scroll;
 static float nextLookup;
 static Type? api;
 static Func<bool>? active;
 static Func<BotOwner,IPlayer,bool>? hostile;
 static void FindApi() {
  if(active?.Invoke()==true) return;
  api=null; active=null; hostile=null;
  if(Time.unscaledTime<nextLookup) return;
  nextLookup=Time.unscaledTime+1;
  api=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LocalQuickhacks.QuickhackApi")).FirstOrDefault(t=>t?.GetProperty("Active")?.GetValue(null) is true);
  active=api?.GetProperty("Active")?.GetMethod?.CreateDelegate<Func<bool>>();
  hostile=api?.GetMethod("IsHostile")?.CreateDelegate<Func<BotOwner,IPlayer,bool>>();
 }
 internal static bool IsHostile(BotOwner? owner,IPlayer? target) {
  if(owner==null||target==null) return false;
  try { FindApi(); return hostile?.Invoke(owner,target)==true; }
  catch { api=null; active=null; hostile=null; nextLookup=Time.unscaledTime+1; return false; }
 }
 static string Name(ConfigEntryBase entry)=>entry.Definition.Key switch {
  "Keycard hacking enabled"=>"门卡破解", "Enabled"=>"启用独立快速破解", "Infinite RAM"=>"无限 RAM", "Range"=>"扫描与上传距离（米）",
  "Gateway spread range"=>"黑墙每跳传染范围（米）", "Gateway target limit"=>"黑墙连锁总目标数",
  "Gateway hop duration"=>"黑墙每跳上传时长（秒）", "Volume"=>"快速破解音量", _=>entry.Definition.Key
 };
 internal static void DrawDoorToggle(Rect rect){try{FindApi();var entries=api?.GetMethod("GetSettings")?.Invoke(null,null) as ConfigEntryBase[];var entry=entries?.FirstOrDefault(e=>e.Definition.Key=="Keycard hacking enabled");if(entry!=null)InterfaceMenu.DrawEntry(entry,rect,"门卡破解");else InterfaceMenu.Paragraph(rect,"门卡破解开关需要新版网络接入仓模块。",null,13);}catch{InterfaceMenu.Paragraph(rect,"网络接入仓模块尚未就绪。",null,13);}}
 internal static void Draw(Rect rect) {
  try {
   FindApi();
   InterfaceMenu.Label(new Rect(rect.x,rect.y,rect.width,30),"CP2077 快速破解",InterfaceMenu.Accent,20);
   if(api?.GetMethod("GetSettings")?.Invoke(null,null) is not ConfigEntryBase[] entries) {
    InterfaceMenu.Paragraph(new Rect(rect.x,rect.y+44,rect.width,85),"独立快速破解 Mod 尚未就绪。安装并启用 CP2077 Quickhacks 后，即可在这里调整设置。",null,15);return;
   }
   InterfaceMenu.Paragraph(new Rect(rect.x,rect.y+38,rect.width,54),"修改立即生效并自动保存。黑墙从每个感染目标继续搜索；总目标数包含首次施放的目标。",null,13);
   var area=new Rect(rect.x,rect.y+102,rect.width,Math.Max(40,rect.height-102));
   scroll=GUI.BeginScrollView(area,scroll,new Rect(0,0,rect.width-22,entries.Length*102));
   try { for(int i=0;i<entries.Length;i++) InterfaceMenu.DrawEntry(entries[i],new Rect(0,i*102,rect.width-28,92),Name(entries[i])); }
   finally { GUI.EndScrollView(); }
  } catch(Exception e) { api=null; InterfaceMenu.Paragraph(new Rect(rect.x,rect.y+44,rect.width,80),"快速破解设置暂不可用："+e.GetBaseException().Message,null,14); }
 }
}
