using System;
using Comfort.Common;
using EFT;
namespace LocalSpt5Esp;

internal static partial class RaidEnd {
 static IntPtr nativeEndingGame,nativeEndingWorld;

 internal static bool SettlementStarted(AbstractGame game)=>
  (nativeEndingGame==game.Pointer&&Singleton<GameWorld>.Instance?.Pointer==nativeEndingWorld)||ManualEnding||
  game.Status is GameStatus.Stopping or GameStatus.SoftStopping or GameStatus.Stopped||
  game.GameTimer?._stop.HasValue==true;

 internal static void ObserveNativeStop(AbstractGame game,string profile){
  var world=Singleton<GameWorld>.Instance;
  if(Singleton<AbstractGame>.Instance?.Pointer!=game.Pointer||world?.MainPlayer is not {IsYourPlayer:true} player||
   player.ProfileId!=profile)return;
  nativeEndingGame=game.Pointer;
  nativeEndingWorld=world.Pointer;
 }
}
