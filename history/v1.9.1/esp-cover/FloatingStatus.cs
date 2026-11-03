using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class FloatingStatus {
 static ConfigEntry<bool> show=null!,collapsed=null!;
 static ConfigEntry<float> opacity=null!;
 static readonly MenuInputGate gate=new();
 static bool dragging;static Vector2 offset;
 static Rect lastBounds;
 internal static bool Capturing=>gate.Blocked||(show!=null&&show.Value&&RaidClock.Active&&!InterfaceMenu.Open&&Cursor.visible&&Input.GetMouseButton(0)&&lastBounds.Contains(new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y)));
 internal static void Initialize(){show=FeatureConfig.Bind("32. 状态悬浮窗","Show","显示功能状态悬浮窗",true);collapsed=FeatureConfig.Bind("32. 状态悬浮窗","Collapsed","收起状态悬浮窗",false);opacity=FeatureConfig.Bind("32. 状态悬浮窗","Opacity","状态窗不透明度",.94f,"",new AcceptableValueRange<float>(.2f,1));}
 internal static void Update(){gate.Tick(Time.frameCount,Input.GetMouseButton(0)||Input.GetMouseButton(1));if(!RaidClock.Active||!show.Value||InterfaceMenu.Open){dragging=false;if(gate.Open)gate.SetOpen(false,Time.frameCount);}}
 static bool Flag(string section,string key){return Plugin.Self.Config.TryGetEntry<bool>(section,key,out var entry)&&entry.Value;}
 internal static void Draw(){if(show==null||!show.Value||!RaidClock.Active||InterfaceMenu.Editing)return;var rect=InterfaceMenu.Place("status",new Rect(Math.Max(12,Screen.width-622),12,610,collapsed.Value?43:132));
  lastBounds=rect;var e=Event.current;bool interactive=Cursor.visible&&!InterfaceMenu.Open;var header=new Rect(rect.x,rect.y,rect.width-80,39);
  if(interactive&&e.type==EventType.MouseDown&&e.button==0&&rect.Contains(e.mousePosition)){gate.SetOpen(true,Time.frameCount);if(header.Contains(e.mousePosition)){dragging=true;offset=e.mousePosition-rect.position;} }
  if(interactive&&e.type==EventType.MouseDrag&&dragging){InterfaceMenu.MoveSlot("status",e.mousePosition-offset,rect);e.Use();}
  if(e.type==EventType.MouseUp&&gate.Open){dragging=false;gate.SetOpen(false,Time.frameCount);InterfaceMenu.Save();}
  var bg=InterfaceMenu.Background;bg.a=opacity.Value;InterfaceMenu.Surface(new Rect(rect.x+2,rect.y+5,rect.width,rect.height),new Color(0,0,0,.22f),12);InterfaceMenu.Surface(rect,bg,12);
  MenuIcons.Draw(new Rect(rect.x+13,rect.y+11,19,19),"战局",InterfaceMenu.Accent);InterfaceMenu.Label(new Rect(rect.x+43,rect.y+6,180,28),"功能状态",InterfaceMenu.Text,14);InterfaceMenu.Label(new Rect(rect.x+130,rect.y+7,rect.width-220,26),"SPT  /  "+RaidClock.GameTime,Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.4f),11);
  var control=new Rect(rect.xMax-65,rect.y+7,52,28);if(interactive){if(InterfaceMenu.Button(control,collapsed.Value?"展开":"收起"))collapsed.Value=!collapsed.Value;}else InterfaceMenu.Label(control,collapsed.Value?"＋":"－",InterfaceMenu.Accent,16);
  if(!collapsed.Value){var p=Plugin.Self;var states=new List<(string Name,bool On)>{("人物透视",p.People.Value),("物品透视",p.Loot.Value),("无敌",p.God.Value),("敌人忽略",p.IgnoreAI.Value),("辅助瞄准",AimEnabled()),("无后座",Flag("10. Recoil assist","No recoil")),("夜视",Flag("16. Night vision","Enabled")),("超时保护",RaidClock.PreventTimeout.Value),("无限弹药",Flag("28. 武器便利","Infinite ammunition")),("无限体力",Flag("20. Movement","Unlimited leg stamina")),("人物加速",Flag("20. Movement","Speed enabled")),("手雷预警",Flag("30. 手雷预警","Enabled"))};float w=(rect.width-24)/4;for(int i=0;i<states.Count;i++){var item=states[i];var cell=new Rect(rect.x+12+(i%4)*w,rect.y+44+(i/4)*27,w-7,23);InterfaceMenu.Surface(cell,item.On?new Color(InterfaceMenu.Accent.r,InterfaceMenu.Accent.g,InterfaceMenu.Accent.b,.075f):new Color(1,1,1,.025f),5);var dot=item.On?InterfaceMenu.Accent:Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.7f);InterfaceMenu.Surface(new Rect(cell.x+8,cell.y+9,5,5),dot,2.5f);InterfaceMenu.Label(new Rect(cell.x+20,cell.y,cell.width-24,23),item.Name,item.On?InterfaceMenu.Text:Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.5f),11);}}
 }
 static bool AimEnabled()=>Plugin.Self.Config.TryGetEntry<AimMode>("8. Aim assist","Mode",out var mode)&&mode.Value!=AimMode.Off;
 internal static void DrawSettings(Rect rect){InterfaceMenu.Label(new Rect(rect.x,rect.y,rect.width,28),"功能状态悬浮窗",InterfaceMenu.Accent,16);if(InterfaceMenu.Button(new Rect(rect.x,rect.y+42,170,32),show.Value?"状态窗 · 显示":"状态窗 · 隐藏",show.Value))show.Value=!show.Value;if(InterfaceMenu.Button(new Rect(rect.x+182,rect.y+42,170,32),collapsed.Value?"默认收起":"默认展开",collapsed.Value))collapsed.Value=!collapsed.Value;InterfaceMenu.Label(new Rect(rect.x,rect.y+88,170,24),"不透明度 "+opacity.Value.ToString("P0"));opacity.Value=GUI.HorizontalSlider(new Rect(rect.x+182,rect.y+96,Math.Max(60,rect.width-182),20),opacity.Value,.2f,1);InterfaceMenu.Paragraph(new Rect(rect.x,rect.y+123,rect.width,40),"鼠标可见时拖动标题栏调整位置；也可点击菜单顶部「布局」统一调整。",null,12);}
 internal static void Shutdown(){dragging=false;gate.Reset();}
}
