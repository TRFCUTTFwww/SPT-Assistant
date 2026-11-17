using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Configuration;
using EFT.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class CornerVersionControl {
 static ConfigEntry<bool> enabled=null!;static ConfigEntry<string> text=null!;static PreloaderUI? preloader;static LocalizedText? label;static string original="",written="",draft="";static bool restoring;static float next;
 static readonly Dictionary<long,(TextMeshProUGUI Label,bool Rich)> formats=new();
 static ConfigEntry<bool> dynamic=null!;static ConfigEntry<string> script=null!;static ConfigEntry<int> interval=null!;
 static string scriptDraft="",intervalDraft="30",dynamicText="",scriptStatus="尚未运行";static Task<string>? work;static CancellationTokenSource? cancellation;static float nextScript;static GUIStyle? codeStyle;
 static bool changed;
 internal static void Initialize(Plugin plugin){
  CornerScriptDependencies.Install();
  dynamic=FeatureConfig.Bind("24. Interface","Dynamic corner version","动态左下角文字",false,"使用 JavaScript 返回显示文字，支持 httpGet 获取 API；在后台按间隔刷新。");
  script=FeatureConfig.Bind("24. Interface","Corner version script","左下角 JavaScript",CornerScript.Example,"使用 return 返回文字；httpGet(url) 返回响应正文，可用 JSON.parse 读取。");
  interval=FeatureConfig.Bind("24. Interface","Corner script interval","动态文字刷新间隔（秒）",30,"5～3600 秒；上次请求完成后才开始计时。");scriptDraft=script.Value;intervalDraft=interval.Value.ToString();
  enabled=FeatureConfig.Bind("24. Interface","Custom corner version","自定义左下角版本",false,"只修改游戏左下角显示文字，不改变真实客户端版本。");
  text=FeatureConfig.Bind("24. Interface","Corner version text","左下角版本文字","", "最多 160 个字符；留空或关闭开关时恢复原始显示。");draft=text.Value;
  plugin.Patches.Patch(AccessTools.Method(typeof(PreloaderUI),nameof(PreloaderUI.RefreshCornerLabel)),postfix:new HarmonyMethod(typeof(CornerVersionControl),nameof(Refreshed)));
 }
 static string Clean(string value){string result=new(value.Where(c=>!char.IsControl(c)).ToArray());if(result.Length>160)result=result[..(char.IsHighSurrogate(result[159])?159:160)];return result.Trim();}
 static void Refreshed(PreloaderUI __instance){if(restoring)return;preloader=__instance;var current=__instance._alphaVersionLabel;if(current==null)return;if(label?.Pointer!=current.Pointer){Restore();label=current;written="";}original=current.FormattedText;Apply();}
 static void Apply(){
  if(restoring||preloader==null)return;var current=preloader._alphaVersionLabel;if(current==null)return;
  if(label?.Pointer!=current.Pointer){Restore();label=current;original=current.FormattedText;}
  string value=enabled.Value?Clean(dynamic.Value&&dynamicText.Length>0?dynamicText:text.Value):"";
  if(value.Length==0){if(written.Length>0){Restore();restoring=true;try{preloader.RefreshCornerLabel();}finally{restoring=false;}original=current.FormattedText;}return;}
  if(current.FormattedText!=written&&written.Length>0)original=current.FormattedText;
  if(current._labels is {} labels)foreach(var child in labels){if(child==null)continue;long pointer=child.Pointer.ToInt64();if(!formats.ContainsKey(pointer))formats[pointer]=(child,child.richText);child.richText=false;}
  if(current.FormattedText!=value)current.SetLabelText(value);written=value;
 }
 static void Restore(){if(label!=null&&written.Length>0)label.SetLabelText(original);foreach(var entry in formats.Values)if(entry.Label!=null)entry.Label.richText=entry.Rich;formats.Clear();written="";}
 static void CancelScript(){cancellation?.Cancel();cancellation?.Dispose();cancellation=null;if(work is {} pending)_=pending.ContinueWith(t=>{_=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);work=null;}
 static void StartScript(){if(work!=null)return;cancellation=new();var token=cancellation.Token;string source=script.Value;work=Task.Run(()=>CornerScript.Evaluate(source,token),token);scriptStatus="正在后台执行…";}
 static void ScriptTick(){
  if(!enabled.Value||!dynamic.Value){if(work!=null)CancelScript();return;}
  if(work?.IsCompleted==true){var finished=work;work=null;cancellation?.Dispose();cancellation=null;nextScript=Time.unscaledTime+Math.Clamp(interval.Value,5,3600);try{dynamicText=Clean(finished.GetAwaiter().GetResult());scriptStatus="已更新 · "+DateTime.Now.ToString("HH:mm:ss");changed=true;}catch(Exception e){scriptStatus="执行失败，保留上次文字："+Clean(e.GetBaseException().Message);}}
  if(work==null&&Time.unscaledTime>=nextScript)StartScript();
 }
 internal static void Tick(){ScriptTick();if(!enabled.Value&&written.Length==0||!changed&&Time.unscaledTime<next)return;changed=false;next=Time.unscaledTime+.5f;try{preloader??=PreloaderUI.Instance??UnityEngine.Object.FindObjectOfType<PreloaderUI>();Apply();}catch(Exception error){next=Time.unscaledTime+10;Plugin.Self.Error(error);}}
 internal static void Draw(Rect r){
  InterfaceMenu.Label(new(r.x,r.y,r.width,28),"左下角版本文字",InterfaceMenu.Accent,17);
  if(InterfaceMenu.Button(new(r.x,r.y+34,Math.Min(340,r.width),32),I18n.Text("自定义左下角版本")+" · "+I18n.Text(enabled.Value?"开启":"关闭"),enabled.Value)){enabled.Value=!enabled.Value;Apply();}
  draft=InterfaceMenu.Field(new(r.x,r.y+78,Math.Max(100,r.width-204),32),draft);
  if(InterfaceMenu.Button(new(r.xMax-194,r.y+78,86,32),"应用")){text.Value=Clean(draft);draft=text.Value;enabled.Value=text.Value.Length>0;Apply();}
  if(InterfaceMenu.Button(new(r.xMax-98,r.y+78,98,32),"恢复原始")){enabled.Value=false;text.Value=draft="";CancelScript();dynamicText="";Apply();}
  if(InterfaceMenu.Button(new(r.x,r.y+120,r.width,32),"动态 JavaScript · "+(dynamic.Value?"开启":"关闭"),dynamic.Value)){dynamic.Value=!dynamic.Value;CancelScript();nextScript=0;dynamicText="";Apply();}
  if(!dynamic.Value){InterfaceMenu.Paragraph(new(r.x,r.y+164,r.width,44),"只修改左下角显示文字。开启动态模式后可输入 JavaScript、读取 API 并定时刷新。",null,12);return;}
  float y=r.y+164;InterfaceMenu.Paragraph(new(r.x,y,r.width,42),"使用 return 返回文字；httpGet(url) 获取 HTTP / HTTPS 正文，JSON.parse 解析数据。脚本和网络请求在后台运行。",null,12);y+=48;
  float height=Math.Clamp(r.yMax-y-150,90,220);codeStyle??=new GUIStyle(GUI.skin.textArea){fontSize=13,wordWrap=true,richText=false};
  scriptDraft=GUI.TextArea(new Rect(r.x,y,r.width,height),scriptDraft,16384,codeStyle);y+=height+10;
  InterfaceMenu.Label(new(r.x,y,132,30),"刷新间隔（秒）",null,12);intervalDraft=InterfaceMenu.Field(new(r.x+136,y,80,30),intervalDraft);
  if(InterfaceMenu.Button(new(r.x+228,y,Math.Max(90,r.width-228),30),"保存并立即刷新")){
   if(!int.TryParse(intervalDraft,out int seconds)||seconds is <5 or >3600)scriptStatus="刷新间隔须为 5～3600 秒。";
   else{CancelScript();script.Value=scriptDraft;interval.Value=seconds;enabled.Value=true;dynamic.Value=true;nextScript=0;dynamicText="";StartScript();}
  }y+=40;
  if(InterfaceMenu.Button(new(r.x,y,Math.Min(180,r.width),30),"填入时间示例"))scriptDraft=CornerScript.Example;
  InterfaceMenu.Paragraph(new(r.x,y+38,r.width,46),scriptStatus,null,12);
 }
 internal static object Diagnostics()=>new{enabled=enabled.Value,found=label!=null,applied=written.Length>0,matches=written.Length>0&&label?.FormattedText==written,length=written.Length};
 internal static void Shutdown(){CancelScript();CornerScriptDependencies.Shutdown();codeStyle=null;try{Restore();}finally{preloader=null;label=null;formats.Clear();}}
}
