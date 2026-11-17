using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using EFT.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class CornerVersionControl {
 static ConfigEntry<bool> enabled=null!;static ConfigEntry<string> text=null!;static PreloaderUI? preloader;static LocalizedText? label;static string original="",written="",draft="";static bool restoring;static float next;
 static readonly Dictionary<long,(TextMeshProUGUI Label,bool Rich)> formats=new();
 internal static void Initialize(Plugin plugin){
  enabled=FeatureConfig.Bind("24. Interface","Custom corner version","自定义左下角版本",false,"只修改游戏左下角显示文字，不改变真实客户端版本。");
  text=FeatureConfig.Bind("24. Interface","Corner version text","左下角版本文字","", "最多 160 个字符；留空或关闭开关时恢复原始显示。");draft=text.Value;
  plugin.Patches.Patch(AccessTools.Method(typeof(PreloaderUI),nameof(PreloaderUI.RefreshCornerLabel)),postfix:new HarmonyMethod(typeof(CornerVersionControl),nameof(Refreshed)));
 }
 static string Clean(string value){string result=new(value.Where(c=>!char.IsControl(c)).ToArray());if(result.Length>160)result=result[..(char.IsHighSurrogate(result[159])?159:160)];return result.Trim();}
 static void Refreshed(PreloaderUI __instance){if(restoring)return;preloader=__instance;var current=__instance._alphaVersionLabel;if(current==null)return;if(label?.Pointer!=current.Pointer){Restore();label=current;written="";}original=current.FormattedText;Apply();}
 static void Apply(){
  if(restoring||preloader==null)return;var current=preloader._alphaVersionLabel;if(current==null)return;
  if(label?.Pointer!=current.Pointer){Restore();label=current;original=current.FormattedText;}
  string value=enabled.Value?Clean(text.Value):"";
  if(value.Length==0){if(written.Length>0){Restore();restoring=true;try{preloader.RefreshCornerLabel();}finally{restoring=false;}original=current.FormattedText;}return;}
  if(current.FormattedText!=written&&written.Length>0)original=current.FormattedText;
  if(current._labels is {} labels)foreach(var child in labels){if(child==null)continue;long pointer=child.Pointer.ToInt64();if(!formats.ContainsKey(pointer))formats[pointer]=(child,child.richText);child.richText=false;}
  if(current.FormattedText!=value)current.SetLabelText(value);written=value;
 }
 static void Restore(){if(label!=null&&written.Length>0)label.SetLabelText(original);foreach(var entry in formats.Values)if(entry.Label!=null)entry.Label.richText=entry.Rich;formats.Clear();written="";}
 internal static void Tick(){if(!enabled.Value&&written.Length==0||Time.unscaledTime<next)return;next=Time.unscaledTime+.5f;try{preloader??=PreloaderUI.Instance??UnityEngine.Object.FindObjectOfType<PreloaderUI>();Apply();}catch(Exception error){next=Time.unscaledTime+10;Plugin.Self.Error(error);}}
 internal static void Draw(Rect r){
  InterfaceMenu.Label(new(r.x,r.y,r.width,28),"左下角版本文字",InterfaceMenu.Accent,17);
  if(InterfaceMenu.Button(new(r.x,r.y+34,Math.Min(340,r.width),32),I18n.Text("自定义左下角版本")+" · "+I18n.Text(enabled.Value?"开启":"关闭"),enabled.Value)){enabled.Value=!enabled.Value;Apply();}
  draft=InterfaceMenu.Field(new(r.x,r.y+78,Math.Max(100,r.width-204),32),draft);
  if(InterfaceMenu.Button(new(r.xMax-194,r.y+78,86,32),"应用")){text.Value=Clean(draft);draft=text.Value;enabled.Value=text.Value.Length>0;Apply();}
  if(InterfaceMenu.Button(new(r.xMax-98,r.y+78,98,32),"恢复原始")){enabled.Value=false;text.Value=draft="";Apply();}
  InterfaceMenu.Paragraph(new(r.x,r.y+120,r.width,40),"只修改游戏左下角显示文字，不改变真实客户端版本。",null,12);
 }
 internal static object Diagnostics()=>new{enabled=enabled.Value,found=label!=null,applied=written.Length>0,matches=written.Length>0&&label?.FormattedText==written,length=written.Length};
 internal static void Shutdown(){try{Restore();}finally{preloader=null;label=null;formats.Clear();}}
}
