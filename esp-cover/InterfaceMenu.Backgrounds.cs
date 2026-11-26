using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace LocalSpt5Esp;

internal static partial class InterfaceMenu {
 static ConfigEntry<bool> animeBackgrounds=null!;
 static string backgroundsDirectory="";
 sealed record Artwork(Texture2D Texture,bool CenterHorizontally);
 static readonly Dictionary<string,Artwork?> backgrounds=new(StringComparer.Ordinal);
 static readonly Dictionary<string,string> backgroundFiles=new(StringComparer.Ordinal){
  ["主页面"]="战局.png",
  ["战局与物资"]="人物.png",
  ["AI 工作台"]="AI管理.png",
  ["枪械编辑"]="枪械编辑.png",
  ["数据"]="数据.png",
  ["剧情"]="剧情.png",
  ["服务端"]="服务端2.png",
  ["杂项"]="战局2.png",
  ["镜头与画面"]="视觉2.png",
  ["战斗与枪械"]="射击.png",
  ["传送与导航"]="导航.png",
  ["载具与空投"]="载具.png",
  ["人物与移动"]="战局4.png",
  ["物品与装备"]="物品.png",
  ["趣味功能"]="趣味功能.png",
  ["内存编辑"]="内存编辑.png",
  [MenuSections.FleaMarket]="跳蚤市场.png",
  ["开发工具"]="开发工具.png"
 };
 static readonly int backgroundTabCount=backgroundFiles.Keys.Max(name=>MenuSections.Pages(name).Length);
 const float BackgroundTransitionSeconds=.24f;
 static string? artworkCategory;
 static Artwork? currentArtwork,outgoingArtwork;
 static float artworkStarted,outgoingOpacity,outgoingShift;

 static void InitializeBackgrounds(string pluginDirectory){
  animeBackgrounds=Bind("Show anime backgrounds","显示吉祥物背景",false);
  backgroundsDirectory=Path.Combine(pluginDirectory,"runtime","menu-backgrounds");
 }

 static bool DrawCategoryBackground(Rect content){
  if(Event.current.type!=EventType.Repaint)return false;
  if(!animeBackgrounds.Value){ResetBackgroundTransition();return false;}
  // Reserve shared tab space so artwork size does not change with the active page.
  content.yMin+=MenuWorkspaceLayout.Height(backgroundTabCount,content.width)+10;
  if(content.width<=0||content.height<=0)return false;

  float now=Time.unscaledTime;
  float progress=BackgroundProgress(now);
  if(artworkCategory!=category){
   outgoingArtwork=currentArtwork;
   outgoingOpacity=progress;
   outgoingShift=1-progress;
   currentArtwork=backgroundFiles.TryGetValue(category,out var name)?LoadBackground(name):null;
   artworkStarted=artworkCategory!=null?Time.unscaledTime:float.NegativeInfinity;
   artworkCategory=category;
   progress=BackgroundProgress(Time.unscaledTime);
  }
  if(progress>=1)outgoingArtwork=null;
  if(outgoingArtwork==null&&currentArtwork==null)return false;

  float distance=Math.Min(54,content.width*.08f);
  var previousColor=GUI.color;
  // This layer runs only on Repaint. Clipping must not allocate a GUI control ID.
  GUI.BeginClip(content);
  try{
   DrawArtwork(outgoingArtwork,content.width,content.height,distance*Math.Min(1,outgoingShift+progress),outgoingOpacity*(1-progress));
   DrawArtwork(currentArtwork,content.width,content.height,distance*(1-progress),progress);
  }finally{GUI.color=previousColor;GUI.EndClip();}
  return true;
 }

 static float BackgroundProgress(float now){
  float t=Math.Clamp((now-artworkStarted)/BackgroundTransitionSeconds,0,1);
  return t*t*(3-2*t);
 }

 static void DrawArtwork(Artwork? artwork,float width,float height,float shift,float opacity){
  if(artwork?.Texture is not {} texture||texture==null||opacity<=0)return;
  var fit=MenuWorkspaceLayout.FitBackground(width,height,texture.width,texture.height,artwork.CenterHorizontally);
  GUI.color=new Color(1,1,1,.62f*opacity);
  GUI.DrawTexture(new Rect(fit.X+shift,fit.Y,fit.Width,fit.Height),texture,ScaleMode.StretchToFill,true);
 }

 static void ResetBackgroundTransition(){
  artworkCategory=null;
  currentArtwork=outgoingArtwork=null;
 }

 static Artwork? LoadBackground(string name){
  if(backgrounds.TryGetValue(name,out var cached))return cached;
  string path=Path.Combine(backgroundsDirectory,name);
  Texture2D? texture=null;
  try{
   byte[] data;
   if(File.Exists(path))data=File.ReadAllBytes(path);
   else if(name is "剧情.png" or "服务端2.png"){
    string resource=name=="剧情.png"?"LocalSpt5Esp.Assets.Story.png":"LocalSpt5Esp.Assets.Server.png";
    using var stream=typeof(InterfaceMenu).Assembly.GetManifestResourceStream(resource)
     ??throw new InvalidDataException("Missing embedded menu artwork: "+name);
    using var buffer=new MemoryStream();
    stream.CopyTo(buffer);
    data=buffer.ToArray();
   }else return backgrounds[name]=null;
   texture=new Texture2D(2,2,TextureFormat.RGBA32,false){
    hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp
   };
   var bytes=new Il2CppStructArray<byte>(data);
   if(!ImageConversion.LoadImage(texture,bytes,true))throw new InvalidDataException("Invalid PNG data");
   return backgrounds[name]=new(texture,name=="战局4.png");
  }catch(Exception error){
   if(texture!=null)UnityEngine.Object.Destroy(texture);
   Plugin.Self.Log.LogWarning("Menu background "+name+": "+error.Message);
   return backgrounds[name]=null;
  }
 }

 static void ClearBackgrounds(){
  ResetBackgroundTransition();
  foreach(var artwork in backgrounds.Values)if(artwork?.Texture is {} texture&&texture!=null)UnityEngine.Object.Destroy(texture);
  backgrounds.Clear();
 }
}
