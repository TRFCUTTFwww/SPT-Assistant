using System;
using System.Diagnostics;
using Comfort.Common;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp.HotReload;

internal sealed class ReloadPause:IDisposable {
 readonly float scale=Time.timeScale;
 readonly bool audio=AudioListener.pause,background=Application.runInBackground;
 readonly CursorLockMode cursor=Cursor.lockState;
 readonly bool cursorVisible=Cursor.visible;
 readonly long started=Stopwatch.GetTimestamp();
 readonly AbstractGame? game=Singleton<AbstractGame>.Instance;
 readonly GameTimer? timer;
 readonly Il2CppSystem.Nullable<Il2CppSystem.DateTime>? start,escape;
 bool disposed;
 internal ReloadPause(){timer=game?.GameTimer;if(timer!=null){start=timer._start;escape=timer._escape;}Time.timeScale=0;AudioListener.pause=true;Application.runInBackground=true;Tick();}
 internal void Tick(){
  Time.timeScale=0;Input.ResetInputAxes();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
  // The raid deadline uses wall time, not Unity's time scale. Preserve elapsed
  // raid time as well; never apply an old timer snapshot to a replacement game.
  if(game==null||timer==null||Singleton<AbstractGame>.Instance?.Pointer!=game.Pointer||game.GameTimer?.Pointer!=timer.Pointer||start?.HasValue!=true)return;
  double seconds=Stopwatch.GetElapsedTime(started).TotalSeconds;
  timer._start=new(start.Value.AddSeconds(seconds));if(escape?.HasValue==true)timer._escape=new(escape.Value.AddSeconds(seconds));
 }
 public void Dispose(){if(disposed)return;try{Tick();}finally{disposed=true;Time.timeScale=scale;AudioListener.pause=audio;Application.runInBackground=background;Cursor.lockState=cursor;Cursor.visible=cursorVisible;}}
}
