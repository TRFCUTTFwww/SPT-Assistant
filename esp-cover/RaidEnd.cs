using System;
using Comfort.Common;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class RaidEnd {
 static RaidEndMode mode;static bool dropdown;
 static RaidEndRequest? request;
 static string notice="";
 static IntPtr endingWorld;
 internal static bool ManualEnding=>endingWorld!=IntPtr.Zero&&Singleton<GameWorld>.Instance?.Pointer==endingWorld;
 internal static void Tick(){if(!RaidClock.Active){endingWorld=IntPtr.Zero;request=null;dropdown=false;}}
 internal static void CancelConfirmation(){request=null;dropdown=false;}
 static void Arm(){try{var game=Singleton<AbstractGame>.Instance;var world=Singleton<GameWorld>.Instance;if(!RaidClock.Active||game==null||world?.MainPlayer?.HealthController?.IsAlive!=true)throw new ArgumentException("需要进入战局且角色存活。");if(game.TryCast<LocalGame>()==null)throw new ArgumentException("当前战局类型不支持本地结算。");request=new(world.Pointer.ToInt64(),game.Pointer.ToInt64(),world.MainPlayer.ProfileId,mode);notice="";}catch(Exception e){notice=e.Message;}}
 static void Confirm(){try{var pending=request??throw new ArgumentException("请先选择结算状态并点击结束本局。");var game=Singleton<AbstractGame>.Instance;var world=Singleton<GameWorld>.Instance;if(!RaidClock.Active||game==null||world?.MainPlayer?.HealthController?.IsAlive!=true||!pending.Matches(world.Pointer.ToInt64(),game.Pointer.ToInt64(),world.MainPlayer.ProfileId))throw new ArgumentException("当前战局已改变，请重新选择。");var local=game.TryCast<LocalGame>()??throw new ArgumentException("当前战局类型不支持本地结算。");var result=Enum.Parse<ExitStatus>(RaidEndRules.StatusName(pending.Mode));endingWorld=world.Pointer;request=null;dropdown=false;InterfaceMenu.Close();Plugin.Self.Log.LogInfo("Manual raid end requested: "+result);local.Stop(world.MainPlayer.ProfileId,result,"控制中心 · "+RaidEndRules.Name(pending.Mode),0);notice="正在按"+RaidEndRules.Name(pending.Mode)+"结算…";}catch(Exception e){endingWorld=IntPtr.Zero;request=null;notice="未结束战局："+e.Message;Plugin.Self.Error(e);}}
 internal static void Draw(Rect rect){
  InterfaceMenu.Surface(rect,Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.035f),11);float x=rect.x+18,w=rect.width-36;
  InterfaceMenu.Label(new Rect(x,rect.y+12,w,28),"结束当前战局",InterfaceMenu.Accent,17);
  InterfaceMenu.Label(new Rect(x,rect.y+53,76,32),"结算状态",null,13);
  if(InterfaceMenu.Button(new Rect(x+88,rect.y+53,196,32),RaidEndRules.Name(mode)+"   ▾",dropdown)){dropdown=!dropdown;request=null;}
  if(InterfaceMenu.Button(new Rect(x+300,rect.y+53,150,32),ManualEnding?"正在结束…":"结束本局")&&!ManualEnding){dropdown=false;Arm();}
  float y=rect.y+99;
  if(dropdown){int index=0;foreach(var option in Enum.GetValues<RaidEndMode>()){if(InterfaceMenu.Button(new Rect(x+88,y+index++*34,196,31),RaidEndRules.Name(option),mode==option)){mode=option;dropdown=false;request=null;notice="";}}}
  else if(request!=null){InterfaceMenu.Paragraph(new Rect(x,y,w,42),"确认以「"+RaidEndRules.Name(request.Mode)+"」结束本局？"+RaidEndRules.Consequence(request.Mode),new Color(1,.8f,.4f),13);if(InterfaceMenu.Button(new Rect(x,y+54,196,33),"确认结束 · "+RaidEndRules.Name(request.Mode)))Confirm();if(InterfaceMenu.Button(new Rect(x+208,y+54,108,33),"取消"))request=null;}
  else InterfaceMenu.Paragraph(new Rect(x,y,w,70),notice.Length>0?notice:RaidEndRules.Consequence(mode)+"\n选择状态不会立即结束，需点击结束并确认。",null,13);
 }
}
