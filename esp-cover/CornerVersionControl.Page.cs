using System;
using System.Collections.Generic;
using UnityEngine;
namespace LocalSpt5Esp;

internal static partial class CornerVersionControl {
 static Color MutedText=>Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.32f);

 static void DrawScriptSettings(Rect r,bool wide){
  const float unitWidth=42,buttonWidth=150,gap=6;
  InterfaceMenu.Label(new(r.x,r.y,r.width-buttonWidth-gap,30),"JavaScript · 左下角文字",InterfaceMenu.Accent,18);
  bool running=dynamic.Value&&enabled.Value;
  if(InterfaceMenu.Button(new(r.xMax-buttonWidth,r.y,buttonWidth,30),running?"动态显示已开启":"动态显示已关闭",running)){
   dynamic.Value=!running;
   if(dynamic.Value)enabled.Value=true;
   CancelScript();nextScript=0;dynamicText="";Apply();
  }
  float y=r.y+38;
  float expressionWidth=wide?r.width*.51f:r.width;
  var expression=new Rect(r.x+106,y,expressionWidth-106,30);
  InterfaceMenu.Label(new(r.x,y,100,30),"显示表达式",null,12);
  GUI.SetNextControlName("LocalCornerExpression");
  expressionDraft=InterfaceMenu.Field(expression,expressionDraft);
  if(expressionDraft.Length==0&&GUI.GetNameOfFocusedControl()!="LocalCornerExpression")
   InterfaceMenu.Label(new(expression.x+8,y,expression.width-16,30),"例如 status 或 game.fps。使用 return 时留空。",MutedText,12);

  float refreshX=wide?r.x+expressionWidth+16:r.x;
  if(!wide)y+=38;
  float valueWidth=Math.Min(86,Math.Max(36,r.xMax-refreshX-82-unitWidth*4-gap*4));
  InterfaceMenu.Label(new(refreshX,y,82,30),"刷新间隔",null,12);
  intervalDraft=InterfaceMenu.Field(new(refreshX+82,y,valueWidth,30),intervalDraft);
  float unitsX=refreshX+82+valueWidth+gap;
  for(int i=0;i<unitNames.Length;i++){
   var unit=(CornerScriptTimeUnit)i;
   if(!InterfaceMenu.Button(new(unitsX+i*(unitWidth+gap),y,unitWidth,30),unitNames[i],unit==intervalUnit.Value)||unit==intervalUnit.Value)continue;
   double seconds=CornerScriptInterval.TrySeconds(intervalDraft,intervalUnit.Value,out var entered)?entered:IntervalSeconds;
   intervalUnit.Value=unit;
   intervalDraft=CornerScriptInterval.Format(seconds,unit);
  }

  y+=38;
  if(InterfaceMenu.Button(new(r.xMax-buttonWidth,y,buttonWidth,32),"保存并立即刷新",true))SaveScript();
  float infoWidth=r.width-buttonWidth-12;
  InterfaceMenu.Label(new(r.x,y,infoWidth,20),"当前显示："+(written.Length>0?written:I18n.Text("原始版本文字")),null,12);
  InterfaceMenu.Paragraph(new(r.x,y+22,infoWidth,32),scriptStatus,MutedText,12);
 }

 static void SaveScript(){
  if(!CornerScriptInterval.TrySeconds(intervalDraft,intervalUnit.Value,out double seconds)){
   scriptStatus="请输入有效数值，刷新间隔须在 1 毫秒～1 小时之间。";
   return;
  }
  try{CornerScript.SourceWithExpression(scriptDraft,expressionDraft);}
  catch(ArgumentException error){scriptStatus=error.Message;return;}
  CancelScript();
  script.Value=scriptDraft;
  displayExpression.Value=expressionDraft;
  interval.Value=seconds;
  enabled.Value=true;
  dynamic.Value=true;
  nextScript=0;
  StartScript();
 }

 static void SetDraft(string source,string expression){
  scriptDraft=source;
  expressionDraft=expression;
  codeScroll=Vector2.zero;
 }

 static void DrawEditorPane(Rect pane,float bodyHeight,bool pointerInPage){
  InterfaceMenu.Surface(pane,Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.055f),8);
  const float pad=10,gap=6,actionWidth=82;
  float x=pane.x+pad,y=pane.y+pad,width=pane.width-pad*2;
  InterfaceMenu.Label(new(x,y,width-actionWidth*2-gap*2,28),"脚本编辑器",InterfaceMenu.Accent,15);
  if(InterfaceMenu.Button(new(pane.xMax-pad-actionWidth*2-gap,y,actionWidth,28),"导入 JS")&&fileDialog==null){
   fileDialogMode="import";
   fileDialog=WindowsDialogs.PickOpenFile("导入 JavaScript 脚本","JavaScript (*.js;*.txt)\0*.js;*.txt\0所有文件 (*.*)\0*.*\0");
  }
  if(InterfaceMenu.Button(new(pane.xMax-pad-actionWidth,y,actionWidth,28),"导出 JS")&&fileDialog==null){
   fileDialogMode="export";
   fileDialog=WindowsDialogs.PickFolder("选择 JavaScript 导出目录");
  }
  y+=36;
  DrawScriptEditor(new(x,y,width,bodyHeight),pointerInPage);
  y+=bodyHeight+8;
  InterfaceMenu.Label(new(x,y,46,28),"示例",MutedText,12);
  float exampleWidth=(width-48-gap*(examples.Length-1))/examples.Length;
  for(int i=0;i<examples.Length;i++){
   var example=examples[i];
   if(InterfaceMenu.Button(new(x+48+i*(exampleWidth+gap),y,exampleWidth,28),example.Label))SetDraft(example.Source,example.Expression);
  }
 }

 static void DrawOutputPane(Rect pane,float bodyHeight,bool pointerInPage){
  InterfaceMenu.Surface(pane,Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.055f),8);
  const float pad=10,gap=6,testWidth=106,clearWidth=76;
  float x=pane.x+pad,y=pane.y+pad,width=pane.width-pad*2;
  InterfaceMenu.Label(new(x,y,width-testWidth-clearWidth-gap*2,28),"测试输出",InterfaceMenu.Accent,15);
  bool testing=testWork!=null;
  if(InterfaceMenu.Button(new(pane.xMax-pad-testWidth-clearWidth-gap,y,testWidth,28),testing?"停止测试":"测试运行一次",true)){
   if(testing){CancelTest();testStatus="测试已取消。";}
   else StartTest();
  }
  if(InterfaceMenu.Button(new(pane.xMax-pad-clearWidth,y,clearWidth,28),"清空输出")){
   testOutput="";testOutputScroll=Vector2.zero;testStatus="已清空测试输出。";
  }
  y+=36;
  DrawTestOutput(new(x,y,width,bodyHeight),pointerInPage);
  InterfaceMenu.Paragraph(new(x,y+bodyHeight+8,width,32),testStatus,MutedText,12);
 }

 static void DrawPresets(Rect r){
  const float pad=10,gap=8,saveWidth=136;
  InterfaceMenu.Surface(r,Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.055f),8);
  float x=r.x+pad,y=r.y+pad,width=r.width-pad*2;
  InterfaceMenu.Label(new(x,y,width,24),"脚本预设",InterfaceMenu.Accent,15);
  y+=30;
  var field=new Rect(x,y,width-saveWidth-gap,30);
  GUI.SetNextControlName("LocalCornerPresetName");
  presetName=InterfaceMenu.Field(field,presetName);
  if(presetName.Length==0&&GUI.GetNameOfFocusedControl()!="LocalCornerPresetName")
   InterfaceMenu.Label(new(x+8,y,field.width-16,30),"预设名称",MutedText,12);
  if(InterfaceMenu.Button(new(r.xMax-pad-saveWidth,y,saveWidth,30),"保存为脚本预设"))SavePreset();
  y+=40;
  for(int i=presets.Count-1;i>=0;i--){
   var p=presets[i];
   InterfaceMenu.Label(new(x,y,width-160,30),$"{p.Name}  ·  {p.Saved}",null,12,translate:false);
   if(InterfaceMenu.Button(new(r.xMax-pad-152,y,72,30),"载入")){
    SetDraft(p.Source,p.Expression??"");
    intervalUnit.Value=p.Unit;
    intervalDraft=CornerScriptInterval.Format(p.Interval,p.Unit);
    presetName=p.Name;
    scriptStatus="已载入预设："+p.Name;
   }
   if(InterfaceMenu.Button(new(r.xMax-pad-72,y,72,30),"删除")){
    try{
     var updated=new List<ScriptPreset>(presets);updated.RemoveAt(i);
     PresetStorage.Save(PresetPath,updated);
     presets.RemoveAt(i);
     scriptStatus="已删除预设："+p.Name;
    }catch(Exception e){scriptStatus="保存预设失败："+e.Message;}
    break;
   }
   y+=38;
  }
 }

 static void SavePreset(){
  try{
   CornerScript.SourceWithExpression(scriptDraft,expressionDraft);
   string title=string.IsNullOrWhiteSpace(presetName)?"脚本 "+DateTime.Now.ToString("MMdd-HHmmss"):presetName.Trim();
   if(title.Length>60)title=title[..60];
   double seconds=CornerScriptInterval.TrySeconds(intervalDraft,intervalUnit.Value,out var parsed)?parsed:IntervalSeconds;
   var p=new ScriptPreset(Guid.NewGuid().ToString("N"),title,scriptDraft,seconds,intervalUnit.Value,DateTime.Now.ToString("yyyy-MM-dd HH:mm"),expressionDraft);
   var updated=new List<ScriptPreset>(presets){p};
   PresetStorage.Save(PresetPath,updated);
   presets.Add(p);presetName="";scriptStatus="已保存脚本预设："+title;
  }catch(Exception e){scriptStatus="保存预设失败："+e.Message;}
 }
}
