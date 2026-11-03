using System;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class RaidClock {
 internal static ConfigEntry<bool> PreventTimeout=null!,ShowClock=null!;
 static string minutes="60",notice="";
 static bool scenarioHook,stopHook;
 static float nextError;
 internal static bool Active {get{try{return ItemSpawner.InRaid()&&Singleton<GameWorld>.Instance?.MainPlayer?.IsYourPlayer==true;}catch{return false;}}}
 internal static GameTimer? Timer=>Active?Singleton<AbstractGame>.Instance?.GameTimer:null;
 internal static void Initialize(Plugin plugin){
  PreventTimeout=FeatureConfig.Bind("26. 战局时间","Prevent timeout","时间结束后不迷失（继续战局）",true,"到零后继续游玩，仍可正常撤离；关闭后恢复超时结算。");
  ShowClock=FeatureConfig.Bind("26. 战局时间","Show clock","常驻显示当前时间与剩余时间",true);
  try{plugin.Patches.Patch(AccessTools.Method(typeof(EndByTimerScenario),"Update"),prefix:new HarmonyMethod(typeof(RaidClock),nameof(BeforeTimeout)));scenarioHook=true;}catch(Exception e){plugin.Log.LogError("Raid timeout hook failed: "+e);}
  try{plugin.Patches.Patch(typeof(LocalGame).GetMethod("Stop",new[]{typeof(string),typeof(ExitStatus),typeof(string),typeof(float)}),prefix:new HarmonyMethod(typeof(RaidClock),nameof(BeforeStop)));stopHook=true;}catch(Exception e){plugin.Log.LogError("Raid stop guard failed: "+e);}
 }
 static bool BeforeTimeout(EndByTimerScenario __instance){try{return !PreventTimeout.Value||!Active||Timer==null||__instance._timer==null||__instance._timer.Pointer!=Timer.Pointer;}catch(Exception e){Warn(e);return true;}}
 static bool BeforeStop(string __0,ExitStatus __1){try{if(PreventTimeout.Value&&Active&&__1==ExitStatus.MissingInAction&&__0==Singleton<GameWorld>.Instance.MainPlayer.ProfileId&&Remaining is double seconds&&seconds<=1)return false;}catch(Exception e){Warn(e);}return true;}
 internal static double? Remaining {get{var timer=Timer;if(timer==null||!timer.SessionTime.HasValue)return null;return timer.SessionTime.Value.TotalSeconds-timer.PastTime.TotalSeconds;}}
 internal static string RemainingText=>Remaining is double s?RaidRules.Clock(s)+(s<=0&&PreventTimeout.Value?" · 超时保护中":""):"未进入战局 / 计时未同步";
 internal static string GameTime {get{try{return Active?Singleton<GameWorld>.Instance.GameDateTime?.Calculate().ToString("HH:mm:ss")??"未同步":"未进入战局";}catch{return "未同步";}}}
 internal static void SetRemaining(double seconds){var timer=Timer??throw new ArgumentException("请进入战局后修改时间。");double duration=RaidRules.Duration(timer.PastTime.TotalSeconds,seconds);timer.ChangeSessionTime(Il2CppSystem.TimeSpan.FromSeconds(duration));if(Remaining is not double actual||Math.Abs(actual-seconds)>2)throw new InvalidOperationException("计时回读不一致，请重新检查。");notice="剩余时间已设置为 "+RaidRules.Clock(seconds);Plugin.Self.Log.LogInfo("Raid remaining seconds changed to "+seconds);}
 static void Action(Action action){try{action();}catch(Exception e){notice=e.Message;Warn(e);}}
 internal static void DrawPanel(Rect rect){
  float gap=18,half=(rect.width-gap)/2;
  InterfaceMenu.Label(new Rect(rect.x,rect.y,rect.width,28),"当前战局",InterfaceMenu.Text,18);
  InterfaceMenu.Label(new Rect(rect.xMax-220,rect.y,220,26),"本机时间  "+DateTime.Now.ToString("HH:mm:ss"),Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.4f),12);
  var clockCard=new Rect(rect.x,rect.y+42,half,102);var remainingCard=new Rect(rect.x+half+gap,rect.y+42,half,102);
  foreach(var card in new[]{clockCard,remainingCard})InterfaceMenu.Surface(card,Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.045f),11);
  InterfaceMenu.Label(new Rect(clockCard.x+18,clockCard.y+12,half-36,25),"游戏时间",Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.35f),12);InterfaceMenu.Label(new Rect(clockCard.x+18,clockCard.y+44,half-36,42),GameTime,InterfaceMenu.Text,29);
  InterfaceMenu.Label(new Rect(remainingCard.x+18,remainingCard.y+12,half-36,25),"剩余时间",Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.35f),12);InterfaceMenu.Label(new Rect(remainingCard.x+18,remainingCard.y+44,half-36,42),Remaining is double seconds?RaidRules.Clock(seconds):"未同步",Remaining is <=300?Color.yellow:InterfaceMenu.Accent,29);
  float y=rect.y+164;var left=new Rect(rect.x,y,half,510);var right=new Rect(rect.x+half+gap,y,half,510);InterfaceMenu.Surface(left,Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.025f),11);InterfaceMenu.Surface(right,Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.025f),11);
  float x=left.x+16,w=left.width-32;InterfaceMenu.Label(new Rect(x,y+16,w,28),"时间控制",InterfaceMenu.Accent,17);
  if(InterfaceMenu.Button(new Rect(x,y+62,w,36),"超时不迷失  ·  "+(PreventTimeout.Value?"已开启":"已关闭"),PreventTimeout.Value))PreventTimeout.Value=!PreventTimeout.Value;
  InterfaceMenu.Paragraph(new Rect(x,y+112,w,62),Remaining is <=0&&PreventTimeout.Value?"超时保护中，可继续搜刮并正常撤离。":"到零后继续当前战局，正常撤离仍然有效。",null,12);
  InterfaceMenu.Label(new Rect(x,y+186,w,25),"设置剩余时间（分钟）",null,13);minutes=InterfaceMenu.Field(new Rect(x,y+224,Math.Max(80,w-102),34),minutes);if(InterfaceMenu.Button(new Rect(x+w-90,y+224,90,34),"应用"))Action(()=>SetRemaining(RaidRules.Minutes(minutes)));
  if(InterfaceMenu.Button(new Rect(x,y+278,(w-10)/2,35),"＋ 15 分钟"))Action(()=>SetRemaining(Math.Min(RaidRules.MaximumSeconds,Math.Max(0,Remaining??throw new ArgumentException("计时未同步"))+900)));
  if(InterfaceMenu.Button(new Rect(x+(w+10)/2,y+278,(w-10)/2,35),"＋ 60 分钟"))Action(()=>SetRemaining(Math.Min(RaidRules.MaximumSeconds,Math.Max(0,Remaining??throw new ArgumentException("计时未同步"))+3600)));
  InterfaceMenu.Paragraph(new Rect(x,y+338,w,120),notice.Length>0?notice:"修改只作用于当前战局，范围 0～1440 分钟。",null,12);
  if(!scenarioHook||!stopHook)InterfaceMenu.Paragraph(new Rect(x,y+462,w,40),"计时保护接口未全部就绪，请检查日志。",Color.yellow,12);
  RaidEnvironment.Draw(new Rect(right.x+16,right.y+16,right.width-32,480));
 }
 internal static void DrawHud(){if(!ShowClock.Value||!Active)return;var rect=InterfaceMenu.Place("raidclock",new Rect(Math.Max(12,Screen.width/2-265),Screen.height-65,530,30));InterfaceMenu.Panel(rect);InterfaceMenu.Label(new Rect(rect.x+8,rect.y+2,rect.width-16,26),"时间 "+GameTime+"  |  剩余 "+RemainingText,Remaining is <=300?Color.yellow:InterfaceMenu.Text,14);}
 static void Warn(Exception e){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+20;Plugin.Self.Error(e);}
}
