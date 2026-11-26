using System;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class RaidStartClock {
 const string Marker="LocalSpt5Esp.RaidStartClock.Owner";
 static IntPtr lastWorld,lastGame,lastDate;static ConfigEntry<bool> enabled=null!;static ConfigEntry<string> clock=null!;static string h="6",m="0",s="0",notice="";
 internal static bool SessionOverride{get;private set;}
 internal static bool HasFixedTime=>enabled.Value;
 internal static void Initialize(){enabled=FeatureConfig.Bind("31. 环境控制","Next raid clock enabled","指定下次行动的游戏时刻",false,"设置地图内的昼夜时刻。");clock=FeatureConfig.Bind("31. 环境控制","Next raid clock","下次行动游戏时刻","06:00:00");var parts=clock.Value.Split(':');if(parts.Length==3){h=parts[0];m=parts[1];s=parts[2];}}
 internal static int FixedSeconds(){
  var parts=clock.Value.Split(':');
  if(parts.Length!=3)throw new ArgumentException("保存的游戏时刻无效。");
  int seconds=(int)TrainTimeRules.Parse(parts[0],parts[1],parts[2]);
  if(seconds>=86400)throw new ArgumentException("游戏时刻不能超过一天。");
  return seconds;
 }
 internal static void Save(string value,bool on=true){var parts=value.Split(':');if(parts.Length!=3)throw new ArgumentException("时刻格式为 HH:MM:SS。");int seconds=(int)TrainTimeRules.Parse(parts[0],parts[1],parts[2]);if(seconds>=86400)throw new ArgumentException("游戏时刻范围为 00:00:00～23:59:59。");clock.Value=TimeSpan.FromSeconds(seconds).ToString("hh\\:mm\\:ss");enabled.Value=on;RaidSelectionClock.ResetSelection();notice="下次行动的地图时刻已保存："+clock.Value;}
 internal static void Tick(){
  if(!RaidClock.Active){SessionOverride=false;lastWorld=lastGame=lastDate=IntPtr.Zero;AppDomain.CurrentDomain.SetData(Marker,null);return;}
  var world=Singleton<GameWorld>.Instance;var date=world?.GameDateTime;var game=Singleton<AbstractGame>.Instance;if(world==null||date==null||game==null)return;
  if(world.Pointer==lastWorld&&game.Pointer==lastGame&&date.Pointer==lastDate)return;
  bool selected=RaidSelectionClock.TryTakeStart(game,out long ticks,out bool waiting);
  if(waiting)return;
  lastWorld=world.Pointer;lastGame=game.Pointer;lastDate=date.Pointer;
  string owner=Environment.ProcessId+":"+world.Pointer+":"+game.Pointer+":"+date.Pointer;
  if(AppDomain.CurrentDomain.GetData(Marker) as string==owner){SessionOverride=AppDomain.CurrentDomain.GetData(Marker+".Sync") is true;return;}
  if(selected||HasFixedTime){date.ResetForce(date.Calculate().Date.AddTicks(selected?ticks:FixedSeconds()*TimeSpan.TicksPerSecond));SessionOverride=true;}
  AppDomain.CurrentDomain.SetData(Marker,owner);AppDomain.CurrentDomain.SetData(Marker+".Sync",SessionOverride);
 }
 internal static void Disable(){enabled.Value=false;RaidSelectionClock.ResetSelection();}
 internal static void AllowSystemSync(){SessionOverride=false;AppDomain.CurrentDomain.SetData(Marker+".Sync",false);}
 internal static void Draw(Rect r){InterfaceMenu.Label(new(r.x,r.y,r.width,28),"行动前设置地图游戏时刻",InterfaceMenu.Accent,17);InterfaceMenu.Paragraph(new(r.x,r.y+34,r.width,38),"控制下一次行动的昼夜时刻。",null,12);float cell=Math.Min(110,(r.width-160)/3);string Field(float x,string value,string label){InterfaceMenu.Label(new(x,r.y+80,22,32),label,null,12);return InterfaceMenu.Field(new(x+24,r.y+80,cell-28,34),value);}h=Field(r.x,h,"时");m=Field(r.x+cell,m,"分");s=Field(r.x+cell*2,s,"秒");if(InterfaceMenu.Button(new(r.x+cell*3+8,r.y+80,140,34),"保存下次游戏时刻"))try{Save(h+":"+m+":"+s);}catch(Exception e){notice=e.Message;}if(InterfaceMenu.Button(new(r.x,r.y+126,140,30),enabled.Value?"已启用 · 停用":"已停用 · 启用",enabled.Value)){if(enabled.Value)Disable();else{enabled.Value=true;RaidSelectionClock.ResetSelection();}}InterfaceMenu.Label(new(r.x+154,r.y+128,r.width-154,30),notice,null,12);}
}
