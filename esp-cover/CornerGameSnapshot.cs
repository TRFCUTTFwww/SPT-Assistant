using System;
using System.Collections.Generic;
using CommonAssets.Scripts.Game;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.Weather;
using UnityEngine;
using static LocalSpt5Esp.CornerGameData;
namespace LocalSpt5Esp;

internal static class CornerGameSnapshot {
 static readonly string[] partNames={"head","chest","stomach","leftArm","rightArm","leftLeg","rightLeg"};
 static int capturedFrame=-1;
 static CornerGameData? cached;

 internal static string CapturePrettyJson()=>FormatJson(ReadFrame().ToJson());

 internal static CornerGameData ReadFrame(){
  int frame=Time.frameCount;
  if(frame==capturedFrame&&cached!=null)return cached;
  long stamp=RuntimePerformance.Start();
  var snapshot=Capture();
  RuntimeCosts.Mark(ref stamp,CostStage.CornerSnapshot);
  capturedFrame=frame;
  return cached=snapshot;
 }

 static CornerGameData Capture(){
  bool inRaid=RaidClock.Active;
  bool inHideout=!inRaid&&Read(()=>CultistCircleControl.InHideout,false);
  var world=inRaid?Read(()=>Singleton<GameWorld>.Instance):null;
  var local=Read(()=>world?.MainPlayer);
  var profile=Read(()=>local?.Profile??(inRaid?null:HideoutCraftControl.CurrentProfile));
  var raid=world==null?null:CaptureRaid(world,local);
  var sample=PerformanceHud.CurrentSample;
  PerformanceData? performance=sample.Count==0?null:new(
   Math.Round(sample.Fps,1),Math.Round(sample.Frame,2),Math.Round(sample.P99,2),Math.Round(sample.Peak,2),
   sample.Slow,sample.Count,Math.Round(sample.Seconds,2),Math.Round(sample.Mod,2));
  int craftsReady=Read(HideoutCraftControl.ReadyCount,0);
  var cultist=inRaid||profile==null?null:Read(()=>CultistCircleControl.ReadSnapshot(profile));
  return new(){
   Client=new(Read(()=>SPTushonka.Common.Http.RequestHandler.Host),Read(()=>ClientEventVersion.Version)),
   State=inRaid?"raid":inHideout?"hideout":"menu",CapturedAtUtc=DateTimeOffset.UtcNow,InRaid=inRaid,
   Map=raid?.MapId??"",
   // This legacy field historically returned the raid duration. The nested field uses the countdown.
   RaidRemainingSeconds=(int)Math.Clamp(Math.Round(raid?.DurationSeconds??0),0,int.MaxValue),
   AlivePlayers=raid?.Population?.Alive??0,AliveAi=raid?.Population?.Ai??0,
   Fps=performance?.Fps??0,HideoutCraftsReady=craftsReady,CultistReady=cultist?.Ready==true,
   Raid=raid,Player=CaptureCharacter(local,profile),Hideout=new(craftsReady,cultist),Performance=performance
  };
 }

 static RaidData CaptureRaid(GameWorld world,Player? local){
  string map=Read(()=>world.LocationId)??"";
  string name=I18n.Text(AiSpawnInfoRows.MapName(map.ToLowerInvariant()));
  var timer=Read(RaidClock.CaptureTime);
  double? remaining=timer?.RemainingSeconds;
  return new(map,name,timer?.DurationSeconds,timer?.ElapsedSeconds,remaining is double left?Math.Max(0,left):null,
   remaining is double overtime?Math.Max(0,-overtime):null,RaidClock.PauseRemaining.Value,
   Read(()=>world.GameDateTime?.Calculate().ToString("HH:mm:ss")),Read(ReadWeather),Read(()=>CountPlayers(world)),
   Read(()=>ReadExits(local),Array.Empty<Exit>()),
   Read(()=>TrainControl.ReadSnapshot(world)),Read(AirdropControl.ReadSnapshot));
 }

 static Weather? ReadWeather(){
  if(WeatherController.Instance?.WeatherCurve is not {} weather)return null;
  var wind=weather.Wind;
  return new(Number(weather.Rain,3),Number(weather.Fog,3),Number(weather.Cloudiness,3),
   Number(weather.Temperature,1),Number(wind.x,3),Number(wind.y,3));
 }

 static Population? CountPlayers(GameWorld world){
  if(world.AllAlivePlayersList is not {} players)return null;
  int alive=0,ai=0,count=players.Count;
  for(int i=0;i<count;i++){
   var participant=players[i];
   if(participant?.HealthController?.IsAlive!=true)continue;
   alive++;
   if(participant.IsAI||participant.AIData?.BotOwner!=null)ai++;
  }
  return new(alive,ai,alive-ai);
 }

 static Exit[] ReadExits(Player? player){
  if(player?.Profile is not {} profile||ExfiltrationController.Instance is not {} controller)return Array.Empty<Exit>();
  var results=new List<Exit>();
  var seen=new HashSet<IntPtr>();
  var position=player.Position;
  if(controller.EligiblePoints(profile) is {} eligible)
   foreach(var point in eligible)Add(point);
  if(controller.SecretEligiblePoints() is {} secret)
   foreach(var point in secret)Add(point);
  results.Sort((a,b)=>(a.DistanceMeters??double.MaxValue).CompareTo(b.DistanceMeters??double.MaxValue));
  return results.ToArray();

  void Add(ExfiltrationPoint? point){
   if(point==null||!seen.Add(point.Pointer))return;
   var exit=Read(()=>{
    var id=point.Settings?.Name??point.name;
    var name=LocalizationExtensions.Localized(id,EStringCase.None);
    return new Exit(id,string.IsNullOrWhiteSpace(name)?id:name,point.Status.ToString(),
     Number(Vector3.Distance(position,point.transform.position),1));
   });
   if(exit!=null)results.Add(exit);
  }
 }

 static Character? CaptureCharacter(Player? local,Profile? profile){
  if(local==null&&profile==null)return null;
  var info=Read(()=>profile?.Info);
  var name=Read(()=>profile?.Nickname);
  var side=Read(()=>profile?.Side.ToString());
  int? level=Read<int?>(()=>info?.Level);
  int? xp=Read<int?>(()=>info?.Experience);
  if(local==null)return new(name,side,level,xp,null,null,null,null,null,null,null);
  var position=Read(()=>{
   var p=local.Position;
   return new Position(Number(p.x,2),Number(p.y,2),Number(p.z,2));
  });
  return new(name,side,level,xp,Read<bool?>(()=>local.HealthController?.IsAlive),position,
   Read<double?>(()=>Number((local.Rotation.x%360+360)%360,1)),
   Read(()=>local.IsInPronePose?"prone":local.PoseLevel<.5f?"crouched":"standing"),
   Read<bool?>(()=>local.ProceduralWeaponAnimation?.IsAiming),Read(()=>ReadVitals(local)),Read(()=>ReadWeapon(local)));
 }

 static Vitals? ReadVitals(Player player){
  if(player.ActiveHealthController is not {} health)return null;
  var total=health.GetBodyPartHealth(EBodyPart.Common,false);
  var energy=health.Energy;
  var hydration=health.Hydration;
  var parts=new Dictionary<string,Gauge>(partNames.Length,StringComparer.Ordinal);
  for(int i=0;i<partNames.Length;i++){
   var part=health.GetBodyPartHealth(AiBodyHealth.Parts[i],false);
   parts[partNames[i]]=new(Number(part.Current,1),Number(part.Maximum,1));
  }
  var physical=Read(()=>player.Physical);
  return new(new(Number(total.Current,1),Number(total.Maximum,1)),new(Number(energy.Current,1),Number(energy.Maximum,1)),
   new(Number(hydration.Current,1),Number(hydration.Maximum,1)),Read(()=>ReadStamina(physical?.Stamina)),
   Read(()=>ReadStamina(physical?.HandsStamina)),parts);
 }

 static Gauge? ReadStamina(Stamina? gauge)=>gauge==null?null:new(Number(gauge.Current,1),Number(gauge.TotalCapacity?.Value??double.NaN,1));

 static Weapon? ReadWeapon(Player player){
  if(player.HandsController?.TryCast<Player.FirearmController>()?.Item is not {} weapon)return null;
  var magazine=weapon.GetCurrentMagazine();
  int loaded=0;
  if(weapon.Chambers is {} chambers)
   foreach(var chamber in chambers)
    if(chamber?.ContainedItem?.TryCast<EFT.InventoryLogic.Ammo>()!=null)loaded++;
  var ammo=MagazineDisplay.CurrentAmmo(player);
  return new(LootDisplay.ItemName(weapon),weapon.StringTemplateId,magazine?.Count,magazine?.MaxCount,loaded,ammo==null?null:LootDisplay.ItemName(ammo));
 }

 static double? Number(double value,int digits)=>double.IsFinite(value)?Math.Round(value,digits):null;
 static T Read<T>(Func<T> read,T fallback=default!){
  try{return read();}
  catch(Exception){return fallback;}
 }
}
