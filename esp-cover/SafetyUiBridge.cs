using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using AssistantSafetyUi;
using HarmonyLib;
using LocalSpt5Esp.HotReload;
using UnityEngine;
namespace LocalSpt5Esp;
// A managed compatibility patch lets an already-running 1.6.0 host use the
// new screen. Future hosts render the same shared screen without this patch.
internal static class SafetyUiBridge {
 static RuntimeReloader? runtime;static Type? type;static float nextError;
 internal static void Initialize(object services,Harmony harmony){if(services is not RuntimeReloader host)return;runtime=host;type=host.GetType();if(type.GetProperty("FriendlyErrorUi")?.GetValue(host) is true)return;var draw=type.GetMethod("DrawSafety",BindingFlags.Instance|BindingFlags.NonPublic);if(draw!=null){var ui=new Harmony("local.spt5.esp.safety-ui");ui.UnpatchSelf();ui.Patch(draw,prefix:new HarmonyMethod(typeof(SafetyUiBridge),nameof(Draw)));}}
 static object? Get(string name)=>type?.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(runtime);
 static bool Flag(string name)=>Get(name) is true;
 static string Text(string name)=>Get(name) as string??"";
 static void Set(string name,object value)=>type?.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)?.SetValue(runtime,value);
 static void Invoke(string name)=>type?.GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(runtime,null);
 static void Watch(string method)=>type?.Assembly.GetType("LocalSpt5Esp.HotReload.CrashWatchClient")?.GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)?.Invoke(null,null);
 static bool Draw(){if(runtime==null)return true;try{if(Event.current.type==EventType.Repaint)Set("painted",(Get("painted") as int? ??0)+1);bool working=Get("sequence")!=null||Get("persistence")!=null||Get("rollbackPreparing")!=null;bool recoveryBusy=type?.GetProperty("RecoveryBusy",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(runtime) is true;var diagnostics=runtime.Diagnostics;var view=new SafetyPresentation{Failure=Text("failure"),Details=Text("fatalDetails"),Phase=Text("phase"),Recovery=Text("recoveryStatus"),Simulation=Flag("simulation"),ReloadFailure=Flag("reloadFault"),CleanupUnsafe=Flag("cleanupUnsafe"),Working=working,RecoveryBusy=recoveryBusy,CanRollback=!working&&!recoveryBusy&&!Flag("cleanupUnsafe")&&(Get("lastGood")!=null||Get("previousGood")!=null),ExportStatus=runtime.DiagnosticExportStatus,CanOpenLogs=diagnostics.LastExport.Length>0};Color accent=new(.47f,.85f,.78f);if(runtime.Config.TryGetEntry<string>("24. Interface","Panel accent",out var configured)&&ColorUtility.TryParseHtmlString(configured.Value,out var selected))accent=selected;SafetyScreen.Draw(view,accent,Action);return false;}catch(Exception e){if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+10;Plugin.Self?.Log.LogWarning("Friendly error UI fell back to the host screen: "+e.GetBaseException().Message);}return true;}}
 static void Action(SafetyAction action){if(runtime==null)return;switch(action){case SafetyAction.Export:runtime.ExportLogs();break;case SafetyAction.Rollback:runtime.RollbackPreviousVersion();break;case SafetyAction.Restart:Watch("RequestRestart");Application.Quit();break;case SafetyAction.Close:Watch("MarkDismissed");Application.Quit();break;case SafetyAction.Continue:Set("failure","");Set("fatalDetails","");Set("simulation",false);Set("reloadFault",false);Watch("ClearFatal");Invoke("EndPause");break;case SafetyAction.OpenLogs:string file=runtime.Diagnostics.LastExport;if(File.Exists(file))Process.Start(new ProcessStartInfo(Path.GetDirectoryName(file)!){UseShellExecute=true});break;}}
}
