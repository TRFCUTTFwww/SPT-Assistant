using System;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class FlightControl {
 static ConfigEntry<bool> enabled=null!;static ConfigEntry<KeyCode> toggle=null!;static ConfigEntry<float> speed=null!;
 static Player? owner;static IntPtr world;static float nextError;
 internal static bool Active=>enabled?.Value==true&&!NoclipControl.Active;
 internal static void Initialize(Plugin plugin){
  const string section="20. Movement";
  enabled=FeatureConfig.Bind(section,"Collision flight","飞行（保留碰撞）",false,"悬停并保留人物碰撞，WASD 水平移动，空格上升，左 Ctrl 下降，左 Shift 加速。关闭后恢复重力；开启穿墙时本功能暂停。");
  toggle=FeatureConfig.Bind(section,"Collision flight toggle key","飞行切换键",KeyCode.None,"默认不占用按键，可自行绑定。");
  speed=FeatureConfig.Bind(section,"Collision flight speed","飞行速度（米／秒）",6f,"飞行基础速度，Shift 为三倍。",new AcceptableValueRange<float>(.1f,30));
  plugin.Patches.Patch(AccessTools.Method(typeof(MovementContext),"ApplyGravity"),prefix:new HarmonyMethod(typeof(FlightControl),nameof(Gravity)));
 }
 static bool Owns(Player? player)=>player!=null&&enabled.Value&&!NoclipControl.Active&&RaidClock.Active&&player.IsYourPlayer&&player.HealthController?.IsAlive==true&&player.CharacterController?.detectCollisions==true&&Singleton<GameWorld>.Instance?.MainPlayer?.Pointer==player.Pointer;
 internal static void Tick(){
  try{
   var current=Singleton<GameWorld>.Instance;var player=current?.MainPlayer;
   if(RaidClock.Active&&player?.IsYourPlayer==true&&player.HealthController?.IsAlive==true&&!InterfaceMenu.InputBlocked&&!Cursor.visible&&Application.isFocused&&toggle.Value!=KeyCode.None&&Input.GetKeyDown(toggle.Value)){enabled.Value=!enabled.Value;FeatureToasts.Show("飞行",enabled.Value);}
   if(!Owns(player)){Reset();return;}
   if(owner!=null&&(owner.Pointer!=player!.Pointer||world!=current!.Pointer))Reset();
   owner=player;world=current!.Pointer;ClearFall(owner!);
  }catch(Exception error){Reset();Warn(error);}
 }
 static Vector3 Velocity(Player player){
  if(InterfaceMenu.InputBlocked||DirectorStage.FreeActive||Cursor.visible||!Application.isFocused)return Vector3.zero;
  float x=(Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0),z=(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0),y=(Input.GetKey(KeyCode.Space)?1:0)-(Input.GetKey(KeyCode.LeftControl)?1:0);
  var velocity=FlightRules.Velocity(x,z,y,player.Rotation.x,speed.Value,Input.GetKey(KeyCode.LeftShift));return new(velocity.X,velocity.Y,velocity.Z);
 }
 internal static bool FilterMotion(Player player,ref Vector3 motion,float delta){
  if(!Owns(player))return false;
  motion=Velocity(player)*FlightRules.Step(delta);ClearFall(player);return true;
 }
 static bool Gravity(MovementContext __instance,ref Vector3 __0){
  try{if(!Owns(__instance._player))return true;__0.y=0;ClearFall(__instance._player);return false;}catch(Exception error){Warn(error);return true;}
 }
 static void ClearFall(Player player){var movement=player.MovementContext;if(movement==null)return;movement.FreefallTime=0;movement._FallHeight_k__BackingField=0;movement._startFallingHeight=player.Position.y;movement.ResetFlying();}
 internal static void Reset(){if(owner!=null)try{ClearFall(owner);}catch{}owner=null;world=IntPtr.Zero;}
 static void Warn(Exception error){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+15;Plugin.Self.Log.LogWarning("Collision flight: "+error);}
}
