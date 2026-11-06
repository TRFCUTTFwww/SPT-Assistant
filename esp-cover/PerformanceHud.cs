using System;
using BepInEx.Configuration;
using UnityEngine;
namespace LocalSpt5Esp;

internal static class PerformanceHud {
 static ConfigEntry<bool> enabled=null!;static ConfigEntry<float> refresh=null!,opacity=null!;
 static readonly PerformanceWindow window=new();
 static int lastFrame=-1;static float nextRefresh;static bool wasEnabled;
 static string headline="采样中…",frames="",stalls="",cpu="",parts="";static Color state=Color.white;
 internal static void Initialize(){const string section="58. 性能监测";
  enabled=FeatureConfig.Bind(section,"Performance monitor","显示性能监测",true,"显示最近约 5 秒的 FPS、帧时间及辅助 CPU 耗时；位置可通过菜单顶部「布局」调整。");
  refresh=FeatureConfig.Bind(section,"Monitor refresh seconds","性能窗刷新间隔（秒）",.5f,"仅影响文字刷新，逐帧采样保持不变。",new AcceptableValueRange<float>(.25f,2));
  opacity=FeatureConfig.Bind(section,"Monitor opacity","性能窗不透明度",.9f,"",new AcceptableValueRange<float>(.2f,1));
 }
 internal static void Frame(int frame,double milliseconds,PerformanceWindow.Sample sample){
  if(enabled==null||!enabled.Value){if(wasEnabled)Reset();return;}
  wasEnabled=true;if(frame==lastFrame)return;lastFrame=frame;
  window.Add(sample with{Frame=milliseconds});
  if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+refresh.Value;
  var s=window.Read();if(s.Count==0)return;
  headline=$"{s.Fps:F0} FPS   ·   {s.Frame:F1} ms / 帧";
  frames=$"P99 {s.P99:F1} ms     最慢 {s.Peak:F1} ms";
  stalls=$"卡顿帧（>50 ms） {s.Slow} / {s.Count}   ·   {s.Seconds:F1} 秒";
  cpu=$"辅助 CPU  {s.Mod:F2} ms / 帧";
  parts=$"更新 {s.Update+s.Late:F2}    界面 {s.Gui:F2}    镜内 {s.Render:F2} ms";

  state=s.Frame>33.34f?new Color(1,.4f,.3f):s.Frame>16.68f?new Color(1,.8f,.35f):InterfaceMenu.Accent;
 }
 internal static void Draw(){
  if(enabled==null||!enabled.Value||InterfaceMenu.Editing||Event.current.type!=EventType.Repaint)return;
  var r=InterfaceMenu.Place("performance",new Rect(12,Math.Max(12,Screen.height-240),354,165));var background=InterfaceMenu.Background;background.a=opacity.Value;
  InterfaceMenu.Surface(r,background,10);MenuIcons.Draw(new Rect(r.x+12,r.y+12,18,18),"界面",InterfaceMenu.Accent);
  InterfaceMenu.Label(new Rect(r.x+39,r.y+6,r.width-48,26),"性能监测 · 最近约 5 秒",InterfaceMenu.Text,12);
  InterfaceMenu.Label(new Rect(r.x+12,r.y+33,r.width-24,28),headline,state,18);
  InterfaceMenu.Label(new Rect(r.x+12,r.y+65,r.width-24,22),frames,InterfaceMenu.Text,12);
  InterfaceMenu.Label(new Rect(r.x+12,r.y+87,r.width-24,22),stalls,InterfaceMenu.Text,11);
  InterfaceMenu.Label(new Rect(r.x+12,r.y+113,r.width-24,22),cpu,InterfaceMenu.Accent,13);
  InterfaceMenu.Label(new Rect(r.x+12,r.y+136,r.width-24,22),parts,InterfaceMenu.Text,11);

 }
 internal static void Reset(){window.Clear();lastFrame=-1;nextRefresh=0;wasEnabled=false;headline="采样中…";frames=stalls=cpu=parts="";}
}
