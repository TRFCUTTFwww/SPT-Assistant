using System;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class RaidClock {
 internal static ConfigEntry<bool> PreventTimeout=null!,ShowClock=null!;
 static ConfigEntry<int> hudWidth=null!,hudHeight=null!,hudFont=null!;
 static string minutes="60",hoursPart="1",minutesPart="0",secondsPart="0",notice="";static bool useHms=true;
 static bool scenarioHook,stopHook;
 static float nextError;
 internal static bool Active {get{try{return ItemSpawner.InRaid()&&Singleton<GameWorld>.Instance?.MainPlayer?.IsYourPlayer==true;}catch{return false;}}}
 internal static GameTimer? Timer=>Active?Singleton<AbstractGame>.Instance?.GameTimer:null;
 internal static void Initialize(Plugin plugin){
  PreventTimeout=FeatureConfig.Bind("26. 战局时间","Prevent timeout","时间结束后不迷失（继续战局）",true,"到零后继续游玩，仍可正常撤离；关闭后恢复超时结算。");
  ShowClock=FeatureConfig.Bind("26. 战局时间","Show clock","常驻显示现实、游戏与剩余时间",true);
  hudWidth=FeatureConfig.Bind("26. 战局时间","Clock width","时间悬浮窗宽度",260,"",new AcceptableValueRange<int>(180,720));hudHeight=FeatureConfig.Bind("26. 战局时间","Clock height","时间悬浮窗高度",84,"",new AcceptableValueRange<int>(70,160));hudFont=FeatureConfig.Bind("26. 战局时间","Clock font size","时间悬浮窗字号",13,"",new AcceptableValueRange<int>(10,24));
  NativeRaidTimer.Initialize(plugin);
  try{plugin.Patches.Patch(AccessTools.Method(typeof(EndByTimerScenario),"Update"),prefix:new HarmonyMethod(typeof(RaidClock),nameof(BeforeTimeout)));scenarioHook=true;}catch(Exception e){plugin.Log.LogError("Raid timeout hook failed: "+e);}
  try{plugin.Patches.Patch(typeof(LocalGame).GetMethod("Stop",new[]{typeof(string),typeof(ExitStatus),typeof(string),typeof(float)}),prefix:new HarmonyMethod(typeof(RaidClock),nameof(BeforeStop)));stopHook=true;}catch(Exception e){plugin.Log.LogError("Raid stop guard failed: "+e);}
 }
 static bool BeforeTimeout(EndByTimerScenario __instance){try{return !PreventTimeout.Value||!Active||Timer==null||__instance._timer==null||__instance._timer.Pointer!=Timer.Pointer;}catch(Exception e){Warn(e);return true;}}
 static bool BeforeStop(string __0,ExitStatus __1){try{if(RaidEnd.ManualEnding)return true;if(PreventTimeout.Value&&Active&&__1==ExitStatus.MissingInAction&&__0==Singleton<GameWorld>.Instance.MainPlayer.ProfileId&&Remaining is double seconds&&seconds<=1)return false;}catch(Exception e){Warn(e);}return true;}
 internal static double? Remaining {get{var timer=Timer;if(timer==null||!timer.SessionTime.HasValue)return null;return timer.SessionTime.Value.TotalSeconds-timer.PastTime.TotalSeconds;}}
 static Color RemainingColor=>RaidRules.TimeTone(Remaining,Timer?.SessionTime is {} span&&span.HasValue?span.Value.TotalSeconds:null) switch{RaidTimeTone.Green=>Color.green,RaidTimeTone.Yellow=>Color.yellow,RaidTimeTone.Red=>Color.red,_=>InterfaceMenu.Accent};
 internal static string RemainingText=>Remaining is double s?RaidRules.Clock(s)+(s<=0&&PreventTimeout.Value?" · 超时保护中":""):"未进入战局 / 计时未同步";
 internal static string GameTime {get{try{return Active?Singleton<GameWorld>.Instance.GameDateTime?.Calculate().ToString("HH:mm:ss")??"未同步":"未进入战局";}catch{return "未同步";}}}
 internal static void SetRemaining(double seconds){var timer=Timer??throw new ArgumentException("请进入战局后修改时间。");double duration=RaidRules.Duration(timer.PastTime.TotalSeconds,seconds);var span=Il2CppSystem.TimeSpan.FromSeconds(duration);if(timer.StartDateTime.HasValue)timer.StartDateTime.Value.AddSeconds(duration);timer.ChangeSessionTime(span);if(Remaining is not double actual||Math.Abs(actual-seconds)>2)throw new InvalidOperationException("计时回读不一致，请重新检查。");bool synced=NativeRaidTimer.Changed(timer,duration);notice="剩余时间已设置为 "+RaidRules.Clock(seconds)+(synced?"，O 键面板已同步。":"；打开 O 键面板后更新。");Plugin.Self.Log.LogInfo($"Raid remaining seconds changed to {seconds}; duration={duration}; native UI synchronized={synced}");}
 static void Action(Action action){try{action();}catch(Exception e){notice=e.Message;Warn(e);}}
 internal static void DrawPanel(Rect rect){
  float gap=18,half=(rect.width-gap)/2;
  InterfaceMenu.Label(new Rect(rect.x,rect.y,rect.width,28),"当前战局",InterfaceMenu.Text,18);
  InterfaceMenu.Label(new Rect(rect.xMax-220,rect.y,220,26),"本机时间  "+DateTime.Now.ToString("HH:mm:ss"),Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.4f),12);
  var clockCard=new Rect(rect.x,rect.y+42,half,102);var remainingCard=new Rect(rect.x+half+gap,rect.y+42,half,102);
  foreach(var card in new[]{clockCard,remainingCard})InterfaceMenu.Surface(card,Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.045f),11);
  InterfaceMenu.Label(new Rect(clockCard.x+18,clockCard.y+12,half-36,25),"游戏时间",Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.35f),12);InterfaceMenu.Label(new Rect(clockCard.x+18,clockCard.y+44,half-36,42),GameTime,InterfaceMenu.Text,29);
  InterfaceMenu.Label(new Rect(remainingCard.x+18,remainingCard.y+12,half-36,25),"剩余时间",Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.35f),12);InterfaceMenu.Label(new Rect(remainingCard.x+18,remainingCard.y+44,half-36,42),Remaining is double seconds?RaidRules.Clock(seconds):"未同步",RemainingColor,29);
  float y=rect.y+164;var left=new Rect(rect.x,y,half,510);var right=new Rect(rect.x+half+gap,y,half,510);InterfaceMenu.Surface(left,Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.025f),11);InterfaceMenu.Surface(right,Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.025f),11);
  float x=left.x+16,w=left.width-32;InterfaceMenu.Label(new Rect(x,y+16,w,28),"时间控制",InterfaceMenu.Accent,17);
  if(InterfaceMenu.Button(new Rect(x,y+62,w,36),"超时不迷失  ·  "+(PreventTimeout.Value?"已开启":"已关闭"),PreventTimeout.Value))PreventTimeout.Value=!PreventTimeout.Value;
  InterfaceMenu.Paragraph(new Rect(x,y+112,w,62),Remaining is <=0&&PreventTimeout.Value?"超时保护中，可继续搜刮并正常撤离。":"到零后继续当前战局，正常撤离仍然有效。",null,12);
  InterfaceMenu.Label(new Rect(x,y+186,w-110,25),"设置剩余时间"+(useHms?"（时／分／秒）":"（分钟）"),null,13);if(InterfaceMenu.Button(new Rect(x+w-104,y+184,104,28),useHms?"切换分钟":"切换时分秒"))useHms=!useHms;
  if(useHms){float cell=(w-102)/3;InterfaceMenu.Label(new Rect(x,y+224,19,34),"时",null,12);hoursPart=InterfaceMenu.Field(new Rect(x+20,y+224,cell-25,34),hoursPart);InterfaceMenu.Label(new Rect(x+cell,y+224,19,34),"分",null,12);minutesPart=InterfaceMenu.Field(new Rect(x+cell+20,y+224,cell-25,34),minutesPart);InterfaceMenu.Label(new Rect(x+2*cell,y+224,19,34),"秒",null,12);secondsPart=InterfaceMenu.Field(new Rect(x+2*cell+20,y+224,cell-25,34),secondsPart);}else minutes=InterfaceMenu.Field(new Rect(x,y+224,Math.Max(80,w-102),34),minutes);if(InterfaceMenu.Button(new Rect(x+w-90,y+224,90,34),"应用"))Action(()=>SetRemaining(useHms?RaidRules.Hms(hoursPart,minutesPart,secondsPart):RaidRules.Minutes(minutes)));
  if(InterfaceMenu.Button(new Rect(x,y+278,(w-10)/2,35),"＋ 15 分钟"))Action(()=>SetRemaining(Math.Min(RaidRules.MaximumSeconds,Math.Max(0,Remaining??throw new ArgumentException("计时未同步"))+900)));
  if(InterfaceMenu.Button(new Rect(x+(w+10)/2,y+278,(w-10)/2,35),"＋ 60 分钟"))Action(()=>SetRemaining(Math.Min(RaidRules.MaximumSeconds,Math.Max(0,Remaining??throw new ArgumentException("计时未同步"))+3600)));
  InterfaceMenu.Paragraph(new Rect(x,y+330,w,72),notice.Length>0?notice:"设置当前战局的剩余时间。",null,12);
  InterfaceMenu.Label(new Rect(x,y+402,w,24),NativeRaidTimer.State,null,11);
  if(InterfaceMenu.Button(new Rect(x,y+438,w,32),$"调整时间窗大小 · {hudWidth.Value} × {hudHeight.Value}"))InterfaceMenu.BeginLayout();
  if(!scenarioHook||!stopHook)InterfaceMenu.Paragraph(new Rect(x,y+462,w,40),"时间保护暂不可用。",Color.yellow,12);
  RaidEnvironment.Draw(new Rect(right.x+16,right.y+16,right.width-32,480));
  RaidEnd.Draw(new Rect(rect.x,rect.y+696,rect.width,260));
 }
 internal static Rect DefaultBounds(){float width=Math.Min(hudWidth.Value,Math.Max(120,Screen.width-24)),height=Math.Min(hudHeight.Value,Math.Max(40,Screen.height-24));return new Rect(Math.Max(12,(Screen.width-width)/2),Math.Max(12,Screen.height-height-25),width,height);}
 internal static void Resize(float width,float height){var size=RaidRules.HudSize(width,height);hudWidth.Value=size.Width;hudHeight.Value=size.Height;}
 internal static void DrawHud(){
  if(!ShowClock.Value||!Active)return;
  var rect=InterfaceMenu.Place("raidclock",DefaultBounds());InterfaceMenu.Panel(rect);
  float row=(rect.height-10)/3;int font=Math.Clamp(Math.Min(hudFont.Value,(int)row-3),10,24);
  InterfaceMenu.Label(new Rect(rect.x+10,rect.y+4,rect.width-20,row),"现实  "+DateTime.Now.ToString("HH:mm:ss"),InterfaceMenu.Text,font);
  InterfaceMenu.Label(new Rect(rect.x+10,rect.y+4+row,rect.width-20,row),"游戏  "+GameTime,InterfaceMenu.Text,font);
  string remaining=Remaining is double s?RaidRules.Clock(s):"未同步";bool overtime=Remaining is <=0&&PreventTimeout.Value;
  InterfaceMenu.Label(new Rect(rect.x+10,rect.y+4+2*row,rect.width-20,row),"剩余  "+remaining+(overtime?"  保护中":""),RemainingColor,font);
 }
 static void Warn(Exception e){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+20;Plugin.Self.Error(e);}
}
