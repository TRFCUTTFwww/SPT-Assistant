using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Configuration;
using EFT.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class CornerVersionControl {
 static ConfigEntry<bool> enabled=null!;static ConfigEntry<string> text=null!;static PreloaderUI? preloader;static LocalizedText? label;static string original="",written="",draft="";static bool restoring;static float next;
 static readonly Dictionary<long,(TextMeshProUGUI Label,bool Rich)> formats=new();
 static ConfigEntry<bool> dynamic=null!;static ConfigEntry<string> script=null!,displayExpression=null!;static ConfigEntry<double> interval=null!;static ConfigEntry<CornerScriptTimeUnit> intervalUnit=null!;
 static string scriptDraft="",expressionDraft="",intervalDraft="30",dynamicText="",scriptStatus="尚未运行",presetName="";static Task<string>? work;static CancellationTokenSource? cancellation;static double nextScript;static GUIStyle? codeStyle;static bool presetsLoaded;
 static Task<string?>? fileDialog;static string fileDialogMode="";
 sealed record ScriptPreset(string Id,string Name,string Source,double Interval,CornerScriptTimeUnit Unit,string Saved,string? Expression=null);
 static readonly List<ScriptPreset> presets=new();
 static string PresetPath=>System.IO.Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","corner-scripts.json");
 static readonly string[] unitNames={"时","分","秒","毫秒"};
 const string ScriptControl="LocalCornerScriptEditor",OutputControl="LocalCornerScriptOutput";
 static readonly (string Label,string Source,string Expression)[] examples={
  ("时间",CornerScript.Example,""),("战局",CornerScript.GameExample,""),
  ("实时",CornerScript.LiveExample,""),("变量",CornerScript.VariableExample,"status"),("联网",CornerScript.AsyncExample,"")
 };
 static bool changed;static Vector2 scriptScroll,codeScroll,testOutputScroll;
 static string measuredCode="",measuredOutput="";static float measuredWidth=-1,measuredHeight,measuredOutputWidth=-1,measuredOutputHeight;
 static Task<CornerScript.TestRunResult>? testWork;static CancellationTokenSource? testCancel;static Stopwatch? testClock;
 static string testStatus="尚未测试",testOutput="";static GUIStyle? outputStyle;
 sealed record CopyMenu(string Control,string Text,Vector2 ScreenPosition);
 static CopyMenu? copyMenu;
 static int lastScriptFrame=-2;
 // Retain the existing seconds-based config key so saved intervals keep their meaning.
 static double IntervalSeconds=>CornerScriptInterval.Normalize(interval.Value);
 internal static void Initialize(Plugin plugin){
  CornerScriptDependencies.Install();
  dynamic=FeatureConfig.Bind("24. Interface","Dynamic corner version","动态左下角文字",false);
  script=FeatureConfig.Bind("24. Interface","Corner version script","左下角 JavaScript",CornerScript.Example);
  displayExpression=FeatureConfig.Bind("24. Interface","Corner display expression","显示表达式","");
  interval=FeatureConfig.Bind("24. Interface","Corner script interval","动态文字刷新间隔（秒）",30d,"0.001～3600 秒。");
  intervalUnit=FeatureConfig.Bind("24. Interface","Corner script display unit","刷新间隔显示单位",CornerScriptTimeUnit.Seconds);
  if(!Enum.IsDefined(intervalUnit.Value))intervalUnit.Value=CornerScriptTimeUnit.Seconds;
  scriptDraft=script.Value;expressionDraft=displayExpression.Value;intervalDraft=CornerScriptInterval.Format(IntervalSeconds,intervalUnit.Value);
  enabled=FeatureConfig.Bind("24. Interface","Custom corner version","自定义左下角版本",false,"修改游戏左下角显示文字。");
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
 static CornerScript.LiveObjects LiveObjects(){
  var world=Comfort.Common.Singleton<EFT.GameWorld>.Instance;
  var player=world?.MainPlayer;
  return new(world,player,player?.Profile??HideoutCraftControl.CurrentProfile);
 }
 static void StartScript(){
  if(work!=null)return;
  cancellation=new();
  var token=cancellation.Token;
  string source=script.Value,expression=displayExpression.Value;
  var snapshot=CornerGameSnapshot.ReadFrame();
  if(CornerScript.UsesGameThread(source)){
   long stamp=RuntimePerformance.Start();
   try{work=Task.FromResult(CornerScript.ExecuteCore(source,expression,token,null,snapshot.ToJson(),null,LiveObjects()));}
   catch(Exception error){work=Task.FromException<string>(error);}
   finally{RuntimeCosts.Mark(ref stamp,CostStage.CornerLiveScript);}
  }else{
   work=Task.Run(()=>CornerScript.EvaluateDisplay(source,expression,token,null,snapshot.ToJson()),token);
   scriptStatus="正在后台执行…";
  }
 }
 static void ScriptTick(){
  if(!enabled.Value||!dynamic.Value){if(work!=null)CancelScript();return;}
  if(work?.IsCompleted==true){
   var finished=work;work=null;cancellation?.Dispose();cancellation=null;
   double delay=IntervalSeconds;
   try{
    string value=Clean(finished.GetAwaiter().GetResult());
    changed|=value!=dynamicText;dynamicText=value;
    scriptStatus="已更新 · "+DateTime.Now.ToString("HH:mm:ss.fff");
   }catch(Exception e){
    delay=Math.Max(delay,1);
    scriptStatus="执行失败，保留上次文字："+Clean(e.GetBaseException().Message);
   }
   nextScript=Time.realtimeSinceStartupAsDouble+delay;
  }
  if(work==null&&Time.realtimeSinceStartupAsDouble>=nextScript)StartScript();
 }
 static void CancelTest(){testCancel?.Cancel();testCancel?.Dispose();testCancel=null;if(testWork is {} pending)_=pending.ContinueWith(t=>{_=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);testWork=null;testClock=null;}
 static void StartTest(){
  CancelTest();testCancel=new();var token=testCancel.Token;
  string source=scriptDraft,expression=expressionDraft;
  var snapshot=CornerGameSnapshot.ReadFrame();
  testClock=Stopwatch.StartNew();testOutput="";testOutputScroll=Vector2.zero;testStatus="正在测试运行…";
  if(CornerScript.UsesGameThread(source)){
   try{testWork=Task.FromResult(CornerScript.RunTest(source,expression,token,null,snapshot.ToJson(),LiveObjects()));}
   catch(Exception error){testWork=Task.FromException<CornerScript.TestRunResult>(error);}
  }else testWork=Task.Run(()=>CornerScript.EvaluateDisplayTest(source,expression,token,null,snapshot.ToJson()),token);
 }
 static void PollTest(){
  if(testWork is not {IsCompleted:true} finished)return;
  testWork=null;testCancel?.Dispose();testCancel=null;long ms=testClock?.ElapsedMilliseconds??0;testClock=null;
  if(finished.IsCanceled){testStatus=$"测试已取消 · {ms} ms";return;}
  try{
   var res=finished.GetAwaiter().GetResult();
   string logs=res.Logs.Length>0?string.Join("\n",res.Logs):"";
   if(res.Ok){
    testOutput=logs.Length>0?$"{logs}\n{I18n.Text("--- 返回值 ---")}\n{res.Value}":res.Value;
    string preview=Clean(res.Value);
    testStatus=preview==res.Value?$"测试成功 · {ms} ms · {DateTime.Now:HH:mm:ss.fff}":$"测试成功 · {ms} ms · {DateTime.Now:HH:mm:ss.fff}（左下角将截取并过滤为 {preview.Length} 字）";
   }else{
    string errorLabel=I18n.Text("[错误]");
    testOutput=logs.Length>0?$"{logs}\n{errorLabel} {res.Error}":errorLabel+" "+res.Error;
    testStatus=$"测试失败 · {ms} ms · {DateTime.Now:HH:mm:ss.fff}";
   }
  }catch(Exception e){
   testOutput=I18n.Text("[错误]")+" "+e.GetBaseException().Message;
   testStatus=$"测试失败 · {ms} ms · {DateTime.Now:HH:mm:ss.fff}";
  }
 }
 internal static void Tick(){PollTest();ScriptTick();if(!enabled.Value&&written.Length==0||!changed&&Time.unscaledTime<next)return;changed=false;next=Time.unscaledTime+.5f;try{preloader??=PreloaderUI.Instance??UnityEngine.Object.FindObjectOfType<PreloaderUI>();Apply();}catch(Exception error){next=Time.unscaledTime+10;Plugin.Self.Error(error);}}
 internal static void Draw(Rect r){
  InterfaceMenu.Label(new(r.x,r.y,r.width,28),"左下角版本文字",InterfaceMenu.Accent,17);
  if(InterfaceMenu.Button(new(r.x,r.y+34,Math.Min(340,r.width),32),I18n.Text("自定义左下角版本")+" · "+I18n.Text(enabled.Value?"开启":"关闭"),enabled.Value)){enabled.Value=!enabled.Value;Apply();}
  draft=InterfaceMenu.Field(new(r.x,r.y+78,Math.Max(100,r.width-204),32),draft);
  if(InterfaceMenu.Button(new(r.xMax-194,r.y+78,86,32),"应用")){text.Value=Clean(draft);draft=text.Value;enabled.Value=text.Value.Length>0;dynamic.Value=false;CancelScript();dynamicText="";Apply();}
  if(InterfaceMenu.Button(new(r.xMax-98,r.y+78,98,32),"恢复原始")){enabled.Value=false;text.Value=draft="";CancelScript();dynamicText="";Apply();}
  if(InterfaceMenu.Button(new(r.x,r.y+120,r.width,32),"JavaScript · "+(dynamic.Value&&enabled.Value?"运行中":"打开编辑器")))InterfaceMenu.Navigate("设置与热键","JavaScript");
 }
 internal static void DrawScript(Rect content){
  LoadPresets();PollFileDialog();PollTest();
  if(Time.frameCount-lastScriptFrame>1)copyMenu=null;
  lastScriptFrame=Time.frameCount;
  HandleCopyMenu(content);
  bool pointerInPage=content.Contains(Event.current.mousePosition);
  float width=Math.Max(1,content.width-24);
  bool wide=width>=780;
  float codeHeight=wide?Math.Clamp(content.height-276,180,450):Math.Clamp(content.height*.48f,180,330);
  float outputHeight=wide?codeHeight:Math.Clamp(content.height*.42f,150,250);
  float settingsHeight=wide?140:178;
  const float paneChrome=94;
  float panesHeight=wide?codeHeight+paneChrome:codeHeight+outputHeight+paneChrome*2+12;
  float presetsY=settingsHeight+panesHeight+16;
  float pageHeight=presetsY+80+presets.Count*38;
  scriptScroll=GUI.BeginScrollView(content,scriptScroll,new Rect(0,0,width,pageHeight));
  try{
   DrawScriptSettings(new Rect(0,0,width,settingsHeight),wide);
   float editorWidth=wide?(width-12)*.57f:width;
   DrawEditorPane(new Rect(0,settingsHeight,editorWidth,codeHeight+paneChrome),codeHeight,pointerInPage);
   float outputX=wide?editorWidth+12:0;
   float outputY=wide?settingsHeight:settingsHeight+codeHeight+paneChrome+12;
   DrawOutputPane(new Rect(outputX,outputY,width-outputX,outputHeight+paneChrome),outputHeight,pointerInPage);
   DrawPresets(new Rect(0,presetsY,width,pageHeight-presetsY));
  }finally{GUI.EndScrollView();}
  DrawCopyMenu(content);
 }
 static void LoadPresets(){if(presetsLoaded)return;presetsLoaded=true;try{presets.Clear();presets.AddRange(PresetStorage.Load<ScriptPreset>(PresetPath));}catch(Exception e){scriptStatus="脚本预设读取失败，原文件保留："+e.Message;}}

 static void PollFileDialog(){
  if(fileDialog is not {IsCompleted:true} done)return;
  string mode=fileDialogMode;fileDialog=null;fileDialogMode="";
  try{
   string? path=done.GetAwaiter().GetResult();if(string.IsNullOrWhiteSpace(path))return;
   if(mode=="import"){
    string source=System.IO.File.ReadAllText(path);if(source.Length>16384)throw new ArgumentException("脚本最多 16384 个字符。");
    SetDraft(source,"");if(string.IsNullOrWhiteSpace(presetName))presetName=System.IO.Path.GetFileNameWithoutExtension(path);
    scriptStatus="已导入脚本："+System.IO.Path.GetFileName(path);FeatureToasts.Show(scriptStatus);
   }else if(mode=="export"){
    string safeName=string.Concat((string.IsNullOrWhiteSpace(presetName)?"corner-script":presetName.Trim()).Select(c=>System.IO.Path.GetInvalidFileNameChars().Contains(c)?'_':c));
    if(string.IsNullOrWhiteSpace(safeName))safeName="corner-script";
    string file=System.IO.Path.Combine(path,$"{safeName}-{DateTime.Now:yyyyMMdd-HHmmss}.js");
    System.IO.File.WriteAllText(file,CornerScript.SourceWithExpression(scriptDraft,expressionDraft));
    scriptStatus="已导出脚本到："+file;FeatureToasts.Show(scriptStatus);
   }
  }catch(Exception e){scriptStatus="脚本文件操作失败："+e.GetBaseException().Message;FeatureToasts.Show(scriptStatus);}
 }
 static void DrawScriptEditor(Rect viewport,bool pointerInPage){
  codeStyle??=new GUIStyle(GUI.skin.textArea){fontSize=13,wordWrap=true,richText=false,padding=new RectOffset(8,8,8,8)};
  scriptDraft=DrawTextBox(viewport,scriptDraft,ScriptControl,codeStyle,ref codeScroll,ref measuredCode,ref measuredWidth,ref measuredHeight,editable:true,pointerInPage);
 }
 static void DrawTestOutput(Rect viewport,bool pointerInPage){
  outputStyle??=new GUIStyle(GUI.skin.textArea){fontSize=12,wordWrap=true,richText=false,padding=new RectOffset(8,8,8,8)};
  DrawTextBox(viewport,testOutput,OutputControl,outputStyle,ref testOutputScroll,ref measuredOutput,ref measuredOutputWidth,ref measuredOutputHeight,editable:false,pointerInPage);
 }
 static string DrawTextBox(Rect viewport,string content,string control,GUIStyle style,ref Vector2 boxScroll,ref string measured,ref float prevWidth,ref float prevHeight,bool editable,bool pointerInPage){
  var bar=GUI.skin.verticalScrollbar;
  float barWidth=Math.Max(18,bar.fixedWidth)+bar.margin.left+bar.margin.right;
  float width=Math.Max(1,viewport.width-barWidth-2);
  if(measured!=content||prevWidth!=width){measured=content;prevWidth=width;prevHeight=style.CalcHeight(new GUIContent(content+"\n"),width)+12;}
  float height=Math.Max(viewport.height,prevHeight);
  boxScroll.x=0;boxScroll.y=Math.Clamp(boxScroll.y,0,Math.Max(0,height-viewport.height));
  var ev=Event.current;
  // Consume the wheel here so the surrounding settings page stays in place.
  bool focused=GUI.GetNameOfFocusedControl()==control;
  if(pointerInPage&&viewport.Contains(ev.mousePosition)){
   if(ev.type==EventType.ScrollWheel){boxScroll.y=Math.Clamp(boxScroll.y+ev.delta.y*42,0,Math.Max(0,height-viewport.height));ev.Use();}
   else if(ev.type==EventType.ContextClick||ev.type==EventType.MouseDown&&ev.button==1){
    string selection=focused?SelectedText(content):"";
    copyMenu=selection.Length>0?new(control,selection,GUIUtility.GUIToScreenPoint(ev.mousePosition)):null;
    ev.Use();
   }
  }
  if(focused&&!editable&&OutputEditEvent(ev))ev.Use();
  bool revealCaret=focused&&ev.type==EventType.KeyDown;
  boxScroll=GUI.BeginScrollView(viewport,boxScroll,new Rect(0,0,width,height),false,true,GUIStyle.none,bar);
  try{
   GUI.SetNextControlName(control);
   string nextText=GUI.TextArea(new Rect(0,0,width,height),content,editable?16384:Math.Max(16384,content.Length),style);
   if(revealCaret){
    var editor=FocusedEditor();
    if(editor!=null){
     var caret=style.GetCursorPixelPosition(new Rect(0,0,width,height),new GUIContent(nextText),Math.Clamp(editor.cursorIndex,0,nextText.Length));
     float lineHeight=Math.Max(18,style.lineHeight);
     if(caret.y<boxScroll.y+8)boxScroll.y=Math.Max(0,caret.y-8);
     else if(caret.y+lineHeight>boxScroll.y+viewport.height-8)boxScroll.y=caret.y+lineHeight-viewport.height+8;
    }
   }
   return editable?nextText:content;
  }finally{GUI.EndScrollView();}
 }
 static TextEditor? FocusedEditor()=>GUIUtility.GetStateObject(Il2CppInterop.Runtime.Il2CppType.Of<TextEditor>(),GUIUtility.keyboardControl)?.TryCast<TextEditor>();
 static string SelectedText(string content){
  var editor=FocusedEditor();
  if(editor==null||editor.text!=content)return "";
  int first=Math.Clamp(editor.cursorIndex,0,content.Length),last=Math.Clamp(editor.selectIndex,0,content.Length);
  return content[Math.Min(first,last)..Math.Max(first,last)];
 }
 static bool OutputEditEvent(Event ev){
  if(ev.type is EventType.ValidateCommand or EventType.ExecuteCommand)
   return ev.commandName is "Cut" or "Paste" or "Delete" or "SoftDelete" or "UndoRedoPerformed";
  if(ev.type!=EventType.KeyDown)return false;
  if(ev.control||ev.command)return ev.keyCode is KeyCode.V or KeyCode.X or KeyCode.Z or KeyCode.Y;
  return ev.character!='\0'||ev.shift&&ev.keyCode==KeyCode.Insert||ev.keyCode is KeyCode.Backspace or KeyCode.Delete or KeyCode.Return or KeyCode.KeypadEnter;
 }
 static Rect CopyMenuBounds(Rect content,CopyMenu menu){
  const float width=140,height=36;
  var point=GUIUtility.ScreenToGUIPoint(menu.ScreenPosition);
  return new(Math.Clamp(point.x,content.x,Math.Max(content.x,content.xMax-width)),
   Math.Clamp(point.y,content.y,Math.Max(content.y,content.yMax-height)),width,height);
 }
 static void HandleCopyMenu(Rect content){
  if(copyMenu is not {} menu)return;
  var ev=Event.current;
  string value=menu.Control==ScriptControl?scriptDraft:testOutput;
  if(GUI.GetNameOfFocusedControl()!=menu.Control||SelectedText(value)!=menu.Text){
   copyMenu=null;return;
  }
  if(ev.type==EventType.MouseDown){
   copyMenu=null;
   if(ev.button==0&&CopyMenuBounds(content,menu).Contains(ev.mousePosition)){
    GUIUtility.systemCopyBuffer=menu.Text;ev.Use();
   }
  }else if(ev.type==EventType.ContextClick||ev.type==EventType.MouseUp&&ev.button==1)ev.Use();
  else if(ev.type==EventType.ScrollWheel||ev.type==EventType.KeyDown){
   copyMenu=null;
   if(ev.type==EventType.KeyDown&&ev.keyCode==KeyCode.Escape)ev.Use();
  }
 }
 static void DrawCopyMenu(Rect content){
  if(copyMenu is not {} menu)return;
  var rect=CopyMenuBounds(content,menu);
  InterfaceMenu.Surface(rect,Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.16f),7);
  InterfaceMenu.Label(new(rect.x+10,rect.y,rect.width-20,rect.height),"复制选中内容",null,13);
 }
 internal static object Diagnostics()=>new{enabled=enabled.Value,found=label!=null,applied=written.Length>0,matches=written.Length>0&&label?.FormattedText==written,length=written.Length};
 internal static void Shutdown(){CancelScript();CancelTest();fileDialog=null;fileDialogMode="";copyMenu=null;CornerScriptDependencies.Shutdown();codeStyle=null;outputStyle=null;measuredCode="";measuredOutput="";measuredWidth=-1;measuredOutputWidth=-1;codeScroll=Vector2.zero;testOutputScroll=Vector2.zero;try{Restore();}finally{preloader=null;label=null;formats.Clear();}}
}
