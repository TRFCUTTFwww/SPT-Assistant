using System;
using System.Reflection;
using UnityEngine;
namespace LocalSpt5Esp;
// Optional host API keeps the feature module loadable during a staged upgrade.
internal static class DiagnosticsPanel {
 static object? host;static MethodInfo? settings,option,export,simulate,snapshot;static PropertyInfo? exportStatus;static float next;static Vector2 scroll;static string notice="";static string[] values=Array.Empty<string>();
 internal static bool DetailedTiming{get;private set;}
 internal static bool PhysicsTracing{get;private set;}
 internal static void Initialize(object services){host=services;var type=services.GetType();settings=type.GetMethod("DiagnosticSettings");option=type.GetMethod("SetDiagnosticOption");export=type.GetMethod("ExportLogs");simulate=type.GetMethod("SimulateError");snapshot=type.GetMethod("SetPerformanceSnapshot");exportStatus=type.GetProperty("DiagnosticExportStatus");Refresh();}
 static void Refresh(){values=settings?.Invoke(host,null) as string[]??Array.Empty<string>();DetailedTiming=Flag(5);PhysicsTracing=Flag(7);}
 static bool Flag(int index)=>index<values.Length&&bool.TryParse(values[index],out bool flag)&&flag;
 internal static void Tick(){if(Time.unscaledTime<next)return;next=Time.unscaledTime+1;Refresh();}
 internal static void Performance(string json)=>snapshot?.Invoke(host,new object[]{json});
 static void Set(string key,string value){if(option==null)throw new ArgumentException("错误保护宿主需要更新并重启游戏。");option.Invoke(host,new object[]{key,value});Refresh();}
 static void Toggle(Rect r,int index,string key,string label){bool on=Flag(index);if(InterfaceMenu.Button(r,I18n.Text(label)+" · "+I18n.Text(on?"开启":"关闭"),on))Run(()=>Set(key,(!on).ToString()));}
 static void Run(Action action){try{action();notice="";}catch(Exception e){notice=e.GetBaseException().Message;}}
 internal static void DrawLogs(Rect r){scroll=GUI.BeginScrollView(r,scroll,new Rect(0,0,r.width-22,800));try{float w=r.width-30,y=0;InterfaceMenu.Label(new(0,y,w,30),"日志与诊断",InterfaceMenu.Accent,20);y+=44;
  InterfaceMenu.Paragraph(new(0,y,w,48),"按最低等级记录和导出日志。性能记录用于定位辅助功能的 CPU 开销；游戏、驱动或其他 Mod 的耗时需要结合其日志判断。",null,13);y+=65;
  string[] levels={"ALL","TRACE","DEBUG","INFO","WARNING","ERROR","FATAL","OFF"};float cell=(w-24)/4;for(int i=0;i<levels.Length;i++){string level=levels[i];if(InterfaceMenu.Button(new(i%4*(cell+8),y+i/4*40,cell,32),level,values.Length>0&&values[0]==level))Run(()=>Set("level",level));}y+=96;
  string[] keys={"hardware","performance","spt","game","timing","physics"},labels={"附加系统硬件信息","附加性能信息","附加 SPT 服务端日志","附加游戏及 BepInEx 日志","详细功能耗时","物理警告调用栈采集"};int[] indexes={1,2,3,4,5,7};for(int i=0;i<keys.Length;i++){Toggle(new(0,y,w,38),indexes[i],keys[i],labels[i]);y+=48;}
  InterfaceMenu.Paragraph(new(0,y,w,48),"物理警告调用栈默认关闭，开启后可能增加开销。导出包会过滤聊天轮询，脱敏常见令牌和密码，不包含配置、存档或认证文件。",null,12);y+=64;
  if(InterfaceMenu.Button(new(0,y,220,40),"导出日志 ZIP"))Run(()=>export?.Invoke(host,null));y+=56;InterfaceMenu.Paragraph(new(0,y,w,100),notice+"\n"+(exportStatus?.GetValue(host) as string??"宿主更新后可用"),null,13);
 }finally{GUI.EndScrollView();}}
 internal static void DrawDebug(Rect r){
  scroll=GUI.BeginScrollView(r,scroll,new Rect(0,0,r.width-22,730));
  try{float w=r.width-30,y=0;InterfaceMenu.Label(new(0,y,w,30),"错误处理测试",InterfaceMenu.Accent,20);y+=40;
   InterfaceMenu.Paragraph(new(0,y,w,56),"用于检查错误上报、保护界面与返回流程。测试不会结束战局；真实崩溃能否被接住，取决于故障发生的位置和进程状态。",null,13);y+=68;
   Toggle(new(0,y,w,34),6,"debug","启用错误测试");y+=48;
   string[] labels={"对象空引用","数组越界","热更新加载失败","栈溢出报告 · 仅界面演练","内存访问错误报告 · 仅界面演练"};
   string[] help={"对象已销毁、初始化未完成或第三方 Mod 返回空值，都可能引发。此按钮在托管测试代码中实际触发空引用。","列表刷新后继续使用旧索引、空数组访问等情况可能引发。此按钮在托管测试代码中实际触发越界。","模块初始化、依赖或卸载失败时可能发生。此按钮演练失败界面，不加载损坏模块。","真实无限递归可能耗尽线程栈并直接终止游戏。这里仅提交同类报告，无法验证真实栈溢出的恢复能力。","原生插件或 IL2CPP 指针失效可能导致进程直接退出。这里不执行非法内存访问，也不验证内存损坏后的恢复能力。"};
   string[] types={"null","bounds","reload","stack","access"};
   for(int i=0;i<types.Length;i++){string type=types[i];InterfaceMenu.Surface(new(0,y,w,104),Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Accent,.04f),6);
    bool old=GUI.enabled;GUI.enabled=old&&Flag(6)&&simulate!=null;
    if(InterfaceMenu.Button(new(10,y+10,w-20,30),labels[i]))Run(()=>simulate?.Invoke(host,new object[]{type}));
    GUI.enabled=old;InterfaceMenu.Paragraph(new(12,y+48,w-24,48),help[i],null,12);y+=116;
   }
   InterfaceMenu.Paragraph(new(0,y,w,50),notice,new Color(1,.7f,.3f),13);
  }finally{GUI.EndScrollView();}
 }
 internal static void Command(string name,string[] args){if(name=="nexttime"){if(args.Length!=1)throw new ArgumentException("/nexttime HH:MM:SS|off");if(args[0]=="off")RaidStartClock.Disable();else RaidStartClock.Save(args[0]);RaidChat.Output("已保存下次行动的地图游戏时刻设置。");return;}if(name=="logs"){if(args.Length==1&&args[0]=="export"){export?.Invoke(host,null);RaidChat.Output("日志导出已开始，可在设置 → 日志与诊断查看结果。");return;}if(args.Length==2&&args[0]=="level"){Set("level",args[1].ToUpperInvariant());RaidChat.Output("日志等级已修改。");return;}throw new ArgumentException("/logs export | level ALL|TRACE|DEBUG|INFO|WARNING|ERROR|FATAL|OFF");}if(args.Length!=1)throw new ArgumentException("/debug on|off|null|bounds|stack|access|reload");if(args[0] is "on" or "off"){Set("debug",(args[0]=="on").ToString());return;}if(!Flag(6))throw new ArgumentException("请先开启调试总开关。");simulate?.Invoke(host,new object[]{args[0]});}
 internal static void Reset(){host=null;values=Array.Empty<string>();DetailedTiming=PhysicsTracing=false;}
}
