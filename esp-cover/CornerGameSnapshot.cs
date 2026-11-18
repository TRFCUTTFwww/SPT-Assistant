using System;
using System.Text.Json;
using Comfort.Common;
using EFT;
namespace LocalSpt5Esp;
internal static class CornerGameSnapshot {
 internal static string CaptureJson(){
  bool inRaid=false;string map="";int raidRemainingSeconds=0,aliveAi=0,alivePlayers=0;
  try{
   inRaid=RaidClock.Active;
   if(inRaid&&Singleton<GameWorld>.Instance is {} world){
    map=world.LocationId??"";
    if(RaidClock.DisplayDuration is {} left)raidRemainingSeconds=(int)Math.Max(0,Math.Round(left));
    if(world.AllAlivePlayersList is {} players){
     for(int i=0;i<players.Count;i++){
      var p=players[i];if(p?.HealthController?.IsAlive!=true)continue;
      alivePlayers++;if(p.IsAI||p.AIData?.BotOwner!=null)aliveAi++;
     }
    }
   }
  }catch{}
  double fps=Math.Round(PerformanceHud.CurrentFps,1);
  int hideoutCraftsReady=0;bool cultistReady=false;
  try{hideoutCraftsReady=HideoutCraftControl.ReadyCount();}catch{}
  try{cultistReady=CultistCircleControl.RewardReady();}catch{}
  return JsonSerializer.Serialize(new{inRaid,map,raidRemainingSeconds,fps,aliveAi,alivePlayers,hideoutCraftsReady,cultistReady});
 }
}
