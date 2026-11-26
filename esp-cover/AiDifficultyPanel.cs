using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using UnityEngine;
namespace LocalSpt5Esp;
internal static class AiDifficultyPanel {
 static AiDifficultySettings? draft;
 static readonly Dictionary<(int Category,string Key),string> inputs=new();
 static Vector2 scroll;
 static int selected;
 static string notice="";
 static bool unreadable;
 static readonly string[] presets={"休闲","简单","原有","困难","极难"};
 static string Group=>AiDifficultyRules.Groups[selected-1];
 static AiDifficultyProfile Profile=>selected==0?draft!.Overall:draft!.Groups.GetValueOrDefault(Group,draft.Overall);
 static bool Inherited=>selected>0&&!draft!.Groups.ContainsKey(Group);
 static void Load(){inputs.Clear();try{draft=AiDifficultyControl.Read();unreadable=false;notice="";}catch{draft=new();unreadable=true;notice="AI 难度设置无法读取，可恢复默认后重新保存。";}}
 static void Change(Action action){action();notice="尚未保存。";}
 static AiDifficultyProfile EditableProfile(){
  if(!Inherited)return Profile;
  return draft!.Groups[Group]=draft.Overall.Clone();
 }
 static void ClearSelectedInputs(){
  foreach(var field in AiDifficultyRules.Fields)inputs.Remove((selected,field.Key));
 }
 static void SetMultiplier(string key,float value){
  EditableProfile().Values[key]=value;
  notice="尚未保存。";
 }
 static void Save(){
  if(inputs.Values.Any(input=>!AiDifficultyRules.TryParseMultiplier(input,out _))){
   notice="AI 难度数值无效。";
   return;
  }
  try{
   AiDifficultyControl.Save(draft!);
   inputs.Clear();
   notice="已保存，当前及后续出现的 AI 将使用此设置。";
   FeatureToasts.Show(notice);
  }catch{notice="保存失败，请检查设置后重试。";}
 }
 static void DrawMultiplier(AiDifficultyField field,float left,float top,float width){
  float value=Profile.Value(field.Key);
  var key=(selected,field.Key);
  float inputWidth=Math.Min(124,width*.3f),inputX=left+width-inputWidth-22;
  InterfaceMenu.Label(new Rect(left,top,inputX-left-8,29),field.Name,null,14);
  string raw=inputs.GetValueOrDefault(key,value.ToString("R",CultureInfo.InvariantCulture));
  string entered=InterfaceMenu.Field(new Rect(inputX,top,inputWidth,28),raw);
  InterfaceMenu.Label(new Rect(inputX+inputWidth+6,top,16,29),"×",InterfaceMenu.Accent,14);
  if(entered!=raw){
   inputs[key]=entered;
   notice="尚未保存。";
   if(AiDifficultyRules.TryParseMultiplier(entered,out value))SetMultiplier(field.Key,value);
   else value=Profile.Value(field.Key);
  }

  InterfaceMenu.Label(new Rect(left,top+30,width,22),field.Help,null,12);
  float sliderValue=Math.Clamp(value,field.Min,field.Max);
  float next=GUI.HorizontalSlider(new Rect(left+42,top+59,width-134,18),sliderValue,field.Min,field.Max);
  bool changed=Math.Abs(next-sliderValue)>.004f;
  if(changed)next=(float)Math.Round(next,2);
  if(InterfaceMenu.Button(new Rect(left,top+50,32,28),"−")){next=Math.Max(0,value-.05f);changed=true;}
  if(InterfaceMenu.Button(new Rect(left+width-84,top+50,32,28),"+")){next=value+.05f;changed=true;}
  if(InterfaceMenu.Button(new Rect(left+width-44,top+50,44,28),"1×")){next=1;changed=true;}
  if(changed){
   inputs.Remove(key);
   SetMultiplier(field.Key,next);
  }
 }
 internal static void Draw(Rect rect){
  if(draft==null)Load();float x=rect.x,y=rect.y,w=rect.width;
  InterfaceMenu.Label(new Rect(x,y,w,30),"AI 难度",InterfaceMenu.Accent,21);y+=38;
  if(InterfaceMenu.Button(new Rect(x,y,Math.Min(265,w*.48f),34),draft!.Enabled?"自定义 AI 难度 · 开启":"自定义 AI 难度 · 关闭",draft.Enabled))Change(()=>draft.Enabled=!draft.Enabled);
  InterfaceMenu.Label(new Rect(x+w*.5f,y,w*.5f,34),"保存后用于本局及后续战局",null,12);y+=46;
  var viewport=new Rect(x,y,w,Math.Max(40,rect.yMax-y-116));float width=w-34;
  int columns=width<620?3:4;int rows=(AiDifficultyRules.Groups.Length+1+columns-1)/columns;
  float height=rows*42+355+AiDifficultyRules.Fields.Length*90;
  scroll=GUI.BeginScrollView(viewport,scroll,new Rect(0,0,w-20,height));float top=8,left=4;
  if(Plugin.Self.IgnoreAI.Value){InterfaceMenu.Paragraph(new Rect(left,top,width,48),"“AI 忽略玩家”已开启，关闭后可体验交战难度。",InterfaceMenu.Accent,12);top+=58;}
  InterfaceMenu.Label(new Rect(left,top,width,26),"选择 AI 类别",null,15);top+=34;
  float cell=(width-(columns-1)*8)/columns;
  for(int i=0;i<=AiDifficultyRules.Groups.Length;i++){
   int index=i;string label=i==0?"全部 AI":AiDifficultyRules.Name(AiDifficultyRules.Groups[i-1]);
   if(InterfaceMenu.Button(new Rect(left+i%columns*(cell+8),top+i/columns*42,cell,34),label,selected==i)){selected=index;notice="";}
  }
  top+=rows*42+10;
  if(selected>0){
   if(InterfaceMenu.Button(new Rect(left,top,width,34),Inherited?"跟随全部 AI 设置":"此类别单独设置",!Inherited))Change(()=>{if(Inherited)EditableProfile();else draft.Groups.Remove(Group);ClearSelectedInputs();});top+=46;
  }
  InterfaceMenu.Paragraph(new Rect(left,top,width,48),"数值为原有难度的倍数，1.00× 保持原有表现。各类别可单独设置；未单独设置的类别跟随全部 AI。",null,12);top+=60;
  bool wasEnabled=GUI.enabled;
  InterfaceMenu.Label(new Rect(left,top,width,26),"难度预设",null,15);top+=34;
  float presetWidth=(width-32)/5;
  for(int i=0;i<presets.Length;i++){int level=i;if(InterfaceMenu.Button(new Rect(left+i*(presetWidth+8),top,presetWidth,34),presets[i]))Change(()=>{if(selected==0)draft.Overall=AiDifficultyRules.Preset(level);else draft.Groups[Group]=AiDifficultyRules.Preset(level);ClearSelectedInputs();});}top+=48;
  float half=(width-10)/2;
  if(InterfaceMenu.Button(new Rect(left,top,half,34),Profile.AllowGrenades?"手雷使用 · 原有":"手雷使用 · 禁用",Profile.AllowGrenades))Change(()=>EditableProfile().AllowGrenades=!Profile.AllowGrenades);
  if(InterfaceMenu.Button(new Rect(left+half+10,top,half,34),Profile.AllowHeadshots?"瞄头倾向 · 原有":"瞄头倾向 · 降低",!Profile.AllowHeadshots))Change(()=>EditableProfile().AllowHeadshots=!Profile.AllowHeadshots);top+=48;
  foreach(var field in AiDifficultyRules.Fields){
   DrawMultiplier(field,left,top,width);
   top+=90;
  }
  GUI.enabled=wasEnabled;GUI.EndScrollView();
  float bottom=rect.yMax-104,button=(w-20)/3;
  GUI.enabled=wasEnabled&&!unreadable;
  if(InterfaceMenu.Button(new Rect(x,bottom,button,34),"保存难度设置"))Save();
  GUI.enabled=wasEnabled;
  if(InterfaceMenu.Button(new Rect(x+button+10,bottom,button,34),"重新读取"))Load();
  if(InterfaceMenu.Button(new Rect(x+2*(button+10),bottom,button,34),"恢复全部默认")){draft=new();inputs.Clear();unreadable=false;notice="已恢复原有难度，点击保存后生效。";}
  InterfaceMenu.Paragraph(new Rect(x,bottom+44,w,54),notice.Length>0?notice:AiDifficultyControl.Error.Length>0?AiDifficultyControl.Error:"关闭自定义难度并保存，可恢复 AI 原有表现。",InterfaceMenu.Accent,12);
 }
}
