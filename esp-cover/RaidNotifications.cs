using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.MovingPlatforms;
using EFT.Vehicle;
using HarmonyLib;
using UnityEngine;

namespace LocalSpt5Esp;

internal enum RaidEventKind { BtrStarted,BtrArrived,BtrLeft,PlaneArrived,AirdropReleased,BossKilled,TrainArrived,TrainStarted }

internal static class RaidNotifications {
 internal const string Slot="event-notices";
 const string Title="事件通知";
 const string Section="72. 事件通知";
 static readonly (string Key,string Name)[] kinds={
  ("BTR start","BTR 发车"),("BTR arrival","BTR 到站"),("BTR leaving","BTR 离站"),
  ("Plane arrival","空投飞机到来"),("Airdrop release","空投已投下"),
  ("Boss death","BOSS 被击杀"),("Train arrival","装甲列车到站"),("Train departure","装甲列车发车")
 };
 static readonly ConfigEntry<bool>[] popups=new ConfigEntry<bool>[kinds.Length];
 static readonly ConfigEntry<bool>[] reminders=new ConfigEntry<bool>[kinds.Length];
 static ConfigEntry<bool> enabled=null!;
 static ConfigEntry<float> duration=null!;
 sealed record Notice(RaidEventKind Kind,string Text,float Until);
 static readonly List<Notice> notices=new();
 static readonly HashSet<(RaidEventKind Kind,string Id)> seen=new();
 static readonly BtrEventTracker btrEvents=new();
 static IntPtr world,btrPointer;
 static float nextBtrScan,nextError;
 static Vector2 scroll;

 internal static IntPtr VisibleRaid=>world!=IntPtr.Zero&&enabled?.Value==true&&DirectorStage.ModNotifications&&RaidClock.Active?world:IntPtr.Zero;

 internal static void Initialize(Plugin plugin){
  enabled=FeatureConfig.Bind(Section,"Enabled",Title,true);
  duration=FeatureConfig.Bind(Section,"Display duration","通知时长（秒）",6f,"",new AcceptableValueRange<float>(1,120));
  for(int i=0;i<kinds.Length;i++){
   var (key,name)=kinds[i];
   popups[i]=FeatureConfig.Bind(Section,key+" popup",name+" · 弹出",true);
   reminders[i]=FeatureConfig.Bind(Section,key+" reminder",name+" · 提醒",false);
  }
  try{
   var method=AccessTools.DeclaredMethod(typeof(Locomotive),nameof(Locomotive.TravelStateChanged),new[]{typeof(Locomotive.ETravelState)})??throw new MissingMethodException(nameof(Locomotive.TravelStateChanged));
   plugin.Patches.Patch(method,postfix:new HarmonyMethod(typeof(RaidNotifications),nameof(TrainTravelChanged)));
   plugin.Log.LogInfo("Raid notifications: native train travel hook ready");
  }catch(Exception e){plugin.Log.LogWarning("Train notification hook: "+e.Message);}
 }

 internal static bool Wants(RaidEventKind kind)=>enabled?.Value==true&&DirectorStage.ModNotifications&&
  (popups[(int)kind].Value||reminders[(int)kind].Value);
 static bool WantsBtr=>Wants(RaidEventKind.BtrStarted)||Wants(RaidEventKind.BtrArrived)||Wants(RaidEventKind.BtrLeft);

 internal static void Publish(RaidEventKind kind,string detail="",string? identity=null){
  if(!Wants(kind)||!RaidClock.Active||Singleton<GameWorld>.Instance is not {} current)return;
  BindWorld(current.Pointer);
  if(identity!=null&&!seen.Add((kind,identity)))return;
  string text=I18n.Text(kinds[(int)kind].Name);
  if(detail.Length>0)text+=" · "+detail;
  float seconds=ExpiryRules.Lifetime(duration.Value);
  if(popups[(int)kind].Value){
   notices.Add(new(kind,text,Time.unscaledTime+seconds));
   if(notices.Count>12)notices.RemoveAt(0);
  }
  if(reminders[(int)kind].Value)FeatureToasts.ShowRaidEvent(text,seconds,world);
 }

 internal static void BossKilled(Player victim){
  if(!Wants(RaidEventKind.BossKilled)||!victim.IsAI||victim.Profile?.Info is not {} info)return;
  string role=info.Settings?.Role.ToString()??"";
  if(RolePalette.Classify(role,victim.Profile.Side==EPlayerSide.Savage)!=CharacterKind.Boss&&
   !AiSpawnCatalog.Options.Any(option=>option.Boss&&option.Role.Equals(role,StringComparison.OrdinalIgnoreCase)))return;
  string name=BossLabels.DisplayName(victim).Replace('\r',' ').Replace('\n',' ');
  Publish(RaidEventKind.BossKilled,name,victim.ProfileId);
 }

 static void TrainTravelChanged(Locomotive __instance,Locomotive.ETravelState __0){
  try{
   if(__0==Locomotive.ETravelState.Arrived)Publish(RaidEventKind.TrainArrived,identity:__instance.Pointer.ToString());
   else if(__0 is Locomotive.ETravelState.Departing or Locomotive.ETravelState.OnRouteBack)
    Publish(RaidEventKind.TrainStarted,identity:__instance.Pointer.ToString());
  }catch(Exception e){Warn(e);}
 }

 internal static void BtrArrived(BTRVehicle? vehicle,string stop){
  if(!WantsBtr||!RaidClock.Active||BtrControl.Disabled||vehicle==null||
   Singleton<GameWorld>.Instance is not {} current||vehicle.VehicleRouteState==EVehicleRouteState.OnDepot)return;
  BindWorld(current.Pointer);
  BindBtr(vehicle.Pointer);
  var position=vehicle.transform.position;
  if(btrEvents.Arrive(stop,position.x,position.z))
   PublishBtr(current,RaidEventKind.BtrArrived,stop);
 }

 internal static void Tick(GameWorld? current){
  BindWorld(current?.Pointer??IntPtr.Zero);
  if(current==null||enabled?.Value!=true){notices.Clear();return;}
  notices.RemoveAll(n=>n.Until<=Time.unscaledTime||!popups[(int)n.Kind].Value);
  if(!WantsBtr||BtrControl.Disabled){BindBtr(IntPtr.Zero);return;}
  if(Time.unscaledTime<nextBtrScan)return;
  nextBtrScan=Time.unscaledTime+.25f;
  try{
   var vehicle=current.BtrController?.BtrVehicle;
   if(vehicle==null){BindBtr(IntPtr.Zero);return;}
   BindBtr(vehicle.Pointer);
   var position=vehicle.transform.position;
   bool depot=vehicle.VehicleRouteState==EVehicleRouteState.OnDepot;
   bool stopped=vehicle.BtrState is EBtrState.Stop or EBtrState.StopPaid;
   bool moving=vehicle.BtrState==EBtrState.Running&&vehicle._moveByPath&&!vehicle._pauseOn;
   var result=btrEvents.Observe(depot,stopped,moving,vehicle._currentPathDestination?.id??"",position.x,position.z);
   if(result.Arrived)PublishBtr(current,RaidEventKind.BtrArrived,result.Stop);
   if(result.Started)PublishBtr(current,RaidEventKind.BtrStarted,result.Stop);
   if(result.Left)PublishBtr(current,RaidEventKind.BtrLeft,result.Stop);
  }catch(Exception e){Warn(e);}
 }

 static void PublishBtr(GameWorld current,RaidEventKind kind,string stop){
  if(Wants(kind))Publish(kind,stop.Length>0?BtrDisplay.StopName(current,stop):"");
 }

 static void BindWorld(IntPtr pointer){
  if(world==pointer)return;
  Reset();
  world=pointer;
 }

 static void BindBtr(IntPtr pointer){
  if(btrPointer==pointer)return;
  btrPointer=pointer;
  btrEvents.Reset();
 }

 internal static void Reset(){
  notices.Clear();
  seen.Clear();
  world=btrPointer=IntPtr.Zero;
  nextBtrScan=nextError=0;
  btrEvents.Reset();
 }

 static void Warn(Exception e){
  if(Time.unscaledTime<nextError)return;
  nextError=Time.unscaledTime+10;
  Plugin.Self.Log.LogWarning("Raid event: "+e.Message);
 }

 const float HeaderHeight=33, Footer=9, TextMargin=37;
 static float MaxWidth=>Math.Max(1,Math.Min(480,Screen.width-24));

 internal static Rect DefaultBounds(){
  float width=Math.Min(230,MaxWidth);
  return Bounds(width,HeaderHeight+24+Footer);
 }

 static Rect Bounds(float width,float height)=>new((Screen.width-width)/2,76,width,Math.Min(height,Screen.height));

 internal static void Draw(){
  if(notices.Count==0||VisibleRaid==IntPtr.Zero||InterfaceMenu.Editing||Event.current.type!=EventType.Repaint)return;
  using var opacity=InterfaceMenu.BeginWindow(Slot);
  float width=InterfaceMenu.TextWidth(I18n.Text(Title),14)+26;
  foreach(var notice in notices)width=Math.Max(width,InterfaceMenu.TextWidth(notice.Text,13)+TextMargin);
  width=Math.Clamp(MathF.Ceiling(width),Math.Min(166,MaxWidth),MaxWidth);

  float textWidth=Math.Max(1,width-TextMargin),height=HeaderHeight+Footer;
  Span<float> rowHeights=stackalloc float[notices.Count];
  int rows=0;
  for(int i=notices.Count-1;i>=0;i--){
   float row=Math.Max(24,MathF.Ceiling(InterfaceMenu.ParagraphHeight(notices[i].Text,textWidth,13))+6);
   if(rows>0&&height+row>Screen.height)break;
   rowHeights[rows++]=row;
   height+=row;
  }

  var rect=InterfaceMenu.Place(Slot,Bounds(width,height));
  InterfaceMenu.Panel(rect);
  InterfaceMenu.Label(new Rect(rect.x+13,rect.y+4,rect.width-26,28),Title,InterfaceMenu.Accent,14);
  GUI.BeginClip(rect);
  try{
   float y=HeaderHeight;
   for(int i=0;i<rows;i++){
    float row=rowHeights[i];
    InterfaceMenu.Surface(new Rect(14,y+10,4,4),InterfaceMenu.Accent,2);
    InterfaceMenu.Paragraph(new Rect(26,y+3,textWidth,row-6),notices[notices.Count-1-i].Text,null,13);
    y+=row;
   }
  }finally{GUI.EndClip();}
 }

 internal static void DrawSettings(Rect rect){
  const int rowHeight=40;
  scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,Math.Max(1,rect.width-20),Math.Max(rect.height-1,224+kinds.Length*rowHeight)));
  try{
   float width=Math.Max(1,rect.width-34);
   InterfaceMenu.DrawEntry(enabled,new Rect(4,0,width,82));
   InterfaceMenu.DrawEntry(duration,new Rect(4,92,width,82));
   float column=Math.Min(138,width*.23f),popupX=width-2*column+4,reminderX=width-column+4;
   InterfaceMenu.Label(new Rect(12,184,popupX-16,30),"事件",InterfaceMenu.Accent,13);
   InterfaceMenu.Label(new Rect(popupX+10,184,column-12,30),"弹出",InterfaceMenu.Accent,13);
   InterfaceMenu.Label(new Rect(reminderX+10,184,column-12,30),"提醒",InterfaceMenu.Accent,13);
   for(int i=0;i<kinds.Length;i++){
    float y=216+i*rowHeight;
    InterfaceMenu.Surface(new Rect(4,y,width,36),Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.035f),7);
    InterfaceMenu.Label(new Rect(12,y,popupX-16,36),kinds[i].Name,null,13);
    Toggle(popups[i],new Rect(popupX+4,y+4,column-8,28));
    Toggle(reminders[i],new Rect(reminderX+4,y+4,column-8,28));
   }
  }finally{GUI.EndScrollView();}
 }

 static void Toggle(ConfigEntry<bool> entry,Rect rect){
  if(InterfaceMenu.Button(rect,entry.Value?"●  已开启":"○  已关闭",entry.Value))entry.Value=!entry.Value;
 }
}
