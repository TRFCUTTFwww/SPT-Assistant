using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.CameraControl;
using EFT.Communications;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class DirectorStage {
 static ConfigEntry<bool> free=null!,hud=null!,modNotices=null!,gameNotices=null!;
 static ConfigEntry<KeyCode> freeKey=null!,hudKey=null!;
 static ConfigEntry<KeyCode> noticesKey=null!;
 static ConfigEntry<float> speed=null!,sensitivity=null!,fov=null!;
 static readonly Dictionary<IntPtr,(Canvas Canvas,bool Enabled)> canvases=new();
 static Camera? camera;static Vector3 savedPosition,savedLocalPosition;static Quaternion savedLocalRotation;static float savedFov;
 static Vector3 position;static Vector2 angles;static IntPtr world;static float nextCanvas,nextError;static Vector2 scroll;
 internal static bool FreeActive=>free!=null&&free.Value&&RaidClock.Active&&world!=IntPtr.Zero;
 internal static bool HudHidden=>hud!=null&&hud.Value&&RaidClock.Active&&!InterfaceMenu.Open;
 internal static bool ModNotifications=>modNotices==null||modNotices.Value;
 static bool AllowNotification()=>gameNotices==null||gameNotices.Value;
 internal static void Initialize(Plugin plugin){
  const string section="48. 导演与画面";
  free=FeatureConfig.Bind(section,"Free camera","自由视角",false,"镜头独立移动，玩家留在原地；WASD 移动，空格／Ctrl 升降，Shift 加速，Esc 退出。与第三人称共用原游戏镜头及渲染效果。");
  freeKey=FeatureConfig.Bind(section,"Free camera key","自由视角切换键",KeyCode.None);
  speed=FeatureConfig.Bind(section,"Free camera speed","自由镜头速度",5f,"米／秒",new AcceptableValueRange<float>(.1f,100));
  sensitivity=FeatureConfig.Bind(section,"Free camera sensitivity","自由镜头鼠标灵敏度",2f,"",new AcceptableValueRange<float>(.05f,10));
  fov=FeatureConfig.Bind(section,"Free camera FOV","自由镜头视野角",60f,"",new AcceptableValueRange<float>(10,120));
  hud=FeatureConfig.Bind(section,"Hide all HUD","隐藏所有 HUD（游戏与模组）",false,"拍摄时隐藏游戏 Canvas 和本模组叠加信息；打开本模组菜单可恢复操作，关菜单后继续隐藏。");
  hudKey=FeatureConfig.Bind(section,"Hide HUD key","隐藏 HUD 切换键",KeyCode.None);
  modNotices=FeatureConfig.Bind(section,"Mod notifications","显示模组通知",true);
  gameNotices=FeatureConfig.Bind(section,"Game notifications","显示游戏弹出通知",true,"仅关闭通知的画面提示，游戏消息、任务、邮件和奖励处理继续运行。");
  noticesKey=FeatureConfig.Bind(section,"Notifications toggle key","游戏与模组通知切换键",KeyCode.None);
  plugin.Patches.Patch(typeof(NotificationManager).GetMethod("DisplayNotification",new[]{typeof(Notification)}),prefix:new HarmonyMethod(typeof(DirectorStage),nameof(AllowNotification)));
 }
 static bool Pressed(ConfigEntry<KeyCode> key)=>key.Value!=KeyCode.None&&Input.GetKeyDown(key.Value);
 internal static void Update(){
  var current=Singleton<GameWorld>.Instance;var local=current?.MainPlayer;
  if(!RaidClock.Active||current==null||local?.HealthController?.IsAlive!=true){Reset();return;}
  if(world!=IntPtr.Zero&&world!=current.Pointer)Reset();world=current.Pointer;
  if(!InterfaceMenu.InputBlocked){if(Pressed(freeKey))free.Value=!free.Value;if(Pressed(hudKey))hud.Value=!hud.Value;if(Pressed(noticesKey)){bool show=!modNotices.Value;modNotices.Value=gameNotices.Value=show;}if(free.Value&&Input.GetKeyDown(KeyCode.Escape))free.Value=false;}
  if(!free.Value){RestoreCamera();return;}
  var main=CameraManager.Instance?.Camera??Camera.main;if(main==null)return;
  if(camera==null){camera=main;savedLocalPosition=main.transform.localPosition;savedLocalRotation=main.transform.localRotation;savedFov=main.fieldOfView;savedPosition=local.Position;position=main.transform.position;angles=new(main.transform.eulerAngles.y,Mathf.DeltaAngle(0,main.transform.eulerAngles.x));local.Move(Vector2.zero);}
  local.Move(Vector2.zero);
  if(InterfaceMenu.InputBlocked||Cursor.visible)return;
  angles.x+=Input.GetAxisRaw("Mouse X")*sensitivity.Value;angles.y=Math.Clamp(angles.y-Input.GetAxisRaw("Mouse Y")*sensitivity.Value,-89.9f,89.9f);
  var rotation=Quaternion.Euler(angles.y,angles.x,0);var move=Vector3.zero;
  if(Input.GetKey(KeyCode.W))move+=rotation*Vector3.forward;if(Input.GetKey(KeyCode.S))move-=rotation*Vector3.forward;if(Input.GetKey(KeyCode.D))move+=rotation*Vector3.right;if(Input.GetKey(KeyCode.A))move-=rotation*Vector3.right;if(Input.GetKey(KeyCode.Space))move+=Vector3.up;if(Input.GetKey(KeyCode.LeftControl))move-=Vector3.up;
  if(move.sqrMagnitude>0)position+=move.normalized*speed.Value*(Input.GetKey(KeyCode.LeftShift)?3:1)*Math.Min(Time.unscaledDeltaTime,.05f);
 }
 internal static void PositionCamera(Camera target){if(!FreeActive||camera==null||target.Pointer!=camera.Pointer)return;target.transform.SetPositionAndRotation(position,Quaternion.Euler(angles.y,angles.x,0));target.fieldOfView=fov.Value;}
 static void RestoreCamera(){if(camera==null)return;try{camera.transform.localPosition=savedLocalPosition;camera.transform.localRotation=savedLocalRotation;camera.fieldOfView=savedFov;}catch{}camera=null;}
 internal static void Late(){
  if(!HudHidden){RestoreHud();return;}
  try{if(Time.unscaledTime>=nextCanvas){nextCanvas=Time.unscaledTime+.5f;foreach(var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true))if(canvas!=null&&!canvases.ContainsKey(canvas.Pointer))canvases[canvas.Pointer]=(canvas,canvas.enabled);}foreach(var entry in canvases.Values)if(entry.Canvas!=null)entry.Canvas.enabled=false;}
  catch(Exception e){if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+15;Plugin.Self.Log.LogWarning("Director HUD: "+e.Message);}}
 }
 static void RestoreHud(){foreach(var entry in canvases.Values)try{if(entry.Canvas!=null)entry.Canvas.enabled=entry.Enabled;}catch{}canvases.Clear();nextCanvas=0;}
 internal static void Reset(){RestoreCamera();RestoreHud();world=IntPtr.Zero;}
 internal static void Draw(Rect rect){scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-20,820));float x=6,y=8,w=rect.width-34;InterfaceMenu.Label(new Rect(x,y,w,32),"镜头与画面",InterfaceMenu.Accent,21);y+=48;foreach(var entry in new ConfigEntryBase[]{free,freeKey,speed,sensitivity,fov,hud,hudKey,modNotices,gameNotices}){InterfaceMenu.DrawEntry(entry,new Rect(x,y,w,78));y+=84;}GUI.EndScrollView();}
}
