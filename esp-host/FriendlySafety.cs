using System;
using System.Diagnostics;
using System.IO;
using AssistantSafetyUi;
using UnityEngine;
namespace LocalSpt5Esp.HotReload;
public sealed partial class RuntimeReloader {
 void DrawFriendlySafety(){bool working=sequence!=null||persistence!=null||rollbackPreparing!=null;var view=new SafetyPresentation{Failure=failure,Details=fatalDetails,Phase=phase,Recovery=recoveryStatus,Simulation=simulation,ReloadFailure=reloadFault,CleanupUnsafe=cleanupUnsafe,Working=working,RecoveryBusy=RecoveryBusy,CanRollback=!working&&!RecoveryBusy&&!cleanupUnsafe&&(lastGood!=null||previousGood!=null),ExportStatus=Diagnostics.ExportStatus,CanOpenLogs=Diagnostics.LastExport.Length>0};Color accent=new(.47f,.85f,.78f);if(Config.TryGetEntry<string>("24. Interface","Panel accent",out var configured)&&ColorUtility.TryParseHtmlString(configured.Value,out var selected))accent=selected;SafetyScreen.Draw(view,accent,SafetyActionSelected);}
 void SafetyActionSelected(SafetyAction action){switch(action){case SafetyAction.Export:Diagnostics.BeginExport();break;case SafetyAction.Rollback:RequestRollback();break;case SafetyAction.Restart:CrashWatchClient.RequestRestart();Application.Quit();break;case SafetyAction.Close:CrashWatchClient.MarkDismissed();Application.Quit();break;case SafetyAction.Continue:failure=fatalDetails="";simulation=reloadFault=false;CrashWatchClient.ClearFatal();EndPause();break;case SafetyAction.OpenLogs:if(File.Exists(Diagnostics.LastExport))Process.Start(new ProcessStartInfo(Path.GetDirectoryName(Diagnostics.LastExport)!){UseShellExecute=true});break;}}
}
