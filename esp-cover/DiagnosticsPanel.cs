using System;
using System.Reflection;
using UnityEngine;
namespace LocalSpt5Esp;
// Optional host API keeps the feature module loadable during a staged upgrade.
internal static class DiagnosticsPanel {
 static object? host;static MethodInfo? settings,option,export,snapshot;static PropertyInfo? exportStatus;static float next;static Vector2 scroll;static string notice="";static string[] values=Array.Empty<string>();
 internal static bool DetailedTiming{get;private set;}
 internal static bool PhysicsTracing{get;private set;}
 internal static void Initialize(object services){host=services;var type=services.GetType();settings=type.GetMethod("DiagnosticSettings");option=type.GetMethod("SetDiagnosticOption");export=type.GetMethod("ExportLogs");snapshot=type.GetMethod("SetPerformanceSnapshot");exportStatus=type.GetProperty("DiagnosticExportStatus");Refresh();}
 static void Refresh(){values=settings?.Invoke(host,null) as string[]??Array.Empty<string>();DetailedTiming=Flag(5);PhysicsTracing=Flag(7);}
 static bool Flag(int index)=>index<values.Length&&bool.TryParse(values[index],out bool flag)&&flag;
 internal static void Tick(){if(Time.unscaledTime<next)return;next=Time.unscaledTime+1;Refresh();}
 internal static void Performance(string json)=>snapshot?.Invoke(host,new object[]{json});
 static void Set(string key,string value){if(option==null)throw new ArgumentException("日志宿主需要更新并重启游戏。");option.Invoke(host,new object[]{key,value});Refresh();}
 static void Toggle(Rect r,int index,string key,string label){bool on=Flag(index);if(InterfaceMenu.Button(r,I18n.Text(label)+" · "+I18n.Text(on?"开启":"关闭"),on))Run(()=>Set(key,(!on).ToString()));}
 static void Run(Action action){try{action();notice="";}catch(Exception e){notice=e.GetBaseException().Message;}}
 internal static void DrawLogs(Rect r){scroll=GUI.BeginScrollView(r,scroll,new Rect(0,0,r.width-22,640));try{float w=r.width-30,y=0;InterfaceMenu.Label(new(0,y,w,30),"日志与诊断",InterfaceMenu.Accent,20);y+=44;
  string[] levels={"ALL","TRACE","DEBUG","INFO","WARNING","ERROR","FATAL","OFF"};float cell=(w-24)/4;for(int i=0;i<levels.Length;i++){string level=levels[i];if(InterfaceMenu.Button(new(i%4*(cell+8),y+i/4*40,cell,32),level,values.Length>0&&values[0]==level))Run(()=>Set("level",level));}y+=96;
  string[] keys={"hardware","performance","spt","game","timing","physics"},labels={"附加系统硬件信息","附加性能信息","附加 SPT 服务端日志","附加游戏及 BepInEx 日志","详细功能耗时","物理警告调用栈采集"};int[] indexes={1,2,3,4,5,7};for(int i=0;i<keys.Length;i++){Toggle(new(0,y,w,38),indexes[i],keys[i],labels[i]);y+=48;}
  if(InterfaceMenu.Button(new(0,y+4,220,40),"导出日志 ZIP"))Run(()=>export?.Invoke(host,null));y+=60;InterfaceMenu.Paragraph(new(0,y,w,100),(notice+"\n"+(exportStatus?.GetValue(host) as string??"宿主更新后可用")).Trim(),null,13);
 }finally{GUI.EndScrollView();}}
 internal static void Command(string name,string[] args){if(name=="nexttime"){if(args.Length!=1)throw new ArgumentException("/nexttime HH:MM:SS|off");if(args[0]=="off")RaidStartClock.Disable();else RaidStartClock.Save(args[0]);RaidChat.Output("已保存下次行动的地图游戏时刻设置。");return;}if(name=="logs"){if(args.Length==1&&args[0]=="export"){export?.Invoke(host,null);RaidChat.Output("日志导出已开始，可在设置 → 日志与诊断查看结果。");return;}if(args.Length==2&&args[0]=="level"){Set("level",args[1].ToUpperInvariant());RaidChat.Output("日志等级已修改。");return;}throw new ArgumentException("/logs export | level ALL|TRACE|DEBUG|INFO|WARNING|ERROR|FATAL|OFF");}}
 internal static void Reset(){host=null;values=Array.Empty<string>();DetailedTiming=PhysicsTracing=false;}
}
