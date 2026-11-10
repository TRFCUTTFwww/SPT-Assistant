using System;
using BepInEx.Configuration;
using EFT;
using EFT.CameraControl;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class AiBuffHud {
 static ConfigEntry<bool> enabled=null!;static Player? target;static IntPtr world,health;
 static string profile="",name="";static string[] lines=Array.Empty<string>();
 static float nextRead,nextError;static int offset;static GUIStyle? label;
 internal static void Initialize()=>enabled=FeatureConfig.Bind("1. ESP","Crosshair AI buffs","准星 AI Buff 信息",true,"准星指向 AI 时在左侧容器详情区显示当前状态、强度、作用部位和剩余时间。PageUp／PageDown 翻页。");
 internal static bool Visible=>enabled.Value&&!InterfaceMenu.Open&&!Cursor.visible&&!DirectorStage.HudHidden&&RaidClock.Active&&target!=null&&target.HealthController?.IsAlive==true&&(target.ActiveHealthController?.Pointer??IntPtr.Zero)==health&&target.ProfileId==profile&&lines.Length>0;
 internal static void Reset(){target=null;world=health=IntPtr.Zero;profile=name="";lines=Array.Empty<string>();offset=0;nextRead=0;}
 internal static void Tick(GameWorld? current,Player? local){try{
  if(!enabled.Value||!RaidClock.Active||current==null||local?.HealthController?.IsAlive!=true||InterfaceMenu.Open||Cursor.visible||DirectorStage.HudHidden){Reset();return;}
  if(world!=current.Pointer){Reset();world=current.Pointer;}
  if(Time.unscaledTime<nextRead)return;nextRead=Time.unscaledTime+.1f;
  var camera=OverlayProjection.MainCamera;var optics=CameraManager.Instance?.OpticCameraManager;
  if(local.ProceduralWeaponAnimation?.IsAiming==true&&optics?.CurrentOpticSight!=null&&optics.Camera?.isActiveAndEnabled==true)camera=optics.Camera;
  if(camera==null){Reset();return;}
  var hit=CrosshairProbe.Read(current,local,camera,2000);var actor=hit.Actor;
  if(hit.State!=CrosshairState.Target||actor==null||actor.IsYourPlayer||!FikaTeams.IsAi(actor)||actor.HealthController?.IsAlive!=true){target=null;lines=Array.Empty<string>();offset=0;return;}
  var controller=actor.ActiveHealthController;
  if(target?.Pointer!=actor.Pointer||profile!=actor.ProfileId||health!=controller?.Pointer)offset=0;
  target=actor;profile=actor.ProfileId;health=controller?.Pointer??IntPtr.Zero;name=actor.Profile?.Nickname??profile;
  lines=CharacterBuffs.ReadAiHud(actor);
 }catch(Exception e){Reset();if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+20;Plugin.Self.Log.LogWarning("AI Buff HUD: "+e.Message);}}}
 internal static void Draw(GUIStyle source){
  if(!Visible)return;
  label??=new GUIStyle(source){alignment=TextAnchor.UpperLeft,wordWrap=true,richText=false};label.fontSize=source.fontSize;
  float width=Math.Min(580,Screen.width-24),x=12,y=Math.Max(110,Screen.height*.25f),header=source.fontSize+14;
  float available=Math.Max(header,Screen.height-y-header-35);var heights=new float[lines.Length];
  for(int i=0;i<lines.Length;i++)heights[i]=Math.Max(source.fontSize+6,label.CalcHeight(new GUIContent(lines[i]),width-16)+4);
  bool ctrl=Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl);
  offset=Math.Clamp(offset,0,lines.Length-1);
  int end=End(offset,heights,available);
  if(!ctrl&&Input.GetKeyDown(KeyCode.PageDown)&&end<lines.Length)offset=end;
  if(!ctrl&&Input.GetKeyDown(KeyCode.PageUp)){int previous=offset;while(previous>0&&Sum(heights,previous-1,offset)<=available)previous--;offset=previous==offset?Math.Max(0,offset-1):previous;}
  end=End(offset,heights,available);float height=header+Math.Min(available,Sum(heights,offset,end))+8;
  var old=GUI.color;try{
   var rect=new Rect(x,y,width,height);GUI.color=new Color(0,0,0,.8f);GUI.DrawTexture(rect,Texture2D.whiteTexture);
   GUI.color=new Color(.45f,.85f,1);GUI.DrawTexture(new Rect(x,y,3,height),Texture2D.whiteTexture);
   string title=I18n.Text("准星 AI 状态")+" · "+name+" · "+(offset+1)+"–"+end+"/"+lines.Length;
   GUI.Label(new Rect(x+8,y+4,width-16,header),title,label);GUI.color=Color.white;
   GUI.BeginGroup(new Rect(x+8,y+header,width-16,height-header-4));float top=0;
   try{for(int i=offset;i<end;i++){GUI.Label(new Rect(0,top,width-16,heights[i]),lines[i],label);top+=heights[i];}}finally{GUI.EndGroup();}
  }finally{GUI.color=old;}
 }
 static int End(int start,float[] heights,float available){int end=start;float total=0;while(end<heights.Length&&(end==start||total+heights[end]<=available)){total+=heights[end++];}return end;}
 static float Sum(float[] heights,int start,int end){float sum=0;for(int i=start;i<end;i++)sum+=heights[i];return sum;}
}
