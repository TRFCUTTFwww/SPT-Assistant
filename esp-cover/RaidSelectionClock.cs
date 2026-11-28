using System;
using System.Diagnostics;
using EFT;
using EFT.UI.Matchmaker;
using EFT.UI.Screens;
using HarmonyLib;
using UnityEngine;
using NativeDateTime=Il2CppSystem.DateTime;

namespace LocalSpt5Esp;

internal static class RaidSelectionClock {
#if EFT_48558
 const string AnchorKey="LocalSpt5Esp.RaidSelectionClock.Anchor";
 const string StartKey="LocalSpt5Esp.RaidSelectionClock.Start";
 static MatchMakerSelectionLocationScreen? screen;
 static LocationConditionsPanel? panel;
 static (long Ticks,long Stamp,float Rate,bool Paused)? anchor;
 static float retryAt;
 static float nextWarning;
 static long displayedSecond=-1;
 static JsonType.EDateTime displayedPhase;
 static bool displayedBoth;
 static string? displayedLocation;
 static bool wasSelecting,localSession;

 internal static void Initialize(Plugin plugin){
  if(AppDomain.CurrentDomain.GetData(AnchorKey) is ValueTuple<long,long,float,bool> saved)anchor=saved;
  plugin.Patches.Patch(AccessTools.Method(typeof(LocationConditionsPanel),"Update"),
   prefix:new HarmonyMethod(typeof(RaidSelectionClock),nameof(BeforeConditionsUpdate)));
  plugin.Patches.Patch(AccessTools.DeclaredMethod(typeof(LocalGame),nameof(LocalGame.Create)),
   postfix:new HarmonyMethod(typeof(RaidSelectionClock),nameof(LocalGameCreated)));
 }

 static bool BeforeConditionsUpdate(LocationConditionsPanel __instance)=>
  panel==null||panel.Pointer!=__instance.Pointer||!anchor.HasValue;

 static bool FixedMap(string? id)=>id is "factory4_day" or "factory4_night" or "factory_event" or "icebreaker" or "Icebreaker";
 static NativeDateTime Phase(NativeDateTime current,JsonType.EDateTime phase)=>
  phase==JsonType.EDateTime.PAST?current.AddHours(-12):current;

 static void SetAnchor((long Ticks,long Stamp,float Rate,bool Paused)? value){
  anchor=value;
  AppDomain.CurrentDomain.SetData(AnchorKey,value);
  displayedSecond=-1;
 }

 static NativeDateTime ReadAnchor((long Ticks,long Stamp,float Rate,bool Paused) saved){
  var instant=new NativeDateTime(saved.Ticks);
  return saved.Paused?instant:instant.AddSeconds(Stopwatch.GetElapsedTime(saved.Stamp).TotalSeconds*saved.Rate);
 }

 static void StartPreview(MatchMakerSelectionLocationScreen selection,NativeDateTime time,bool paused){
  var rate=selection._session?.Acceleration??1;
  if(!float.IsFinite(rate)||rate<=0)rate=1;
  SetAnchor((time.Ticks,Stopwatch.GetTimestamp(),rate,paused));
 }

 internal static void ResetSelection()=>SetAnchor(null);

 internal static void LateUpdate(){
  try{
   bool selecting=EftScreenManager.Instance?.CurrentScreenController?.ScreenType==EEftScreenType.SelectLocation;
   if(!selecting){
    screen=null;
    panel=null;
    wasSelecting=false;
    retryAt=0;
    displayedSecond=-1;
    return;
   }
   if(!wasSelecting){wasSelecting=true;localSession=ChatFikaSession.Role==0;}
   if(!localSession)return;
   if(screen==null&&Time.unscaledTime>=retryAt){
    screen=UnityEngine.Object.FindObjectOfType<MatchMakerSelectionLocationScreen>();
    retryAt=Time.unscaledTime+1;
   }
   var selection=screen;
   var target=selection?._conditions;
   if(selection==null||target==null||!target.isActiveAndEnabled||!target._needTimerUpdate||
      selection.SelectedLocation==null||FixedMap(selection.SelectedLocation.Id)){
    panel=null;
    displayedSecond=-1;
    return;
   }
   if(panel==null||panel.Pointer!=target.Pointer)displayedSecond=-1;
   panel=target;
   if(anchor==null&&RaidStartClock.HasFixedTime){
    var configured=target.GetCurrentLocationTime.Date.AddSeconds(RaidStartClock.FixedSeconds());
    StartPreview(selection,target.SelectedDateTime==JsonType.EDateTime.PAST?configured.AddHours(12):configured,false);
   }
   if(anchor is not {} preview){displayedSecond=-1;return;}
   var current=ReadAnchor(preview);
   long second=current.Ticks/TimeSpan.TicksPerSecond;
   var phase=target.SelectedDateTime;
   bool both=target._takeFromCurrent;
   // Switching maps reuses this panel and writes native server time into its labels.
   // A paused clock has no new second to invalidate the previous map's display.
   string location=selection.SelectedLocation.Id;
   if(second==displayedSecond&&phase==displayedPhase&&both==displayedBoth&&location==displayedLocation)return;
   var first=both?current:Phase(current,phase);
   if(target._currentPhaseTime!=null)EFT.StringExtensions.SetMonospaceText(target._currentPhaseTime,first.ToString("HH:mm:ss"),true);
   if(both&&target._nextPhaseTime!=null)EFT.StringExtensions.SetMonospaceText(target._nextPhaseTime,Phase(current,JsonType.EDateTime.PAST).ToString("HH:mm:ss"),true);
   displayedSecond=second;
   displayedPhase=phase;
   displayedBoth=both;
   displayedLocation=location;
  }catch(Exception error){panel=null;Warn(error);}
 }

 internal static void Draw(){
  var selection=screen;
  var target=panel;
  if(selection==null||target==null||!target.isActiveAndEnabled||InterfaceMenu.Open)return;
  try{
   var first=target._currentPhaseTime;
   if(first==null||!first.isActiveAndEnabled||first.canvas==null)return;
   var canvas=first.canvas;
   var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
   var right=first.rectTransform;
   var second=target._nextPhaseTime;
   if(second!=null&&second.isActiveAndEnabled){
    if(RectTransformUtility.WorldToScreenPoint(camera,second.rectTransform.position).x>
       RectTransformUtility.WorldToScreenPoint(camera,right.position).x)right=second.rectTransform;
   }
   Vector2 edge=RectTransformUtility.WorldToScreenPoint(camera,right.TransformPoint(new Vector3(right.rect.xMax,right.rect.center.y)));
   float width=Mathf.Clamp(126*canvas.scaleFactor,106,185);
   float height=Mathf.Clamp(26*canvas.scaleFactor,24,34);
   float x=edge.x+10,y=Screen.height-edge.y-height*.5f+Mathf.Round(20*canvas.scaleFactor);
   if(x+width>Screen.width-8){x=Mathf.Clamp(edge.x-width,8,Screen.width-width-8);y+=height+5;}
   var button=new Rect(x,Mathf.Clamp(y,8,Screen.height-height-8),width,height);
   bool paused=anchor?.Paused==true;
   if(InterfaceMenu.Button(button,paused?"恢复选图时钟":"暂停选图时钟",paused))
    StartPreview(selection,anchor is {} preview?ReadAnchor(preview):target.GetCurrentLocationTime,!paused);
  }catch(Exception error){Warn(error);}
 }

 static void LocalGameCreated(LocalGame __result,JsonType.LocationSettings.Location __6,JsonType.EDateTime __9){
  try{
   AppDomain.CurrentDomain.SetData(StartKey,null);
   if(anchor is not {} saved||__result==null||__6==null||ChatFikaSession.Role!=0)return;
   SetAnchor(null);
   if(FixedMap(__6.Id))return;
   long ticks=Phase(ReadAnchor(saved),__9).TimeOfDay.Ticks;
   AppDomain.CurrentDomain.SetData(StartKey,(__result.Pointer,ticks));
   // Seed the load-in lighting, then rebase at raid start so loading time
   // does not advance the selected clock before the player takes control.
   if(__result.GameDateTime is {} date)date.ResetForce(date.Calculate().Date.AddTicks(ticks));
  }catch(Exception error){Warn(error);}
 }

 internal static bool TryTakeStart(AbstractGame game,out long ticks,out bool waiting){
  ticks=0;
  waiting=false;
  if(AppDomain.CurrentDomain.GetData(StartKey) is not ValueTuple<IntPtr,long> start||start.Item1!=game.Pointer)return false;
  waiting=game.GameTimer?.Status!=GameTimer.EGameTimerStatus.Started&&game.Status!=GameStatus.Started;
  if(waiting)return false;
  AppDomain.CurrentDomain.SetData(StartKey,null);
  ticks=start.Item2;
  return true;
 }

 static void Warn(Exception error){
  if(Time.unscaledTime<nextWarning)return;
  nextWarning=Time.unscaledTime+15;
  Plugin.Self?.Log.LogWarning("Raid selection clock: "+error.Message);
 }
#else
 internal static void Initialize(Plugin plugin){}
 internal static void ResetSelection(){}
 internal static void LateUpdate(){}
 internal static void Draw(){}
 internal static bool TryTakeStart(AbstractGame game,out long ticks,out bool waiting){ticks=0;waiting=false;return false;}
#endif
}
