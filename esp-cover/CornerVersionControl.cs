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
 static ConfigEntry<bool> dynamic=null!;static ConfigEntry<string> script=null!;static ConfigEntry<double> interval=null!;static ConfigEntry<CornerScriptTimeUnit> intervalUnit=null!;
 static string scriptDraft="",intervalDraft="30",dynamicText="",scriptStatus="尚未运行";static Task<string>? work;static CancellationTokenSource? cancellation;static double nextScript;static GUIStyle? codeStyle;static bool unitDropdown;
 static readonly string[] unitNames={"时","分","秒","毫秒"};
 static bool changed;static Vector2 scriptScroll,codeScroll;
 static string measuredCode="";static float measuredWidth=-1,measuredHeight;
 // Retain the existing seconds-based config key so saved intervals keep their meaning.
 static double IntervalSeconds=>CornerScriptInterval.Normalize(interval.Value);
 internal static void Initialize(Plugin plugin){
  CornerScriptDependencies.Install();
  dynamic=FeatureConfig.Bind("24. Interface","Dynamic corner version","动态左下角文字",false);
  script=FeatureConfig.Bind("24. Interface","Corner version script","左下角 JavaScript",CornerScript.Example);
  interval=FeatureConfig.Bind("24. Interface","Corner script interval","动态文字刷新间隔（秒）",30d,"0.001～3600 秒。");
  intervalUnit=FeatureConfig.Bind("24. Interface","Corner script display unit","刷新间隔显示单位",CornerScriptTimeUnit.Seconds);
  if(!Enum.IsDefined(intervalUnit.Value))intervalUnit.Value=CornerScriptTimeUnit.Seconds;
  scriptDraft=script.Value;intervalDraft=CornerScriptInterval.Format(IntervalSeconds,intervalUnit.Value);
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
  if(work?.IsCompleted==true){var finished=work;work=null;cancellation?.Dispose();cancellation=null;nextScript=Time.realtimeSinceStartupAsDouble+IntervalSeconds;try{dynamicText=Clean(finished.GetAwaiter().GetResult());scriptStatus="已更新 · "+DateTime.Now.ToString("HH:mm:ss.fff");changed=true;}catch(Exception e){scriptStatus="执行失败，保留上次文字："+Clean(e.GetBaseException().Message);}}
  if(work==null&&Time.realtimeSinceStartupAsDouble>=nextScript)StartScript();
 }
 internal static void Tick(){ScriptTick();if(!enabled.Value&&written.Length==0||!changed&&Time.unscaledTime<next)return;changed=false;next=Time.unscaledTime+.5f;try{preloader??=PreloaderUI.Instance??UnityEngine.Object.FindObjectOfType<PreloaderUI>();Apply();}catch(Exception error){next=Time.unscaledTime+10;Plugin.Self.Error(error);}}
 internal static void Draw(Rect r){
  InterfaceMenu.Label(new(r.x,r.y,r.width,28),"左下角版本文字",InterfaceMenu.Accent,17);
  if(InterfaceMenu.Button(new(r.x,r.y+34,Math.Min(340,r.width),32),I18n.Text("自定义左下角版本")+" · "+I18n.Text(enabled.Value?"开启":"关闭"),enabled.Value)){enabled.Value=!enabled.Value;Apply();}
  draft=InterfaceMenu.Field(new(r.x,r.y+78,Math.Max(100,r.width-204),32),draft);
  if(InterfaceMenu.Button(new(r.xMax-194,r.y+78,86,32),"应用")){text.Value=Clean(draft);draft=text.Value;enabled.Value=text.Value.Length>0;dynamic.Value=false;CancelScript();dynamicText="";Apply();}
  if(InterfaceMenu.Button(new(r.xMax-98,r.y+78,98,32),"恢复原始")){enabled.Value=false;text.Value=draft="";CancelScript();dynamicText="";Apply();}
  if(InterfaceMenu.Button(new(r.x,r.y+120,r.width,32),"JavaScript · "+(dynamic.Value&&enabled.Value?"运行中":"打开编辑器")))InterfaceMenu.Navigate("设置与热键","JavaScript");
 }
 internal static void DrawScript(Rect content){
  float pageHeight=Math.Max(580,content.height-2)+(unitDropdown?144:0);bool scrolling=pageHeight>content.height;
  var r=new Rect(0,0,content.width-(scrolling?24:4),pageHeight);
  scriptScroll=GUI.BeginScrollView(content,scriptScroll,new Rect(0,0,r.width,pageHeight));
  try{DrawScriptContent(r);}finally{GUI.EndScrollView();}
 }
 static void DrawScriptContent(Rect r){
  InterfaceMenu.Label(new(r.x,r.y,r.width,28),"JavaScript · 左下角文字",InterfaceMenu.Accent,17);
  bool running=dynamic.Value&&enabled.Value;
  if(InterfaceMenu.Button(new(r.x,r.y+38,r.width,32),"动态 JavaScript · "+(running?"开启":"关闭"),running)){dynamic.Value=!running;if(dynamic.Value)enabled.Value=true;CancelScript();nextScript=0;dynamicText="";Apply();}
  float y=r.y+84;
  float height=Math.Clamp(r.yMax-y-216-(unitDropdown?144:0),220,520);
  DrawScriptEditor(new Rect(r.x,y,r.width,height));y+=height+10;
  InterfaceMenu.Label(new(r.x,y,96,30),"刷新间隔",null,12);float fieldWidth=Math.Clamp(r.width-330,80,140),unitX=r.x+108+fieldWidth;
  intervalDraft=InterfaceMenu.Field(new(r.x+100,y,fieldWidth,30),intervalDraft);
  if(InterfaceMenu.Button(new(unitX,y,80,30),I18n.Text(unitNames[(int)intervalUnit.Value])+(unitDropdown?"  ▴":"  ▾"),unitDropdown))unitDropdown=!unitDropdown;
  float saveX=unitX+90;
  if(InterfaceMenu.Button(new(saveX,y,Math.Max(90,r.xMax-saveX),30),"保存并立即刷新")){
   if(!CornerScriptInterval.TrySeconds(intervalDraft,intervalUnit.Value,out double seconds))scriptStatus="请输入有效数值，刷新间隔须在 1 毫秒～1 小时之间。";
   else{CancelScript();script.Value=scriptDraft;interval.Value=seconds;enabled.Value=true;dynamic.Value=true;nextScript=0;dynamicText="";unitDropdown=false;StartScript();}
  }y+=40;
  if(unitDropdown){
   bool close=false;for(int i=0;i<unitNames.Length;i++)if(InterfaceMenu.Button(new(unitX,y+i*36,80,30),unitNames[i],(int)intervalUnit.Value==i)){
    double seconds=CornerScriptInterval.TrySeconds(intervalDraft,intervalUnit.Value,out var entered)?entered:IntervalSeconds;
    intervalUnit.Value=(CornerScriptTimeUnit)i;intervalDraft=CornerScriptInterval.Format(seconds,intervalUnit.Value);close=true;
   }
   y+=144;if(close)unitDropdown=false;
  }
  if(InterfaceMenu.Button(new(r.x,y,Math.Min(180,r.width),30),"填入时间示例")){scriptDraft=CornerScript.Example;codeScroll=Vector2.zero;}
  if(InterfaceMenu.Button(new(r.x+190,y,Math.Min(180,r.width-190),30),"填入异步联网示例")){scriptDraft=CornerScript.AsyncExample;codeScroll=Vector2.zero;}
  InterfaceMenu.Paragraph(new(r.x,y+38,r.width,46),scriptStatus,null,12);
  InterfaceMenu.Paragraph(new(r.x,y+92,r.width,44),"当前显示："+(written.Length>0?written:I18n.Text("原始版本文字")),null,12);
 }
 static void DrawScriptEditor(Rect viewport){
  const string control="LocalCornerScriptEditor";
  codeStyle??=new GUIStyle(GUI.skin.textArea){fontSize=13,wordWrap=true,richText=false,padding=new RectOffset(8,8,8,8)};
  var bar=GUI.skin.verticalScrollbar;
  float barWidth=Math.Max(18,bar.fixedWidth)+bar.margin.left+bar.margin.right;
  float width=Math.Max(1,viewport.width-barWidth-2);
  if(measuredCode!=scriptDraft||measuredWidth!=width){measuredCode=scriptDraft;measuredWidth=width;measuredHeight=codeStyle.CalcHeight(new GUIContent(scriptDraft+"\n"),width)+12;}
  float height=Math.Max(viewport.height,measuredHeight);
  codeScroll.x=0;codeScroll.y=Math.Clamp(codeScroll.y,0,Math.Max(0,height-viewport.height));
  var ev=Event.current;
  // Consume the wheel here so the surrounding settings page stays in place.
  if(ev.type==EventType.ScrollWheel&&viewport.Contains(ev.mousePosition)){codeScroll.y=Math.Clamp(codeScroll.y+ev.delta.y*42,0,Math.Max(0,height-viewport.height));ev.Use();}
  bool revealCaret=ev.type==EventType.KeyDown&&GUI.GetNameOfFocusedControl()==control;
  codeScroll=GUI.BeginScrollView(viewport,codeScroll,new Rect(0,0,width,height),false,true,GUIStyle.none,bar);
  try{
   GUI.SetNextControlName(control);
   scriptDraft=GUI.TextArea(new Rect(0,0,width,height),scriptDraft,16384,codeStyle);
   if(revealCaret){
    var editor=GUIUtility.GetStateObject(Il2CppInterop.Runtime.Il2CppType.Of<TextEditor>(),GUIUtility.keyboardControl).Cast<TextEditor>();
    var caret=codeStyle.GetCursorPixelPosition(new Rect(0,0,width,height),new GUIContent(scriptDraft),Math.Clamp(editor.cursorIndex,0,scriptDraft.Length));
    float lineHeight=Math.Max(18,codeStyle.lineHeight);
    if(caret.y<codeScroll.y+8)codeScroll.y=Math.Max(0,caret.y-8);
    else if(caret.y+lineHeight>codeScroll.y+viewport.height-8)codeScroll.y=caret.y+lineHeight-viewport.height+8;
   }
  }finally{GUI.EndScrollView();}
 }
 internal static object Diagnostics()=>new{enabled=enabled.Value,found=label!=null,applied=written.Length>0,matches=written.Length>0&&label?.FormattedText==written,length=written.Length};
 internal static void Shutdown(){CancelScript();CornerScriptDependencies.Shutdown();codeStyle=null;measuredCode="";measuredWidth=-1;codeScroll=Vector2.zero;try{Restore();}finally{preloader=null;label=null;formats.Clear();}}
}
