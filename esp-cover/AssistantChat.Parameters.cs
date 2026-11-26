using System;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class RaidChat {
 static Vector2 chipScroll;static ChatNamedArguments.Chip? editingChip;static string chipValue="",chipSource="";
 static float DrawParameterChips(Rect area){
  if(!ChatCommandSyntax.IsCommand(input))return 0;var chips=ChatNamedArguments.Chips(input);if(chips.Length==0){editingChip=null;return 0;}float full=0;foreach(var c in chips)full+=Math.Clamp((ChatNamedArguments.Describe(c.Key).Label+"："+c.Value).Length*13+24,105,270);
  var viewport=new Rect(area.x,area.yMax-36,area.width,34);chipScroll=GUI.BeginScrollView(viewport,chipScroll,new Rect(0,0,Math.Max(full,area.width-20),25));try{float x=0;foreach(var c in chips){string label=ChatNamedArguments.Describe(c.Key).Label+"："+c.Value;float w=Math.Clamp(label.Length*13+24,105,270);var r=new Rect(x,0,w-7,25);InterfaceMenu.Surface(r,new Color(accent.r,accent.g,accent.b,.12f),5);Label(new Rect(x+8,3,w-22,20),label,captionStyle!,accent);if(GUI.Button(r,GUIContent.none,GUIStyle.none)){editingChip=c;chipValue=c.Value;chipSource=input;GUI.FocusControl(null);}x+=w;}}finally{GUI.EndScrollView();}return 42;
 }
 static void DrawParameterPopup(Rect bounds){if(editingChip is not {} c)return;if(chipSource!=input){editingChip=null;return;}var r=new Rect(bounds.x+16,Math.Max(bounds.y+50,bounds.yMax-206),Math.Min(460,bounds.width-32),126);Card(r,new Color(.055f,.077f,.09f,.99f));Label(new Rect(r.x+12,r.y+9,r.width-24,22),ChatNamedArguments.Describe(c.Key).Label+" · "+c.Key,captionStyle!,accent);GUI.SetNextControlName("ChatParameterValue");chipValue=GUI.TextField(new Rect(r.x+12,r.y+37,r.width-24,30),chipValue,2048,field);float w=(r.width-40)/3;
  if(InterfaceMenu.Button(new Rect(r.x+12,r.y+81,w,30),"应用")){string token=ChatCommandSyntax.Quote(c.Key+"="+chipValue);ReplaceInput(input[..c.Start]+token+input[c.End..],c.Start+token.Length);completion.Reset();editingChip=null;focus=true;}
  if(InterfaceMenu.Button(new Rect(r.x+20+w,r.y+81,w,30),"移除")){string value=input.Remove(c.Start,c.End-c.Start).TrimEnd();ReplaceInput(value,Math.Min(c.Start,value.Length));completion.Reset();editingChip=null;focus=true;}
  if(InterfaceMenu.Button(new Rect(r.x+28+w*2,r.y+81,w,30),"取消")){editingChip=null;focus=true;}
 }
}
