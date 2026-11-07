using System;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class NoclipControl {
 static ConfigEntry<KeyCode> toggle=null!;static ConfigEntry<bool> enabled=null!;static ConfigEntry<float> speed=null!;static Player? owner;static IntPtr world;
 internal static void Initialize(Plugin plugin){toggle=FeatureConfig.Bind("20. Movement","Noclip toggle key","穿墙开关热键",KeyCode.None,"在战局中切换穿墙；支持键盘及鼠标侧键，默认不占用按键。");enabled=FeatureConfig.Bind("20. Movement","Noclip","Noclip 穿墙飞行",false,"WASD 移动，空格上升，左 Ctrl 下降，左 Shift 加速。滚轮调速；关闭后停留在当前位置。");speed=FeatureConfig.Bind("20. Movement","Noclip speed","穿墙移动速度",8f,"米／秒",new AcceptableValueRange<float>(.1f,100));foreach(var type in new[]{typeof(SimpleCharacterController),typeof(PlayerCharacterController),typeof(ImpostorCharacterController),typeof(PlayerSpiritCharacterController)})plugin.Patches.Patch(type.GetMethod("Move",new[]{typeof(Vector3),typeof(float)}),prefix:new HarmonyMethod(typeof(NoclipControl),nameof(AllowMove)));}
 static bool AllowMove(Il2CppObjectBase __instance)=>owner==null||!enabled.Value||owner.CharacterController?.Pointer!=__instance.Pointer;
 internal static void Tick(){var current=Singleton<GameWorld>.Instance;var player=current?.MainPlayer;if(!RaidClock.Active||player?.HealthController?.IsAlive!=true){owner=null;world=IntPtr.Zero;return;}if(owner!=null&&(current!.Pointer!=world||owner.Pointer!=player.Pointer)){owner=null;world=IntPtr.Zero;}if(!InterfaceMenu.InputBlocked&&!Cursor.visible&&toggle.Value!=KeyCode.None&&Input.GetKeyDown(toggle.Value)){enabled.Value=!enabled.Value;FeatureToasts.Show("穿墙",enabled.Value);}if(!enabled.Value){if(owner!=null){owner=null;world=IntPtr.Zero;FeatureToasts.Show("已关闭穿墙，停留在当前位置");}return;}if(owner==null){owner=player;world=current!.Pointer;}if(InterfaceMenu.InputBlocked||DirectorStage.FreeActive||Cursor.visible)return;float wheel=Input.mouseScrollDelta.y;if(Math.Abs(wheel)>.001f){double chosen=speed.Value*Math.Pow(1.15,wheel);speed.Value=(float)Math.Clamp(chosen,.1,float.MaxValue);InterfaceMenu.Save();FeatureToasts.Show("穿墙速度 · "+speed.Value.ToString("G5")+" 米／秒");}var camera=Camera.main;if(camera==null)return;Vector3 motion=Vector3.zero;if(Input.GetKey(KeyCode.W))motion+=camera.transform.forward;if(Input.GetKey(KeyCode.S))motion-=camera.transform.forward;if(Input.GetKey(KeyCode.D))motion+=camera.transform.right;if(Input.GetKey(KeyCode.A))motion-=camera.transform.right;if(Input.GetKey(KeyCode.Space))motion+=Vector3.up;if(Input.GetKey(KeyCode.LeftControl))motion-=Vector3.up;if(motion.sqrMagnitude>0)player.Teleport(player.Position+motion.normalized*speed.Value*(Input.GetKey(KeyCode.LeftShift)?3:1)*Math.Min(Time.unscaledDeltaTime,.05f),true);}
 internal static void Draw(){if(owner==null||!enabled.Value)return;using var windowOpacity=InterfaceMenu.BeginWindow("noclip");var bounds=InterfaceMenu.Place("noclip",new Rect(Screen.width*.5f-130,Screen.height-110,260,32));InterfaceMenu.Label(bounds,"Noclip · "+speed.Value.ToString("G5")+" 米／秒 · 滚轮调速",InterfaceMenu.Accent,14);}
 internal static void Reset(){owner=null;world=IntPtr.Zero;}
}
